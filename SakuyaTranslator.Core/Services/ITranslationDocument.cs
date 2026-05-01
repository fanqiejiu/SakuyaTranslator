using SakuyaTranslator.Core.Models;

namespace SakuyaTranslator.Core.Services;

public interface ITranslationDocument
{
    string SourcePath { get; }
    string Kind { get; }
    IReadOnlyList<TranslationEntry> Entries { get; }
    string ExportPreservingFormat(IEnumerable<TranslationEntry>? replacementEntries = null);
    void ExportTo(string outputPath, IEnumerable<TranslationEntry>? replacementEntries = null);
}
