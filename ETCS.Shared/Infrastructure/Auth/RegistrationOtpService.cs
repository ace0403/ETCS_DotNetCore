using System.Security.Cryptography;
using System.Text;
using ETCS.Shared.Application.Email;
using ETCS.Shared.Infrastructure.Auth.Models;
using ETCS.Shared.Infrastructure.Data;
using Dapper;
using System.Data.Common;
using Microsoft.Extensions.Logging;

namespace ETCS.Shared.Infrastructure.Auth;

public interface IRegistrationOtpService
{
    Task<RegistrationOtpSendResult> SendOtpAsync(string email, CancellationToken cancellationToken = default);

    Task<RegistrationOtpVerifyResult> VerifyOtpAsync(string email, string otp, CancellationToken cancellationToken = default);

    Task<RegistrationVerificationConsumeResult> ValidateVerificationTokenAsync(
        string email,
        string verificationToken,
        CancellationToken cancellationToken = default);

    Task MarkVerificationTokenUsedAsync(
        string verificationToken,
        CancellationToken cancellationToken = default);
}

public sealed record RegistrationOtpSendResult(bool IsSuccess, string Message, int ExpiresInSeconds);

public sealed record RegistrationOtpVerifyResult(
    bool IsSuccess,
    string Message,
    string? VerificationToken,
    int ExpiresInSeconds);

public sealed record RegistrationVerificationConsumeResult(bool IsSuccess, string Message);

public sealed class RegistrationOtpService : IRegistrationOtpService
{
    public const int OtpLifetimeSeconds = 300;
    public const int VerificationTokenLifetimeSeconds = 600;
    public const int MaxVerifyAttempts = 5;

    private static readonly TimeSpan OtpLifetime = TimeSpan.FromSeconds(OtpLifetimeSeconds);
    private static readonly TimeSpan VerificationTokenLifetime = TimeSpan.FromSeconds(VerificationTokenLifetimeSeconds);

    private const string ExistingGuardianIdByEmailSql = """
        SELECT TOP (1) g.GrdID
        FROM GuardianInfo g
        WHERE LOWER(LTRIM(RTRIM(ISNULL(g.Email, '')))) = @Email;
        """;

    private readonly IGuardianOtpStore _otpStore;
    private readonly IGuardianEmailNotificationService _emailNotificationService;
    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<RegistrationOtpService> _logger;

