using System.Windows.Input;
using SakuyaTranslator.Core.Models;
using SakuyaTranslator.Core.Services;

namespace SakuyaTranslator.App.ViewModels;

public sealed class ManualTranslateViewModel : ViewModelBase
{
    private readonly AppSettings _settings;
    private readonly IList<ApiProfile> _profiles;
    private readonly LogsViewModel _logs;
    private readonly ITranslationClient _translationClient;
    private string _sourceText = "";
    private string _translatedText = "";
    private string _statusText = "等待输入";

    public ManualTranslateViewModel(
        AppSettings settings,
        IList<ApiProfile> profiles,
        LogsViewModel logs,
        ITranslationClient? translationClient = null)
    {
        _settings = settings;
        _profiles = profiles;
        _logs = logs;
        _translationClient = translationClient ?? new OpenAiCompatibleTranslationClient();
        TranslateCommand = new AsyncRelayCommand(async _ => await TranslateAsync());
        UseSelectedCommand = new RelayCommand(_ => UseSelectedEntryText());
    }

    public TranslateQueueViewModel? Queue { get; set; }
    public ICommand TranslateCommand { get; }
    public ICommand UseSelectedCommand { get; }

    public string SourceText
    {
        get => _sourceText;
        set => SetProperty(ref _sourceText, value);
    }

    public string TranslatedText
    {
        get => _translatedText;
        set => SetProperty(ref _translatedText, value);
    }

    public string StatusText
    {
        get => _statusText;
        set => SetProperty(ref _statusText, value);
    }

    private void UseSelectedEntryText()
    {
        if (Queue?.SelectedEntry is null)
        {
            StatusText = "未选择队列文本";
            return;
        }

        SourceText = Queue.SelectedEntry.SourceText;
        TranslatedText = Queue.SelectedEntry.TranslationText;
        StatusText = "已载入队列文本";
    }

    private async Task TranslateAsync()
    {
        if (string.IsNullOrWhiteSpace(SourceText))
        {
            StatusText = "请输入文本";
            return;
        }

        var profile = _profiles.FirstOrDefault(x => x.IsActive);
        if (profile is null || string.IsNullOrWhiteSpace(profile.ApiKey))
        {
            StatusText = "API 未配置";
            return;
        }

        try
        {
            StatusText = "翻译中...";
            var result = await _translationClient.TranslateAsync(
                [new TranslationBatchItem { Index = 0, SourceText = SourceText }],
                _settings,
                profile);
            TranslatedText = result.FirstOrDefault()?.TranslationText ?? "";
            StatusText = "完成";
            _logs.Add("手动翻译完成。");
        }
        catch (Exception ex)
        {
            StatusText = "失败";
            _logs.Add($"手动翻译失败：{ex.Message}");
        }
    }
}
