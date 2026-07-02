namespace ETCS.Shared.Infrastructure.Orders;

public sealed class OrderMealLineItemRequest
{
    public int? ItemId { get; init; }
    public int? PackageId { get; init; }
    public DateTime MealDate { get; init; }
    public Guid Id { get; init; }
    public decimal Price { get; init; }
    public decimal Total { get; init; }
    public int Quantity { get; init; }
}
