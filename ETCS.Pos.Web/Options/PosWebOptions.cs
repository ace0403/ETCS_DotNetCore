namespace ETCS.Pos.Web.Options;

public sealed class PosWebOptions
{
    public const string SectionName = "PosWeb";

    public string ApiBaseUrl { get; set; } = "https://localhost:7204";

    public string BridgeBaseUrl { get; set; } = "http://127.0.0.1:5050";

    public string BridgeSetupPath { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    public string StorePath { get; set; } = string.Empty;

    public decimal VatPercent { get; set; }

    public decimal DefaultDiscount { get; set; }
}
