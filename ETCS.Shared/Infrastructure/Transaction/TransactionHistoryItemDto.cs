namespace ETCS.Shared.Infrastructure.Transaction;

public sealed class TransactionHistoryItemDto
{
    public int Id { get; init; }

    public int GuardianId { get; init; }

    public int? StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public string TransactionType { get; init; } = "topup";

    public int? OrderTypeId { get; init; }

    public string OrderId { get; init; } = string.Empty;

    public string GatewayTransactionId { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Remarks { get; init; } = string.Empty;

    public bool IsTransactionCompleted { get; init; }

    public int? StatusId { get; init; }

    public DateTime CreatedOn { get; init; }

    public DateTime? UpdatedOn { get; init; }
}
