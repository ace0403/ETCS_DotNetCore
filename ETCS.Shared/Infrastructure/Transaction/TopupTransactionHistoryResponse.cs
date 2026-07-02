namespace ETCS.Shared.Infrastructure.Transaction;

public sealed class TopupTransactionHistoryResponse
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public IReadOnlyList<TopupTransactionHistoryItemDto> Items { get; init; } = [];
}
