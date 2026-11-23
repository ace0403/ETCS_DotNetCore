namespace ETCS.Admin.Options;

public sealed class PosReceiptReprintOptions
{
    public const string SectionName = "PosReceiptReprint";

    public decimal VatPercent { get; set; } = 5m;

    public string BridgeBaseUrl { get; set; } = "http://127.0.0.1:5050";
}
