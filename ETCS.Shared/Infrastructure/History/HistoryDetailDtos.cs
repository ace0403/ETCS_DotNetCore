namespace ETCS.Shared.Infrastructure.History;

public sealed class AccessLogHeaderDto
{
    public long AccessLogId { get; init; }

    public string TransactionId { get; init; } = string.Empty;

    public string CustomerId { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateTime LogDateTimeServer { get; init; }

    public int TransactionType { get; init; }

    public string Description { get; init; } = string.Empty;

    public int GuardianId { get; init; }

    public string StudentName { get; init; } = string.Empty;
}

public sealed class HistoryLineItemDto
{
    public string ItemName { get; init; } = string.Empty;

    public string SkuCode { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public DateTime? DeliveryDate { get; init; }
}
