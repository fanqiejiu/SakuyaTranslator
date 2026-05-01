using SakuyaTranslator.Core.Models;

namespace SakuyaTranslator.Core.Services;

public static class TranslationSpeedProfiles
{
    public static readonly IReadOnlyList<string> Names = ["保守", "普通", "快速", "批量并发", "自定义"];

    public static void Apply(AppSettings settings, string preset)
    {
        settings.SpeedPreset = preset;

        switch (preset)
        {
            case "保守":
                settings.FileConcurrency = 1;
                settings.RequestConcurrency = 1;
                settings.BatchSize = 10;
                settings.RequestIntervalMs = 500;
                break;
            case "快速":
                settings.FileConcurrency = 2;
                settings.RequestConcurrency = 5;
                settings.BatchSize = 30;
                settings.RequestIntervalMs = 120;
                break;
            case "批量并发":
                settings.FileConcurrency = 3;
                settings.RequestConcurrency = 8;
                settings.BatchSize = 50;
                settings.RequestIntervalMs = 60;
                break;
            case "普通":
                settings.FileConcurrency = 1;
                settings.RequestConcurrency = 3;
                settings.BatchSize = 20;
                settings.RequestIntervalMs = 250;
                break;
        }
    }
}
