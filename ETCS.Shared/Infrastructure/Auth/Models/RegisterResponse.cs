namespace ETCS.Shared.Infrastructure.Auth.Models;

public sealed record RegisterResponse(int GuardianId, UserResponse User);
