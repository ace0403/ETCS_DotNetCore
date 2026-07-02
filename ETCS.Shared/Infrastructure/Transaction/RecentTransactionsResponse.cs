namespace ETCS.Shared.Infrastructure.Transaction;

public sealed class RecentTransactionsResponse
{
    public int GuardianId { get; init; }

    public int Count { get; init; }

    public IReadOnlyList<TransactionHistoryItemDto> Items { get; init; } = [];
}
