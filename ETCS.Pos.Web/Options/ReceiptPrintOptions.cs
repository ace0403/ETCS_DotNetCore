namespace ETCS.Pos.Web.Options;

public sealed class ReceiptPrintOptions
{
    public const string SectionName = "ReceiptPrint";

    /// <summary>
    /// Print = send to bridge printer. Preview = show receipt image in browser. Disabled = skip receipt output.
    /// </summary>
    public string Mode { get; set; } = "Print";
}
