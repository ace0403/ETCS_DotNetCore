namespace ETCS.Web.Models;

public sealed class SupportPageViewModel
{
    public string SupportEmail { get; init; } = string.Empty;

    public string? SupportPhone { get; init; }

    public string? SupportHours { get; init; }

    public string GuardianName { get; init; } = string.Empty;

    public string GuardianEmail { get; init; } = string.Empty;

    public int? GuardianId { get; init; }

    public string MailtoHref { get; init; } = string.Empty;

    public bool HasPhone => !string.IsNullOrWhiteSpace(SupportPhone);

    public bool HasHours => !string.IsNullOrWhiteSpace(SupportHours);
}
