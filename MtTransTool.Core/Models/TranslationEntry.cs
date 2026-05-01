namespace MtTransTool.Core.Models;

public sealed class TranslationEntry
{
    public int Index { get; init; }
    public string SourceText { get; init; } = "";
    public string TranslationText { get; set; } = "";
    public int ValueLiteralStart { get; init; }
    public int ValueLiteralLength { get; init; }
    public string Status { get; set; } = TranslationStatus.Pending;
    public string Warning { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}

public static class TranslationStatus
{
    public const string Pending = "待翻译";
    public const string Running = "翻译中";
    public const string Done = "完成";
    public const string DoneWithWarnings = "完成(警告)";
    public const string Paused = "暂停";
    public const string Error = "错误";
    public const string Skipped = "跳过";
}
