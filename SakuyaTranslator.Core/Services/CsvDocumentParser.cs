using System.Text;
using SakuyaTranslator.Core.Models;

namespace SakuyaTranslator.Core.Services;

public sealed class CsvDocumentParser
{
    public async Task<CsvTranslationDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var raw = await File.ReadAllTextAsync(path, new UTF8Encoding(false, true), cancellationToken);
        var entries = new List<TranslationEntry>();
        var lines = new List<CsvTranslationDocument.CsvLine>();

        foreach (var row in ParseRows(raw))
        {
            var cells = new List<CsvTranslationDocument.CsvEntryCell>();
            for (var i = 0; i < row.Count; i++)
            {
                var value = row[i];
                if (!LooksTranslatable(value))
                {
                    continue;
                }

                var entry = new TranslationEntry
                {
                    Index = entries.Count,
                    SourceText = value,
                    TranslationText = value,
                    Status = TranslationStatus.Pending
                };
                entries.Add(entry);
                cells.Add(new CsvTranslationDocument.CsvEntryCell(entry.Index, i));
            }

            lines.Add(new CsvTranslationDocument.CsvLine(row, cells));
        }

        return new CsvTranslationDocument(path, lines, entries);
    }

    private static bool LooksTranslatable(string value)
    {
        return !string.IsNullOrWhiteSpace(value)
               && value.Any(c => c is >= '\u3040' and <= '\u30ff' or >= '\u4e00' and <= '\u9fff')
               && !PlaceholderValidator.LooksCodeLike(value);
    }

    private static IReadOnlyList<IReadOnlyList<string>> ParseRows(string raw)
    {
        var rows = new List<IReadOnlyList<string>>();
        var row = new List<string>();
        var cell = new StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < raw.Length; i++)
        {
            var c = raw[i];
            if (inQuotes)
            {
                if (c == '"' && i + 1 < raw.Length && raw[i + 1] == '"')
                {
                    cell.Append('"');
                    i++;
                }
                else if (c == '"')
                {
                    inQuotes = false;
                }
                else
                {
                    cell.Append(c);
                }

                continue;
            }

            if (c == '"')
            {
                inQuotes = true;
            }
            else if (c == ',')
            {
                row.Add(cell.ToString());
                cell.Clear();
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < raw.Length && raw[i + 1] == '\n')
                {
                    i++;
                }

                row.Add(cell.ToString());
                rows.Add(row.ToArray());
                row.Clear();
                cell.Clear();
            }
            else
            {
                cell.Append(c);
            }
        }

        if (cell.Length > 0 || row.Count > 0)
        {
            row.Add(cell.ToString());
            rows.Add(row.ToArray());
        }

        return rows;
    }
}
