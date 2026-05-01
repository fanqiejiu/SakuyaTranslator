using System.Collections.ObjectModel;
using System.Windows.Input;
using SakuyaTranslator.Core.Models;
using SakuyaTranslator.Core.Services;

namespace SakuyaTranslator.App.ViewModels;

public sealed class TemplatesViewModel : ViewModelBase
{
    private readonly PromptTemplateStore _templateStore;
    private readonly AppSettings _settings;
    private PromptTemplate? _selectedTemplate;
    private string _savedMessage = "";

    public TemplatesViewModel(PromptTemplateStore templateStore, AppSettings settings)
    {
        _templateStore = templateStore;
        _settings = settings;
        Templates = new ObservableCollection<PromptTemplate>(_templateStore.Load());
        _selectedTemplate = Templates.FirstOrDefault(x => x.IsActive) ?? Templates.FirstOrDefault();
        SaveCommand = new RelayCommand(_ => Save());
        NewCommand = new RelayCommand(_ => NewTemplate());
        ApplyCommand = new RelayCommand(_ => Apply());
    }

    public ObservableCollection<PromptTemplate> Templates { get; }
    public ICommand SaveCommand { get; }
    public ICommand NewCommand { get; }
    public ICommand ApplyCommand { get; }

    public PromptTemplate? SelectedTemplate
    {
        get => _selectedTemplate;
        set => SetProperty(ref _selectedTemplate, value);
    }

    public string SavedMessage
    {
        get => _savedMessage;
        set => SetProperty(ref _savedMessage, value);
    }

    private void NewTemplate()
    {
        var template = new PromptTemplate
        {
            Name = $"模板 {Templates.Count + 1}",
            Content = LanguageProfiles.BuildSystemPrompt(_settings.TranslationPreset)
        };
        Templates.Add(template);
        SelectedTemplate = template;
    }

    private void Apply()
    {
        if (SelectedTemplate is null)
        {
            return;
        }

        foreach (var template in Templates)
        {
            template.IsActive = ReferenceEquals(template, SelectedTemplate);
        }

        _settings.CustomSystemPrompt = SelectedTemplate.Content;
        SavedMessage = $"已应用：{SelectedTemplate.Name}";
    }

    private void Save()
    {
        Apply();
        _templateStore.Save(Templates);
        SavedMessage = $"已保存：{DateTime.Now:HH:mm:ss}";
    }
}
