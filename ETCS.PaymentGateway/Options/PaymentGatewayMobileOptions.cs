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

    /// <summary>
    /// Explicit compatibility for EPG endpoints that require merchant credentials in
    /// operation bodies as well as HTTP Basic. These credentials remain server-side.
    /// </summary>
    public bool IncludeCredentialsInBody { get; set; }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(UserName) && !string.IsNullOrWhiteSpace(Password);
}
