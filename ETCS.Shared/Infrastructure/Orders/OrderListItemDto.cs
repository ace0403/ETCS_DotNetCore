namespace ETCS.Shared.Infrastructure.Orders;

public sealed class OrderListItemDto
{
    public int Id { get; init; }

    public string OrderId { get; init; } = string.Empty;

    public int StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public int GuardianId { get; init; }

    public decimal Total { get; init; }

    public int TotalItems { get; init; }

    public int OrderStatusId { get; init; }

    public bool IsPaid { get; init; }

    public DateTime OrderDate { get; init; }

    public DateTime CreatedOn { get; init; }
}
