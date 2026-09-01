namespace ETCS.Shared.Infrastructure.Students;

/// <summary>
/// Student row from guardian listing (same student entity as summary and other student APIs).
/// </summary>
public sealed record StudentListingDto(
    decimal UserId,
    string? StudCode,
    string? Name,
    string? UserName,
    string? Std,
    string? SchoolName,
    string? Cardid,
    string Status,
    DateTime? DateOfBirth,
    decimal? Balprepaid,
    string? ClassName,
    string? GroupName,
    string? Year,
    string? Email, 
    int? IsNoService);
