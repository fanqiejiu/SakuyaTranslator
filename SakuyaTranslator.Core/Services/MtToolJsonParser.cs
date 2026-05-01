using System.Text;
using SakuyaTranslator.Core.Models;

namespace SakuyaTranslator.Core.Services;

public sealed class MtToolJsonParser
{
    public async Task<MtToolJsonDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        var raw = await File.ReadAllTextAsync(path, new UTF8Encoding(false, true), cancellationToken);
        return new MtToolJsonDocument(path, raw, Parse(raw));
    }

    public IReadOnlyList<TranslationEntry> Parse(string rawText)
    {
        var entries = new List<TranslationEntry>();
        var index = 0;
        SkipWhiteSpace(rawText, ref index);
        Expect(rawText, ref index, '{');

        while (true)
        {
            SkipWhiteSpace(rawText, ref index);
            if (index >= rawText.Length)
            {
                throw new FormatException("JSON object is not closed.");
            }

            if (rawText[index] == '}')
            {
                index++;
                break;
            }

            var key = ReadJsonString(rawText, ref index);
            SkipWhiteSpace(rawText, ref index);
            Expect(rawText, ref index, ':');
            SkipWhiteSpace(rawText, ref index);
            var value = ReadJsonString(rawText, ref index);

            entries.Add(new TranslationEntry
            {
                Index = entries.Count,
                SourceText = key.Value,
                TranslationText = value.Value,
                ValueLiteralStart = value.LiteralStart,
                ValueLiteralLength = value.LiteralEnd - value.LiteralStart,
                Status = string.Equals(key.Value, value.Value, StringComparison.Ordinal)
                    ? TranslationStatus.Pending
                    : TranslationStatus.Done
            });

            SkipWhiteSpace(rawText, ref index);
            if (index < rawText.Length && rawText[index] == ',')
            {
                index++;
                continue;
            }

            if (index < rawText.Length && rawText[index] == '}')
            {
                index++;
                break;
            }

            if (index < rawText.Length)
            {
                throw new FormatException($"Unexpected character '{rawText[index]}' at offset {index}.");
            }
        }

        return entries;
    }

    private static void SkipWhiteSpace(string text, ref int index)
    {
        while (index < text.Length && char.IsWhiteSpace(text[index]))
        {
            index++;
        }
    }

    private static void Expect(string text, ref int index, char expected)
    {
        if (index >= text.Length || text[index] != expected)
        {
            throw new FormatException($"Expected '{expected}' at offset {index}.");
        }

        index++;
    }

    private static ParsedString ReadJsonString(string text, ref int index)
    {
        var literalStart = index;
        Expect(text, ref index, '"');
        var builder = new StringBuilder();

        while (index < text.Length)
        {
            var c = text[index++];
            if (c == '"')
            {
                return new ParsedString(builder.ToString(), literalStart, index);
            }

            if (c != '\\')
            {
                builder.Append(c);
                continue;
            }

            if (index >= text.Length)
            {
                throw new FormatException("Unfinished JSON escape sequence.");
            }

            var escaped = text[index++];
            builder.Append(escaped switch
            {
                '"' => '"',
                '\\' => '\\',
                '/' => '/',
                'b' => '\b',
                'f' => '\f',
                'n' => '\n',
                'r' => '\r',
                't' => '\t',
                'u' => ReadUnicodeEscape(text, ref index),
                _ => throw new FormatException($"Unsupported JSON escape '\\{escaped}' at offset {index}.")
            });
        }

        throw new FormatException("JSON string is not closed.");
    }

    private static char ReadUnicodeEscape(string text, ref int index)
    {
        if (index + 4 > text.Length)
        {
            throw new FormatException("Unfinished unicode escape sequence.");
        }

        var hex = text.Substring(index, 4);
        index += 4;
        return (char)Convert.ToInt32(hex, 16);
    }

    private readonly record struct ParsedString(string Value, int LiteralStart, int LiteralEnd);
}
