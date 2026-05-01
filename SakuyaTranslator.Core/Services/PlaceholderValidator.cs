using System.Text.RegularExpressions;

namespace SakuyaTranslator.Core.Services;

public static partial class PlaceholderValidator
{
    public static string Validate(string source, string translation)
    {
        var sourceTokens = TokenRegex().Matches(source).Select(x => x.Value).OrderBy(x => x).ToArray();
        var translationTokens = TokenRegex().Matches(translation).Select(x => x.Value).OrderBy(x => x).ToArray();

        if (!sourceTokens.SequenceEqual(translationTokens))
        {
            return "占位符/控制符可能不一致";
        }

        var sourceNewLines = CountNewLines(source);
        var translatedNewLines = CountNewLines(translation);
        if (sourceNewLines != translatedNewLines)
        {
            return "换行数量不一致";
        }

        return "";
    }

    public static bool LooksCodeLike(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (text.All(c => char.IsDigit(c) || c is '-' or '_' or '.' or '/' or '\\' or ':' or '[' or ']'))
        {
            return true;
        }

        return FileLikeRegex().IsMatch(text) || text.StartsWith("BGM/", StringComparison.OrdinalIgnoreCase);
    }

    private static int CountNewLines(string text)
    {
        var count = 0;
        foreach (var c in text)
        {
            if (c == '\n')
            {
                count++;
            }
        }

        return count;
    }

    [GeneratedRegex(@"\\[A-Za-z]+(?:\[[^\]\r\n]*\])?|\\[0-9]+|%[0-9.\-+]*[sdif]|<[^>\r\n]{1,80}>|\[[A-Za-z][^\]\r\n]{0,48}\]", RegexOptions.Compiled)]
    private static partial Regex TokenRegex();

    [GeneratedRegex(@"^[A-Za-z0-9_./\\\- ]+\.(?:png|jpg|jpeg|webp|bmp|ogg|mp3|wav|mid|midi|dat|project)$", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex FileLikeRegex();
}
