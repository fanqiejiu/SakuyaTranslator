using System.Text;
using MtTransTool.Core.Models;

namespace MtTransTool.Core.Services;

public sealed class CsvTranslationDocument : ITranslationDocument
{
    private readonly IReadOnlyList<CsvLine> _lines;

    public CsvTranslationDocument(string sourcePath, IReadOnlyList<CsvLine> lines, IReadOnlyList<TranslationEntry> entries)
    {
        SourcePath = sourcePath;
        _lines = lines;
        Entries = entries;
    }

    public string SourcePath { get; }
    public string Kind => DocumentKind.Csv;
    public IReadOnlyList<TranslationEntry> Entries { get; }

    public string ExportPreservingFormat(IEnumerable<TranslationEntry>? replacementEntries = null)
    {
        var replacements = (replacementEntries ?? Entries).ToDictionary(x => x.Index);
        var builder = new StringBuilder();

        foreach (var line in _lines)
        {
            var values = line.Values.ToArray();
            foreach (var map in line.EntryCells)
            {
                if (replacements.TryGetValue(map.EntryIndex, out var entry))
                {
                    values[map.CellIndex] = entry.TranslationText;
                }
            }

            builder.AppendLine(string.Join(",", values.Select(EscapeCsv)));
        }

        return builder.ToString();
    }

    public void ExportTo(string outputPath, IEnumerable<TranslationEntry>? replacementEntries = null)
    {
        File.WriteAllText(outputPath, ExportPreservingFormat(replacementEntries), new UTF8Encoding(false));
    }

    private static string EscapeCsv(string value)
    {
        if (!value.Contains(',') && !value.Contains('"') && !value.Contains('\n') && !value.Contains('\r'))
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    public sealed record CsvLine(IReadOnlyList<string> Values, IReadOnlyList<CsvEntryCell> EntryCells);
    public sealed record CsvEntryCell(int EntryIndex, int CellIndex);
}