    public RegistrationOtpService(
        IGuardianOtpStore otpStore,
        IGuardianEmailNotificationService emailNotificationService,
        IDbConnectionFactory connectionFactory,
        ILogger<RegistrationOtpService> logger)
    {
        _otpStore = otpStore;
        _emailNotificationService = emailNotificationService;
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<RegistrationOtpSendResult> SendOtpAsync(string email, CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || !normalizedEmail.Contains('@'))
        {
            return new RegistrationOtpSendResult(false, "A valid email address is required.", OtpLifetimeSeconds);
        }

        if (await EmailExistsAsync(normalizedEmail, cancellationToken).ConfigureAwait(false))
        {
            return new RegistrationOtpSendResult(false, "An account with this email already exists.", OtpLifetimeSeconds);
        }

        var otp = GenerateOtp();
        var otpHash = HashValue(otp);

        await _otpStore.StoreOtpAsync(
            GuardianOtpPurposes.Registration,
            normalizedEmail,
            otpHash,
            OtpLifetime,
            guardianId: null,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await _emailNotificationService.QueueRegistrationOtpAsync(
                normalizedEmail,
                otp,
                (int)OtpLifetime.TotalMinutes,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue registration OTP for {Email}.", normalizedEmail);
            await _otpStore.InvalidateOtpsAsync(
                GuardianOtpPurposes.Registration,
                normalizedEmail,
                cancellationToken).ConfigureAwait(false);
            return new RegistrationOtpSendResult(
                false,
                "We could not send a verification code right now. Please try again.",
                OtpLifetimeSeconds);
        }

        return new RegistrationOtpSendResult(
            true,
            "If the email is available, a verification code has been sent.",
            OtpLifetimeSeconds);
    }

    public async Task<RegistrationOtpVerifyResult> VerifyOtpAsync(
        string email,
        string otp,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        var normalizedOtp = (otp ?? string.Empty).Trim();

        if (string.IsNullOrWhiteSpace(normalizedEmail) || normalizedOtp.Length != 6)
        {
            return new RegistrationOtpVerifyResult(
                false,
                "Enter the 6-digit verification code sent to your email.",
                null,
                VerificationTokenLifetimeSeconds);
        }

        var record = await _otpStore.GetActiveOtpAsync(
            GuardianOtpPurposes.Registration,
            normalizedEmail,
            cancellationToken).ConfigureAwait(false);
        if (record is null || record.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return new RegistrationOtpVerifyResult(
                false,
                "The verification code has expired. Please request a new code.",
                null,
                VerificationTokenLifetimeSeconds);
        }

        if (record.AttemptCount >= MaxVerifyAttempts)
        {
            await _otpStore.MarkOtpUsedAsync(record.Id, cancellationToken).ConfigureAwait(false);
            return new RegistrationOtpVerifyResult(
                false,
                "Too many incorrect attempts. Please request a new verification code.",
                null,
                VerificationTokenLifetimeSeconds);
        }

        var otpHash = HashValue(normalizedOtp);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(otpHash),
                Encoding.UTF8.GetBytes(record.OtpHash)))
        {
            await _otpStore.IncrementAttemptAsync(record.Id, cancellationToken).ConfigureAwait(false);
            var remaining = MaxVerifyAttempts - record.AttemptCount - 1;
            var message = remaining <= 0
                ? "Too many incorrect attempts. Please request a new verification code."
                : "Incorrect verification code. Please try again.";

            if (remaining <= 0)
            {
                await _otpStore.MarkOtpUsedAsync(record.Id, cancellationToken).ConfigureAwait(false);
            }

            return new RegistrationOtpVerifyResult(false, message, null, VerificationTokenLifetimeSeconds);
        }

        await _otpStore.MarkOtpUsedAsync(record.Id, cancellationToken).ConfigureAwait(false);
        var verificationToken = await _otpStore.CreateVerificationTokenAsync(
            GuardianOtpPurposes.Registration,
            normalizedEmail,
            VerificationTokenLifetime,
            cancellationToken).ConfigureAwait(false);

        return new RegistrationOtpVerifyResult(
            true,
            "Email verified.",
            verificationToken,
            VerificationTokenLifetimeSeconds);
    }

    public async Task<RegistrationVerificationConsumeResult> ValidateVerificationTokenAsync(
        string email,
        string verificationToken,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedEmail) || string.IsNullOrWhiteSpace(verificationToken))
        {
            return new RegistrationVerificationConsumeResult(false, "Email verification is required to complete registration.");
        }

        var tokenRecord = await _otpStore.GetValidVerificationTokenAsync(
                GuardianOtpPurposes.Registration,
                verificationToken,
                cancellationToken)
            .ConfigureAwait(false);

        if (tokenRecord is null)
        {
            return new RegistrationVerificationConsumeResult(
                false,
                "Email verification has expired or is invalid. Please verify your email again.");
        }

        if (!string.Equals(tokenRecord.Email, normalizedEmail, StringComparison.OrdinalIgnoreCase))
        {
            return new RegistrationVerificationConsumeResult(
                false,
                "Email verification does not match the registration email.");
        }

        return new RegistrationVerificationConsumeResult(true, "Verified.");
    }

    public Task MarkVerificationTokenUsedAsync(
        string verificationToken,
        CancellationToken cancellationToken = default) =>
        _otpStore.MarkVerificationTokenUsedAsync(verificationToken, cancellationToken);

    private async Task<bool> EmailExistsAsync(string normalizedEmail, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        var existingId = await db.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                ExistingGuardianIdByEmailSql,
                new { Email = normalizedEmail },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return existingId is > 0;
    }

    private static string NormalizeEmail(string email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    private static string GenerateOtp()
    {
        var value = RandomNumberGenerator.GetInt32(0, 1_000_000);
        return value.ToString("D6");
    }

    private static string HashValue(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}
