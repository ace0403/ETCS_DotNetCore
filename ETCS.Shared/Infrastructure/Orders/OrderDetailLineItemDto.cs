namespace ETCS.Shared.Infrastructure.Orders;

public sealed class OrderDetailLineItemDto
{
    public int Id { get; init; }

    public int? ItemId { get; init; }

    public int? PackageId { get; init; }

    public string ItemName { get; init; } = string.Empty;

    public decimal ItemPrice { get; init; }

    public DateTime MealDate { get; init; }

    public DateTime CreatedOn { get; init; }
}
