using MtTransTool.Core.Models;

namespace MtTransTool.Core.Services;

public sealed class HistoryStore
{
    private readonly PortablePaths _paths;
    private readonly JsonFileStore _jsonFileStore;

    public HistoryStore(PortablePaths paths, JsonFileStore jsonFileStore)
    {
        _paths = paths;
        _jsonFileStore = jsonFileStore;
    }

    public HistoryState Load()
    {
        return _jsonFileStore.LoadOrCreate(_paths.HistoryPath, new HistoryState());
    }

    public void Save(HistoryState state)
    {
        _jsonFileStore.Save(_paths.HistoryPath, state);
    }

    public void UpsertOngoing(TranslationJob job)
    {
        var state = Load();
        state.OngoingJobs.RemoveAll(x => x.Id == job.Id || string.Equals(x.FileHash, job.FileHash, StringComparison.OrdinalIgnoreCase));
        state.OngoingJobs.Add(job);
        Save(state);
    }

    public void MarkCompleted(TranslationJob job)
    {
        var state = Load();
        state.OngoingJobs.RemoveAll(x => x.Id == job.Id || string.Equals(x.FileHash, job.FileHash, StringComparison.OrdinalIgnoreCase));
        state.CompletedJobs.RemoveAll(x => x.Id == job.Id || string.Equals(x.FileHash, job.FileHash, StringComparison.OrdinalIgnoreCase));
        state.CompletedJobs.Add(job);
        Save(state);
    }

    public void Remove(string jobId)
    {
        var state = Load();
        state.OngoingJobs.RemoveAll(x => x.Id == jobId);
        state.CompletedJobs.RemoveAll(x => x.Id == jobId);
        Save(state);
    }
}
