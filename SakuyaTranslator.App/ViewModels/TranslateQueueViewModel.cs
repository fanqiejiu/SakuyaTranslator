using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using Avalonia.Threading;
using SakuyaTranslator.Core.Models;
using SakuyaTranslator.Core.Services;

namespace SakuyaTranslator.App.ViewModels;

public sealed class TranslateQueueViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly PortablePaths _paths;
    private readonly ProjectStore _projectStore;
    private readonly HistoryStore _historyStore;
    private readonly IList<ApiProfile> _apiProfiles;
    private readonly LogsViewModel? _logs;
    private readonly LocalizationService? _localization;
    private readonly ITranslationClient _translationClient;
    private readonly IProofreadingClient _proofreadingClient;
    private readonly RuleBasedProofreader _ruleProofreader = new();
    private readonly TranslationDocumentLoader _documentLoader = new();
    private TranslationJobViewModel? _selectedJob;
    private TranslationEntryViewModel? _selectedEntry;
    private ProofreadIssueViewModel? _selectedProofreadIssue;
    private ProofreadModelOption? _selectedProofreadModel;
    private bool _isDirectoryVisible = true;
    private bool _isLiveDetailVisible;
    private bool _isProofreadVisible;
    private bool _isProofreading;
    private string _proofreadStatus = "尚未校对";
    private string _queueNotice = "";
    private readonly Dictionary<string, List<LiveStreamSnapshot>> _liveStreams = new(StringComparer.Ordinal);
    private CancellationTokenSource? _queueCancellation;
    private CancellationTokenSource? _proofreadCancellation;
    private string _queueInterruptionStatus = JobStatus.Cancelled;

    public TranslateQueueViewModel(
        AppSettings settings,
        PortablePaths paths,
        ProjectStore projectStore,
        HistoryStore historyStore,
        IList<ApiProfile> apiProfiles,
        ITranslationClient? translationClient = null,
        IProofreadingClient? proofreadingClient = null,
        LocalizationService? localization = null,
        LogsViewModel? logs = null)
    {
        _settings = settings;
        _paths = paths;
        _projectStore = projectStore;
        _historyStore = historyStore;
        _apiProfiles = apiProfiles;
        _logs = logs;
        _localization = localization;
        _translationClient = translationClient ?? new OpenAiCompatibleTranslationClient();
        _proofreadingClient = proofreadingClient ?? new OpenAiCompatibleProofreadingClient();
        _proofreadStatus = Text("proof.status.notRun");
        StartQueueCommand = new AsyncRelayCommand(
            async _ => await StartQueueAsync(),
            ex => _logs?.Add($"队列任务异常：{ex.Message}"));
        PauseCommand = new RelayCommand(_ => PauseQueue());
        StopCommand = new RelayCommand(_ => StopQueue());
        BackToDirectoryCommand = new RelayCommand(_ => ShowDirectory());
        ShowEntryGridCommand = new RelayCommand(_ => ShowEntryGrid());
        ShowLiveDetailCommand = new RelayCommand(_ => ShowLiveDetail());
        ShowProofreadCommand = new RelayCommand(_ => ShowProofread());
        RunRuleProofreadCommand = new RelayCommand(_ => RunRuleProofread(), _ => IsProofreadAvailable);
        RunAiProofreadCommand = new AsyncRelayCommand(
            async _ => await RunAiProofreadAsync(),
            ex => ProofreadStatus = $"AI 校对异常：{ex.Message}");
        CancelProofreadCommand = new RelayCommand(_ => CancelProofread(), _ => IsProofreading);
        ApplyProofreadSuggestionCommand = new RelayCommand(
            _ => ApplyProofreadSuggestion(),
            _ => SelectedProofreadIssue?.HasReplacement == true && SelectedJob is not null);
        RemoveSelectedCommand = new RelayCommand(_ => RemoveSelected(), _ => SelectedJob is not null);
        SaveProjectCommand = new RelayCommand(_ => SaveSelectedProject(), _ => SelectedJob is not null);
        ExportSelectedCommand = new RelayCommand(_ => ExportSelected(), _ => SelectedJob is not null);
        ExportAllCommand = new RelayCommand(_ => ExportAll(), _ => Jobs.Count > 0);
        RefreshProofreadModelOptions();
    }

    public event EventHandler? QueueChanged;

    public ObservableCollection<TranslationJobViewModel> Jobs { get; } = [];
    public ObservableCollection<TranslationEntryViewModel> EmptyEntries { get; } = [];
    public ObservableCollection<ProofreadIssueViewModel> ProofreadIssues { get; } = [];
    public ObservableCollection<ProofreadModelOption> ProofreadModelOptions { get; } = [];
    public TranslationJobViewModel? LatestPreviewJob { get; private set; }
    public TranslationEntryViewModel? LatestPreviewEntry { get; private set; }

    public sealed record ProofreadModelOption(ApiProfile Profile, string DisplayName, string DetailText)
    {
        public override string ToString() => DisplayName;
    }

    private sealed record LiveStreamSnapshot(
        int EntryIndex,
        string SourceText,
        string TranslationText,
        string Status,
        string Warning,
        DateTimeOffset UpdatedAt);

    public TranslationJobViewModel? SelectedJob
    {
        get => _selectedJob;
        set
        {
            if (SetProperty(ref _selectedJob, value))
            {
                SelectedEntry = null;
                ClearProofreadResults(Text("proof.status.notRun"));
                OnPropertyChanged(nameof(SelectedEntries));
                RaiseLiveDetailProperties();
                RaiseCommandStates();
                QueueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public TranslationEntryViewModel? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                RaiseLiveDetailProperties();
                QueueChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public ProofreadIssueViewModel? SelectedProofreadIssue
    {
        get => _selectedProofreadIssue;
        set
        {
            if (SetProperty(ref _selectedProofreadIssue, value))
            {
                ApplyProofreadSuggestionCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public ProofreadModelOption? SelectedProofreadModel
    {
        get => _selectedProofreadModel;
        set
        {
            if (SetProperty(ref _selectedProofreadModel, value))
            {
                OnPropertyChanged(nameof(ProofreadModelHelpText));
            }
        }
    }

    public ObservableCollection<TranslationEntryViewModel> SelectedEntries => SelectedJob?.Entries ?? EmptyEntries;

    public bool IsDirectoryVisible
    {
        get => _isDirectoryVisible;
        private set
        {
            if (SetProperty(ref _isDirectoryVisible, value))
            {
                OnPropertyChanged(nameof(IsDetailVisible));
                OnPropertyChanged(nameof(IsEntryGridVisible));
                OnPropertyChanged(nameof(IsLiveDetailVisible));
                OnPropertyChanged(nameof(IsProofreadVisible));
                OnPropertyChanged(nameof(IsEntryGridModeSelected));
                OnPropertyChanged(nameof(IsLiveDetailModeSelected));
                OnPropertyChanged(nameof(IsProofreadModeSelected));
            }
        }
    }

    public bool IsDetailVisible => !IsDirectoryVisible;

    public bool IsEntryGridVisible => IsDetailVisible && !_isLiveDetailVisible && !IsProofreadPanelVisible;

    public bool IsEntryGridModeSelected
    {
        get => IsEntryGridVisible;
        set
        {
            if (value)
            {
                ShowEntryGrid();
                return;
            }

            OnPropertyChanged();
        }
    }

    public bool IsLiveDetailVisible
    {
        get => IsDetailVisible && _isLiveDetailVisible;
        private set
        {
            if (SetProperty(ref _isLiveDetailVisible, value))
            {
                OnPropertyChanged(nameof(IsEntryGridVisible));
                OnPropertyChanged(nameof(IsEntryGridModeSelected));
                OnPropertyChanged(nameof(IsLiveDetailModeSelected));
                OnPropertyChanged(nameof(IsProofreadModeSelected));
                RaiseLiveDetailProperties();
            }
        }
    }

    public bool IsLiveDetailModeSelected
    {
        get => IsLiveDetailVisible;
        set
        {
            if (value)
            {
                ShowLiveDetail();
                return;
            }

            OnPropertyChanged();
        }
    }

    public bool IsProofreadEntryVisible => _settings.EnableExperimentalProofread;

    public bool IsProofreadVisible
    {
        get => IsDetailVisible && _isProofreadVisible;
        private set
        {
            if (SetProperty(ref _isProofreadVisible, value))
            {
                OnPropertyChanged(nameof(IsEntryGridVisible));
                OnPropertyChanged(nameof(IsEntryGridModeSelected));
                OnPropertyChanged(nameof(IsLiveDetailModeSelected));
                OnPropertyChanged(nameof(IsProofreadModeSelected));
            }
        }
    }

    public bool IsProofreadPanelVisible => IsProofreadEntryVisible && IsProofreadVisible;

    public bool IsProofreadModeSelected
    {
        get => IsProofreadPanelVisible;
        set
        {
            if (value)
            {
                ShowProofread();
                return;
            }

            OnPropertyChanged();
        }
    }

    private TranslationEntryViewModel? CurrentLiveEntry =>
        LatestPreviewJob == SelectedJob && LatestPreviewEntry is not null
            ? LatestPreviewEntry
            : SelectedEntry ?? SelectedJob?.Entries.FirstOrDefault();

    public string LiveCurrentFile => SelectedJob?.FileName ?? "未选择文件";
    public string LiveStatusLine => SelectedJob?.StatusLine ?? "等待翻译";
    public string QueueNotice
    {
        get => _queueNotice;
        private set
        {
            if (SetProperty(ref _queueNotice, value))
            {
                OnPropertyChanged(nameof(HasQueueNotice));
            }
        }
    }

    public bool HasQueueNotice => !string.IsNullOrWhiteSpace(QueueNotice);
    public double LiveProgressPercent => SelectedJob?.ProgressPercent ?? 0;
    public string LiveProgressPercentText => $"{LiveProgressPercent:0}%";
    public string LiveEntryStatus => CurrentLiveEntry is null ? "" : $"{CurrentLiveEntry.Status}  #{CurrentLiveEntry.Index}";
    public string LiveSourceText => CurrentLiveEntry?.SourceText ?? "暂无原文。";
    public string LiveTranslationText => CurrentLiveEntry?.TranslationText ?? "暂无译文。开始翻译后会实时显示当前文件的内容。";
    public string LiveTerminalText => BuildLiveTerminalText();
    public string LiveSourceStreamText => BuildLiveStreamText(isSource: true);
    public string LiveTranslationStreamText => BuildLiveStreamText(isSource: false);
    public bool IsProofreading
    {
        get => _isProofreading;
        private set
        {
            if (SetProperty(ref _isProofreading, value))
            {
                CancelProofreadCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public string ProofreadStatus
    {
        get => _proofreadStatus;
        private set => SetProperty(ref _proofreadStatus, value);
    }

    public string ProofreadSummaryText => ProofreadIssues.Count == 0
        ? Text("proof.summary.none")
        : string.Format(Text("proof.summary.count"), ProofreadIssues.Count);
    public bool IsProofreadAvailable => SelectedJob is not null
        && SelectedJob.Document is not null
        && SelectedJob.TotalCount > 0
        && SelectedJob.Entries.All(x => x.Status is TranslationStatus.Done or TranslationStatus.DoneWithWarnings or TranslationStatus.Skipped);
    public string ProofreadReadinessText
    {
        get
        {
            if (SelectedJob is null)
            {
                return Text("proof.ready.select");
            }

            if (IsProofreadAvailable)
            {
                return SelectedJob.WarningCount > 0
                    ? string.Format(Text("proof.ready.okWithWarnings"), SelectedJob.WarningCount)
                    : Text("proof.ready.ok");
            }

            return string.Format(
                Text("proof.ready.wait"),
                SelectedJob.CompletedCount,
                SelectedJob.TotalCount,
                SelectedJob.ErrorCount);
        }
    }

    public string ProofreadModelHelpText => SelectedProofreadModel is null
        ? Text("proof.model.none")
        : string.Format(Text("proof.model.current"), SelectedProofreadModel.DetailText);

    public ICommand StartQueueCommand { get; }
    public RelayCommand PauseCommand { get; }
    public RelayCommand StopCommand { get; }
    public RelayCommand BackToDirectoryCommand { get; }
    public RelayCommand ShowEntryGridCommand { get; }
    public RelayCommand ShowLiveDetailCommand { get; }
    public RelayCommand ShowProofreadCommand { get; }
    public RelayCommand RunRuleProofreadCommand { get; }
    public ICommand RunAiProofreadCommand { get; }
    public RelayCommand CancelProofreadCommand { get; }
    public RelayCommand ApplyProofreadSuggestionCommand { get; }
    public RelayCommand RemoveSelectedCommand { get; }
    public RelayCommand SaveProjectCommand { get; }
    public RelayCommand ExportSelectedCommand { get; }
    public RelayCommand ExportAllCommand { get; }

    public Task RestoreOpenQueueAsync(CancellationToken cancellationToken = default)
    {
        var savedJobs = _projectStore.LoadOpenQueue()
            .Where(x => File.Exists(x.FilePath))
            .Where(x => _documentLoader.IsSupported(x.FilePath))
            .ToList();

        if (savedJobs.Count == 0)
        {
            savedJobs = _historyStore.Load().OngoingJobs
                .Where(x => File.Exists(x.FilePath))
                .Where(x => _documentLoader.IsSupported(x.FilePath))
                .ToList();
        }

        foreach (var savedJob in savedJobs)
        {
            if (Jobs.Any(x => string.Equals(x.FilePath, savedJob.FilePath, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            var vm = new TranslationJobViewModel(savedJob);
            EnsureOutputPath(vm.Job);

            vm.Status = NormalizeRestoredStatus(vm.Status);
            Jobs.Add(vm);
            SelectedJob ??= vm;
        }

        if (Jobs.Count > 0)
        {
            ShowDirectory();
            PersistOpenQueue();
            _logs?.Add($"已恢复上次打开的文件：{Jobs.Count} 个");
        }

        RaiseCommandStates();
        QueueChanged?.Invoke(this, EventArgs.Empty);
        return Task.CompletedTask;
    }

    public async Task AddFilesAsync(
        IEnumerable<string> filePaths,
        Func<TranslationProject, Task<ResumeChoice>>? resumeSelector = null,
        bool forceResumeCheck = false,
        CancellationToken cancellationToken = default)
    {
        foreach (var path in filePaths.Where(File.Exists).Where(_documentLoader.IsSupported))
        {
            var existing = Jobs.FirstOrDefault(x => string.Equals(x.FilePath, path, StringComparison.OrdinalIgnoreCase));
            if (existing is not null)
            {
                SelectedJob = existing;
                continue;
            }

            var job = new TranslationJob
            {
                FilePath = path,
                FileName = Path.GetFileName(path),
                TranslationDirection = _settings.TranslationPreset,
                SpeedPreset = _settings.SpeedPreset
            };
            EnsureOutputPath(job);

            var vm = new TranslationJobViewModel(job);
            await vm.LoadAsync(_documentLoader, _settings, cancellationToken);
            _logs?.Add($"已加入任务：{job.FileName}");

            var resumeProject = await _projectStore.FindResumeProjectAsync(path, cancellationToken);
            if (resumeProject is not null
                && resumeSelector is not null
                && (_settings.PromptResumeOnImport || forceResumeCheck))
            {
                var choice = await resumeSelector(resumeProject);
                if (choice == ResumeChoice.Cancel)
                {
                    continue;
                }

                if (choice is ResumeChoice.Continue or ResumeChoice.ViewOnly)
                {
                    vm.ApplyProject(resumeProject);
                    vm.Status = choice == ResumeChoice.ViewOnly
                        ? JobStatus.Paused
                        : NormalizeResumeStatus(resumeProject.Job.Status);
                    if (vm.Status == JobStatus.Completed)
                    {
                        ExportJob(vm);
                    }
                }
            }

            Jobs.Add(vm);
            SelectedJob ??= vm;
            PersistJob(vm);
        }

        PersistOpenQueue();
        RaiseCommandStates();
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshSelectedProgress()
    {
        SelectedJob?.RefreshProgress();
        RaiseLiveDetailProperties();
        RaiseProofreadStateProperties();
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task<bool> EnsureJobLoadedAsync(
        TranslationJobViewModel job,
        CancellationToken cancellationToken = default)
    {
        if (job.IsLoaded)
        {
            return true;
        }

        try
        {
            QueueNotice = $"正在加载文件：{job.FileName}";
            await job.LoadAsync(_documentLoader, _settings, cancellationToken);
            EnsureOutputPath(job.Job);

            var resumeProject = await Task.Run(
                async () => await _projectStore.FindResumeProjectAsync(job.FilePath, cancellationToken).ConfigureAwait(false),
                cancellationToken);
            if (resumeProject is not null)
            {
                job.ApplyProject(resumeProject);
                EnsureOutputPath(job.Job);
            }

            QueueNotice = $"已加载：{job.FileName}";
            OnPropertyChanged(nameof(SelectedEntries));
            RaiseLiveDetailProperties();
            RaiseCommandStates();
            QueueChanged?.Invoke(this, EventArgs.Empty);
            return true;
        }
        catch (OperationCanceledException)
        {
            QueueNotice = $"已取消加载：{job.FileName}";
            return false;
        }
        catch (Exception ex)
        {
            job.Status = JobStatus.Error;
            QueueNotice = $"加载失败：{job.FileName} - {ex.Message}";
            _logs?.Add(QueueNotice);
            QueueChanged?.Invoke(this, EventArgs.Empty);
            return false;
        }
    }

    public void RefreshProofreadModelOptions()
    {
        var selectedProfile = SelectedProofreadModel?.Profile;
        ProofreadModelOptions.Clear();

        foreach (var profile in _apiProfiles.Where(IsUsableProofreadProfile))
        {
            var provider = string.IsNullOrWhiteSpace(profile.Provider) ? "API" : profile.Provider;
            var displayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? provider : profile.DisplayName;
            var model = string.IsNullOrWhiteSpace(profile.Model) ? "未配置模型" : profile.Model;
            ProofreadModelOptions.Add(new ProofreadModelOption(profile, model, $"{displayName} / {provider}"));
        }

        SelectedProofreadModel = ProofreadModelOptions.FirstOrDefault(x => ReferenceEquals(x.Profile, selectedProfile))
            ?? ProofreadModelOptions.FirstOrDefault(x => x.Profile.IsActive)
            ?? ProofreadModelOptions.FirstOrDefault();
        OnPropertyChanged(nameof(ProofreadModelOptions));
        OnPropertyChanged(nameof(ProofreadModelHelpText));
    }

    public void RefreshProofreadFeatureState()
    {
        if (!IsProofreadEntryVisible && _isProofreadVisible)
        {
            ShowEntryGrid();
        }

        OnPropertyChanged(nameof(IsProofreadEntryVisible));
        OnPropertyChanged(nameof(IsProofreadPanelVisible));
        OnPropertyChanged(nameof(IsProofreadModeSelected));
        OnPropertyChanged(nameof(IsEntryGridVisible));
    }

    public async Task ShowSelectedJobDetailAsync(CancellationToken cancellationToken = default)
    {
        if (SelectedJob is not null)
        {
            if (!await EnsureJobLoadedAsync(SelectedJob, cancellationToken))
            {
                return;
            }

            IsDirectoryVisible = false;
            ShowEntryGrid();
        }
    }

    public void ShowDirectory()
    {
        IsDirectoryVisible = true;
        IsLiveDetailVisible = false;
        IsProofreadVisible = false;
    }

    public void ShowEntryGrid()
    {
        IsLiveDetailVisible = false;
        IsProofreadVisible = false;
        OnPropertyChanged(nameof(IsEntryGridModeSelected));
        OnPropertyChanged(nameof(IsLiveDetailModeSelected));
        OnPropertyChanged(nameof(IsProofreadModeSelected));
    }

    public void ShowLiveDetail()
    {
        if (SelectedJob is not null)
        {
            IsDirectoryVisible = false;
            IsProofreadVisible = false;
            IsLiveDetailVisible = true;
            OnPropertyChanged(nameof(IsEntryGridModeSelected));
            OnPropertyChanged(nameof(IsLiveDetailModeSelected));
            OnPropertyChanged(nameof(IsProofreadModeSelected));
        }
    }

    public void ShowProofread()
    {
        if (!IsProofreadEntryVisible)
        {
            ShowEntryGrid();
            return;
        }

        if (SelectedJob is not null)
        {
            IsDirectoryVisible = false;
            IsLiveDetailVisible = false;
            IsProofreadVisible = true;
            if (!IsProofreadAvailable)
            {
                ProofreadStatus = ProofreadReadinessText;
            }

            OnPropertyChanged(nameof(IsEntryGridModeSelected));
            OnPropertyChanged(nameof(IsLiveDetailModeSelected));
            OnPropertyChanged(nameof(IsProofreadModeSelected));
        }
    }

    private void MarkSelectedOrAll(string status)
    {
        var targets = GetCommandTargets();
        foreach (var job in targets)
        {
            job.Status = status;
            ResetRunningEntries(job);
            job.RefreshProgress();
            RaiseLiveDetailProperties();
            if (job.Document is not null)
            {
                _projectStore.SaveProject(job.Job, job.Entries.Select(x => x.Model));
                _historyStore.UpsertOngoing(job.Job);
            }
        }

        PersistOpenQueue();
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void PauseQueue()
    {
        _queueInterruptionStatus = JobStatus.Paused;
        _queueCancellation?.Cancel();
        MarkSelectedOrAll(JobStatus.Paused);
        _logs?.Add("已暂停翻译，可点击开始继续。");
    }

    private void RunRuleProofread()
    {
        if (SelectedJob is null)
        {
            ProofreadStatus = Text("proof.status.selectJson");
            return;
        }

        ShowProofread();
        if (!IsProofreadAvailable)
        {
            ClearProofreadResults(ProofreadReadinessText);
            return;
        }

        ClearProofreadResults(Text("proof.status.ruleRunning"));
        var issues = _ruleProofreader.Analyze(
            SelectedJob.Entries
                .Where(x => x.Status is TranslationStatus.Done or TranslationStatus.DoneWithWarnings)
                .Select(x => x.Model),
            _settings,
            SelectedJob.Document?.Kind ?? DocumentKind.Txt);
        foreach (var issue in issues)
        {
            ProofreadIssues.Add(new ProofreadIssueViewModel(issue));
        }

        SelectedProofreadIssue = ProofreadIssues.FirstOrDefault();
        ProofreadStatus = string.Format(Text("proof.status.ruleDone"), ProofreadIssues.Count);
        OnPropertyChanged(nameof(ProofreadSummaryText));
        _logs?.Add($"{Text("proof.log.ruleDone")}：{SelectedJob.FileName} - {ProofreadIssues.Count}");
    }

    private async Task RunAiProofreadAsync()
    {
        if (SelectedJob is null)
        {
            ProofreadStatus = Text("proof.status.selectJson");
            return;
        }

        ShowProofread();
        if (!IsProofreadAvailable)
        {
            ClearProofreadResults(ProofreadReadinessText);
            return;
        }

        RefreshProofreadModelOptions();
        var modelOption = SelectedProofreadModel;
        if (modelOption is null)
        {
            ProofreadStatus = Text("proof.status.noModel");
            return;
        }

        var profile = modelOption.Profile;
        if (!IsUsableProofreadProfile(profile))
        {
            ProofreadStatus = Text("proof.status.modelIncomplete");
            return;
        }

        RemoveAiProofreadIssues();

        var items = SelectedJob.Entries
            .Where(x => x.Status is TranslationStatus.Done or TranslationStatus.DoneWithWarnings)
            .Where(x => !string.IsNullOrWhiteSpace(x.TranslationText))
            .Select(x => new ProofreadBatchItem
            {
                Index = x.Model.Index,
                SourceText = x.SourceText,
                TranslationText = x.TranslationText,
                DocumentKind = SelectedJob.Document?.Kind ?? DocumentKind.Txt
            })
            .ToArray();

        if (items.Length == 0)
        {
            ProofreadStatus = Text("proof.status.noItems");
            return;
        }

        _proofreadCancellation?.Cancel();
        _proofreadCancellation = new CancellationTokenSource();
        var cancellationToken = _proofreadCancellation.Token;
        IsProofreading = true;

        var processedCount = 0;
        var batchSize = Math.Clamp(_settings.BatchSize, 5, 30);
        var batches = items.Chunk(batchSize).ToArray();
        using var requestSemaphore = new SemaphoreSlim(Math.Clamp(_settings.RequestConcurrency, 1, 2));
        ProofreadStatus = string.Format(Text("proof.status.aiRunning"), 0, items.Length);

        var tasks = batches.Select(async batch =>
        {
            await requestSemaphore.WaitAsync(cancellationToken);
            try
            {
                await Task.Delay(Math.Max(0, _settings.RequestIntervalMs), cancellationToken);
                var issues = await _proofreadingClient.ProofreadAsync(batch, _settings, profile, cancellationToken);
                var done = Interlocked.Add(ref processedCount, batch.Length);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var issue in issues)
                    {
                        ProofreadIssues.Add(new ProofreadIssueViewModel(issue));
                    }

                    SelectedProofreadIssue ??= ProofreadIssues.FirstOrDefault();
                    ProofreadStatus = string.Format(Text("proof.status.aiRunning"), Math.Min(done, items.Length), items.Length);
                    OnPropertyChanged(nameof(ProofreadSummaryText));
                });
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                var done = Interlocked.Add(ref processedCount, batch.Length);
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    ProofreadIssues.Add(new ProofreadIssueViewModel(new ProofreadIssue
                    {
                        EntryIndex = batch[0].Index,
                        Origin = ProofreadOrigin.Ai,
                        Severity = ProofreadSeverity.Error,
                        Category = "AI校对失败",
                        Message = ex.Message,
                        Suggestion = Text("proof.status.aiFailureAdvice"),
                        SourceText = batch[0].SourceText,
                        TranslationText = batch[0].TranslationText,
                        DocumentKind = batch[0].DocumentKind
                    }));
                    SelectedProofreadIssue ??= ProofreadIssues.FirstOrDefault();
                    ProofreadStatus = string.Format(Text("proof.status.aiRunning"), Math.Min(done, items.Length), items.Length);
                    OnPropertyChanged(nameof(ProofreadSummaryText));
                });
            }
            finally
            {
                requestSemaphore.Release();
            }
        });

        try
        {
            await Task.WhenAll(tasks);
            ProofreadStatus = string.Format(Text("proof.status.aiDone"), ProofreadIssues.Count);
            _logs?.Add($"{Text("proof.log.aiDone")}：{SelectedJob.FileName} - {ProofreadIssues.Count}");
        }
        catch (OperationCanceledException)
        {
            ProofreadStatus = Text("proof.status.aiStopped");
            _logs?.Add(Text("proof.status.aiStopped"));
        }
        finally
        {
            IsProofreading = false;
            _proofreadCancellation?.Dispose();
            _proofreadCancellation = null;
            OnPropertyChanged(nameof(ProofreadSummaryText));
        }
    }

    private void CancelProofread()
    {
        _proofreadCancellation?.Cancel();
        ProofreadStatus = Text("proof.status.aiStopping");
    }

    private void ApplyProofreadSuggestion()
    {
        if (SelectedJob is null || SelectedProofreadIssue is not { HasReplacement: true } issue)
        {
            return;
        }

        var entry = SelectedJob.Entries.FirstOrDefault(x => x.Model.Index == issue.EntryIndex);
        if (entry is null)
        {
            return;
        }

        entry.TranslationText = issue.ReplacementText;
        entry.Warning = PlaceholderValidator.Validate(entry.SourceText, entry.TranslationText);
        entry.Model.ErrorMessage = "";
        entry.Status = string.IsNullOrWhiteSpace(entry.Warning)
            ? TranslationStatus.Done
            : TranslationStatus.DoneWithWarnings;
        SelectedJob.RefreshProgress();
        _projectStore.SaveProject(SelectedJob.Job, SelectedJob.Entries.Select(x => x.Model));
        _historyStore.UpsertOngoing(SelectedJob.Job);
        PersistOpenQueue();

        var index = ProofreadIssues.IndexOf(issue);
        ProofreadIssues.Remove(issue);
        SelectedProofreadIssue = ProofreadIssues.Count == 0 ? null : ProofreadIssues[Math.Clamp(index, 0, ProofreadIssues.Count - 1)];
        ProofreadStatus = Text("proof.status.applyDone");
        OnPropertyChanged(nameof(ProofreadSummaryText));
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task StartQueueAsync()
    {
        var profile = _apiProfiles.FirstOrDefault(x => x.IsActive);
        if (profile is null || string.IsNullOrWhiteSpace(profile.ApiKey))
        {
            foreach (var job in Jobs)
            {
                job.Status = JobStatus.Error;
            }

            QueueChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        var targets = GetCommandTargets()
            .Where(x => x.Status is JobStatus.Waiting or JobStatus.Paused or JobStatus.Error or JobStatus.Cancelled)
            .ToArray();
        if (targets.Length == 0)
        {
            _logs?.Add(IsDetailVisible ? "当前文件没有需要继续翻译的内容。" : "队列中没有需要继续翻译的文件。");
            QueueChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        _queueInterruptionStatus = JobStatus.Cancelled;
        _queueCancellation?.Cancel();
        _queueCancellation = new CancellationTokenSource();
        var cancellationToken = _queueCancellation.Token;

        using var fileSemaphore = new SemaphoreSlim(Math.Max(1, _settings.FileConcurrency));
        var tasks = targets.Select(async job =>
        {
            await fileSemaphore.WaitAsync(cancellationToken);
            try
            {
                await TranslateJobAsync(job, profile, cancellationToken);
            }
            finally
            {
                fileSemaphore.Release();
            }
        });

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                _logs?.Add(_queueInterruptionStatus == JobStatus.Paused
                    ? "翻译已暂停。"
                    : "翻译已终止。");
                QueueChanged?.Invoke(this, EventArgs.Empty);
            });
        }
        finally
        {
            _queueCancellation?.Dispose();
            _queueCancellation = null;
            _queueInterruptionStatus = JobStatus.Cancelled;
        }

        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    private async Task TranslateJobAsync(
        TranslationJobViewModel job,
        ApiProfile profile,
        CancellationToken cancellationToken)
    {
        if (!await EnsureJobLoadedAsync(job, cancellationToken))
        {
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            job.SetModel(profile.Model);
            job.Status = JobStatus.Running;
            RaiseLiveDetailProperties();
            PersistJob(job);
            PersistOpenQueue();
            _logs?.Add($"开始任务：{job.FileName}");
            QueueChanged?.Invoke(this, EventArgs.Empty);
        });

        var entries = job.Entries
            .Where(x => x.Status is TranslationStatus.Pending or TranslationStatus.Error)
            .ToArray();

        foreach (var entry in entries)
        {
            if (_settings.SkipCodeLikeEntries && PlaceholderValidator.LooksCodeLike(entry.SourceText))
            {
                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    entry.Status = TranslationStatus.Skipped;
                    entry.Warning = "";
                    job.RefreshProgress();
                    RaiseLiveDetailProperties();
                });
            }
        }

        var translatable = entries
            .Where(x => x.Status is TranslationStatus.Pending or TranslationStatus.Error)
            .Chunk(Math.Max(1, _settings.BatchSize))
            .ToArray();

        using var requestSemaphore = new SemaphoreSlim(Math.Max(1, _settings.RequestConcurrency));
        var tasks = translatable.Select(async batch =>
        {
            await requestSemaphore.WaitAsync(cancellationToken);
            try
            {
                await Task.Delay(Math.Max(0, _settings.RequestIntervalMs), cancellationToken);
                await TranslateBatchAsync(job, batch, profile, cancellationToken);
            }
            finally
            {
                requestSemaphore.Release();
            }
        });

        try
        {
            await Task.WhenAll(tasks);
        }
        catch (OperationCanceledException)
        {
            await MarkJobInterruptedAsync(job, ResolveInterruptedStatus(job));
            return;
        }

        if (cancellationToken.IsCancellationRequested || job.Status is JobStatus.Cancelled or JobStatus.Paused)
        {
            await MarkJobInterruptedAsync(job, ResolveInterruptedStatus(job));
            return;
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            job.RefreshProgress();
            job.Status = job.ErrorCount > 0 ? JobStatus.Error : JobStatus.Completed;
            RaiseLiveDetailProperties();
            _projectStore.SaveProject(job.Job, job.Entries.Select(x => x.Model));
            PersistOpenQueue();
            _logs?.Add($"任务结束：{job.FileName} - {job.Status}");
            if (job.Status == JobStatus.Completed)
            {
                ExportJob(job);
                _historyStore.MarkCompleted(job.Job);
            }
            else
            {
                _historyStore.UpsertOngoing(job.Job);
            }

            QueueChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private async Task TranslateBatchAsync(
        TranslationJobViewModel job,
        IReadOnlyList<TranslationEntryViewModel> batch,
        ApiProfile profile,
        CancellationToken cancellationToken)
    {
        try
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var entry in batch)
                {
                    entry.Status = TranslationStatus.Running;
                    entry.Warning = "";
                    entry.Model.ErrorMessage = "";
                }

                SetLivePreview(job, batch[0]);
                RaiseLiveDetailProperties();
            });

            var requestItems = batch.Select(x => new TranslationBatchItem
            {
                Index = x.Model.Index,
                SourceText = x.SourceText
            }).ToArray();

            var results = await _translationClient.TranslateAsync(requestItems, _settings, profile, cancellationToken);
            var translated = results.ToDictionary(x => x.Index);
            if (cancellationToken.IsCancellationRequested || job.Status is JobStatus.Cancelled or JobStatus.Paused)
            {
                throw new OperationCanceledException(cancellationToken);
            }

            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var entry in batch)
                {
                    if (!translated.TryGetValue(entry.Model.Index, out var result))
                    {
                        entry.Status = TranslationStatus.Error;
                        entry.Warning = "";
                        entry.Model.ErrorMessage = "API 未返回该条译文";
                        continue;
                    }

                    entry.TranslationText = result.TranslationText;
                    var placeholderWarning = _settings.ValidatePlaceholders
                        ? PlaceholderValidator.Validate(entry.SourceText, entry.TranslationText)
                        : "";
                    entry.Warning = string.Join(
                        "；",
                        new[] { result.Warning, placeholderWarning }.Where(x => !string.IsNullOrWhiteSpace(x)));
                    entry.Model.ErrorMessage = result.ErrorMessage;
                    entry.Status = !string.IsNullOrWhiteSpace(entry.Model.ErrorMessage)
                        ? TranslationStatus.Error
                        : string.IsNullOrWhiteSpace(entry.Warning)
                            ? TranslationStatus.Done
                            : TranslationStatus.DoneWithWarnings;
                    SetLivePreview(job, entry);
                }

                job.RefreshProgress();
                RaiseLiveDetailProperties();
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            await Dispatcher.UIThread.InvokeAsync(() =>
            {
                foreach (var entry in batch)
                {
                    entry.Status = TranslationStatus.Error;
                    entry.Warning = "";
                    entry.Model.ErrorMessage = ex.Message;
                }

                job.RefreshProgress();
                RaiseLiveDetailProperties();
                SetLivePreview(job, batch[0]);
                _logs?.Add($"批次失败：{job.FileName} - {ex.Message}");
            });
        }
    }

    private void StopQueue()
    {
        _queueInterruptionStatus = JobStatus.Cancelled;
        _queueCancellation?.Cancel();

        foreach (var job in GetCommandTargets().Where(x => x.Status != JobStatus.Completed))
        {
            job.Status = JobStatus.Cancelled;
            ResetRunningEntries(job);
            job.RefreshProgress();
            RaiseLiveDetailProperties();
            if (job.Document is not null)
            {
                _projectStore.SaveProject(job.Job, job.Entries.Select(x => x.Model));
                _historyStore.UpsertOngoing(job.Job);
            }
        }

        PersistOpenQueue();
        _logs?.Add("已发送终止翻译请求。");
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void RemoveSelected()
    {
        if (SelectedJob is null)
        {
            return;
        }

        var index = Jobs.IndexOf(SelectedJob);
        _historyStore.Remove(SelectedJob.Job.Id);
        Jobs.Remove(SelectedJob);
        SelectedJob = Jobs.Count == 0 ? null : Jobs[Math.Clamp(index, 0, Jobs.Count - 1)];
        if (SelectedJob is null)
        {
            ShowDirectory();
        }

        PersistOpenQueue();
        RaiseCommandStates();
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    private void SaveSelectedProject()
    {
        if (SelectedJob?.Document is null)
        {
            return;
        }

        _projectStore.SaveProject(SelectedJob.Job, SelectedJob.Entries.Select(x => x.Model));
        PersistOpenQueue();
    }

    private async void ExportSelected()
    {
        if (SelectedJob is null)
        {
            return;
        }

        if (!await EnsureJobLoadedAsync(SelectedJob))
        {
            return;
        }

        if (SelectedJob.Document is null)
        {
            return;
        }

        ExportJob(SelectedJob);
        SelectedJob.Status = JobStatus.Completed;
        RaiseLiveDetailProperties();
        SaveSelectedProject();
        _historyStore.MarkCompleted(SelectedJob.Job);
        PersistOpenQueue();
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    private async void ExportAll()
    {
        foreach (var job in Jobs)
        {
            if (!await EnsureJobLoadedAsync(job))
            {
                continue;
            }

            if (job.Document is null)
            {
                continue;
            }

            ExportJob(job);
            job.Status = JobStatus.Completed;
            RaiseLiveDetailProperties();
            _projectStore.SaveProject(job.Job, job.Entries.Select(x => x.Model));
            _historyStore.MarkCompleted(job.Job);
        }

        PersistOpenQueue();
        QueueChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool ExportJob(TranslationJobViewModel job)
    {
        if (job.Document is null)
        {
            return false;
        }

        EnsureOutputPath(job.Job);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(job.Job.OutputPath) ?? _paths.OutputDirectory);
            job.ExportTo(job.Job.OutputPath);
            QueueNotice = $"已保存到：{job.Job.OutputPath}";
            _logs?.Add($"已导出：{job.Job.OutputPath}");
            return true;
        }
        catch (Exception ex)
        {
            QueueNotice = $"导出失败：{job.FileName} - {ex.Message}";
            _logs?.Add($"导出失败：{job.FileName} - {ex.Message}");
            return false;
        }
    }

    private void EnsureOutputPath(TranslationJob job)
    {
        job.OutputPath = BuildOutputPath(job);
    }

    private string BuildOutputPath(TranslationJob job)
    {
        var sourcePath = string.IsNullOrWhiteSpace(job.FilePath)
            ? job.FileName
            : job.FilePath;
        var baseName = Path.GetFileNameWithoutExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(baseName))
        {
            baseName = string.IsNullOrWhiteSpace(job.Id) ? "translation" : job.Id;
        }

        var extension = Path.GetExtension(sourcePath);
        if (string.IsNullOrWhiteSpace(extension))
        {
            extension = ".txt";
        }

        var languageSuffix = TargetLanguageSuffix(job.TranslationDirection);
        var safeName = MakeSafeFileName($"{baseName}-{languageSuffix}{extension}");
        return Path.Combine(_paths.OutputDirectory, safeName);
    }

    private string TargetLanguageSuffix(string translationDirection)
    {
        return translationDirection switch
        {
            "中译日" => "ja",
            "中译英" => "en",
            "日译中" or "英译中" => "zh",
            _ => NormalizeLanguageSuffix(_settings.TargetLanguage)
        };
    }

    private static string NormalizeLanguageSuffix(string language)
    {
        if (string.IsNullOrWhiteSpace(language))
        {
            return "out";
        }

        var normalized = language.Trim();
        return normalized.StartsWith("zh", StringComparison.OrdinalIgnoreCase) ? "zh"
            : normalized.StartsWith("ja", StringComparison.OrdinalIgnoreCase) ? "ja"
            : normalized.StartsWith("en", StringComparison.OrdinalIgnoreCase) ? "en"
            : normalized.Replace('_', '-').ToLowerInvariant();
    }

    private static string MakeSafeFileName(string fileName)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(fileName.Length);
        foreach (var character in fileName)
        {
            builder.Append(invalid.Contains(character) ? '_' : character);
        }

        return builder.ToString();
    }

    private void RaiseCommandStates()
    {
        RunRuleProofreadCommand.RaiseCanExecuteChanged();
        ApplyProofreadSuggestionCommand.RaiseCanExecuteChanged();
        RemoveSelectedCommand.RaiseCanExecuteChanged();
        SaveProjectCommand.RaiseCanExecuteChanged();
        ExportSelectedCommand.RaiseCanExecuteChanged();
        ExportAllCommand.RaiseCanExecuteChanged();
        RaiseProofreadStateProperties();
    }

    private void RaiseProofreadStateProperties()
    {
        OnPropertyChanged(nameof(IsProofreadAvailable));
        OnPropertyChanged(nameof(ProofreadReadinessText));
        OnPropertyChanged(nameof(ProofreadModelHelpText));
        OnPropertyChanged(nameof(IsProofreadPanelVisible));
        OnPropertyChanged(nameof(IsProofreadModeSelected));
        RunRuleProofreadCommand.RaiseCanExecuteChanged();
    }

    private void ClearProofreadResults(string status)
    {
        ProofreadIssues.Clear();
        SelectedProofreadIssue = null;
        ProofreadStatus = status;
        OnPropertyChanged(nameof(ProofreadSummaryText));
    }

    private void RemoveAiProofreadIssues()
    {
        for (var i = ProofreadIssues.Count - 1; i >= 0; i--)
        {
            if (ProofreadIssues[i].Origin == ProofreadOrigin.Ai)
            {
                ProofreadIssues.RemoveAt(i);
            }
        }

        SelectedProofreadIssue = ProofreadIssues.FirstOrDefault();
        OnPropertyChanged(nameof(ProofreadSummaryText));
    }

    private static bool IsUsableProofreadProfile(ApiProfile profile)
    {
        return profile.Provider != "本地 GGUF"
            && !string.IsNullOrWhiteSpace(profile.BaseUrl)
            && !string.IsNullOrWhiteSpace(profile.ApiKey)
            && !string.IsNullOrWhiteSpace(profile.Model);
    }

    private string Text(string key)
    {
        return _localization?[key] ?? key;
    }

    private void SetLivePreview(TranslationJobViewModel job, TranslationEntryViewModel entry)
    {
        UpdateLiveStream(job, entry);
        LatestPreviewJob = job;
        LatestPreviewEntry = entry;
        OnPropertyChanged(nameof(LatestPreviewJob));
        OnPropertyChanged(nameof(LatestPreviewEntry));
        RaiseLiveDetailProperties();
    }

    private async Task MarkJobInterruptedAsync(TranslationJobViewModel job, string status)
    {
        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            ResetRunningEntries(job);
            job.Status = status;
            job.RefreshProgress();
            RaiseLiveDetailProperties();
            if (job.Document is not null)
            {
                _projectStore.SaveProject(job.Job, job.Entries.Select(x => x.Model));
                _historyStore.UpsertOngoing(job.Job);
            }

            PersistOpenQueue();
            QueueChanged?.Invoke(this, EventArgs.Empty);
        });
    }

    private string ResolveInterruptedStatus(TranslationJobViewModel job)
    {
        if (job.Status == JobStatus.Paused)
        {
            return JobStatus.Paused;
        }

        if (job.Status == JobStatus.Cancelled)
        {
            return JobStatus.Cancelled;
        }

        return _queueInterruptionStatus;
    }

    private TranslationJobViewModel[] GetCommandTargets()
    {
        return IsDetailVisible && SelectedJob is not null
            ? [SelectedJob]
            : Jobs.ToArray();
    }

    private static void ResetRunningEntries(TranslationJobViewModel job)
    {
        foreach (var entry in job.Entries.Where(x => x.Status == TranslationStatus.Running))
        {
            entry.Status = TranslationStatus.Pending;
        }
    }

    private static string NormalizeResumeStatus(string status)
    {
        return status is JobStatus.Paused or JobStatus.Error or JobStatus.Waiting
            ? status
            : JobStatus.Waiting;
    }

    private static string NormalizeRestoredStatus(string status)
    {
        return status is JobStatus.Completed or JobStatus.Paused or JobStatus.Error or JobStatus.Waiting
            ? status
            : JobStatus.Waiting;
    }

    private void PersistJob(TranslationJobViewModel job)
    {
        if (job.Document is null)
        {
            return;
        }

        _projectStore.SaveProject(job.Job, job.Entries.Select(x => x.Model));
        if (job.Status == JobStatus.Completed)
        {
            _historyStore.MarkCompleted(job.Job);
        }
        else
        {
            _historyStore.UpsertOngoing(job.Job);
        }
    }

    private void PersistOpenQueue()
    {
        _projectStore.SaveOpenQueue(Jobs.Select(x => x.Job));
    }

    private void RaiseLiveDetailProperties()
    {
        OnPropertyChanged(nameof(IsEntryGridVisible));
        OnPropertyChanged(nameof(IsLiveDetailVisible));
        OnPropertyChanged(nameof(IsProofreadVisible));
        OnPropertyChanged(nameof(IsEntryGridModeSelected));
        OnPropertyChanged(nameof(IsLiveDetailModeSelected));
        OnPropertyChanged(nameof(IsProofreadModeSelected));
        OnPropertyChanged(nameof(LiveCurrentFile));
        OnPropertyChanged(nameof(LiveStatusLine));
        OnPropertyChanged(nameof(LiveProgressPercent));
        OnPropertyChanged(nameof(LiveProgressPercentText));
        OnPropertyChanged(nameof(LiveEntryStatus));
        OnPropertyChanged(nameof(LiveSourceText));
        OnPropertyChanged(nameof(LiveTranslationText));
        OnPropertyChanged(nameof(LiveTerminalText));
        OnPropertyChanged(nameof(LiveSourceStreamText));
        OnPropertyChanged(nameof(LiveTranslationStreamText));
        RaiseProofreadStateProperties();
    }

    private string BuildLiveTerminalText()
    {
        var builder = new StringBuilder();
        var job = SelectedJob;
        var entry = CurrentLiveEntry;

        builder.AppendLine("> sakuya live-preview --scope current-json");
        builder.AppendLine($"[{DateTime.Now:HH:mm:ss}] file     : {job?.FileName ?? "未选择文件"}");
        builder.AppendLine($"[{DateTime.Now:HH:mm:ss}] status   : {job?.StatusLine ?? "等待翻译"}");
        builder.AppendLine($"[{DateTime.Now:HH:mm:ss}] progress : {LiveProgressPercentText}");
        builder.AppendLine();

        if (entry is null)
        {
            builder.AppendLine("[entry] waiting...");
            builder.AppendLine("  还没有可显示的条目。开始翻译后，这里会持续刷新当前文件的内容。");
            return builder.ToString();
        }

        builder.AppendLine($"[entry #{entry.Index}] {entry.Status}");
        if (!string.IsNullOrWhiteSpace(entry.Warning))
        {
            builder.AppendLine($"[warning] {entry.Warning}");
        }

        builder.AppendLine();
        builder.AppendLine("[source]");
        builder.AppendLine(IndentTerminalBlock(entry.SourceText));
        builder.AppendLine();
        builder.AppendLine("[translation]");
        builder.AppendLine(IndentTerminalBlock(string.IsNullOrWhiteSpace(entry.TranslationText) ? "(waiting for output...)" : entry.TranslationText));
        builder.AppendLine();
        builder.Append("> ");

        return builder.ToString();
    }

    private void UpdateLiveStream(TranslationJobViewModel job, TranslationEntryViewModel entry)
    {
        if (string.IsNullOrWhiteSpace(job.Job.Id))
        {
            return;
        }

        if (!_liveStreams.TryGetValue(job.Job.Id, out var stream))
        {
            stream = [];
            _liveStreams[job.Job.Id] = stream;
        }

        var existingIndex = stream.FindIndex(x => x.EntryIndex == entry.Model.Index);
        var snapshot = new LiveStreamSnapshot(
            entry.Model.Index,
            entry.SourceText,
            entry.TranslationText,
            entry.Status,
            entry.Warning,
            DateTimeOffset.Now);

        if (existingIndex >= 0)
        {
            stream[existingIndex] = snapshot;
        }
        else
        {
            stream.Add(snapshot);
        }

        stream.Sort((left, right) => left.UpdatedAt.CompareTo(right.UpdatedAt));
        if (stream.Count > 80)
        {
            stream.RemoveRange(0, stream.Count - 80);
        }
    }

    private string BuildLiveStreamText(bool isSource)
    {
        var job = SelectedJob;
        if (job is null)
        {
            return isSource ? Text("live.noSource") : Text("live.noTranslation");
        }

        var snapshots = GetLiveStreamSnapshots(job);
        if (snapshots.Count == 0)
        {
            return isSource ? Text("live.noSource") : Text("live.noTranslation");
        }

        var builder = new StringBuilder();
        foreach (var item in snapshots)
        {
            var marker = isSource ? ">>" : "<<";
            var text = isSource
                ? item.SourceText
                : string.IsNullOrWhiteSpace(item.TranslationText)
                    ? Text("live.waitingOutput")
                    : item.TranslationText;

            builder.AppendLine($"{marker} #{item.EntryIndex + 1:00000}  {item.Status}  {item.UpdatedAt:HH:mm:ss}");
            builder.AppendLine(IndentTerminalBlock(text));
            if (!isSource && !string.IsNullOrWhiteSpace(item.Warning))
            {
                builder.AppendLine($"  ! {item.Warning}");
            }

            builder.AppendLine();
        }

        return builder.ToString().TrimEnd();
    }

    private IReadOnlyList<LiveStreamSnapshot> GetLiveStreamSnapshots(TranslationJobViewModel job)
    {
        if (_liveStreams.TryGetValue(job.Job.Id, out var stream) && stream.Count > 0)
        {
            return stream
                .OrderByDescending(x => x.UpdatedAt)
                .Take(5)
                .OrderBy(x => x.UpdatedAt)
                .ToArray();
        }

        return job.Entries
            .Where(x => x.Status is TranslationStatus.Running or TranslationStatus.Done or TranslationStatus.DoneWithWarnings or TranslationStatus.Error)
            .OrderByDescending(x => x.Model.Index)
            .Take(5)
            .OrderBy(x => x.Model.Index)
            .Select(x => new LiveStreamSnapshot(
                x.Model.Index,
                x.SourceText,
                x.TranslationText,
                x.Status,
                x.Warning,
                DateTimeOffset.Now))
            .ToArray();
    }

    private static string IndentTerminalBlock(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "  (empty)";
        }

        return string.Join(
            Environment.NewLine,
            text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').Select(line => $"  {line}"));
    }
}
