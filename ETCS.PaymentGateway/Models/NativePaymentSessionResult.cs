namespace ETCS.PaymentGateway.Models;

public sealed class NativePaymentSessionResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public string TransactionId { get; init; } = string.Empty;

    public string AuthenticationToken { get; init; } = string.Empty;

    public string MerchantUserName { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public string CallbackUrl { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Currency { get; init; } = "AED";

    public string PaymentMethod { get; init; } = "Card";
}

public sealed class NativeWalletSessionResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public string TransactionId { get; init; } = string.Empty;

    public string AuthenticationToken { get; init; } = string.Empty;

    public string SessionId { get; init; } = string.Empty;

    public string MerchantUserName { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public string CallbackUrl { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string Currency { get; init; } = "AED";

    public string PaymentMethod { get; init; } = string.Empty;

    public string MerchantIdentifier { get; init; } = string.Empty;

    public string SamsungPayMerchantId { get; init; } = string.Empty;

    public string SamsungPayServiceId { get; init; } = string.Empty;
}

public sealed class GenerateTokenResult
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public string AuthenticationToken { get; init; } = string.Empty;
}
