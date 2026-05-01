using System.Collections.ObjectModel;
using System.Net.Http;
using System.Windows.Input;
using SakuyaTranslator.Core.Models;
using SakuyaTranslator.Core.Services;

namespace SakuyaTranslator.App.ViewModels;

public sealed class ApiConfigViewModel : ViewModelBase
{
    private sealed record ProviderPreset(
        string Name,
        string BaseUrl,
        string DisplayKey,
        string SubtitleKey,
        string ApiKeyUrl,
        string DocsUrl,
        string DefaultModelHintKey);

    private static readonly ProviderPreset[] ProviderPresets =
    [
        new(
            "OpenAI",
            "https://api.openai.com/v1",
            "provider.openai.name",
            "provider.openai.subtitle",
            "https://platform.openai.com/api-keys",
            "https://platform.openai.com/docs/api-reference/chat/create",
            "provider.openai.modelHint"),
        new(
            "Gemini",
            "https://generativelanguage.googleapis.com/v1beta/openai",
            "provider.gemini.name",
            "provider.gemini.subtitle",
            "https://aistudio.google.com/app/apikey",
            "https://ai.google.dev/gemini-api/docs/openai",
            "provider.gemini.modelHint"),
        new(
            "DeepSeek",
            "https://api.deepseek.com",
            "provider.deepseek.name",
            "provider.deepseek.subtitle",
            "https://platform.deepseek.com/api_keys",
            "https://api-docs.deepseek.com/",
            "provider.deepseek.modelHint"),
        new(
            "MiMo",
            "https://api.xiaomimimo.com/v1",
            "provider.mimo.name",
            "provider.mimo.subtitle",
            "https://platform.xiaomimimo.com",
            "https://mimo.mi.com/",
            "provider.mimo.modelHint"),
        new(
            "OpenRouter",
            "https://openrouter.ai/api/v1",
            "provider.openrouter.name",
            "provider.openrouter.subtitle",
            "https://openrouter.ai/keys",
            "https://openrouter.ai/docs/api-reference/authentication",
            "provider.openrouter.modelHint"),
        new(
            "Kimi",
            "https://api.moonshot.ai/v1",
            "provider.kimi.name",
            "provider.kimi.subtitle",
            "https://platform.kimi.ai/console/api-keys",
            "https://platform.kimi.ai/docs/api/overview",
            "provider.kimi.modelHint"),
        new(
            "通义千问",
            "https://dashscope.aliyuncs.com/compatible-mode/v1",
            "provider.qwen.name",
            "provider.qwen.subtitle",
            "https://bailian.console.aliyun.com/?apiKey=1",
            "https://help.aliyun.com/zh/model-studio/compatibility-of-openai-with-dashscope",
            "provider.qwen.modelHint"),
        new(
            "智谱 GLM",
            "https://open.bigmodel.cn/api/paas/v4",
            "provider.glm.name",
            "provider.glm.subtitle",
            "https://open.bigmodel.cn/usercenter/apikeys",
            "https://docs.bigmodel.cn/cn/guide/develop/openai/introduction",
            "provider.glm.modelHint"),
        new(
            "硅基流动",
            "https://api.siliconflow.cn/v1",
            "provider.siliconflow.name",
            "provider.siliconflow.subtitle",
            "https://cloud.siliconflow.cn/account/ak",
            "https://docs.siliconflow.com/cn/api-reference/chat-completions/chat-completions",
            "provider.siliconflow.modelHint"),
        new(
            "本地 GGUF",
            "",
            "provider.local.name",
            "provider.local.subtitle",
            "",
            "",
            "provider.local.modelHint"),
        new(
            "自定义",
            "",
            "provider.custom.name",
            "provider.custom.subtitle",
            "",
            "",
            "provider.custom.modelHint")
    ];

