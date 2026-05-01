namespace MtTransTool.Core.Services;

public sealed class PortablePaths
{
    public PortablePaths(string? baseDirectory = null)
    {
        BaseDirectory = baseDirectory ?? AppContext.BaseDirectory;
        DataDirectory = Path.Combine(BaseDirectory, "Data");
        ProjectsDirectory = Path.Combine(BaseDirectory, "Projects");
        BackupsDirectory = Path.Combine(BaseDirectory, "Backups");
        LogsDirectory = Path.Combine(BaseDirectory, "Logs");
        OutputDirectory = Path.Combine(BaseDirectory, "output");
        LocalesDirectory = Path.Combine(BaseDirectory, "Locales");
    }

    public string BaseDirectory { get; }
    public string DataDirectory { get; }
    public string ProjectsDirectory { get; }
    public string BackupsDirectory { get; }
    public string LogsDirectory { get; }
    public string OutputDirectory { get; }
    public string LocalesDirectory { get; }

    public string SettingsPath => Path.Combine(DataDirectory, "settings.json");
    public string ApiProfilesPath => Path.Combine(DataDirectory, "api_profiles.json");
    public string HistoryPath => Path.Combine(DataDirectory, "history.json");
    public string OpenQueuePath => Path.Combine(DataDirectory, "open_queue.json");
    public string UpdateCachePath => Path.Combine(DataDirectory, "last_update_check.json");
    public string GlossaryPath => Path.Combine(DataDirectory, "glossary.csv");
    public string TranslationMemoryPath => Path.Combine(DataDirectory, "translation_memory.jsonl");

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(ProjectsDirectory);
        Directory.CreateDirectory(BackupsDirectory);
        Directory.CreateDirectory(LogsDirectory);
        Directory.CreateDirectory(OutputDirectory);
        Directory.CreateDirectory(LocalesDirectory);
    }
}
