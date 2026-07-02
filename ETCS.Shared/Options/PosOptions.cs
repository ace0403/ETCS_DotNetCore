namespace ETCS.Shared.Options;

public sealed class PosOptions
{
    public const string SectionName = "Pos";

    public string ApiKey { get; set; } = string.Empty;

    public int OrderTypeId { get; set; } = 43;

    public string DefaultCompanyCode { get; set; } = "240";

    /// <summary>Legacy POS cash/card branch code passed to spInsertCashPurcahse (old WinForms used "1").</summary>
    public string DefaultBranchCode { get; set; } = "1";
}
