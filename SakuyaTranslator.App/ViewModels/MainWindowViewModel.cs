using System.Collections.ObjectModel;
using SakuyaTranslator.Core.Models;
using SakuyaTranslator.Core.Services;

namespace SakuyaTranslator.App.ViewModels;

public sealed class MainWindowViewModel : ViewModelBase
{
    private object _currentPage;
    private readonly LocalizationService _localization;
    private double _totalProgress;
    private string _globalStatus = "就绪";
    private ObservableCollection<TranslationProviderOption> _translationProviderOptions = [];
    private ObservableCollection<TranslationModelOption> _translationModelOptions = [];
    private TranslationProviderOption? _selectedTranslationProvider;
    private TranslationModelOption? _selectedTranslationModel;

    public MainWindowViewModel(
        PortablePaths paths,
        JsonFileStore store,
        AppSettings settings,
        IList<ApiProfile> apiProfiles,
        LocalizationService? localization = null)
    {
        Paths = paths;
        Store = store;
        Settings = settings;
        ApiProfiles = apiProfiles;
        _localization = localization ?? new LocalizationService(paths);
        if (localization is null)
        {
            _localization.Load(settings.UiCulture);
        }

        foreach (var profile in apiProfiles)
        {
            ApiProfileRules.NormalizeForUse(profile);
        }

        var projectStore = new ProjectStore(paths, store);
        var historyStore = new HistoryStore(paths, store);
        LogsPage = new LogsViewModel();
        QueuePage = new TranslateQueueViewModel(settings, paths, projectStore, historyStore, apiProfiles, localization: _localization, logs: LogsPage);
        PreviewPage = new PreviewViewModel(QueuePage);
        LivePreviewPage = new LivePreviewViewModel(QueuePage, _localization);
        ManualPage = new ManualTranslateViewModel(settings, apiProfiles, LogsPage)
        {
            Queue = QueuePage
        };
        ApiPage = new ApiConfigViewModel(paths, store, apiProfiles, _localization);
        HistoryPage = new HistoryViewModel(historyStore, QueuePage);
        SettingsPage = new SettingsViewModel(paths, store, settings, _localization);
        TemplatesPage = new TemplatesViewModel(new PromptTemplateStore(paths, store), settings);
        AboutPage = new AboutViewModel(paths, store, settings);
        _currentPage = QueuePage;

        NavigateCommand = new RelayCommand(Navigate);
        HistoryPage.ContinueRequested += (_, _) => CurrentPage = QueuePage;
        ApiPage.ProfilesChanged += (_, _) =>
        {
            QueuePage.RefreshProofreadModelOptions();
            RefreshTranslationModelOptions();
            RefreshGlobalStatus();
        };
        SettingsPage.SettingsChanged += (_, _) =>
        {
            QueuePage.RefreshProofreadFeatureState();
            RefreshGlobalStatus();
        };
        QueuePage.QueueChanged += (_, _) => RefreshGlobalStatus();
        RefreshTranslationModelOptions();
        RefreshGlobalStatus();
        _ = QueuePage.RestoreOpenQueueAsync();
    }

    public PortablePaths Paths { get; }
    public JsonFileStore Store { get; }
    public AppSettings Settings { get; }
    public IList<ApiProfile> ApiProfiles { get; }
    public TranslateQueueViewModel QueuePage { get; }
    public PreviewViewModel PreviewPage { get; }
    public LivePreviewViewModel LivePreviewPage { get; }
    public ManualTranslateViewModel ManualPage { get; }
    public ApiConfigViewModel ApiPage { get; }
    public HistoryViewModel HistoryPage { get; }
    public SettingsViewModel SettingsPage { get; }
    public TemplatesViewModel TemplatesPage { get; }
    public LogsViewModel LogsPage { get; }
    public AboutViewModel AboutPage { get; }
    public RelayCommand NavigateCommand { get; }
    public ObservableCollection<TranslationProviderOption> TranslationProviderOptions
    {
        get => _translationProviderOptions;
        private set => SetProperty(ref _translationProviderOptions, value);
    }

