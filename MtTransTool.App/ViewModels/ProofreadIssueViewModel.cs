using MtTransTool.Core.Models;

namespace MtTransTool.App.ViewModels;

public sealed class ProofreadIssueViewModel : ViewModelBase
{
    private readonly ProofreadIssue _issue;

    public ProofreadIssueViewModel(ProofreadIssue issue)
    {
        _issue = issue;
    }

    public ProofreadIssue Model => _issue;
    public int EntryIndex => _issue.EntryIndex;
    public int Index => _issue.EntryIndex + 1;
    public string Severity => _issue.Severity;
    public string Origin => _issue.Origin;
    public string Category => _issue.Category;
    public string Message => _issue.Message;
    public string Suggestion => _issue.Suggestion;
    public string ReplacementText => _issue.ReplacementText;
    public string SourceText => _issue.SourceText;
    public string TranslationText => _issue.TranslationText;
    public bool HasReplacement => !string.IsNullOrWhiteSpace(_issue.ReplacementText);
    public string DisplayCategory => string.IsNullOrWhiteSpace(Origin) ? Category : $"{Origin}/{Category}";
}
