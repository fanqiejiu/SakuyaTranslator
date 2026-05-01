namespace SakuyaTranslator.Core.Models;

public sealed class TranslationBatchItem
{
    public int Index { get; set; }
    public string SourceText { get; set; } = "";
}

public sealed class TranslationBatchResult
{
    public int Index { get; set; }
    public string TranslationText { get; set; } = "";
    public string Warning { get; set; } = "";
    public string ErrorMessage { get; set; } = "";
}
