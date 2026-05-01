using System.Text.Json;

namespace SakuyaTranslator.Core.Services;

public sealed class LocalizationService
{
    private readonly PortablePaths _paths;
    private Dictionary<string, string> _strings = new(StringComparer.Ordinal);

    public LocalizationService(PortablePaths paths)
    {
        _paths = paths;
    }

    public string Culture { get; private set; } = "zh-CN";
    public IReadOnlyDictionary<string, string> Strings => _strings;

    public string this[string key] => _strings.TryGetValue(key, out var value) ? value : key;

    public void Load(string culture)
    {
        Culture = culture;
        var path = Path.Combine(_paths.LocalesDirectory, $"{culture}.json");
        if (!File.Exists(path))
        {
            path = Path.Combine(_paths.LocalesDirectory, "zh-CN.json");
        }

        if (!File.Exists(path))
        {
            _strings = new Dictionary<string, string>(StringComparer.Ordinal);
            return;
        }

        var json = File.ReadAllText(path);
        _strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json)
                   ?? new Dictionary<string, string>(StringComparer.Ordinal);
    }
}
