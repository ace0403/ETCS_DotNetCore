namespace ETCS.Shared.Infrastructure.Transaction;

public sealed class TopupTransactionCreateRequest
{
    public int GuardianId { get; init; }

    public int StudentId { get; init; }

    public decimal Amount { get; init; }

    public string Remarks { get; init; } = string.Empty;

    public int StatusId { get; init; }

    public int CreatedBy { get; init; }
}
