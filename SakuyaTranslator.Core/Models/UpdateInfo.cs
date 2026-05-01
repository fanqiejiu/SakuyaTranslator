namespace SakuyaTranslator.Core.Models;

public sealed class UpdateInfo
{
    public string Version { get; set; } = "";
    public string Channel { get; set; } = "stable";
    public string Title { get; set; } = "";
    public string Changelog { get; set; } = "";
    public string DownloadUrl { get; set; } = "";
    public string ChangelogUrl { get; set; } = "";
    public string Sha256 { get; set; } = "";
    public DateTimeOffset? PublishedAt { get; set; }
    public string Source { get; set; } = "";
}

public sealed class UpdateCheckResult
{
    public bool Success { get; set; }
    public bool HasUpdate { get; set; }
    public string CurrentVersion { get; set; } = "";
    public UpdateInfo? Latest { get; set; }
    public string ErrorMessage { get; set; } = "";
}
