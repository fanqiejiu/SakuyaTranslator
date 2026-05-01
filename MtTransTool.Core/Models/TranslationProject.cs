namespace MtTransTool.Core.Models;

public sealed class TranslationProject
{
    public TranslationJob Job { get; set; } = new();
    public List<TranslationEntrySnapshot> Entries { get; set; } = [];
}

public sealed class TranslationEntrySnapshot
{
    public int Index { get; set; }
    public string SourceText { get; set; } = "";
    public string TranslationText { get; set; } = "";
    public string Status { get; set; } = TranslationStatus.Pending;
    public string Warning { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}
