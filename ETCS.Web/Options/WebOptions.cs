namespace ETCS.Web.Options;

public sealed class WebOptions
{
    public const string SectionName = "Web";

    public string DisplayCurrency { get; set; } = "AED";

    public string StorePath { get; set; } = string.Empty;

    public string PublicBaseUrl { get; set; } = string.Empty;

    public string SupportEmail { get; set; } = "info@etasteuae.com";

    public string SupportInquiriesEmail { get; set; } = "info@etasteuae.com";

    public string SupportPhone { get; set; } = string.Empty;

    public string SupportHours { get; set; } = string.Empty;

    public int MealOrderCutoffHour { get; set; } = 15;

    public string GooglePlayUrl { get; set; } = string.Empty;

    public string AppStoreUrl { get; set; } = string.Empty;
}
