using System.Windows.Input;
using Avalonia;
using MtTransTool.App;
using MtTransTool.Core.Models;
using MtTransTool.Core.Services;

namespace MtTransTool.App.ViewModels;

public sealed class SettingsViewModel : ViewModelBase
{
    public sealed record SettingOption(string Value, string DisplayName)
    {
        public override string ToString() => DisplayName;
    }

    private readonly JsonFileStore _store;
    private readonly PortablePaths _paths;
    private readonly AppSettings _settings;
    private readonly LocalizationService _localization;
    private string _originalUiCulture;
    private string _savedMessage = "";

    public SettingsViewModel(
        PortablePaths paths,
        JsonFileStore store,
        AppSettings settings,
        LocalizationService? localization = null)
    {
        _paths = paths;
        _store = store;
        _settings = settings;
        _localization = localization ?? new LocalizationService(paths);
        if (localization is null)
        {
            _localization.Load(settings.UiCulture);
        }

        _originalUiCulture = settings.UiCulture;
        SaveCommand = new RelayCommand(_ => Save());
    }

    public event EventHandler? SettingsChanged;
    public event EventHandler? RestartRequired;

    public AppSettings Settings => _settings;
    public IReadOnlyList<SettingOption> UiCultures =>
    [
        new("zh-CN", _localization["culture.zh"]),
        new("ja-JP", _localization["culture.ja"]),
        new("en-US", _localization["culture.en"])
    ];

    public IReadOnlyList<SettingOption> ThemeModeOptions =>
    [
        new("跟随系统", _localization["theme.system"]),
        new("浅色", _localization["theme.light"]),
        new("深色", _localization["theme.dark"])
    ];

    public IReadOnlyList<SettingOption> SourceLanguageOptions =>
    [
        new("ja", _localization["language.ja"]),
        new("en", _localization["language.en"]),
        new("zh-CN", _localization["language.zh"]),
        new("auto", _localization["language.auto"])
    ];

    public IReadOnlyList<SettingOption> TargetLanguageOptions =>
    [
        new("zh-CN", _localization["language.zh"]),
        new("ja", _localization["language.ja"]),
        new("en", _localization["language.en"])
    ];

    public IReadOnlyList<SettingOption> TranslationPresetOptions =>
    [
        new("日译中", _localization["preset.jaToZh"]),
        new("英译中", _localization["preset.enToZh"]),
        new("中译日", _localization["preset.zhToJa"]),
        new("中译英", _localization["preset.zhToEn"])
    ];

    public IReadOnlyList<SettingOption> SpeedPresetOptions => TranslationSpeedProfiles.Names
        .Select(x => new SettingOption(x, SpeedPresetLabel(x)))
        .ToArray();

    public IReadOnlyList<SettingOption> UpdateFrequencyOptions =>
    [
        new("每天", _localization["frequency.daily"]),
        new("每周", _localization["frequency.weekly"]),
        new("每次启动", _localization["frequency.startup"]),
        new("手动", _localization["frequency.manual"])
    ];

    public ICommand SaveCommand { get; }

