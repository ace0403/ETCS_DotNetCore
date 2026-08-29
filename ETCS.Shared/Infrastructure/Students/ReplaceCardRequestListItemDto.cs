namespace ETCS.Shared.Infrastructure.Students;

public sealed class ReplaceCardRequestListItemDto
{
    public int RefCode { get; init; }

    public string CustomerId { get; init; } = string.Empty;

    public string? CardNumber { get; init; }

    public string? CardId { get; init; }

    public string? CardSn { get; init; }

    public short? Status { get; init; }

    public DateTime TimeChgState { get; init; }

    public decimal? BalPrepaid { get; init; }

    public int? ExpiryDate { get; init; }

    public DateTime? RecordExpiryDate { get; init; }

    public string? Reason { get; init; }
}
