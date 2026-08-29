namespace ETCS.Web.Models;

public sealed class ErrorViewModel
{
    public int StatusCode { get; set; }

    public string Title { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string IconClass { get; set; } = "ti-alert-circle";

    public string HeroTagline { get; set; } = string.Empty;

    public string PrimaryActionText { get; set; } = string.Empty;

    public string? PrimaryActionUrl { get; set; }

    public bool UsePrimaryAsTryAgain { get; set; }

    public string? SecondaryLinkText { get; set; }

    public string? SecondaryLinkUrl { get; set; }

    public string? TertiaryLinkText { get; set; }

    public string? TertiaryLinkUrl { get; set; }

    public string? RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
