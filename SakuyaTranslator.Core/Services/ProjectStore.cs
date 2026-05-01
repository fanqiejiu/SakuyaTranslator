using System.Text.RegularExpressions;
using SakuyaTranslator.Core.Models;

namespace SakuyaTranslator.Core.Services;

public sealed partial class ProjectStore
{
    private readonly PortablePaths _paths;
    private readonly JsonFileStore _jsonFileStore;

    public ProjectStore(PortablePaths paths, JsonFileStore jsonFileStore)
    {
        _paths = paths;
        _jsonFileStore = jsonFileStore;
    }

    public async Task<TranslationProject?> FindResumeProjectAsync(string sourceFilePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(sourceFilePath))
        {
            return null;
        }

        Directory.CreateDirectory(_paths.ProjectsDirectory);
        var hash = await TranslationJob.ComputeSha256Async(sourceFilePath, cancellationToken);

        foreach (var projectPath in Directory.EnumerateFiles(_paths.ProjectsDirectory, "*.mtproj.json"))
        {
            TranslationProject? project;
            try
            {
                project = _jsonFileStore.LoadOrCreate<TranslationProject?>(projectPath, null);
            }
            catch
            {
                continue;
            }

            if (project?.Job is null)
            {
                continue;
            }

            if (string.Equals(project.Job.FileHash, hash, StringComparison.OrdinalIgnoreCase)
                || string.Equals(project.Job.FilePath, sourceFilePath, StringComparison.OrdinalIgnoreCase))
            {
                NormalizeLegacyWarnings(project);
                return project;
            }
        }

        return null;
    }

    public string GetProjectPath(TranslationJob job)
    {
        var name = Path.GetFileNameWithoutExtension(job.FileName);
        var safeName = UnsafeFileNameRegex().Replace(name, "_");
        var hash = string.IsNullOrWhiteSpace(job.FileHash) ? "new" : job.FileHash[..Math.Min(12, job.FileHash.Length)];
        return Path.Combine(_paths.ProjectsDirectory, $"{safeName}.{hash}.mtproj.json");
    }

    public void SaveProject(TranslationJob job, IEnumerable<TranslationEntry> entries)
    {
        job.UpdatedAt = DateTimeOffset.Now;
        var project = new TranslationProject
        {
            Job = job,
            Entries = entries.Select(x => new TranslationEntrySnapshot
            {
                Index = x.Index,
                SourceText = x.SourceText,
                TranslationText = x.TranslationText,
                Status = x.Status,
                Warning = x.Warning,
                ErrorMessage = x.ErrorMessage
            }).ToList()
        };

        _jsonFileStore.Save(GetProjectPath(job), project);
    }

    public List<TranslationJob> LoadOpenQueue()
    {
        return _jsonFileStore.LoadOrCreate(_paths.OpenQueuePath, new List<TranslationJob>());
    }

    public void SaveOpenQueue(IEnumerable<TranslationJob> jobs)
    {
        _jsonFileStore.Save(_paths.OpenQueuePath, jobs.ToList());
    }

    private static void NormalizeLegacyWarnings(TranslationProject project)
    {
        foreach (var entry in project.Entries)
        {
            if (entry.Status != TranslationStatus.Error
                || !string.IsNullOrWhiteSpace(entry.ErrorMessage)
                || string.IsNullOrWhiteSpace(entry.TranslationText)
                || string.IsNullOrWhiteSpace(entry.Warning))
            {
                continue;
            }

            if (entry.Warning.Contains("占位符/控制符可能不一致", StringComparison.Ordinal)
                || entry.Warning.Contains("换行数量不一致", StringComparison.Ordinal))
            {
                entry.Status = TranslationStatus.DoneWithWarnings;
            }
        }
    }

    [GeneratedRegex(@"[^\w\-.]+", RegexOptions.Compiled)]
    private static partial Regex UnsafeFileNameRegex();
}
