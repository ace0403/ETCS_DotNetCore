namespace ETCS.Pos.Web.Options;

public sealed class ReceiptBrandingOptions
{
    public const string SectionName = "ReceiptBranding";

    public string CompanyLine { get; set; } = "Emirates Taste Catering Services Food LLC";

    public string LogoPath { get; set; } = "wwwroot/images/receipt-logo.png";
}
