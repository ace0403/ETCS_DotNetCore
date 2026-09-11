using System.Text.Json.Serialization;

namespace ETCS.PaymentGateway.Models;

public sealed class ComtrustGenerateTokenRequest
{
    [JsonPropertyName("GenerateToken")]
    public required ComtrustGenerateTokenPayload GenerateToken { get; init; }
}

public sealed class ComtrustGenerateTokenPayload
{
    public required string UserName { get; init; }

    public required string Password { get; init; }
}

public sealed class ComtrustWalletRegistrationRequest
{
    [JsonPropertyName("WalletRegistration")]
    public required ComtrustWalletRegistrationPayload WalletRegistration { get; init; }
}

public sealed class ComtrustWalletRegistrationPayload
{
    public required string OrderID { get; init; }

    public required string OrderName { get; init; }

    public string OrderInfo { get; init; } = string.Empty;

    public required string Channel { get; init; }

    public required string Amount { get; init; }

    public required string Currency { get; init; }

    public required string TransactionHint { get; init; }

    public required string Customer { get; init; }

    public required string UserName { get; init; }

    public required string Password { get; init; }

    public required string WalletName { get; init; }
}
