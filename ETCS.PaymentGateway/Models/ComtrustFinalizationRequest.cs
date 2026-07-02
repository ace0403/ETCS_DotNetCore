using System.Text.Json.Serialization;

namespace ETCS.PaymentGateway.Models;

public sealed class ComtrustFinalizationRequest
{
    [JsonPropertyName("Finalization")]
    public required ComtrustFinalizationPayload Finalization { get; init; }
}

public sealed class ComtrustFinalizationPayload
{
    [JsonPropertyName("TransactionID")]
    public required string TransactionId { get; init; }

    public required string Customer { get; init; }

    public required string UserName { get; init; }

    public required string Password { get; init; }
}
