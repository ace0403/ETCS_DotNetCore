namespace ETCS.Shared.Infrastructure.Orders;

public sealed class OrderCompleteRequest
{
    public int StudentId { get; init; }

    public int GuardianId { get; init; }

    public string OrderId { get; init; } = string.Empty;

    public string TransactionId { get; init; } = string.Empty;
}
