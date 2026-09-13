namespace ETCS.Shared.Infrastructure.Pos;

public sealed class PosNfcMemberRow
{
    public string CustomerId { get; init; } = string.Empty;
    public string CardSn { get; init; } = string.Empty;
    public decimal BalPrepaid { get; init; }
    public decimal AccSpending { get; init; }
    public int IdCardStatus { get; init; }

    public bool IsActive => IdCardStatus == 1;
}

public interface IPosNfcMemberRepository
{
    Task<PosNfcMemberRow?> FindByCardSnAsync(
        IReadOnlyList<string> cardSnCandidates,
        CancellationToken cancellationToken);

    Task<PosNfcPurchaseResponse> PurchaseAsync(
        PosNfcMemberRow member,
        decimal amount,
        string transactionId,
        string ipAddress,
        DateTime purchaseDate,
        IReadOnlyList<PosPostPurchaseLineRequest> lines,
        CancellationToken cancellationToken);

    Task<PosNfcPurchaseResponse> UndoAsync(
        PosNfcMemberRow member,
        decimal amount,
        string transactionId,
        CancellationToken cancellationToken);
}
