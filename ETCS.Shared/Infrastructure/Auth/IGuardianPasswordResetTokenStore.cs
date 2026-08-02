namespace ETCS.Shared.Infrastructure.Auth;

public interface IGuardianPasswordResetTokenStore
{
    Task<string> CreateAsync(int guardianId, TimeSpan lifetime, CancellationToken cancellationToken = default);

    Task<GuardianPasswordResetTokenRecord?> GetValidAsync(string token, CancellationToken cancellationToken = default);

    Task MarkUsedAsync(string token, CancellationToken cancellationToken = default);

    Task RevokeUnusedForGuardianAsync(int guardianId, CancellationToken cancellationToken = default);
}

public sealed class GuardianPasswordResetTokenRecord
{
    public required string Token { get; init; }

    public required int GuardianId { get; init; }

    public required DateTime ExpiresAtUtc { get; init; }

    public DateTime? UsedAtUtc { get; init; }
}
