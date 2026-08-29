namespace ETCS.Shared.Infrastructure.Students;

public sealed record StudentIdentityByCustomerDto(
    int UserId,
    int GuardianId,
    string Email,
    string CustomerId,
    string StudentName);
