namespace ETCS.Shared.Infrastructure.Students;

public sealed record StudentGuardianBasicDetailDto(
    int GuardianId,
    string Email,
    string GuardianName,
    string CustomerId);
