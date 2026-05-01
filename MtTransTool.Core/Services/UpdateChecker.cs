using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using MtTransTool.Core.Models;

namespace MtTransTool.Core.Services;

public sealed class UpdateChecker
{
    private readonly HttpClient _httpClient;

    public UpdateChecker(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
        _httpClient.DefaultRequestHeaders.UserAgent.Clear();
        _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("SakuyaTranslator", CurrentVersion));
    }

    public static string CurrentVersion =>
        Assembly.GetEntryAssembly()?.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
        ?? Assembly.GetEntryAssembly()?.GetName().Version?.ToString(3)
        ?? "0.1.0";

    public async Task<UpdateCheckResult> CheckAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var current = CurrentVersion;

        try
        {
            var latest = await TryGitHubReleaseAsync(settings.GitHubRepository, cancellationToken)
                         ?? await TryFallbackJsonAsync(settings.FallbackUpdateJsonUrl, cancellationToken);

            if (latest is null)
            {
                return new UpdateCheckResult
                {
                    Success = false,
                    CurrentVersion = current,
                    ErrorMessage = "未配置 GitHub 仓库或备用 update.json。"
                };
            }

            return new UpdateCheckResult
            {
                Success = true,
                CurrentVersion = current,
                Latest = latest,
                HasUpdate = IsNewer(latest.Version, current)
            };
        }
        catch (Exception ex)
        {
            return new UpdateCheckResult
            {
                Success = false,
                CurrentVersion = current,
                ErrorMessage = ex.Message
            };
        }
    }

    private async Task<UpdateInfo?> TryGitHubReleaseAsync(string repository, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(repository) || !repository.Contains('/'))
        {
            return null;
        }

        var url = $"https://api.github.com/repos/{repository.Trim()}/releases/latest";
        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);
        var root = json.RootElement;

        var tag = root.GetPropertyOrDefault("tag_name");
        var assetUrl = "";
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetPropertyOrDefault("name");
                if (name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                {
                    assetUrl = asset.GetPropertyOrDefault("browser_download_url");
                    break;
                }
            }
        }

        return new UpdateInfo
        {
            Version = tag.TrimStart('v', 'V'),
            Title = root.GetPropertyOrDefault("name"),
            Changelog = root.GetPropertyOrDefault("body"),
            ChangelogUrl = root.GetPropertyOrDefault("html_url"),
            DownloadUrl = assetUrl,
            PublishedAt = root.TryGetProperty("published_at", out var published)
                && DateTimeOffset.TryParse(published.GetString(), out var publishedAt)
                    ? publishedAt
                    : null,
            Source = "GitHub Releases"
        };
    }

    private async Task<UpdateInfo?> TryFallbackJsonAsync(string url, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        using var response = await _httpClient.GetAsync(url, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        var info = await JsonSerializer.DeserializeAsync<UpdateInfo>(stream, cancellationToken: cancellationToken);
        if (info is not null)
        {
            info.Source = string.IsNullOrWhiteSpace(info.Source) ? "update.json" : info.Source;
        }

        return info;
    }

    private static bool IsNewer(string latest, string current)
    {
        if (!Version.TryParse(NormalizeVersion(latest), out var latestVersion))
        {
            return false;
        }

        return !Version.TryParse(NormalizeVersion(current), out var currentVersion)
               || latestVersion > currentVersion;
    }

    private static string NormalizeVersion(string value)
    {
        var clean = value.Trim().TrimStart('v', 'V');
        var plus = clean.IndexOf('+');
        if (plus >= 0)
        {
            clean = clean[..plus];
        }

        var dash = clean.IndexOf('-');
        if (dash >= 0)
        {
            clean = clean[..dash];
        }

        return clean;
    }
}

internal static class JsonElementExtensions
{
    public static string GetPropertyOrDefault(this JsonElement element, string propertyName)
    {
        return element.TryGetProperty(propertyName, out var property) && property.ValueKind != JsonValueKind.Null
            ? property.GetString() ?? ""
            : "";
    }
}
