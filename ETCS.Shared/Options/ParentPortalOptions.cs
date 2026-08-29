namespace ETCS.Shared.Options;

/// <summary>
/// Public parent portal (ETCS.Web) base URL used for links in emails sent from the API.
/// </summary>
public sealed class ParentPortalOptions
{
    public const string SectionName = "ParentPortal";

    public string PublicBaseUrl { get; set; } = string.Empty;
}
