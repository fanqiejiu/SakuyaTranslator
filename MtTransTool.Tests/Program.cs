using System.Text.Json;
using MtTransTool.Core.Models;
using MtTransTool.Core.Services;

var samplePath = args.Length > 0
    ? args[0]
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "ManualTransFile.json"));

if (!File.Exists(samplePath))
{
    throw new FileNotFoundException("Sample MTTool JSON was not found.", samplePath);
}

var parser = new MtToolJsonParser();
var document = await parser.LoadAsync(samplePath);

Assert(document.Entries.Count > 0, "Parser should find entries.");
Assert(document.Entries.Any(x => x.SourceText.Contains("マップ設定")), "Parser should decode Japanese keys.");

var firstTranslatable = document.Entries.First(x => x.SourceText.Contains("マップ設定"));
firstTranslatable.TranslationText = "地图设置";
var exported = document.ExportPreservingFormat();

using var json = JsonDocument.Parse(exported);
Assert(json.RootElement.TryGetProperty("マップ設定", out var translated), "Export should keep the original key.");
Assert(translated.GetString() == "地图设置", "Export should replace only the value.");

Console.WriteLine($"OK: parsed {document.Entries.Count} entries from {samplePath}");

var tempRoot = Path.Combine(Path.GetTempPath(), "MtTransTool.Tests");
Directory.CreateDirectory(tempRoot);

var srtPath = Path.Combine(tempRoot, "sample.srt");
await File.WriteAllTextAsync(srtPath, "1\r\n00:00:01,000 --> 00:00:03,000\r\nこんにちは\r\n\r\n");
var textDoc = await new TextLikeDocumentParser().LoadAsync(srtPath);
Assert(textDoc.Entries.Count == 1, "SRT parser should pick subtitle text lines only.");
textDoc.Entries[0].TranslationText = "你好";
textDoc.Entries[0].Status = TranslationStatus.Done;
Assert(textDoc.ExportPreservingFormat().Contains("你好"), "SRT export should replace subtitle text.");
var srtProofreadIssues = new RuleBasedProofreader().Analyze(textDoc.Entries, new AppSettings(), textDoc.Kind);
Assert(!srtProofreadIssues.Any(x => x.SourceText.Contains("-->")), "SRT proofread should not include timing lines.");

var csvPath = Path.Combine(tempRoot, "sample.csv");
await File.WriteAllTextAsync(csvPath, "id,text\r\n1,マップ設定\r\n");
var csvDoc = await new CsvDocumentParser().LoadAsync(csvPath);
Assert(csvDoc.Entries.Count == 1, "CSV parser should pick translatable cells.");
csvDoc.Entries[0].TranslationText = "地图设置";
csvDoc.Entries[0].Status = TranslationStatus.Done;
Assert(csvDoc.ExportPreservingFormat().Contains("地图设置"), "CSV export should replace translatable cells.");
var csvProofreadIssues = new RuleBasedProofreader().Analyze(csvDoc.Entries, new AppSettings(), csvDoc.Kind);
Assert(!csvProofreadIssues.Any(x => x.Category == "疑似未翻译"), "CSV proofread should use translated cells.");

var txtPath = Path.Combine(tempRoot, "sample.txt");
await File.WriteAllTextAsync(txtPath, "マップ設定\r\n\r\nBGM/title.ogg\r\n");
var txtDoc = await new TextLikeDocumentParser().LoadAsync(txtPath);
Assert(txtDoc.Entries.Count == 2, "TXT parser should pick non-empty text lines.");
txtDoc.Entries[0].TranslationText = "地图设置";
txtDoc.Entries[0].Status = TranslationStatus.Done;
txtDoc.Entries[1].Status = TranslationStatus.Skipped;
var txtProofreadIssues = new RuleBasedProofreader().Analyze(txtDoc.Entries.Where(x => x.Status == TranslationStatus.Done), new AppSettings(), txtDoc.Kind);
Assert(!txtProofreadIssues.Any(x => x.SourceText.Contains("BGM/")), "TXT proofread should allow skipped code-like lines to stay out of proofread.");

var settings = new AppSettings();
TranslationSpeedProfiles.Apply(settings, "批量并发");
Assert(settings.FileConcurrency == 3, "Batch concurrency should raise file concurrency.");
Assert(settings.RequestConcurrency == 8, "Batch concurrency should raise request concurrency.");
Assert(settings.BatchSize == 50, "Batch concurrency should raise batch size.");

var proofreadIssues = new RuleBasedProofreader().Analyze(
    [
        new TranslationEntry
        {
            Index = 0,
            SourceText = "こんにちは\\V[1]",
            TranslationText = "你好"
        },
        new TranslationEntry
        {
            Index = 1,
            SourceText = "マップ設定",
            TranslationText = "マップ設定"
        }
    ],
    new AppSettings());
Assert(proofreadIssues.Any(x => x.Category == "格式保护"), "Rule proofreader should catch broken placeholders.");
Assert(proofreadIssues.Any(x => x.Category == "疑似未翻译"), "Rule proofreader should catch unchanged text.");

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}
