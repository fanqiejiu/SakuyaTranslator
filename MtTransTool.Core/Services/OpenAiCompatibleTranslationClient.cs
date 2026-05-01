using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MtTransTool.Core.Models;

namespace MtTransTool.Core.Services;

public sealed class OpenAiCompatibleTranslationClient : ITranslationClient
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public OpenAiCompatibleTranslationClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    public async Task<IReadOnlyList<TranslationBatchResult>> TranslateAsync(
        IReadOnlyList<TranslationBatchItem> items,
        AppSettings settings,
        ApiProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (items.Count == 0)
        {
            return [];
        }

        ValidateProfile(profile);

        var body = new
        {
            model = profile.Model,
            temperature = 0.2,
            messages = new object[]
            {
                new { role = "system", content = LanguageProfiles.BuildSystemPrompt(settings) },
                new { role = "user", content = BuildUserPrompt(items) }
            }
        };

        var responseText = await SendChatAsync(profile, body, cancellationToken);
        var content = ExtractAssistantContent(responseText);
        return ParseResults(content, items);
    }

    public async Task TestConnectionAsync(ApiProfile profile, CancellationToken cancellationToken = default)
    {
        ValidateProfile(profile);

        object body;
        if (ApiProfileRules.NormalizeProvider(profile.Provider) == "MiMo")
        {
            body = new
            {
                model = profile.Model,
                temperature = 0,
                max_completion_tokens = 8,
                messages = new object[]
                {
                    new { role = "user", content = "Reply with OK." }
                }
            };
        }
        else
        {
            body = new
            {
                model = profile.Model,
                temperature = 0,
                max_tokens = 8,
                messages = new object[]
                {
                    new { role = "user", content = "Reply with OK." }
                }
            };
        }

        _ = ExtractAssistantContent(await SendChatAsync(profile, body, cancellationToken));
    }

    private async Task<string> SendChatAsync(ApiProfile profile, object body, CancellationToken cancellationToken)
    {
        using var request = CreateChatRequest(profile, body);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var responseText = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(BuildHttpErrorMessage(response.StatusCode, responseText), null, response.StatusCode);
        }

        return responseText;
    }

    private HttpRequestMessage CreateChatRequest(ApiProfile profile, object body)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, BuildChatCompletionsUrl(ApiProfileRules.GetEffectiveBaseUrl(profile)));
        ApplyAuthentication(request, profile);
        request.Content = new StringContent(JsonSerializer.Serialize(body, _jsonOptions), Encoding.UTF8, "application/json");
        return request;
    }

    private static void ApplyAuthentication(HttpRequestMessage request, ApiProfile profile)
    {
        if (ApiProfileRules.NormalizeProvider(profile.Provider) == "MiMo")
        {
            request.Headers.Add("api-key", profile.ApiKey);
            return;
        }

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", profile.ApiKey);
    }

    private static void ValidateProfile(ApiProfile profile)
    {
        if (string.IsNullOrWhiteSpace(ApiProfileRules.GetEffectiveBaseUrl(profile))
            || string.IsNullOrWhiteSpace(profile.ApiKey)
            || string.IsNullOrWhiteSpace(profile.Model))
        {
            throw new InvalidOperationException("API Base URL、Key 或模型名尚未配置。");
        }
    }

    private static string BuildHttpErrorMessage(HttpStatusCode statusCode, string responseText)
    {
        var detail = TryExtractErrorMessage(responseText);
        return string.IsNullOrWhiteSpace(detail)
            ? $"HTTP {(int)statusCode} {statusCode}"
            : $"HTTP {(int)statusCode} {statusCode}: {detail}";
    }

    private static string TryExtractErrorMessage(string responseText)
    {
        if (string.IsNullOrWhiteSpace(responseText))
        {
            return "";
        }

        try
        {
            using var document = JsonDocument.Parse(responseText);
            var root = document.RootElement;
            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                {
                    return error.GetString() ?? "";
                }

                if (error.ValueKind == JsonValueKind.Object)
                {
                    if (error.TryGetProperty("message", out var message) && message.ValueKind == JsonValueKind.String)
                    {
                        return message.GetString() ?? "";
                    }

                    return error.ToString();
                }
            }

            if (root.TryGetProperty("message", out var rootMessage) && rootMessage.ValueKind == JsonValueKind.String)
            {
                return rootMessage.GetString() ?? "";
            }

            return root.ToString();
        }
        catch (JsonException)
        {
            return responseText.Trim();
        }
    }

    private static string BuildChatCompletionsUrl(string baseUrl)
    {
        var trimmed = baseUrl.TrimEnd('/');
        return trimmed.EndsWith("/chat/completions", StringComparison.OrdinalIgnoreCase)
            ? trimmed
            : $"{trimmed}/chat/completions";
    }

    private string BuildUserPrompt(IReadOnlyList<TranslationBatchItem> items)
    {
        var json = JsonSerializer.Serialize(items.Select(x => new
        {
            index = x.Index,
            text = x.SourceText
        }), _jsonOptions);

        return
            "Translate the following JSON array.\n" +
            "This is a localization transformation task for user-provided game files. Translate only; do not continue, roleplay, judge, endorse, intensify, or add new content.\n" +
            "Return a JSON array only, with each item in this exact shape:\n" +
            "[{\"index\":1,\"text\":\"translated text\"}]\n\n" +
            "Do not add policy notes, safety disclaimers, explanations, or refusal text. If the text is already in the target language or cannot be translated as a standalone fragment, return it unchanged.\n\n" +
            "Input:\n" +
            json;
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

    private static IReadOnlyList<TranslationBatchResult> ParseResults(
        string content,
        IReadOnlyList<TranslationBatchItem> originalItems)
    {
        var trimmed = StripMarkdownFence(content.Trim());

        try
        {
            using var document = JsonDocument.Parse(trimmed);
            if (document.RootElement.ValueKind == JsonValueKind.Array)
            {
                var originalMap = originalItems.ToDictionary(x => x.Index);
                return document.RootElement.EnumerateArray()
                    .Select(item =>
                    {
                        var index = item.GetProperty("index").GetInt32();
                        var translatedText = item.GetProperty("text").GetString() ?? "";
                        return originalMap.TryGetValue(index, out var original)
                            ? BuildResult(original, translatedText)
                            : new TranslationBatchResult
                            {
                                Index = index,
                                TranslationText = translatedText
                            };
                    })
                    .OrderBy(x => x.Index)
                    .ToArray();
            }
        }
        catch (JsonException)
        {
            // Some models ignore the JSON-only instruction. Fall back to line order below.
        }

        var lines = trimmed.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        return originalItems.Select((item, i) => new TranslationBatchResult
            {
                Index = item.Index,
                TranslationText = i < lines.Length ? lines[i].Trim() : item.SourceText
            })
            .Select((result, i) => BuildResult(originalItems[i], result.TranslationText))
            .ToArray();
    }

    private static TranslationBatchResult BuildResult(TranslationBatchItem item, string translatedText)
    {
        if (!LooksLikePolicyOrRefusalOutput(translatedText))
        {
            return new TranslationBatchResult
            {
                Index = item.Index,
                TranslationText = translatedText
            };
        }

        return new TranslationBatchResult
        {
            Index = item.Index,
            TranslationText = item.SourceText,
            ErrorMessage = "模型返回了疑似政策限制或拒绝回复，已保留原文。可尝试换模型、降低批量并发，或手动处理该条。"
        };
    }

    private static bool LooksLikePolicyOrRefusalOutput(string text)
    {
        var trimmed = text.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        var lower = trimmed.ToLowerInvariant();
        string[] markers =
        [
            "i'm sorry, but i can't",
            "i’m sorry, but i can’t",
            "i cannot assist",
            "i can't assist",
            "i can’t assist",
            "i'm unable to",
            "i’m unable to",
            "as an ai",
            "content policy",
            "safety guidelines",
            "cannot translate",
            "can't translate",
            "无法协助",
            "不能协助",
            "无法提供",
            "不能提供",
            "无法翻译",
            "不能翻译",
            "违反政策",
            "安全政策",
            "内容政策",
            "翻訳できません",
            "お手伝いできません"
        ];

        return markers.Any(marker => lower.Contains(marker, StringComparison.OrdinalIgnoreCase));
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
