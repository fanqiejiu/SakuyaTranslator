using System.Security.Cryptography;

namespace MtTransTool.Core.Models;

public sealed class TranslationJob
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string FilePath { get; set; } = "";
    public string FileName { get; set; } = "";
    public string FileHash { get; set; } = "";
    public string OutputPath { get; set; } = "";
    public string Model { get; set; } = "";
    public string TranslationDirection { get; set; } = "日译中";
    public string SpeedPreset { get; set; } = "普通";
    public string Status { get; set; } = JobStatus.Waiting;
    public int TotalCount { get; set; }
    public int CompletedCount { get; set; }
    public int ErrorCount { get; set; }
    public int WarningCount { get; set; }
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.Now;

    public double ProgressPercent => TotalCount <= 0 ? 0 : Math.Clamp(CompletedCount * 100.0 / TotalCount, 0, 100);

    public static async Task<string> ComputeSha256Async(string path, CancellationToken cancellationToken = default)
    {
        await using var stream = File.OpenRead(path);
        var hash = await SHA256.HashDataAsync(stream, cancellationToken);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }
}

public static class JobStatus
{
    public const string Waiting = "等待中";
    public const string Running = "翻译中";
    public const string Paused = "暂停";
    public const string Completed = "完成";
    public const string Error = "错误";
    public const string Cancelled = "已取消";
}