    public SettingOption SelectedUiCulture
    {
        get => FindOption(UiCultures, _settings.UiCulture);
        set
        {
            if (value is null)
            {
                return;
            }

            if (_settings.UiCulture == value.Value)
            {
                return;
            }

            _settings.UiCulture = value.Value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Settings));
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public SettingOption SelectedThemeModeOption
    {
        get => FindOption(ThemeModeOptions, _settings.ThemeMode);
        set
        {
            if (value is null || _settings.ThemeMode == value.Value)
            {
                return;
            }

            _settings.ThemeMode = value.Value;
            if (Application.Current is not null)
            {
                Application.Current.RequestedThemeVariant = App.ThemeVariantFrom(value.Value);
            }

            OnPropertyChanged();
            OnPropertyChanged(nameof(Settings));
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public SettingOption SelectedSpeedPresetOption
    {
        get => FindOption(SpeedPresetOptions, _settings.SpeedPreset);
        set
        {
            if (value is null || _settings.SpeedPreset == value.Value)
            {
                return;
            }

            TranslationSpeedProfiles.Apply(_settings, value.Value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(SpeedPresetDescription));
            OnPropertyChanged(nameof(IsCustomSpeed));
            RefreshSpeedFields();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public SettingOption SelectedTranslationPresetOption
    {
        get => FindOption(TranslationPresetOptions, _settings.TranslationPreset);
        set
        {
            if (value is null || _settings.TranslationPreset == value.Value)
            {
                return;
            }

            _settings.TranslationPreset = value.Value;
            (_settings.SourceLanguage, _settings.TargetLanguage) = LanguageProfiles.FromPreset(value.Value);
            OnPropertyChanged();
            OnPropertyChanged(nameof(Settings));
            OnPropertyChanged(nameof(SelectedSourceLanguage));
            OnPropertyChanged(nameof(SelectedTargetLanguage));
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public SettingOption SelectedSourceLanguage
    {
        get => FindOption(SourceLanguageOptions, _settings.SourceLanguage);
        set
        {
            if (value is null || _settings.SourceLanguage == value.Value)
            {
                return;
            }

            _settings.SourceLanguage = value.Value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Settings));
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public SettingOption SelectedTargetLanguage
    {
        get => FindOption(TargetLanguageOptions, _settings.TargetLanguage);
        set
        {
            if (value is null || _settings.TargetLanguage == value.Value)
            {
                return;
            }

            _settings.TargetLanguage = value.Value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Settings));
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public SettingOption SelectedUpdateFrequency
    {
        get => FindOption(UpdateFrequencyOptions, _settings.UpdateCheckFrequency);
        set
        {
            if (value is null || _settings.UpdateCheckFrequency == value.Value)
            {
                return;
            }

            _settings.UpdateCheckFrequency = value.Value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Settings));
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public string SavedMessage
    {
        get => _savedMessage;
        set => SetProperty(ref _savedMessage, value);
    }

    public string RestartDialogTitle => _localization["dialog.restart.title"];
    public string RestartDialogBody => _localization["dialog.restart.body"];
    public string RestartDialogButton => _localization["dialog.ok"];
    public string SpeedPresetDescription => SpeedPresetDescriptionFor(_settings.SpeedPreset);
    public bool IsCustomSpeed => _settings.SpeedPreset == "自定义";

    public bool EnableExperimentalProofread
    {
        get => _settings.EnableExperimentalProofread;
        set
        {
            if (_settings.EnableExperimentalProofread == value)
            {
                return;
            }

            _settings.EnableExperimentalProofread = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(Settings));
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int FileConcurrency
    {
        get => _settings.FileConcurrency;
        set
        {
            if (_settings.FileConcurrency == value)
            {
                return;
            }

            _settings.FileConcurrency = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int RequestConcurrency
    {
        get => _settings.RequestConcurrency;
        set
        {
            if (_settings.RequestConcurrency == value)
            {
                return;
            }

            _settings.RequestConcurrency = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int BatchSize
    {
        get => _settings.BatchSize;
        set
        {
            if (_settings.BatchSize == value)
            {
                return;
            }

            _settings.BatchSize = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int RetryCount
    {
        get => _settings.RetryCount;
        set
        {
            if (_settings.RetryCount == value)
            {
                return;
            }

            _settings.RetryCount = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int RequestIntervalMs
    {
        get => _settings.RequestIntervalMs;
        set
        {
            if (_settings.RequestIntervalMs == value)
            {
                return;
            }

            _settings.RequestIntervalMs = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public int TimeoutSeconds
    {
        get => _settings.TimeoutSeconds;
        set
        {
            if (_settings.TimeoutSeconds == value)
            {
                return;
            }

            _settings.TimeoutSeconds = value;
            OnPropertyChanged();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public void Save()
    {
        NormalizeSpeedSettings();
        _store.Save(_paths.SettingsPath, _settings);
        SavedMessage = $"{_localization["settings.saved"]}: {DateTime.Now:HH:mm:ss}";
        if (_settings.UiCulture != _originalUiCulture)
        {
            _originalUiCulture = _settings.UiCulture;
            RestartRequired?.Invoke(this, EventArgs.Empty);
        }

        SettingsChanged?.Invoke(this, EventArgs.Empty);
    }

    private void NormalizeSpeedSettings()
    {
        _settings.FileConcurrency = Math.Clamp(_settings.FileConcurrency, 1, 8);
        _settings.RequestConcurrency = Math.Clamp(_settings.RequestConcurrency, 1, 16);
        _settings.BatchSize = Math.Clamp(_settings.BatchSize, 1, 100);
        _settings.RetryCount = Math.Clamp(_settings.RetryCount, 0, 10);
        _settings.RequestIntervalMs = Math.Clamp(_settings.RequestIntervalMs, 0, 10_000);
        _settings.TimeoutSeconds = Math.Clamp(_settings.TimeoutSeconds, 10, 600);
        OnPropertyChanged(nameof(Settings));
        OnPropertyChanged(nameof(SpeedPresetDescription));
        RefreshSpeedFields();
    }

    private static SettingOption FindOption(IEnumerable<SettingOption> options, string value)
    {
        return options.FirstOrDefault(x => x.Value == value)
               ?? options.First();
    }

    private string SpeedPresetLabel(string value)
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

    private string SpeedPresetDescriptionFor(string value)
    {
        return value switch
        {
            "保守" => _localization["speed.desc.conservative"],
            "普通" => _localization["speed.desc.normal"],
            "快速" => _localization["speed.desc.fast"],
            "批量并发" => _localization["speed.desc.batch"],
            "自定义" => _localization["speed.desc.custom"],
            _ => _localization["speed.desc.custom"]
        };
    }

    private void RefreshSpeedFields()
    {
        OnPropertyChanged(nameof(FileConcurrency));
        OnPropertyChanged(nameof(RequestConcurrency));
        OnPropertyChanged(nameof(BatchSize));
        OnPropertyChanged(nameof(RetryCount));
        OnPropertyChanged(nameof(RequestIntervalMs));
        OnPropertyChanged(nameof(TimeoutSeconds));
    }
}
