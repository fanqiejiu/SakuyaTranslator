using System.Collections.ObjectModel;
using SakuyaTranslator.Core.Models;
using SakuyaTranslator.Core.Services;

namespace SakuyaTranslator.App.ViewModels;

public sealed class TranslationJobViewModel : ViewModelBase
{
    private readonly TranslationJob _job;
    private ObservableCollection<TranslationEntryViewModel> _entries = [];
    private double _progressPercent;
    private string _status;
    private bool _isLoading;

    public TranslationJobViewModel(TranslationJob job)
    {
        _job = job;
        _status = job.Status;
        _progressPercent = job.ProgressPercent;
    }

    public TranslationJob Job => _job;
    public ITranslationDocument? Document { get; private set; }
    public string FileName => _job.FileName;
    public string FilePath => _job.FilePath;
    public int TotalCount => _job.TotalCount;
    public int CompletedCount => _job.CompletedCount;
    public int ErrorCount => _job.ErrorCount;
    public int WarningCount => IsLoaded ? Entries.Count(x => x.Status == TranslationStatus.DoneWithWarnings) : _job.WarningCount;
    public string Model => string.IsNullOrWhiteSpace(_job.Model) ? "未选择模型" : _job.Model;
    public string ModelText => $"模型 {Model}";
    public string TranslationDirection => _job.TranslationDirection;
    public string ProgressPercentText => $"{ProgressPercent:0}%";
    public string CompletedRatioText => $"{CompletedCount}/{TotalCount}";
    public string ErrorCountText => $"错误 {ErrorCount}";
    public string WarningCountText => $"警告 {WarningCount}";

    public string Status
    {
        get => _status;
        set
        {
            if (SetProperty(ref _status, value))
            {
                _job.Status = value;
                OnPropertyChanged(nameof(StatusLine));
            }
        }
    }

    public double ProgressPercent
    {
        get => _progressPercent;
        private set => SetProperty(ref _progressPercent, value);
    }

    public string StatusLine => $"{Status}  {CompletedCount}/{TotalCount}  错误 {ErrorCount}  警告 {WarningCount}";
    public bool IsLoaded => Document is not null;

    public bool IsLoading
    {
        get => _isLoading;
        private set => SetProperty(ref _isLoading, value);
    }

    public ObservableCollection<TranslationEntryViewModel> Entries
    {
        get => _entries;
        private set => SetProperty(ref _entries, value);
    }

    public async Task LoadAsync(TranslationDocumentLoader loader, AppSettings settings, CancellationToken cancellationToken = default)
    {
        if (IsLoaded)
        {
            return;
        }

        IsLoading = true;
        try
        {
            var result = await Task.Run(async () =>
            {
                var document = await loader.LoadAsync(_job.FilePath, cancellationToken).ConfigureAwait(false);
                var hash = await TranslationJob.ComputeSha256Async(_job.FilePath, cancellationToken).ConfigureAwait(false);
                return (Document: document, FileHash: hash);
            }, cancellationToken);

            Document = result.Document;
            _job.FileHash = result.FileHash;
        }
        finally
        {
            IsLoading = false;
        }

        _job.TotalCount = Document.Entries.Count;
        _job.CompletedCount = Document.Entries.Count(x => x.Status is TranslationStatus.Done or TranslationStatus.DoneWithWarnings or TranslationStatus.Skipped);
        _job.ErrorCount = Document.Entries.Count(x => x.Status == TranslationStatus.Error);
        _job.WarningCount = Document.Entries.Count(x => x.Status == TranslationStatus.DoneWithWarnings);
        _job.UpdatedAt = DateTimeOffset.Now;

        Entries = new ObservableCollection<TranslationEntryViewModel>(
            Document.Entries.Select(x => new TranslationEntryViewModel(x)));

        OnPropertyChanged(nameof(IsLoaded));
        RefreshProgress();
    }

    public void ApplyProject(TranslationProject project)
    {
        if (Document is null)
        {
            return;
        }

        var snapshots = project.Entries.ToDictionary(x => x.SourceText, StringComparer.Ordinal);
        foreach (var entry in Entries)
        {
            if (!snapshots.TryGetValue(entry.SourceText, out var snapshot))
            {
                continue;
            }

            entry.Model.TranslationText = snapshot.TranslationText;
            entry.Model.Status = snapshot.Status;
            entry.Model.Warning = snapshot.Warning;
            entry.Model.ErrorMessage = snapshot.ErrorMessage;
            entry.Status = snapshot.Status;
            entry.Warning = snapshot.Warning;
        }

        _job.Id = string.IsNullOrWhiteSpace(project.Job.Id) ? _job.Id : project.Job.Id;
        _job.OutputPath = string.IsNullOrWhiteSpace(project.Job.OutputPath) ? _job.OutputPath : project.Job.OutputPath;
        SetModel(project.Job.Model);
        _job.TranslationDirection = project.Job.TranslationDirection;
        _job.SpeedPreset = project.Job.SpeedPreset;
        OnPropertyChanged(nameof(TranslationDirection));
        RefreshProgress();
    }

    public void SetModel(string model)
    {
        if (_job.Model == model)
        {
            return;
        }

        _job.Model = model;
        OnPropertyChanged(nameof(Model));
        OnPropertyChanged(nameof(ModelText));
    }

    public string ExportText()
    {
        if (Document is null)
        {
            return "";
        }

        return Document.ExportPreservingFormat(Entries.Select(x => x.Model));
    }

    public void ExportTo(string outputPath)
    {
        Document?.ExportTo(outputPath, Entries.Select(x => x.Model));
    }

    public void RefreshProgress()
    {
        _job.CompletedCount = Entries.Count(x => x.Status is TranslationStatus.Done or TranslationStatus.DoneWithWarnings or TranslationStatus.Skipped);
        _job.ErrorCount = Entries.Count(x => x.Status == TranslationStatus.Error);
        _job.WarningCount = Entries.Count(x => x.Status == TranslationStatus.DoneWithWarnings);
        ProgressPercent = _job.ProgressPercent;
        OnPropertyChanged(nameof(CompletedCount));
        OnPropertyChanged(nameof(ErrorCount));
        OnPropertyChanged(nameof(WarningCount));
        OnPropertyChanged(nameof(CompletedRatioText));
        OnPropertyChanged(nameof(ErrorCountText));
        OnPropertyChanged(nameof(WarningCountText));
        OnPropertyChanged(nameof(StatusLine));
        OnPropertyChanged(nameof(ProgressPercentText));
    }
}