    public ObservableCollection<TranslationModelOption> TranslationModelOptions
    {
        get => _translationModelOptions;
        private set => SetProperty(ref _translationModelOptions, value);
    }

    public sealed record TranslationProviderOption(string Provider, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    public sealed record TranslationModelOption(ApiProfile Profile, string DisplayName, string DetailText)
    {
        public override string ToString() => DisplayName;
    }

    public object CurrentPage
    {
        get => _currentPage;
        set => SetProperty(ref _currentPage, value);
    }

    public TranslationProviderOption? SelectedTranslationProvider
    {
        get => _selectedTranslationProvider;
        set
        {
            if (SetProperty(ref _selectedTranslationProvider, value))
            {
                RefreshTranslationModelOptions(value?.Provider, null);
                RefreshGlobalStatus();
            }
        }
    }

    public TranslationModelOption? SelectedTranslationModel
    {
        get => _selectedTranslationModel;
        set
        {
            if (SetProperty(ref _selectedTranslationModel, value))
            {
                if (value is not null)
                {
                    SetActiveProfile(value.Profile);
                    Store.Save(Paths.ApiProfilesPath, ApiProfiles.ToList());
                    QueuePage.RefreshProofreadModelOptions();
                }

                RefreshGlobalStatus();
            }
        }
    }

    public bool IsTranslationProviderSelectorEnabled => TranslationProviderOptions.Count > 0;
    public bool IsTranslationModelSelectorEnabled => TranslationModelOptions.Count > 0;
    public bool IsTranslationModelFallbackVisible => !IsTranslationModelSelectorEnabled;
    public string CurrentModel => SelectedTranslationModel?.DisplayName
        ?? (ApiProfiles.FirstOrDefault(x => x.IsActive && IsDisplayableTranslationProfile(x))?.Model is { Length: > 0 } model
            ? model
            : _localization["model.unconfigured"]);
    public string CurrentModelDetail => SelectedTranslationModel?.DetailText ?? _localization["model.unconfigured"];
    public string ApiStatus => ApiProfiles.Any(x => x.IsActive && IsReadyTranslationProfile(x)) ? _localization["api.configured"] : _localization["api.unconfigured"];
    public string DirectionText => PresetLabel(Settings.TranslationPreset);
    public string SpeedText => SpeedLabel(Settings.SpeedPreset);
    public string ConcurrencyText => string.Format(_localization["status.concurrency"], Settings.FileConcurrency, Settings.RequestConcurrency);
    public string TotalProgressText => $"{TotalProgress:0}%";
    public int QueueCount => QueuePage.Jobs.Count;
    public string NavQueue => _localization["nav.queue.short"];
    public string NavDone => _localization["nav.done.short"];
    public string NavTranslate => _localization["nav.translate"];
    public string NavPreview => _localization["nav.preview"];
    public string NavLivePreview => _localization["nav.livePreview"];
    public string NavManual => _localization["nav.manual.short"];
    public string NavApi => _localization["nav.api"];
    public string NavHistory => _localization["nav.history"];
    public string NavSettings => _localization["nav.settings"];
    public string NavTemplates => _localization["nav.templates.short"];
    public string NavLogs => _localization["nav.logs.short"];
    public string NavAbout => _localization["nav.about"];

    public double TotalProgress
    {
        get => _totalProgress;
        set => SetProperty(ref _totalProgress, value);
    }

    public string GlobalStatus
    {
        get => _globalStatus;
        set => SetProperty(ref _globalStatus, value);
    }

    public async Task CheckUpdatesOnStartupAsync()
    {
        if (!Settings.CheckUpdatesOnStartup || Settings.UpdateCheckFrequency == "手动")
        {
            return;
        }

        await AboutPage.CheckForUpdatesAsync();
        RefreshGlobalStatus();
    }

    private void Navigate(object? parameter)
    {
        CurrentPage = parameter?.ToString() switch
        {
            "Preview" => PreviewPage,
            "LivePreview" => LivePreviewPage,
            "Manual" => ManualPage,
            "Api" => ApiPage,
            "History" => HistoryPage,
            "Settings" => SettingsPage,
            "Templates" => TemplatesPage,
            "Logs" => LogsPage,
            "About" => AboutPage,
            _ => QueuePage
        };

        if (CurrentPage == HistoryPage)
        {
            HistoryPage.SyncFromQueue(QueuePage.Jobs);
        }
    }

    private void RefreshGlobalStatus()
    {
        var total = QueuePage.Jobs.Sum(x => x.TotalCount);
        var done = QueuePage.Jobs.Sum(x => x.CompletedCount);
        TotalProgress = total <= 0 ? 0 : done * 100.0 / total;
        GlobalStatus = QueuePage.Jobs.Any(x => x.Status == JobStatus.Running)
            ? _localization["status.translating"]
            : AboutPage.LastResult?.HasUpdate == true && AboutPage.LastResult.Latest is not null
                ? string.Format(_localization["status.update.available"], AboutPage.LastResult.Latest.Version)
                : _localization["status.ready"];

        OnPropertyChanged(nameof(CurrentModel));
        OnPropertyChanged(nameof(CurrentModelDetail));
        OnPropertyChanged(nameof(ApiStatus));
        OnPropertyChanged(nameof(IsTranslationProviderSelectorEnabled));
        OnPropertyChanged(nameof(IsTranslationModelSelectorEnabled));
        OnPropertyChanged(nameof(IsTranslationModelFallbackVisible));
        OnPropertyChanged(nameof(DirectionText));
        OnPropertyChanged(nameof(SpeedText));
        OnPropertyChanged(nameof(ConcurrencyText));
        OnPropertyChanged(nameof(QueueCount));
        OnPropertyChanged(nameof(TotalProgressText));
    }

    private void RefreshTranslationModelOptions(string? preferredProvider = null, ApiProfile? preferredProfile = null)
    {
        var previousProfile = preferredProfile
            ?? SelectedTranslationModel?.Profile
            ?? ApiProfiles.FirstOrDefault(x => x.IsActive);
        var targetProvider = ApiProfileRules.NormalizeProvider(preferredProvider
            ?? SelectedTranslationProvider?.Provider
            ?? previousProfile?.Provider);

        var providerOptions = ApiProfiles
            .Where(IsDisplayableTranslationProfile)
            .Select(x => ApiProfileRules.NormalizeProvider(x.Provider))
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(GetProviderDisplayName, StringComparer.OrdinalIgnoreCase)
            .Select(provider => new TranslationProviderOption(provider, GetProviderDisplayName(provider)))
            .ToList();
        TranslationProviderOptions = new ObservableCollection<TranslationProviderOption>(providerOptions);

        var providerOption = TranslationProviderOptions.FirstOrDefault(x =>
                string.Equals(x.Provider, targetProvider, StringComparison.OrdinalIgnoreCase))
            ?? TranslationProviderOptions.FirstOrDefault();
        if (!Equals(_selectedTranslationProvider, providerOption))
        {
            _selectedTranslationProvider = providerOption;
            OnPropertyChanged(nameof(SelectedTranslationProvider));
        }

        var modelOptions = ApiProfiles
            .Where(x => IsDisplayableTranslationProfile(x)
                && string.Equals(ApiProfileRules.NormalizeProvider(x.Provider), providerOption?.Provider, StringComparison.OrdinalIgnoreCase))
            .GroupBy(x => x.Model.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.OrderByDescending(profile => profile.IsActive).First())
            .OrderBy(x => x.Model, StringComparer.OrdinalIgnoreCase)
            .Select(profile =>
            {
                var provider = string.IsNullOrWhiteSpace(profile.Provider) ? "API" : ApiProfileRules.NormalizeProvider(profile.Provider);
                var displayName = string.IsNullOrWhiteSpace(profile.DisplayName) ? provider : profile.DisplayName;
                var providerName = GetProviderDisplayName(provider);
                var detailText = string.Equals(displayName, providerName, StringComparison.OrdinalIgnoreCase)
                    ? providerName
                    : $"{displayName} / {providerName}";
                return new TranslationModelOption(profile, profile.Model, detailText);
            })
            .ToList();
        TranslationModelOptions = new ObservableCollection<TranslationModelOption>(modelOptions);

        var selectedOption = TranslationModelOptions.FirstOrDefault(x => ReferenceEquals(x.Profile, previousProfile))
            ?? TranslationModelOptions.FirstOrDefault(x => x.Profile.IsActive)
            ?? TranslationModelOptions.FirstOrDefault();

        if (!ReferenceEquals(_selectedTranslationModel, selectedOption))
        {
            _selectedTranslationModel = selectedOption;
            OnPropertyChanged(nameof(SelectedTranslationModel));
        }

        if (selectedOption is not null)
        {
            var shouldSaveActive = ApiProfiles.Any(x => x.IsActive != ReferenceEquals(x, selectedOption.Profile));
            SetActiveProfile(selectedOption.Profile);
            if (shouldSaveActive)
            {
                Store.Save(Paths.ApiProfilesPath, ApiProfiles.ToList());
            }
        }

        OnPropertyChanged(nameof(TranslationModelOptions));
        OnPropertyChanged(nameof(TranslationProviderOptions));
        OnPropertyChanged(nameof(IsTranslationProviderSelectorEnabled));
        OnPropertyChanged(nameof(IsTranslationModelSelectorEnabled));
        OnPropertyChanged(nameof(IsTranslationModelFallbackVisible));
        OnPropertyChanged(nameof(CurrentModel));
        OnPropertyChanged(nameof(CurrentModelDetail));
        OnPropertyChanged(nameof(ApiStatus));
    }

    private void SetActiveProfile(ApiProfile profile)
    {
        foreach (var item in ApiProfiles)
        {
            item.IsActive = ReferenceEquals(item, profile);
        }
    }

    private static bool IsDisplayableTranslationProfile(ApiProfile profile)
    {
        return ApiProfileRules.IsDisplayableTranslationProfile(profile);
    }

    private static bool IsReadyTranslationProfile(ApiProfile profile)
    {
        return ApiProfileRules.IsReadyRemoteProfile(profile);
    }

    private string GetProviderDisplayName(string provider)
    {
        return ApiProfileRules.NormalizeProvider(provider) switch
        {
            "OpenAI" => _localization["provider.openai.name"],
            "Gemini" => _localization["provider.gemini.name"],
            "DeepSeek" => _localization["provider.deepseek.name"],
            "MiMo" => _localization["provider.mimo.name"],
            "OpenRouter" => _localization["provider.openrouter.name"],
            "Kimi" => _localization["provider.kimi.name"],
            "通义千问" => _localization["provider.qwen.name"],
            "智谱 GLM" => _localization["provider.glm.name"],
            "硅基流动" => _localization["provider.siliconflow.name"],
            "本地 GGUF" => _localization["provider.local.name"],
            "自定义" => _localization["provider.custom.name"],
            _ => provider
        };
    }

    private string PresetLabel(string value)
    {
        return value switch
        {
            "日译中" => _localization["preset.jaToZh"],
            "英译中" => _localization["preset.enToZh"],
            "中译日" => _localization["preset.zhToJa"],
            "中译英" => _localization["preset.zhToEn"],
            _ => value
        };
    }

    private string SpeedLabel(string value)
    {
        return value switch
        {
            "保守" => _localization["speed.conservative"],
            "普通" => _localization["speed.normal"],
            "快速" => _localization["speed.fast"],
            "批量并发" => _localization["speed.batch"],
            "自定义" => _localization["speed.custom"],
            _ => value
        };
    }
}
