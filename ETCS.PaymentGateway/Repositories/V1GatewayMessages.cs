namespace ETCS.PaymentGateway.Repositories;

internal static class V1GatewayMessages
{
    public const string Unconfirmed =
        "We couldn't confirm this payment. Check your balance or order history before paying again. If you were charged and the payment is missing, contact support.";

    public static bool IsUnconfirmed(string? message) =>
        !string.IsNullOrWhiteSpace(message) &&
        (message.Contains("generic error", StringComparison.OrdinalIgnoreCase)
            || message.Replace(" ", string.Empty).Contains("QueryTransaction", StringComparison.OrdinalIgnoreCase));
}
