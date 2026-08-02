namespace ETCS.Web.Options;

public sealed class WebOptions
{
    public const string SectionName = "Web";

    public string DisplayCurrency { get; set; } = "AED";

    public string StorePath { get; set; } = string.Empty;

    /// <summary>Public site base URL used for password-reset email links (no trailing slash).</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;

    /// <summary>Support inbox for parent Contact Support mailto.</summary>
    public string SupportEmail { get; set; } = "info@etasteuae.com";

    /// <summary>Optional support phone (shown only when set).</summary>
    public string SupportPhone { get; set; } = string.Empty;

    /// <summary>Optional support hours text (shown only when set).</summary>
    public string SupportHours { get; set; } = string.Empty;
}
