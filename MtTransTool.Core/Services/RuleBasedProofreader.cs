using System.Text.RegularExpressions;
using MtTransTool.Core.Models;

namespace MtTransTool.Core.Services;

public sealed partial class RuleBasedProofreader
{
    public IReadOnlyList<ProofreadIssue> Analyze(
        IEnumerable<TranslationEntry> entries,
        AppSettings settings,
        string documentKind = DocumentKind.Txt)
    {
        var issues = new List<ProofreadIssue>();
        var targetIsChinese = settings.TargetLanguage.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
            || settings.TranslationPreset.EndsWith("译中", StringComparison.Ordinal);

        foreach (var entry in entries)
        {
            var source = entry.SourceText ?? "";
            var translation = entry.TranslationText ?? "";

            if (string.IsNullOrWhiteSpace(translation))
            {
                issues.Add(CreateIssue(
                    entry,
                    documentKind,
                    ProofreadSeverity.Error,
                    "未翻译",
                    "译文为空，导出后会缺失这条文本。",
                    "补齐译文后再导出。"));
                continue;
            }

            if (string.Equals(source, translation, StringComparison.Ordinal)
                && !PlaceholderValidator.LooksCodeLike(source))
            {
                issues.Add(CreateIssue(
                    entry,
                    documentKind,
                    ProofreadSeverity.Warning,
                    "疑似未翻译",
                    "原文和译文完全相同。",
                    "确认是否为人名、代码或无需翻译文本；否则重新翻译。"));
            }

            var placeholderWarning = PlaceholderValidator.Validate(source, translation);
            if (!string.IsNullOrWhiteSpace(placeholderWarning))
            {
                issues.Add(CreateIssue(
                    entry,
                    documentKind,
                    ProofreadSeverity.Error,
                    "格式保护",
                    placeholderWarning,
                    "保持变量、控制符、换行数量和原文一致。"));
            }

            if (HasLeadingOrTrailingWhitespaceMismatch(source, translation))
            {
                issues.Add(CreateIssue(
                    entry,
                    documentKind,
                    ProofreadSeverity.Warning,
                    "空白字符",
                    "译文开头或结尾的空格/换行和原文不一致。",
                    "游戏文本常用前后空格控制排版，请确认是否需要保留。"));
            }

            if (targetIsChinese && KanaRegex().IsMatch(translation))
            {
                issues.Add(CreateIssue(
                    entry,
                    documentKind,
                    ProofreadSeverity.Warning,
                    "疑似残留日文",
                    "译文中仍包含平假名或片假名。",
                    "如果不是角色名、技能名或专有名词，请重新校对。"));
            }

            if (LooksLikeModelMetaOutput(translation))
            {
                issues.Add(CreateIssue(
                    entry,
                    documentKind,
                    ProofreadSeverity.Warning,
                    "模型输出污染",
                    "译文疑似混入了说明文字、Markdown 或结构化返回格式。",
                    "只保留实际游戏文本，不要保留模型解释。"));
            }

            if (LooksSuspiciouslyLong(source, translation))
            {
                issues.Add(CreateIssue(
                    entry,
                    documentKind,
                    ProofreadSeverity.Info,
                    "长度异常",
                    "译文长度明显超过原文。",
                    "确认是否出现重复翻译、解释性文字或格式污染。"));
            }

            AddDocumentSpecificIssues(issues, entry, source, translation, documentKind);
        }

        return issues;
    }

