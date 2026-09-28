using ETCS.Shared.Application.History;

namespace ETCS.Shared.Infrastructure.Transaction;

public sealed class RecentTransactionsResponse
{
    public int GuardianId { get; init; }

    public int Count { get; init; }

    public IReadOnlyList<GuardianHistoryListItemDto> Items { get; init; } = [];
}
