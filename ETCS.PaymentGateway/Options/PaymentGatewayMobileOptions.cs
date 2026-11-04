namespace ETCS.PaymentGateway.Options;

public sealed class PaymentGatewayMobileOptions
{
    /// <summary>Comtrust IPG base URL for the native mobile SDK. Falls back to PaymentGateway:BaseUrl when empty.</summary>
    public string BaseUrl { get; set; } = string.Empty;

    public string CustomerName { get; set; } = string.Empty;

    public string UserName { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public string Store { get; set; } = string.Empty;

    public string Terminal { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);
}
