using MtTransTool.Core.Models;

namespace MtTransTool.Core.Services;

public interface ITranslationClient
{
    Task<IReadOnlyList<TranslationBatchResult>> TranslateAsync(
        IReadOnlyList<TranslationBatchItem> items,
        AppSettings settings,
        ApiProfile profile,
        CancellationToken cancellationToken = default);

    Task TestConnectionAsync(ApiProfile profile, CancellationToken cancellationToken = default);
}
