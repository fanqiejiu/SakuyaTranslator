using MtTransTool.Core.Services;

namespace MtTransTool.App.ViewModels;

public sealed class LivePreviewViewModel : ViewModelBase
{
    private readonly TranslateQueueViewModel _queue;
    private readonly LocalizationService _localization;

    public LivePreviewViewModel(TranslateQueueViewModel queue, LocalizationService localization)
    {
        _queue = queue;
        _localization = localization;
        _queue.QueueChanged += (_, _) => Refresh();
    }

    private TranslationJobViewModel? CurrentJob => _queue.LatestPreviewJob ?? _queue.SelectedJob;
    private TranslationEntryViewModel? CurrentEntry => _queue.LatestPreviewEntry ?? _queue.SelectedEntry;

    public string CurrentFile => CurrentJob?.FileName ?? _localization["live.noFile"];
    public string StatusLine => CurrentJob?.StatusLine ?? _localization["live.waiting"];
    public string ProgressStatus => CurrentJob?.Status ?? "等待中";
    public double ProgressPercent => CurrentJob?.ProgressPercent ?? 0;
    public string ProgressPercentText => $"{ProgressPercent:0}%";
    public string EntryStatus => CurrentEntry is null ? "" : $"{CurrentEntry.Status}  #{CurrentEntry.Index}";
    public string SourceText => CurrentEntry?.SourceText ?? _localization["live.noSource"];
    public string TranslationText => CurrentEntry?.TranslationText ?? _localization["live.noTranslation"];

    private void Refresh()
    {
        OnPropertyChanged(nameof(CurrentFile));
        OnPropertyChanged(nameof(StatusLine));
        OnPropertyChanged(nameof(ProgressStatus));
        OnPropertyChanged(nameof(ProgressPercent));
        OnPropertyChanged(nameof(ProgressPercentText));
        OnPropertyChanged(nameof(EntryStatus));
        OnPropertyChanged(nameof(SourceText));
        OnPropertyChanged(nameof(TranslationText));
    }
}
