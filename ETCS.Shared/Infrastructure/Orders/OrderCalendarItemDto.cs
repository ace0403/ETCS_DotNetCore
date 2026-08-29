namespace ETCS.Shared.Infrastructure.Orders;

public sealed class OrderCalendarItemDto
{
    public DateTime MealDate { get; init; }

    public int StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public int OrderTypeId { get; init; }

    public string ItemName { get; init; } = string.Empty;

    public decimal ItemPrice { get; init; }

    public int Quantity { get; init; }
}
