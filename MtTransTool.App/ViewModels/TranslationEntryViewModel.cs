using MtTransTool.Core.Models;
using MtTransTool.Core.Services;

namespace MtTransTool.App.ViewModels;

public sealed class TranslationEntryViewModel : ViewModelBase
{
    private readonly TranslationEntry _entry;
    private string _translationText;
    private string _status;
    private string _warning;

    public TranslationEntryViewModel(TranslationEntry entry)
    {
        _entry = entry;
        _translationText = entry.TranslationText;
        _status = entry.Status;
        _warning = entry.Warning;
    }

    public TranslationEntry Model => _entry;
    public int Index => _entry.Index + 1;
    public string SourceText => _entry.SourceText;

    public string TranslationText
    {
        get => _translationText;
        set
        {
            if (SetProperty(ref _translationText, value))
            {
                _entry.TranslationText = value;
                var warning = PlaceholderValidator.Validate(SourceText, value);
                Warning = warning;
                _entry.ErrorMessage = "";
                Status = string.Equals(SourceText, value, StringComparison.Ordinal)
                    ? TranslationStatus.Pending
                    : string.IsNullOrWhiteSpace(warning)
                        ? TranslationStatus.Done
                        : TranslationStatus.DoneWithWarnings;
            }
        }
    }

    public string Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                _entry.Status = value;
            }
        }
    }

    public string Warning
    {
        get => _warning;
        set
        {
            if (SetProperty(ref _warning, value))
            {
                _entry.Warning = value;
            }
        }
    }
}
