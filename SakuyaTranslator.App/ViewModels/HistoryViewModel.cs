using System.Collections.ObjectModel;
using SakuyaTranslator.Core.Models;
using SakuyaTranslator.Core.Services;

namespace SakuyaTranslator.App.ViewModels;

public sealed class HistoryViewModel : ViewModelBase
{
    private readonly HistoryStore _historyStore;
    private readonly TranslateQueueViewModel _queue;
    private string _statusText = "";

    public HistoryViewModel(HistoryStore historyStore, TranslateQueueViewModel queue)
    {
        _historyStore = historyStore;
        _queue = queue;
        Load();
    }

    public event EventHandler? ContinueRequested;

    public ObservableCollection<TranslationJobViewModel> OngoingJobs { get; } = [];
    public ObservableCollection<TranslationJobViewModel> CompletedJobs { get; } = [];

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    public void Load()
    {
        var state = _historyStore.Load();
        OngoingJobs.Clear();
        CompletedJobs.Clear();

        foreach (var job in state.OngoingJobs)
        {
            OngoingJobs.Add(new TranslationJobViewModel(job));
        }

        foreach (var job in state.CompletedJobs)
        {
            CompletedJobs.Add(new TranslationJobViewModel(job));
        }
    }

    public void SyncFromQueue(IEnumerable<TranslationJobViewModel> jobs)
    {
        Load();

        foreach (var job in jobs)
        {
            RemoveInMemory(job.Job.Id);
            if (job.Status == JobStatus.Completed)
            {
                CompletedJobs.Add(job);
            }
            else
            {
                OngoingJobs.Add(job);
            }
        }
    }

    public void Remove(TranslationJobViewModel job)
    {
        _historyStore.Remove(job.Job.Id);
        RemoveInMemory(job.Job.Id);
    }

    public async Task ContinueAsync(TranslationJobViewModel job)
    {
        if (string.IsNullOrWhiteSpace(job.FilePath) || !File.Exists(job.FilePath))
        {
            StatusText = $"找不到原文件：{job.FileName}";
            return;
        }

        await _queue.AddFilesAsync(
            [job.FilePath],
            _ => Task.FromResult(ResumeChoice.Continue),
            forceResumeCheck: true);

        StatusText = $"已加入待翻译：{job.FileName}";
        ContinueRequested?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveInMemory(string jobId)
    {
        foreach (var item in OngoingJobs.Where(x => x.Job.Id == jobId).ToArray())
        {
            OngoingJobs.Remove(item);
        }

        foreach (var item in CompletedJobs.Where(x => x.Job.Id == jobId).ToArray())
        {
            CompletedJobs.Remove(item);
        }
    }
}