    private readonly PortablePaths _paths;
    private readonly JsonFileStore _store;
    private readonly LocalizationService _localization;
    private readonly IList<ApiProfile> _sourceProfiles;
    private readonly ITranslationClient _translationClient;
    private ApiProfile _selectedProfile;
    private SavedModelOption? _selectedSavedModel;
    private bool _updatingSavedModelSelection;
    private string _statusText = string.Empty;

    public ApiConfigViewModel(
        PortablePaths paths,
        JsonFileStore store,
        IList<ApiProfile> profiles,
        LocalizationService? localization = null,
        ITranslationClient? translationClient = null)
    {
        _paths = paths;
        _store = store;
        _localization = localization ?? new LocalizationService(paths);
        _translationClient = translationClient ?? new OpenAiCompatibleTranslationClient();
        if (localization is null)
        {
            _localization.Load("zh-CN");
        }

        _sourceProfiles = profiles;
        Providers = ProviderPresets
            .Select(x => new ProviderOption(x.Name, _localization[x.DisplayKey]))
            .ToArray();
        Profiles = new ObservableCollection<ApiProfile>(profiles);
        if (Profiles.Count == 0)
        {
            Profiles.Add(new ApiProfile());
        }

        foreach (var profile in Profiles)
        {
            ApiProfileRules.NormalizeForUse(profile);
        }

        _selectedProfile = Profiles.FirstOrDefault(x => x.IsActive)
            ?? Profiles.FirstOrDefault()
            ?? new ApiProfile();

        _statusText = _localization["api.status.configPath"];
        AddProfileCommand = new RelayCommand(_ => AddProfile());
        DeleteProfileCommand = new RelayCommand(_ => DeleteProfile(), _ => Profiles.Count > 1);
        AddModelCommand = new RelayCommand(_ => AddModel());
        DeleteModelCommand = new RelayCommand(_ => DeleteModel(), _ => CanDeleteSavedModel);
        SaveCommand = new RelayCommand(_ => Save());
        TestCommand = new AsyncRelayCommand(_ => TestAsync());
        ResetBaseUrlCommand = new RelayCommand(_ => ResetBaseUrlToOfficial(), _ => CanResetBaseUrl);
        EnsureOfficialBaseUrlIfEmpty();
        RefreshProfileFields();
        RefreshProviderInfo();
        RefreshSavedModelOptions();
    }

    public event EventHandler? ProfilesChanged;

    public ObservableCollection<ApiProfile> Profiles { get; }
    public ObservableCollection<SavedModelOption> SavedModelOptions { get; } = [];
    public IReadOnlyList<ProviderOption> Providers { get; }

