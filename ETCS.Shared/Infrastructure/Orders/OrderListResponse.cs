namespace ETCS.Shared.Infrastructure.Orders;

public sealed class OrderListResponse
{
    public int Page { get; init; }

    public int PageSize { get; init; }

    public int TotalCount { get; init; }

    public IReadOnlyList<OrderListItemDto> Items { get; init; } = [];
}
