namespace ETCS.Shared.Infrastructure.Orders;

public sealed class OrderInitiateResponse
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public int StudentId { get; init; }

    public int GuardianId { get; init; }

    public decimal Total { get; init; }

    public string PaymentUrl { get; init; } = string.Empty;

    public string GatewayTransactionId { get; init; } = string.Empty;

    public int MealTransactionId { get; init; }
}
