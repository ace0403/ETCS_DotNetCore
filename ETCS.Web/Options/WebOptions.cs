namespace ETCS.Web.Options;

public sealed class WebOptions
{
    public const string SectionName = "Web";

    public string DisplayCurrency { get; set; } = "AED";

    public string StorePath { get; set; } = string.Empty;
}
