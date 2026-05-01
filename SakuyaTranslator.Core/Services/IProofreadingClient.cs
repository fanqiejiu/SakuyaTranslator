using SakuyaTranslator.Core.Models;

namespace SakuyaTranslator.Core.Services;

public interface IProofreadingClient
{
    Task<IReadOnlyList<ProofreadIssue>> ProofreadAsync(
        IReadOnlyList<ProofreadBatchItem> items,
        AppSettings settings,
        ApiProfile profile,
        CancellationToken cancellationToken = default);
}
