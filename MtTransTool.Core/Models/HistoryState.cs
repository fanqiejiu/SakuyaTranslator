namespace MtTransTool.Core.Models;

public sealed class HistoryState
{
    public List<TranslationJob> OngoingJobs { get; set; } = [];
    public List<TranslationJob> CompletedJobs { get; set; } = [];
}
