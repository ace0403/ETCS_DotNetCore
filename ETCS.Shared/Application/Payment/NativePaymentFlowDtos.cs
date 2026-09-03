namespace ETCS.Shared.Application.Payment;

public sealed class NativeTopupInitiateRequest
{
    public int GuardianId { get; init; }

    public string StudentId { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string PaymentMethod { get; init; } = "Card";

    public string? ReturnUrl { get; init; }
}

public sealed class NativeTopupInitiateResponse
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

    public string SessionId { get; init; } = string.Empty;

    public string MerchantIdentifier { get; init; } = string.Empty;

    public string SamsungPayMerchantId { get; init; } = string.Empty;

    public string SamsungPayServiceId { get; init; } = string.Empty;

    public decimal? MinimumTopupAmount { get; init; }
}

public sealed class NativeOrderInitiateRequest
{
    public int GuardianId { get; init; }

    public int StudentId { get; init; }

    public int OrderTypeId { get; init; }

    public decimal Total { get; init; }

    public string Notes { get; init; } = string.Empty;

    public string PaymentMethod { get; init; } = "Card";

    public IReadOnlyList<ETCS.Shared.Infrastructure.Orders.OrderMealLineItemRequest> MealList { get; init; } =
        Array.Empty<ETCS.Shared.Infrastructure.Orders.OrderMealLineItemRequest>();
}

public sealed class NativeOrderInitiateResponse
{
    public bool IsSuccess { get; init; }

    public string Message { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public int StudentId { get; init; }

    public int GuardianId { get; init; }

    public decimal Total { get; init; }

    public int MealTransactionId { get; init; }

    public string TransactionId { get; init; } = string.Empty;

    public string AuthenticationToken { get; init; } = string.Empty;

    public string MerchantUserName { get; init; } = string.Empty;

    public string CustomerName { get; init; } = string.Empty;

    public string BaseUrl { get; init; } = string.Empty;

    public string CallbackUrl { get; init; } = string.Empty;

    public string Currency { get; init; } = "AED";

    public string PaymentMethod { get; init; } = "Card";

    public string SessionId { get; init; } = string.Empty;

    public string MerchantIdentifier { get; init; } = string.Empty;

    public string SamsungPayMerchantId { get; init; } = string.Empty;

    public string SamsungPayServiceId { get; init; } = string.Empty;
}

public sealed class NativeWalletRegisterRequest
{
    public int GuardianId { get; init; }

    public int StudentId { get; init; }

    public string OrderId { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string PaymentMethod { get; init; } = string.Empty;

    public string OrderInfo { get; init; } = string.Empty;
}

public sealed class NativeWalletRegisterResponse
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
