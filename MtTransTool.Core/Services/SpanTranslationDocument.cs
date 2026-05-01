using System.Text;
using MtTransTool.Core.Models;

namespace MtTransTool.Core.Services;

public sealed class SpanTranslationDocument : ITranslationDocument
{
    public SpanTranslationDocument(string sourcePath, string kind, string rawText, IReadOnlyList<TranslationEntry> entries)
    {
        SourcePath = sourcePath;
        Kind = kind;
        RawText = rawText;
        Entries = entries;
    }

    public string SourcePath { get; }
    public string Kind { get; }
    public string RawText { get; }
    public IReadOnlyList<TranslationEntry> Entries { get; }

    public string ExportPreservingFormat(IEnumerable<TranslationEntry>? replacementEntries = null)
    {
        var entries = (replacementEntries ?? Entries).OrderByDescending(x => x.ValueLiteralStart).ToArray();
        var builder = new StringBuilder(RawText);

        foreach (var entry in entries)
        {
            builder.Remove(entry.ValueLiteralStart, entry.ValueLiteralLength);
            builder.Insert(entry.ValueLiteralStart, entry.TranslationText ?? "");
        }

        return builder.ToString();
    }

    public void ExportTo(string outputPath, IEnumerable<TranslationEntry>? replacementEntries = null)
    {
        File.WriteAllText(outputPath, ExportPreservingFormat(replacementEntries), new UTF8Encoding(false));
    }
}