    public sealed record ProviderOption(string Value, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    public sealed record SavedModelOption(ApiProfile Profile, string ModelName)
    {
        public override string ToString() => ModelName;
    }

    public ApiProfile SelectedProfile
    {
        get => _selectedProfile;
        set
        {
            if (SetProperty(ref _selectedProfile, value))
            {
                EnsureOfficialBaseUrlIfEmpty();
                OnPropertyChanged(nameof(SelectedProvider));
                OnPropertyChanged(nameof(SelectedProviderOption));
                OnPropertyChanged(nameof(CanDeleteProfile));
                RefreshProfileFields();
                RefreshProviderInfo();
                RefreshSavedModelOptions();
                DeleteProfileCommand?.RaiseCanExecuteChanged();
            }
        }
    }

    public string SelectedProvider
    {
        get => SelectedProfile.Provider;
        set
        {
            if (SelectedProfile.Provider == value)
            {
                EnsureOfficialBaseUrlIfEmpty();
                RefreshProfileFields();
                RefreshProviderInfo();
                return;
            }

            var existingProfile = FindPreferredProviderProfile(value);
            if (existingProfile is not null && !ReferenceEquals(existingProfile, SelectedProfile))
            {
                SelectedProfile = existingProfile;
                StatusText = string.Format(_localization["api.status.providerSelected"], _localization[CurrentPreset.DisplayKey]);
                return;
            }

            if (!IsEmptyConnectionProfile(SelectedProfile))
            {
                var newProfile = CreateProviderProfile(value);
                Profiles.Add(newProfile);
                SelectedProfile = newProfile;
                StatusText = string.Format(_localization["api.status.providerChanged"], _localization[CurrentPreset.DisplayKey]);
                return;
            }

            var oldDisplayName = SelectedProfile.DisplayName;
            SelectedProfile.Provider = value;
            ApplyProviderPreset(value, resetConnection: true, oldDisplayName);
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedProviderOption));
            OnPropertyChanged(nameof(SelectedProfile));
            RefreshProfileFields();
            RefreshProviderInfo();
        }
    }

    public ProviderOption SelectedProviderOption
    {
        get => Providers.FirstOrDefault(x => x.Value == SelectedProfile.Provider) ?? Providers.Last();
        set
        {
            if (value is null)
            {
                return;
            }

            SelectedProvider = value.Value;
            OnPropertyChanged();
        }
    }

    public ICommand SaveCommand { get; }
    public ICommand TestCommand { get; }
    public RelayCommand AddProfileCommand { get; }
    public RelayCommand DeleteProfileCommand { get; }
    public RelayCommand AddModelCommand { get; }
    public RelayCommand DeleteModelCommand { get; }
    public RelayCommand ResetBaseUrlCommand { get; }
    public bool CanDeleteProfile => Profiles.Count > 1;
    public bool CanDeleteSavedModel => SelectedSavedModel is not null;

    public SavedModelOption? SelectedSavedModel
    {
        get => _selectedSavedModel;
        set
        {
            if (SetProperty(ref _selectedSavedModel, value))
            {
                DeleteModelCommand?.RaiseCanExecuteChanged();
                if (_updatingSavedModelSelection || value is null || ReferenceEquals(value.Profile, SelectedProfile))
                {
                    return;
                }

                SelectedProfile = value.Profile;
                StatusText = string.Format(_localization["api.status.modelSelected"], value.ModelName);
            }
        }
    }

    public string DisplayName
    {
        get => SelectedProfile.DisplayName;
        set
        {
            if (SelectedProfile.DisplayName == value)
            {
                return;
            }

            SelectedProfile.DisplayName = value;
            OnPropertyChanged();
        }
    }

    public string BaseUrl
    {
        get => SelectedProfile.BaseUrl;
        set
        {
            if (SelectedProfile.BaseUrl == value)
            {
                return;
            }

            SelectedProfile.BaseUrl = value;
            OnPropertyChanged();
        }
    }

    public string ApiKey
    {
        get => SelectedProfile.ApiKey;
        set
        {
            if (SelectedProfile.ApiKey == value)
            {
                return;
            }

            SelectedProfile.ApiKey = value;
            OnPropertyChanged();
        }
    }

    public string Model
    {
        get => SelectedProfile.Model;
        set
        {
            if (SelectedProfile.Model == value)
            {
                return;
            }

            SelectedProfile.Model = value;
            OnPropertyChanged();
        }
    }

    public string LocalModelPath
    {
        get => SelectedProfile.LocalModelPath;
        set
        {
            if (SelectedProfile.LocalModelPath == value)
            {
                return;
            }

            SelectedProfile.LocalModelPath = value;
            OnPropertyChanged();
        }
    }

    public string ProviderSubtitle => _localization[CurrentPreset.SubtitleKey];
    public string ProviderApiKeyUrl => CurrentPreset.ApiKeyUrl;
    public string ProviderDocsUrl => CurrentPreset.DocsUrl;
    public string ModelHint => _localization[CurrentPreset.DefaultModelHintKey];
    public bool HasProviderLinks => !string.IsNullOrWhiteSpace(ProviderApiKeyUrl) || !string.IsNullOrWhiteSpace(ProviderDocsUrl);
    public bool CanResetBaseUrl => !string.IsNullOrWhiteSpace(CurrentPreset.BaseUrl);

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private ProviderPreset CurrentPreset =>
        ProviderPresets.FirstOrDefault(x => x.Name == ApiProfileRules.NormalizeStoredProvider(SelectedProfile.Provider))
        ?? ProviderPresets.Last();

    private ApiProfile? FindPreferredProviderProfile(string providerName)
    {
        return Profiles
            .Where(x => x.Provider == providerName)
            .OrderByDescending(x => x.IsActive)
            .ThenByDescending(IsUsableApiProfile)
            .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.Model))
            .FirstOrDefault();
    }

    private ApiProfile CreateProviderProfile(string providerName)
    {
        var preset = ProviderPresets.FirstOrDefault(x => x.Name == providerName)
            ?? ProviderPresets.Last();

        return new ApiProfile
        {
            Provider = preset.Name,
            DisplayName = _localization[preset.DisplayKey],
            BaseUrl = preset.BaseUrl
        };
    }

    private static bool IsEmptyConnectionProfile(ApiProfile profile)
    {
        return string.IsNullOrWhiteSpace(profile.ApiKey)
            && string.IsNullOrWhiteSpace(profile.Model)
            && string.IsNullOrWhiteSpace(profile.LocalModelPath);
    }

    private static bool IsUsableApiProfile(ApiProfile profile)
    {
        return ApiProfileRules.IsReadyRemoteProfile(profile);
    }

    private void RefreshSavedModelOptions()
    {
        var selectedProfile = SelectedProfile;
        var previousModelName = SelectedSavedModel?.ModelName;
        _updatingSavedModelSelection = true;
        SavedModelOptions.Clear();

        var models = Profiles
            .Where(x => x.Provider == SelectedProfile.Provider && !string.IsNullOrWhiteSpace(x.Model))
            .GroupBy(x => x.Model.Trim(), StringComparer.OrdinalIgnoreCase)
            .Select(x => x.First())
            .OrderBy(x => x.Model, StringComparer.OrdinalIgnoreCase);

        foreach (var profile in models)
        {
            SavedModelOptions.Add(new SavedModelOption(profile, profile.Model.Trim()));
        }

        _selectedSavedModel = SavedModelOptions.FirstOrDefault(x => ReferenceEquals(x.Profile, selectedProfile))
            ?? SavedModelOptions.FirstOrDefault(x => string.Equals(x.ModelName, previousModelName, StringComparison.OrdinalIgnoreCase));
        OnPropertyChanged(nameof(SelectedSavedModel));
        _updatingSavedModelSelection = false;
        OnPropertyChanged(nameof(SavedModelOptions));
        OnPropertyChanged(nameof(CanDeleteSavedModel));
        DeleteModelCommand?.RaiseCanExecuteChanged();
    }

    private void ApplyProviderPreset(
        string providerName,
        bool resetConnection = false,
        string? oldDisplayName = null)
    {
        var preset = ProviderPresets.FirstOrDefault(x => x.Name == providerName);
        if (preset is null)
        {
            return;
        }

        BaseUrl = preset.BaseUrl;
        if (ShouldAutoRenameDisplayName(oldDisplayName ?? SelectedProfile.DisplayName))
        {
            DisplayName = _localization[preset.DisplayKey];
        }

        if (resetConnection)
        {
            ApiKey = "";
            Model = "";
            LocalModelPath = "";
            StatusText = string.Format(_localization["api.status.providerChanged"], _localization[preset.DisplayKey]);
            return;
        }

        if (SelectedProfile.Provider != "自定义" && SelectedProfile.Provider != "本地 GGUF")
        {
            StatusText = string.Format(_localization["api.status.baseUrlApplied"], _localization[preset.DisplayKey]);
        }
        else
        {
            StatusText = _localization["api.status.fillConnection"];
        }
    }

    private bool ShouldAutoRenameDisplayName(string? displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName) || displayName == "默认配置")
        {
            return true;
        }

        foreach (var preset in ProviderPresets)
        {
            var providerName = _localization[preset.DisplayKey];
            if (displayName == providerName)
            {
                return true;
            }

            if (displayName.StartsWith(providerName + " ", StringComparison.Ordinal)
                && displayName[(providerName.Length + 1)..].All(char.IsDigit))
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshProviderInfo()
    {
        OnPropertyChanged(nameof(ProviderSubtitle));
        OnPropertyChanged(nameof(Providers));
        OnPropertyChanged(nameof(SelectedProviderOption));
        OnPropertyChanged(nameof(ProviderApiKeyUrl));
        OnPropertyChanged(nameof(ProviderDocsUrl));
        OnPropertyChanged(nameof(ModelHint));
        OnPropertyChanged(nameof(HasProviderLinks));
        OnPropertyChanged(nameof(CanResetBaseUrl));
        ResetBaseUrlCommand?.RaiseCanExecuteChanged();
    }

    private void RefreshProfileFields()
    {
        OnPropertyChanged(nameof(DisplayName));
        OnPropertyChanged(nameof(BaseUrl));
        OnPropertyChanged(nameof(ApiKey));
        OnPropertyChanged(nameof(Model));
        OnPropertyChanged(nameof(LocalModelPath));
    }

    private void EnsureOfficialBaseUrlIfEmpty()
    {
        SelectedProfile.Provider = ApiProfileRules.NormalizeStoredProvider(SelectedProfile.Provider);
        if (string.IsNullOrWhiteSpace(BaseUrl) && CanResetBaseUrl)
        {
            ResetBaseUrlToOfficial();
        }
    }

    private void ResetBaseUrlToOfficial()
    {
        if (!CanResetBaseUrl)
        {
            return;
        }

        BaseUrl = ApiProfileRules.GetPresetBaseUrl(SelectedProfile.Provider);
        StatusText = string.Format(_localization["api.status.baseUrlApplied"], _localization[CurrentPreset.DisplayKey]);
    }

    private void Save()
    {
        MergeDuplicateSelectedProfile();
        PersistProfiles();
        StatusText = $"{_localization["settings.saved"]}: {DateTime.Now:HH:mm:ss}";
        RefreshSavedModelOptions();
    }

    private void PersistProfiles()
    {
        _sourceProfiles.Clear();
        foreach (var profile in Profiles)
        {
            ApiProfileRules.NormalizeForUse(profile);
            profile.IsActive = ReferenceEquals(profile, SelectedProfile);
            _sourceProfiles.Add(profile);
        }

        _store.Save(_paths.ApiProfilesPath, _sourceProfiles.ToList());
        ProfilesChanged?.Invoke(this, EventArgs.Empty);
    }

    private void MergeDuplicateSelectedProfile()
    {
        SelectedProfile.Model = SelectedProfile.Model.Trim();
        if (string.IsNullOrWhiteSpace(SelectedProfile.Model))
        {
            return;
        }

        var duplicate = Profiles.FirstOrDefault(x =>
            !ReferenceEquals(x, SelectedProfile)
            && x.Provider == SelectedProfile.Provider
            && string.Equals(x.Model.Trim(), SelectedProfile.Model, StringComparison.OrdinalIgnoreCase));
        if (duplicate is null)
        {
            return;
        }

        duplicate.DisplayName = SelectedProfile.DisplayName;
        duplicate.BaseUrl = SelectedProfile.BaseUrl;
        duplicate.ApiKey = SelectedProfile.ApiKey;
        duplicate.LocalModelPath = SelectedProfile.LocalModelPath;
        Profiles.Remove(SelectedProfile);
        SelectedProfile = duplicate;
    }

    private void AddModel()
    {
        var profile = new ApiProfile
        {
            Provider = SelectedProfile.Provider,
            DisplayName = SelectedProfile.DisplayName,
            BaseUrl = SelectedProfile.BaseUrl,
            ApiKey = SelectedProfile.ApiKey,
            LocalModelPath = SelectedProfile.LocalModelPath,
            Model = ""
        };

        Profiles.Add(profile);
        SelectedProfile = profile;
        StatusText = _localization["api.status.newModel"];
    }

    private void DeleteModel()
    {
        if (SelectedSavedModel is null)
        {
            return;
        }

        var deletedProfile = SelectedSavedModel.Profile;
        var provider = deletedProfile.Provider;
        var fallbackBaseUrl = deletedProfile.BaseUrl;
        var fallbackApiKey = deletedProfile.ApiKey;
        var fallbackDisplayName = deletedProfile.DisplayName;
        var fallbackLocalModelPath = deletedProfile.LocalModelPath;
        var deletedIndex = SavedModelOptions.IndexOf(SelectedSavedModel);

        Profiles.Remove(deletedProfile);
        var nextProfile = SavedModelOptions
            .Where(x => !ReferenceEquals(x.Profile, deletedProfile))
            .Select(x => x.Profile)
            .Skip(Math.Max(0, deletedIndex))
            .FirstOrDefault()
            ?? SavedModelOptions
                .Where(x => !ReferenceEquals(x.Profile, deletedProfile))
                .Select(x => x.Profile)
                .FirstOrDefault()
            ?? Profiles.FirstOrDefault(x => x.Provider == provider);

        if (nextProfile is null)
        {
            nextProfile = new ApiProfile
            {
                Provider = provider,
                DisplayName = fallbackDisplayName,
                BaseUrl = fallbackBaseUrl,
                ApiKey = fallbackApiKey,
                LocalModelPath = fallbackLocalModelPath,
                Model = ""
            };
            Profiles.Add(nextProfile);
        }

        SelectedProfile = nextProfile;
        PersistProfiles();
        RefreshSavedModelOptions();
        StatusText = _localization["api.status.deletedModel"];
    }

    private void AddProfile()
    {
        var preset = ProviderPresets.First(x => x.Name == "OpenAI");
        var profile = new ApiProfile
        {
            Provider = preset.Name,
            DisplayName = $"{_localization[preset.DisplayKey]} {Profiles.Count + 1}",
            BaseUrl = preset.BaseUrl
        };

        Profiles.Add(profile);
        SelectedProfile = profile;
        StatusText = _localization["api.status.newProfile"];
        OnPropertyChanged(nameof(CanDeleteProfile));
        DeleteProfileCommand.RaiseCanExecuteChanged();
    }

    private void DeleteProfile()
    {
        if (Profiles.Count <= 1)
        {
            return;
        }

        var index = Profiles.IndexOf(SelectedProfile);
        Profiles.Remove(SelectedProfile);
        SelectedProfile = Profiles[Math.Clamp(index, 0, Profiles.Count - 1)];
        StatusText = _localization["api.status.deletedProfile"];
        OnPropertyChanged(nameof(CanDeleteProfile));
        DeleteProfileCommand.RaiseCanExecuteChanged();
    }

    private async Task TestAsync()
    {
        if (ApiProfileRules.NormalizeProvider(SelectedProfile.Provider) == "本地 GGUF")
        {
            StatusText = File.Exists(SelectedProfile.LocalModelPath)
                ? _localization["api.status.localFound"]
                : _localization["api.status.localMissing"];
            return;
        }

        if (!ApiProfileRules.IsReadyRemoteProfile(SelectedProfile))
        {
            StatusText = _localization["api.status.missingFields"];
            return;
        }

        StatusText = _localization["api.status.testing"];

        try
        {
            await _translationClient.TestConnectionAsync(SelectedProfile);
            StatusText = _localization["api.status.testSuccess"];
        }
        catch (TaskCanceledException)
        {
            StatusText = _localization["api.status.testTimeout"];
        }
        catch (HttpRequestException ex)
        {
            StatusText = string.Format(_localization["api.status.testFailed"], ex.Message);
        }
        catch (Exception ex)
        {
            StatusText = string.Format(_localization["api.status.testFailed"], ex.Message);
        }
    }
}
