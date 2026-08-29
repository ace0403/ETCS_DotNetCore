namespace ETCS.Shared.Infrastructure.Auth;

public interface IGuardianOtpStore
{
    Task StoreOtpAsync(
        string purpose,
        string email,
        string otpHash,
        TimeSpan lifetime,
        int? guardianId = null,
        CancellationToken cancellationToken = default);

    Task<GuardianOtpRecord?> GetActiveOtpAsync(
        string purpose,
        string email,
        CancellationToken cancellationToken = default);

    Task IncrementAttemptAsync(
        long otpId,
        CancellationToken cancellationToken = default);

    Task MarkOtpUsedAsync(
        long otpId,
        CancellationToken cancellationToken = default);

    Task InvalidateOtpsAsync(
        string purpose,
        string email,
        CancellationToken cancellationToken = default);

    Task<string> CreateVerificationTokenAsync(
        string purpose,
        string email,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default);

    Task<GuardianOtpVerificationTokenRecord?> GetValidVerificationTokenAsync(
        string purpose,
        string token,
        CancellationToken cancellationToken = default);

    Task MarkVerificationTokenUsedAsync(
        string token,
        CancellationToken cancellationToken = default);
}

public sealed class GuardianOtpRecord
{
    public required long Id { get; init; }

    public required string Purpose { get; init; }

    public required string Email { get; init; }

    public int? GuardianId { get; init; }

    public required string OtpHash { get; init; }

    public required DateTime ExpiresAtUtc { get; init; }

    public required int AttemptCount { get; init; }

    public DateTime? UsedAtUtc { get; init; }
}

public sealed class GuardianOtpVerificationTokenRecord
{
    public required string Token { get; init; }

    public required string Purpose { get; init; }

    public required string Email { get; init; }

    public required DateTime ExpiresAtUtc { get; init; }

    public DateTime? UsedAtUtc { get; init; }
}
