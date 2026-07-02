namespace ETCS.Shared.Options;

public sealed class MediaOptions
{
    public const string SectionName = "Media";

    /// <summary>Physical root for meal images (e.g. D:\ETCS\Store\).</summary>
    public string StorePath { get; set; } = string.Empty;

    /// <summary>Public base URL for absolute image links returned by the API (e.g. https://orders.example.com).</summary>
    public string PublicBaseUrl { get; set; } = string.Empty;
}