    private static void AddDocumentSpecificIssues(
        List<ProofreadIssue> issues,
        TranslationEntry entry,
        string source,
        string translation,
        string documentKind)
    {
        switch (documentKind)
        {
            case DocumentKind.Srt:
                if (translation.Count(c => c == '\n') >= 2)
                {
                    issues.Add(CreateIssue(
                        entry,
                        documentKind,
                        ProofreadSeverity.Warning,
                        "字幕断行",
                        "单条字幕换行过多，可能影响字幕显示与阅读节奏。",
                        "尽量压缩成 1 到 2 行，并保持自然断句。"));
                }

                if (CountMeaningfulChars(translation) > 42)
                {
                    issues.Add(CreateIssue(
                        entry,
                        documentKind,
                        ProofreadSeverity.Info,
                        "字幕过长",
                        "单条字幕偏长，播放时可能来不及阅读。",
                        "尽量缩短措辞，保留核心语义。"));
                }
                break;
            case DocumentKind.Csv:
                if (translation.Contains(',', StringComparison.Ordinal) && !source.Contains(',', StringComparison.Ordinal))
                {
                    issues.Add(CreateIssue(
                        entry,
                        documentKind,
                        ProofreadSeverity.Info,
                        "CSV分隔风险",
                        "译文新增了逗号，虽然导出会转义，但需要确认目标列允许更复杂内容。",
                        "确认该列确实是可翻译文本列，而不是枚举值或紧凑字段。"));
                }
                break;
            case DocumentKind.MtToolJson:
                if ((source.Contains("\\n", StringComparison.Ordinal) || source.Contains("\\r", StringComparison.Ordinal))
                    && (!translation.Contains("\\n", StringComparison.Ordinal) && !translation.Contains("\\r", StringComparison.Ordinal)))
                {
                    issues.Add(CreateIssue(
                        entry,
                        documentKind,
                        ProofreadSeverity.Warning,
                        "JSON控制符",
                        "原文含有 JSON 转义换行控制符，但译文中可能丢失。",
                        "确认转义换行和控制符被完整保留。"));
                }
                break;
            case DocumentKind.Txt:
                if (source.EndsWith("…", StringComparison.Ordinal) && !translation.EndsWith("…", StringComparison.Ordinal))
                {
                    issues.Add(CreateIssue(
                        entry,
                        documentKind,
                        ProofreadSeverity.Info,
                        "语气延续",
                        "原文以省略号结尾，但译文未保留这种停顿语气。",
                        "确认是否需要保留省略号来维持文本语气。"));
                }
                break;
        }
    }

    private static ProofreadIssue CreateIssue(
        TranslationEntry entry,
        string documentKind,
        string severity,
        string category,
        string message,
        string suggestion)
    {
        return new ProofreadIssue
        {
            EntryIndex = entry.Index,
            Severity = severity,
            Origin = ProofreadOrigin.Rule,
            Category = category,
            Message = message,
            Suggestion = suggestion,
            SourceText = entry.SourceText,
            TranslationText = entry.TranslationText,
            DocumentKind = documentKind
        };
    }

    private static bool HasLeadingOrTrailingWhitespaceMismatch(string source, string translation)
    {
        return StartsWithWhiteSpace(source) != StartsWithWhiteSpace(translation)
            || EndsWithWhiteSpace(source) != EndsWithWhiteSpace(translation);
    }

    private static bool StartsWithWhiteSpace(string text)
    {
        return text.Length > 0 && char.IsWhiteSpace(text[0]);
    }

    private static bool EndsWithWhiteSpace(string text)
    {
        return text.Length > 0 && char.IsWhiteSpace(text[^1]);
    }

    private static bool LooksLikeModelMetaOutput(string translation)
    {
        var trimmed = translation.Trim();
        if (trimmed.Contains("```", StringComparison.Ordinal)
            || trimmed.StartsWith("[{", StringComparison.Ordinal)
            || trimmed.StartsWith("{\"", StringComparison.Ordinal))
        {
            return true;
        }

        return MetaOutputRegex().IsMatch(trimmed);
    }

    private static bool LooksSuspiciouslyLong(string source, string translation)
    {
        var sourceLength = CountMeaningfulChars(source);
        var translationLength = CountMeaningfulChars(translation);
        return sourceLength >= 12 && translationLength >= 80 && translationLength > sourceLength * 3;
    }

    private static int CountMeaningfulChars(string text)
    {
        return text.Count(c => !char.IsWhiteSpace(c));
    }

    [GeneratedRegex(@"[\u3040-\u30ff]", RegexOptions.Compiled)]
    private static partial Regex KanaRegex();

    [GeneratedRegex(@"^(作为\s*AI|作为一个|我无法|抱歉|以下是|Here is|Sure,|I cannot)", RegexOptions.IgnoreCase | RegexOptions.Compiled)]
    private static partial Regex MetaOutputRegex();
}
