using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using MtTransTool.Core.Models;

namespace MtTransTool.Core.Services;

public sealed class MtToolJsonDocument : ITranslationDocument
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public MtToolJsonDocument(string sourcePath, string rawText, IReadOnlyList<TranslationEntry> entries)
    {
        SourcePath = sourcePath;
        RawText = rawText;
        Entries = entries;
    }

    public string SourcePath { get; }
    public string Kind => DocumentKind.MtToolJson;
    public string RawText { get; }
    public IReadOnlyList<TranslationEntry> Entries { get; }

    public string ExportPreservingFormat(IEnumerable<TranslationEntry>? replacementEntries = null)
    {
        var entries = (replacementEntries ?? Entries).OrderByDescending(x => x.ValueLiteralStart).ToArray();
        var builder = new StringBuilder(RawText);

        foreach (var entry in entries)
        {
            var replacement = JsonSerializer.Serialize(entry.TranslationText ?? "", JsonOptions);
            builder.Remove(entry.ValueLiteralStart, entry.ValueLiteralLength);
            builder.Insert(entry.ValueLiteralStart, replacement);
        }

        return builder.ToString();
    }

    public void ExportTo(string outputPath, IEnumerable<TranslationEntry>? replacementEntries = null)
    {
        File.WriteAllText(outputPath, ExportPreservingFormat(replacementEntries), new UTF8Encoding(false));
    }
}
