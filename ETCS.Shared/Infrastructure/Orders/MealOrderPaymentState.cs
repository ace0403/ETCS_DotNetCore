namespace ETCS.Shared.Infrastructure.Orders;

public sealed class MealOrderPaymentState
{
    public string OrderId { get; init; } = string.Empty;

    public int StudentId { get; init; }

    public int GuardianId { get; init; }

    public decimal Total { get; init; }

    public bool IsPaid { get; init; }

    public long? AccessLogId { get; init; }

    public int MealTransactionId { get; init; }

    public int OrderTypeId { get; init; }
}
