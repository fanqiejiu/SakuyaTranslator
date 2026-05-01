using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using SakuyaTranslator.Core.Models;

namespace SakuyaTranslator.Core.Services;

public sealed class OpenAiCompatibleProofreadingClient : IProofreadingClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public OpenAiCompatibleProofreadingClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<IReadOnlyList<ProofreadIssue>> ProofreadAsync(
        IReadOnlyList<ProofreadBatchItem> items,
        AppSettings settings,
        ApiProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return [];
        }

        if (string.IsNullOrWhiteSpace(profile.BaseUrl)
            || string.IsNullOrWhiteSpace(profile.ApiKey)
            || string.IsNullOrWhiteSpace(profile.Model))
        {
            throw new InvalidOperationException("API Base URL、Key 或模型名尚未配置。");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUrl(ApiProfileRules.GetEffectiveBaseUrl(profile)));
        if (ApiProfileRules.NormalizeProvider(profile.Provider) == "MiMo")
        {
            request.Headers.Add("api-key", profile.ApiKey);
        }
        else
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", profile.ApiKey);
        }

        var documentKind = items[0].DocumentKind;
        var body = new
        {
            model = profile.Model,
            temperature = 0.1,
            messages = new object[]
            {
                new { role = "system", content = BuildSystemPrompt(settings, documentKind) },
                new { role = "user", content = BuildUserPrompt(items) }
            }
        };

        request.Content = new StringContent(JsonSerializer.Serialize(body, _jsonOptions), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        response.EnsureSuccessStatusCode();

        var content = ExtractAssistantContent(responseText);
        return ParseResults(content, items);
    }

    private static string BuildChatCompletionsUrl(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');
        return trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/chat/completions";
    }

    private static string BuildSystemPrompt(AppSettings settings, string documentKind)
    {
        var kindGuidance = documentKind switch
        {
            DocumentKind.MtToolJson => "The current document is an MTTool/Wolf RPG style JSON text map. Preserve JSON-friendly escaped control sequences like \\n and \\r exactly when they carry formatting.",
            DocumentKind.Csv => "The current document is CSV-derived text. Be careful with compact UI labels, column-like values, and avoid turning terse cells into long prose.",
            DocumentKind.Srt => "The current document is SRT subtitles. Keep the translation concise, subtitle-friendly, and avoid overly long lines or excessive line breaks. Timing/index lines are not included and must not be invented.",
            _ => "The current document is plain TXT. Preserve paragraph flow, visible line breaks, and the original tone without adding explanation."
        };

        return
            "You are a careful game localization proofreader.\n" +
            $"Direction: {settings.TranslationPreset}. Source language: {settings.SourceLanguage}. Target language: {settings.TargetLanguage}.\n" +
            "Do not assume the transport JSON is the source file format; it only carries extracted text entries.\n" +
            kindGuidance + "\n" +
            "Check each source/translation pair for mistranslation, omitted meaning, awkward target-language wording, untranslated text, broken placeholders/control codes, and model-output contamination.\n" +
            "If a pair is acceptable, omit it from the result.\n" +
            "Return a JSON array only. Each issue must have this exact shape:\n" +
            "[{\"index\":0,\"severity\":\"建议\",\"category\":\"AI校对\",\"message\":\"short issue\",\"suggestion\":\"short fix advice\",\"replacementText\":\"optional corrected translation\"}]\n" +
            "Use severity values only from: 提示, 建议, 警告, 错误.\n" +
            "Do not invent missing context. Keep variables, escape sequences, control codes, names, and line breaks intact in replacementText.";
    }

    private string BuildUserPrompt(IReadOnlyList<ProofreadBatchItem> items)
    {
        var json = JsonSerializer.Serialize(items.Select(x => new
        {
            index = x.Index,
            documentKind = x.DocumentKind,
            source = x.SourceText,
            translation = x.TranslationText
        }), _jsonOptions);

        return "Proofread this transport JSON array of translation-file entries:\n" + json;
    }

    private static string ExtractAssistantContent(string responseText)
    {
        using var document = JsonDocument.Parse(responseText);
        var choices = document.RootElement.GetProperty("choices");
        var first = choices.EnumerateArray().FirstOrDefault();
        if (first.ValueKind == JsonValueKind.Undefined)
        {
            throw new FormatException("API response did not contain choices.");
        }

        return first.GetProperty("message").GetProperty("content").GetString() ?? "";
    }

    private static IReadOnlyList<ProofreadIssue> ParseResults(
        string content,
        IReadOnlyList<ProofreadBatchItem> originalItems)
    {
        var trimmed = StripMarkdownFence(content.Trim());
        using var document = JsonDocument.Parse(trimmed);
        if (document.RootElement.ValueKind != JsonValueKind.Array)
        {
            throw new FormatException("AI 校对返回内容不是 JSON 数组。");
        }

        var sourceMap = originalItems.ToDictionary(x => x.Index);
        var issues = new List<ProofreadIssue>();
        foreach (var item in document.RootElement.EnumerateArray())
        {
            if (!item.TryGetProperty("index", out var indexElement)
                || !sourceMap.TryGetValue(indexElement.GetInt32(), out var original))
            {
                continue;
            }

            var issue = new ProofreadIssue
            {
                EntryIndex = original.Index,
                Origin = ProofreadOrigin.Ai,
                Severity = ReadString(item, "severity", ProofreadSeverity.Suggestion),
                Category = ReadString(item, "category", "AI校对"),
                Message = ReadString(item, "message", ""),
                Suggestion = ReadString(item, "suggestion", ""),
                ReplacementText = ReadString(item, "replacementText", ""),
                SourceText = original.SourceText,
                TranslationText = original.TranslationText,
                DocumentKind = original.DocumentKind
            };

            if (!string.IsNullOrWhiteSpace(issue.Message) || !string.IsNullOrWhiteSpace(issue.Suggestion))
            {
                issues.Add(issue);
            }
        }

        return issues;
    }

    private static string ReadString(JsonElement element, string propertyName, string fallback)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;
    }

    private static string StripMarkdownFence(string text)
    {
        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        var firstLineEnd = text.IndexOf('\n');
        var lastFence = text.LastIndexOf("```", StringComparison.Ordinal);
        if (firstLineEnd < 0 || lastFence <= firstLineEnd)
        {
            return text;
        }

        return text[(firstLineEnd + 1)..lastFence].Trim();
    }
}
