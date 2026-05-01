namespace MtTransTool.Core.Models;

public sealed class AppSettings
{
    public string UiCulture { get; set; } = "zh-CN";
    public string ThemeMode { get; set; } = "深色";
    public string SourceLanguage { get; set; } = "ja";
    public string TargetLanguage { get; set; } = "zh-CN";
    public string TranslationPreset { get; set; } = "日译中";
    public string SpeedPreset { get; set; } = "普通";
    public int FileConcurrency { get; set; } = 1;
    public int RequestConcurrency { get; set; } = 3;
    public int BatchSize { get; set; } = 20;
    public int RetryCount { get; set; } = 2;
    public int RequestIntervalMs { get; set; } = 250;
    public int TimeoutSeconds { get; set; } = 90;
    public bool AutoBackup { get; set; } = true;
    public bool ConfirmBeforeDelete { get; set; } = true;
    public bool PreserveJsonFormat { get; set; } = true;
    public bool SkipCodeLikeEntries { get; set; } = true;
    public bool ValidatePlaceholders { get; set; } = true;
    public bool PromptResumeOnImport { get; set; } = true;
    public bool EnableExperimentalProofread { get; set; }
    public string CustomSystemPrompt { get; set; } = "";
    public bool CheckUpdatesOnStartup { get; set; } = true;
    public string UpdateCheckFrequency { get; set; } = "每天";
    public string UpdateChannel { get; set; } = "stable";
    public string GitHubRepository { get; set; } = "";
    public string FallbackUpdateJsonUrl { get; set; } = "";
}
