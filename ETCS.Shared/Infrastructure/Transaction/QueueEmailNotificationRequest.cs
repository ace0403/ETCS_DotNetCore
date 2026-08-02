namespace ETCS.Shared.Infrastructure.Transaction;

public sealed class QueueEmailNotificationRequest
{
    public string TemplateKey { get; init; } = string.Empty;

    public string ToEmail { get; init; } = string.Empty;

    public string GuardianName { get; init; } = string.Empty;

    public string StudentName { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public string TransactionId { get; init; } = string.Empty;

    public string Amount { get; init; } = string.Empty;

    public string EventDate { get; init; } = string.Empty;

    public string OrderItems { get; init; } = string.Empty;

    public string ResetLink { get; init; } = string.Empty;

    public string ExpiryMinutes { get; init; } = string.Empty;

    /// <summary>When set with OrderId, order line items are loaded in the background worker.</summary>
    public int GuardianId { get; init; }

    public string PayloadJson { get; init; } = string.Empty;
}
