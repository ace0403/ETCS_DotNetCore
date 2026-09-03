namespace ETCS.PaymentGateway.Models;

public sealed class WalletRegistrationRequest
{
    public string OrderId { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string OrderInfo { get; init; } = string.Empty;

    public string WalletName { get; init; } = string.Empty;

    public string PaymentMethod { get; init; } = string.Empty;
}
