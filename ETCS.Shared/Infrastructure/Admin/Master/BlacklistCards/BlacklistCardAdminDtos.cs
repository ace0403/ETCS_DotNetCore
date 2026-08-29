namespace ETCS.Shared.Infrastructure.Admin.Master.BlacklistCards;

public sealed class BlacklistCardListItemDto
{
    public string CardSn { get; init; } = string.Empty;
    public string CustomerId { get; init; } = string.Empty;
    public string LastUsed { get; init; } = string.Empty;
    public decimal Balance { get; init; }
    public string Status { get; init; } = string.Empty;
    public string BalanceTransfer { get; init; } = string.Empty;
    public bool CanTransfer { get; init; }
}

public sealed class BlacklistCardLookupResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<BlacklistCardListItemDto> Items { get; init; } = [];

    public static BlacklistCardLookupResult Ok(IReadOnlyList<BlacklistCardListItemDto> items) =>
        new() { Success = true, Items = items };

    public static BlacklistCardLookupResult Fail(string message) =>
        new() { Success = false, Message = message };
}

public sealed class BlacklistCardRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public string? PerformedBy { get; set; }
}

public sealed class BlacklistCardTransferRequest
{
    public string CustomerId { get; set; } = string.Empty;
    public string CardSn { get; set; } = string.Empty;
    public string? PerformedBy { get; set; }
}
