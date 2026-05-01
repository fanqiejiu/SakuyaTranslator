using System.Text;
using System.Text.RegularExpressions;
using MtTransTool.Core.Models;

namespace MtTransTool.Core.Services;

public sealed partial class TextLikeDocumentParser
{
    public async Task<SpanTranslationDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var raw = await File.ReadAllTextAsync(path, new UTF8Encoding(false, true), cancellationToken);
        var extension = Path.GetExtension(path).ToLowerInvariant();
        var kind = extension == ".srt" ? DocumentKind.Srt : DocumentKind.Txt;
        var entries = extension == ".srt" ? ParseSrt(raw) : ParsePlainText(raw);
        return new SpanTranslationDocument(path, kind, raw, entries);
    }

    private static IReadOnlyList<TranslationEntry> ParsePlainText(string raw)
    {
        var entries = new List<TranslationEntry>();
        foreach (var line in EnumerateLines(raw))
        {
            if (string.IsNullOrWhiteSpace(line.Text))
            {
                continue;
            }

            entries.Add(CreateEntry(entries.Count, line.Text, line.Start));
        }

        return entries;
    }

    private static IReadOnlyList<TranslationEntry> ParseSrt(string raw)
    {
        var entries = new List<TranslationEntry>();
        foreach (var line in EnumerateLines(raw))
        {
            var text = line.Text.Trim();
            if (string.IsNullOrWhiteSpace(text)
                || int.TryParse(text, out _)
                || SrtTimeRegex().IsMatch(text))
            {
                continue;
            }

            entries.Add(CreateEntry(entries.Count, line.Text, line.Start));
        }

        return entries;
    }

    private static TranslationEntry CreateEntry(int index, string text, int start)
    {
        return new TranslationEntry
        {
            Index = index,
            SourceText = text,
            TranslationText = text,
            ValueLiteralStart = start,
            ValueLiteralLength = text.Length,
            Status = TranslationStatus.Pending
        };
    }

    private static IEnumerable<(string Text, int Start)> EnumerateLines(string raw)
    {
        var start = 0;
        for (var i = 0; i < raw.Length; i++)
        {
            if (raw[i] != '\n')
            {
                continue;
            }

            var end = i;
            if (end > start && raw[end - 1] == '\r')
            {
                end--;
            }

            yield return (raw[start..end], start);
            start = i + 1;
        }

        if (start < raw.Length)
        {
            yield return (raw[start..], start);
        }
    }

    [GeneratedRegex(@"^\d{2}:\d{2}:\d{2},\d{3}\s+-->\s+\d{2}:\d{2}:\d{2},\d{3}")]
    private static partial Regex SrtTimeRegex();
}
