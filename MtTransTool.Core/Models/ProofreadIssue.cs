namespace MtTransTool.Core.Models;

public sealed class ProofreadIssue
{
    public int EntryIndex { get; set; }
    public string Severity { get; set; } = ProofreadSeverity.Warning;
    public string Origin { get; set; } = ProofreadOrigin.Rule;
    public string Category { get; set; } = "";
    public string Message { get; set; } = "";
    public string Suggestion { get; set; } = "";
    public string ReplacementText { get; set; } = "";
    public string SourceText { get; set; } = "";
    public string TranslationText { get; set; } = "";
    public string DocumentKind { get; set; } = MtTransTool.Core.Models.DocumentKind.Txt;
}

public sealed class ProofreadBatchItem
{
    public int Index { get; init; }
    public string SourceText { get; init; } = "";
    public string TranslationText { get; init; } = "";
    public string DocumentKind { get; init; } = MtTransTool.Core.Models.DocumentKind.Txt;
}

public static class ProofreadSeverity
{
    public const string Info = "提示";
    public const string Suggestion = "建议";
    public const string Warning = "警告";
    public const string Error = "错误";
}

public static class ProofreadOrigin
{
    public const string Rule = "规则";
    public const string Ai = "AI";
}
