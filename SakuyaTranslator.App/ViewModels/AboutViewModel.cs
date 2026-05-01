using System.Windows.Input;
using SakuyaTranslator.Core.Models;
using SakuyaTranslator.Core.Services;

namespace SakuyaTranslator.App.ViewModels;

public sealed class AboutViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly JsonFileStore _store;
    private readonly PortablePaths _paths;
    private readonly UpdateChecker _updateChecker = new();
    private string _updateStatus = "尚未检查更新";

    public AboutViewModel(PortablePaths paths, JsonFileStore store, AppSettings settings)
    {
        _paths = paths;
        _store = store;
        _settings = settings;
        CheckUpdatesCommand = new AsyncRelayCommand(async _ => await CheckForUpdatesAsync());
    }

    public string AppName => "Sakuya Translator";
    public string Version => UpdateChecker.CurrentVersion;
    public string DataDirectory => _paths.DataDirectory;
    public string LogsDirectory => _paths.LogsDirectory;
    public ICommand CheckUpdatesCommand { get; }
    public UpdateCheckResult? LastResult { get; private set; }

    public string UpdateStatus
    {
        get => _updateStatus;
        set => SetProperty(ref _updateStatus, value);
    }

    public async Task<UpdateCheckResult> CheckForUpdatesAsync()
    {
        UpdateStatus = "正在检查更新...";
        var result = await _updateChecker.CheckAsync(_settings);
        LastResult = result;

        if (result.Success && result.HasUpdate && result.Latest is not null)
        {
            UpdateStatus = $"发现新版本 v{result.Latest.Version}";
            _store.Save(_paths.UpdateCachePath, result.Latest);
        }
        else if (result.Success)
        {
            UpdateStatus = "当前已是最新版本";
        }
        else
        {
            UpdateStatus = $"检查失败：{result.ErrorMessage}";
        }

        return result;
    }
}
