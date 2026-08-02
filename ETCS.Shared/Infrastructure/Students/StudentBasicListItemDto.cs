namespace ETCS.Shared.Infrastructure.Students;

public sealed record StudentBasicListItemDto(decimal UserId, string StudentId, int GuardianId, string Name);
