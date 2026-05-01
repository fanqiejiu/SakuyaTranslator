using SakuyaTranslator.Core.Models;

namespace SakuyaTranslator.Core.Services;

public static class LanguageProfiles
{
    public static (string SourceLanguage, string TargetLanguage) FromPreset(string preset)
    {
        return preset switch
        {
            "英译中" => ("en", "zh-CN"),
            "中译日" => ("zh-CN", "ja"),
            "中译英" => ("zh-CN", "en"),
            _ => ("ja", "zh-CN")
        };
    }

    public static string BuildSystemPrompt(string preset)
    {
        var direction = preset switch
        {
            "英译中" => "Translate English game text into Simplified Chinese.",
            "中译日" => "Translate Simplified Chinese game text into Japanese.",
            "中译英" => "Translate Simplified Chinese game text into English.",
            _ => "Translate Japanese game text into Simplified Chinese."
        };

        return $"""
               {direction}
               This is a localization transformation task for user-provided game files. Translate only; do not continue, roleplay, judge, endorse, intensify, or add new content.
               Keep game variables, control codes, filenames, tags, numbers, and line breaks unchanged.
               Return only translated text for each input item, preserving item order.
               Use concise UI wording for short menu strings, and natural dialogue for character lines.
               Do not add policy notes, safety disclaimers, explanations, or refusal text to the translation output.
               """;
    }

    public static string BuildSystemPrompt(AppSettings settings)
    {
        return string.IsNullOrWhiteSpace(settings.CustomSystemPrompt)
            ? BuildSystemPrompt(settings.TranslationPreset)
            : settings.CustomSystemPrompt;
    }
}
