namespace MtTransTool.Core.Services;

public sealed class TranslationDocumentLoader
{
    private readonly MtToolJsonParser _jsonParser = new();
    private readonly TextLikeDocumentParser _textParser = new();
    private readonly CsvDocumentParser _csvParser = new();

    public static readonly string[] SupportedExtensions = [".json", ".srt", ".txt", ".csv"];

    public bool IsSupported(string path)
    {
        return SupportedExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());
    }

    public async Task<ITranslationDocument> LoadAsync(string path, CancellationToken cancellationToken = default)
    {
        return Path.GetExtension(path).ToLowerInvariant() switch
        {
            ".json" => await _jsonParser.LoadAsync(path, cancellationToken),
            ".srt" or ".txt" => await _textParser.LoadAsync(path, cancellationToken),
            ".csv" => await _csvParser.LoadAsync(path, cancellationToken),
            _ => throw new NotSupportedException($"不支持的文件类型：{Path.GetExtension(path)}")
        };
    }
}
