using System.Text.Json;

namespace ETCS.PaymentGateway.Models;

public sealed record NativePaymentVerificationRequest(
    string TransactionId, string OrderId, decimal Amount, string Currency, bool ManualCapture);

/// <summary>Only this validated, captured evidence may authorize native ledger completion.</summary>
public sealed class NativePaymentVerificationResult
{
    public bool IsSuccess { get; init; }
    public bool IsPending { get; init; }
    public bool IsAuthorized { get; init; }
    public bool IsCaptured { get; init; }
    public string Message { get; init; } = string.Empty;
    public string Status { get; init; } = string.Empty;
    public string? ResponseCode { get; init; }
    public string TransactionId { get; init; } = string.Empty;
    public string OrderId { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string Currency { get; init; } = string.Empty;
    public decimal? RemainingBalance { get; init; }
    public string? ApprovalCode { get; init; }
    public string? UniqueId { get; init; }

    public bool IsCancelled { get; init; }
    public bool MatchesCancellation(NativePaymentVerificationRequest expected) =>
        IsCancelled && !IsPending && !IsCaptured && !IsAuthorized
        && TransactionId == expected.TransactionId && OrderId == expected.OrderId
        && Amount == expected.Amount && Currency == expected.Currency
        && ((ResponseCode == "6824" && !IsSuccess && RemainingBalance is null or 0)
            || (ResponseCode == "0" && IsSuccess && expected.ManualCapture && RemainingBalance == 0
                && !string.IsNullOrWhiteSpace(UniqueId)));

    public bool Matches(NativePaymentVerificationRequest expected) =>
        IsSuccess && !IsPending && IsCaptured && ResponseCode == "0"
        && TransactionId == expected.TransactionId && OrderId == expected.OrderId
        && Amount == expected.Amount && Currency == expected.Currency
        && RemainingBalance is null or 0;
}

// Native-only models: legacy/V1 deserialization and response bodies are unchanged.
internal sealed class NativeEpgResponse
{
    public NativeEpgTransaction? Transaction { get; init; }
}

internal sealed class NativeEpgTransaction
{
    public JsonElement ResponseCode { get; init; }
    public string? ResponseDescription { get; init; }
    public string? TransactionID { get; init; }
    public string? OrderID { get; init; }
    public JsonElement Amount { get; init; }
    public JsonElement Balance { get; init; }
    public JsonElement IsCaptured { get; init; }
    public JsonElement Currency { get; init; }
    public string? ApprovalCode { get; init; }
    public string? UniqueID { get; init; }
    public string? AuthenticationToken { get; init; }
    public string? SessionID { get; init; }

    // Preserve compatibility with the native initiation mapping, not the legacy parser.
    [System.Text.Json.Serialization.JsonIgnore]
    public string? TransactionId => TransactionID;
    [System.Text.Json.Serialization.JsonIgnore]
    public string? SessionId => SessionID;
}

public static class NativeEpgTransport
{
    public static readonly HttpRequestOptionsKey<bool> RequestMarker = new("ETCS.NativeEPG");

    public static bool RequiresManualCapture(string? hint)
    {
        var modes = (hint ?? string.Empty).Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => x.Trim().ToUpperInvariant())
            .Where(x => x.StartsWith("CPT:", StringComparison.Ordinal)).Distinct().ToArray();
        if (modes.Length > 1 || (modes.Length == 1 && modes[0] is not ("CPT:Y" or "CPT:N")))
            throw new InvalidOperationException("Native capture hint must specify at most one CPT:Y or CPT:N.");
        return modes.Length == 1 && modes[0] == "CPT:N";
    }
}
