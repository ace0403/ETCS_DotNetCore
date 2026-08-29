namespace ETCS.Shared.Infrastructure.Students;

public interface IReplaceCardRequestRepository
{
    Task<ReplaceCardCreateResult> CreateAsync(
        int guardianId,
        string customerId,
        string cardNumber,
        string reason,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ReplaceCardRequestListItemDto>> GetByGuardianAsync(
        int guardianId,
        CancellationToken cancellationToken = default);
}

public sealed record ReplaceCardCreateResult(
    bool Success,
    string Message,
    int? RefCode = null);
