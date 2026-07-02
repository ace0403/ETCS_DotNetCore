namespace ETCS.Shared.Infrastructure.Students;

public sealed record SchoolListItemDto(
    int SchoolId,
    int CountryId,
    string? SchoolCode,
    string? SchoolName,
    string? SchoolLogo,
    double? MinimumTopup,
    string? PdfPath,
    bool? EmailAlerts);
