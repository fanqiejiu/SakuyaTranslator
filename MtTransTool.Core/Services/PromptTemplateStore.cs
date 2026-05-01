using MtTransTool.Core.Models;

namespace MtTransTool.Core.Services;

public sealed class PromptTemplateStore
{
    private readonly PortablePaths _paths;
    private readonly JsonFileStore _jsonFileStore;

    public PromptTemplateStore(PortablePaths paths, JsonFileStore jsonFileStore)
    {
        _paths = paths;
        _jsonFileStore = jsonFileStore;
    }

    public string TemplatesPath => Path.Combine(_paths.DataDirectory, "prompt_templates.json");

    public List<PromptTemplate> Load()
    {
        return _jsonFileStore.LoadOrCreate(TemplatesPath, new List<PromptTemplate>
        {
            new()
            {
                Name = "游戏文本通用",
                IsActive = true,
                Content = LanguageProfiles.BuildSystemPrompt("日译中")
            },
            new()
            {
                Name = "字幕简洁风格",
                Content = "Translate the text into Simplified Chinese. Keep subtitle timing, names, variables, punctuation control codes, and line breaks unchanged. Use concise natural wording."
            }
        });
    }

    public void Save(IEnumerable<PromptTemplate> templates)
    {
        _jsonFileStore.Save(TemplatesPath, templates.ToList());
    }
}
