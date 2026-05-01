namespace MtTransTool.App.ViewModels;

public sealed class PreviewViewModel : ViewModelBase
{
    public PreviewViewModel(TranslateQueueViewModel queue)
    {
        Queue = queue;
        queue.QueueChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(SourceText));
            OnPropertyChanged(nameof(TranslationText));
            OnPropertyChanged(nameof(CurrentFile));
        };
    }

    public TranslateQueueViewModel Queue { get; }

    private TranslationJobViewModel? CurrentJob => Queue.SelectedJob ?? Queue.LatestPreviewJob;

    private TranslationEntryViewModel? CurrentEntry =>
        Queue.SelectedEntry
        ?? Queue.LatestPreviewEntry
        ?? CurrentJob?.Entries.FirstOrDefault();

    public string CurrentFile => CurrentJob?.FileName ?? "未选择文件";
    public string SourceText => CurrentEntry?.SourceText ?? "在“待翻译”页面选择一条文本后，这里会显示原文。";
    public string TranslationText => CurrentEntry?.TranslationText ?? "译文预览会保留换行，便于检查游戏文本显示效果。";
}
