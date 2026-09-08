namespace ETCS.PaymentGateway.Models;

public sealed class OrderPaymentSessionRequest
{
    public int StudentId { get; init; }

    public int GuardianId { get; init; }

    public string OrderId { get; init; } = string.Empty;

    public decimal Total { get; init; }

    public string Notes { get; init; } = string.Empty;

    /// <summary>Optional HTTPS template from the mobile app; falls back to NativeReturnBaseUrl.</summary>
    public string? ReturnUrl { get; init; }
}
