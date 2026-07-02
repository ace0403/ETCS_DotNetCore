namespace ETCS.PaymentGateway.Options;

public sealed class PaymentGatewayOptions
{
    public const string SectionName = "PaymentGateway";
    public string BaseUrl { get; set; } = "https://ipg.comtrust.ae";
    public string CustomerName { get; set; } = string.Empty;
    public string OutletId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string ReturnBaseUrl { get; set; } = string.Empty;
    public string CallbackPath { get; set; } = string.Empty;
    public string Currency { get; set; } = "AED";
    public string Channel { get; set; } = "Web";
    public string TransactionHint { get; set; } = "CPT:Y;VCC:Y;";
    public string OrderName { get; set; } = "Smart Food Card";
    public int TimeoutSeconds { get; set; } = 600;

    /// <summary>Max seconds to wait for Comtrust Finalization per attempt.</summary>
    public int CaptureTimeoutSeconds { get; set; } = 90;

    /// <summary>Max seconds for DB finalize after gateway confirms payment.</summary>
    public int CompletionDbTimeoutSeconds { get; set; } = 180;

    /// <summary>Max seconds for payment session (Registration) creation.</summary>
    public int SessionTimeoutSeconds { get; set; } = 60;
}
