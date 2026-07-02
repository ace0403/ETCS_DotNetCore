namespace ETCS.Shared.Infrastructure.Transaction;

public sealed class TransactionHistoryResponse
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public IReadOnlyList<TransactionHistoryItemDto> Items { get; init; } = [];
}
