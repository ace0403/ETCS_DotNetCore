using System.Text.Json.Serialization;

namespace ETCS.PaymentGateway.Models;

public sealed class ComtrustRegistrationRequest
{
    [JsonPropertyName("Registration")]
    public required ComtrustRegistrationPayload Registration { get; init; }
}

public sealed class ComtrustRegistrationPayload
{
    public required string Customer { get; init; }

    public required string Channel { get; init; }

    public decimal Amount { get; init; }

    public required string Currency { get; init; }

    public required string OrderID { get; init; }

    public required string OrderName { get; init; }

    public required string OrderInfo { get; init; }

    public required string TransactionHint { get; init; }

    public required string UserName { get; init; }

    public required string Password { get; init; }

    public required string ReturnPath { get; init; }
}
