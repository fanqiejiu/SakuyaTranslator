using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace SakuyaTranslator.Core.Services;

public sealed class JsonFileStore
{
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    public T LoadOrCreate<T>(string path, T fallback)
    {
        if (!File.Exists(path))
        {
            Save(path, fallback);
            return fallback;
        }

        var json = File.ReadAllText(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<T>(json, _options) ?? fallback;
    }

    public void Save<T>(string path, T value)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonSerializer.Serialize(value, _options);
        File.WriteAllText(path, json, new UTF8Encoding(false));
    }
}
