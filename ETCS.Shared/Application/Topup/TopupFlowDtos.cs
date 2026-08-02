namespace ETCS.Shared.Application.Topup;

public sealed class TopupInitiateRequest
{
    public int GuardianId { get; init; }

    public string StudentId { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    /// <summary>Optional gateway return URL template with {0} for orderId.</summary>
    public string? ReturnUrl { get; init; }
}

public sealed class TopupInitiateResponse
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public string TransactionId { get; init; } = string.Empty;

    public string RedirectUrl { get; init; } = string.Empty;

    public decimal? MinimumTopupAmount { get; init; }
}

public sealed class TopupCompleteRequest
{
    public int StudentId { get; init; }

    public string OrderId { get; init; } = string.Empty;

    public string TransactionId { get; init; } = string.Empty;
}

public sealed class TopupCompleteResponse
{
    public bool IsSuccess { get; init; }

    public bool IsPending { get; init; }

    public bool IsAlreadyProcessed { get; init; }

    public string Message { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public string TransactionId { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Status { get; init; } = string.Empty;
}
