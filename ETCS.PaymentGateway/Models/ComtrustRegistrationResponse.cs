using System.Text.Json.Serialization;

namespace ETCS.PaymentGateway.Models;

public sealed class ComtrustRegistrationResponse
{
    [JsonPropertyName("Transaction")]
    public ComtrustTransaction? Transaction { get; init; }
}

public sealed class ComtrustTransaction
{
    [JsonPropertyName("TransactionID")]
    public string? TransactionId { get; init; }

    [JsonPropertyName("PaymentPage")]
    public string? PaymentPage { get; init; }

    [JsonPropertyName("ResponseCode")]
    public string? ResponseCode { get; init; }

    [JsonPropertyName("ResponseDescription")]
    public string? ResponseDescription { get; init; }

    [JsonPropertyName("ResponseClassDescription")]
    public string? ResponseClassDescription { get; init; }

    [JsonPropertyName("AuthenticationToken")]
    public string? AuthenticationToken { get; init; }

    [JsonPropertyName("SessionID")]
    public string? SessionId { get; init; }

    [JsonPropertyName("IsWalletUsed")]
    public string? IsWalletUsed { get; init; }

    [JsonPropertyName("CardType")]
    public string? CardType { get; init; }
}
