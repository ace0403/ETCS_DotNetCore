namespace ETCS.Shared.Infrastructure.Orders;

public sealed class OrderInitiateRequest
{
    public int StudentId { get; init; }

    public int GuardianId { get; init; }

    public string OrderId { get; init; } = string.Empty;

    public int OrderStatusId { get; init; }

    public int OrderTypeId { get; init; }

    public decimal Total { get; init; }

    public string Notes { get; init; } = string.Empty;

    public IReadOnlyList<OrderMealLineItemRequest> MealList { get; init; } = [];
}
