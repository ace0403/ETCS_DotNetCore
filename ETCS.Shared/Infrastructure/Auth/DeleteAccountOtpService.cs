using System.Security.Cryptography;
using System.Text;
using ETCS.Shared.Application.Email;
using Microsoft.Extensions.Logging;

namespace ETCS.Shared.Infrastructure.Auth;

public interface IDeleteAccountOtpService
{
    Task<DeleteAccountOtpSendResult> SendOtpAsync(int guardianId, CancellationToken cancellationToken = default);

    Task<DeleteAccountOtpVerifyResult> VerifyOtpAsync(
        int guardianId,
        string otp,
        CancellationToken cancellationToken = default);
}

public sealed record DeleteAccountOtpSendResult(
    bool IsSuccess,
    string Message,
    int ExpiresInSeconds,
    string? MaskedEmail = null);

public sealed record DeleteAccountOtpVerifyResult(bool IsSuccess, string Message);

public sealed class DeleteAccountOtpService : IDeleteAccountOtpService
{
    public const int OtpLifetimeSeconds = 300;
    public const int MaxVerifyAttempts = 5;

    private static readonly TimeSpan OtpLifetime = TimeSpan.FromSeconds(OtpLifetimeSeconds);

    private readonly IGuardianOtpStore _otpStore;
    private readonly IParentLoginRepository _parentLoginRepository;
    private readonly IGuardianEmailNotificationService _emailNotificationService;
    private readonly ILogger<DeleteAccountOtpService> _logger;

    public DeleteAccountOtpService(
        IGuardianOtpStore otpStore,
        IParentLoginRepository parentLoginRepository,
        IGuardianEmailNotificationService emailNotificationService,
        ILogger<DeleteAccountOtpService> logger)
    {
        _otpStore = otpStore;
        _parentLoginRepository = parentLoginRepository;
        _emailNotificationService = emailNotificationService;
        _logger = logger;
    }

    public async Task<DeleteAccountOtpSendResult> SendOtpAsync(
        int guardianId,
        CancellationToken cancellationToken = default)
    {
        if (guardianId <= 0)
        {
            return new DeleteAccountOtpSendResult(false, "Account not found.", OtpLifetimeSeconds);
        }

        var account = await _parentLoginRepository.GetActiveAccountEmailAsync(guardianId, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            return new DeleteAccountOtpSendResult(false, "Account not found.", OtpLifetimeSeconds);
        }

        if (account.IsDelete)
        {
            return new DeleteAccountOtpSendResult(false, "Account is already deleted.", OtpLifetimeSeconds);
        }

        if (string.IsNullOrWhiteSpace(account.Email) || !account.Email.Contains('@'))
        {
            return new DeleteAccountOtpSendResult(
                false,
                "Your account does not have a valid email address for verification.",
                OtpLifetimeSeconds);
        }

        var normalizedEmail = NormalizeEmail(account.Email);
        var otp = GenerateOtp();
        var otpHash = HashValue(otp);

        await _otpStore.StoreOtpAsync(
            GuardianOtpPurposes.DeleteAccount,
            normalizedEmail,
            otpHash,
            OtpLifetime,
            guardianId,
            cancellationToken).ConfigureAwait(false);

        try
        {
            await _emailNotificationService.QueueDeleteAccountOtpAsync(
                normalizedEmail,
                account.DisplayName,
                otp,
                (int)OtpLifetime.TotalMinutes,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to queue delete-account OTP for guardian {GuardianId}.", guardianId);
            await _otpStore.InvalidateOtpsAsync(
                GuardianOtpPurposes.DeleteAccount,
                normalizedEmail,
                cancellationToken).ConfigureAwait(false);
            return new DeleteAccountOtpSendResult(
                false,
                "We could not send a verification code right now. Please try again.",
                OtpLifetimeSeconds);
        }

        return new DeleteAccountOtpSendResult(
            true,
            "A verification code has been sent to your email.",
            OtpLifetimeSeconds,
            MaskEmail(normalizedEmail));
    }

    public async Task<DeleteAccountOtpVerifyResult> VerifyOtpAsync(
        int guardianId,
        string otp,
        CancellationToken cancellationToken = default)
    {
        if (guardianId <= 0)
        {
            return new DeleteAccountOtpVerifyResult(false, "Account not found.");
        }

        var normalizedOtp = (otp ?? string.Empty).Trim();
        if (normalizedOtp.Length != 6)
        {
            return new DeleteAccountOtpVerifyResult(
                false,
                "Enter the 6-digit verification code sent to your email.");
        }

        var account = await _parentLoginRepository.GetActiveAccountEmailAsync(guardianId, cancellationToken)
            .ConfigureAwait(false);

        if (account is null)
        {
            return new DeleteAccountOtpVerifyResult(false, "Account not found.");
        }

        if (account.IsDelete)
        {
            return new DeleteAccountOtpVerifyResult(false, "Account is already deleted.");
        }

        if (string.IsNullOrWhiteSpace(account.Email))
        {
            return new DeleteAccountOtpVerifyResult(false, "Account email is missing.");
        }

        var normalizedEmail = NormalizeEmail(account.Email);
        var record = await _otpStore.GetActiveOtpAsync(
            GuardianOtpPurposes.DeleteAccount,
            normalizedEmail,
            cancellationToken).ConfigureAwait(false);

        if (record is null || record.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return new DeleteAccountOtpVerifyResult(
                false,
                "The verification code has expired. Please request a new code.");
        }

        if (record.GuardianId is int storedGuardianId && storedGuardianId != guardianId)
        {
            return new DeleteAccountOtpVerifyResult(false, "Verification code is invalid for this account.");
        }

        if (record.AttemptCount >= MaxVerifyAttempts)
        {
            await _otpStore.MarkOtpUsedAsync(record.Id, cancellationToken).ConfigureAwait(false);
            return new DeleteAccountOtpVerifyResult(
                false,
                "Too many incorrect attempts. Please request a new verification code.");
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

            return new DeleteAccountOtpVerifyResult(false, message);
        }

        await _otpStore.MarkOtpUsedAsync(record.Id, cancellationToken).ConfigureAwait(false);
        return new DeleteAccountOtpVerifyResult(true, "Verified.");
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

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1)
        {
            return email;
        }

        var local = email[..at];
        var domain = email[(at + 1)..];
        var visible = local.Length <= 2 ? local[..1] : local[..2];
        return $"{visible}***@{domain}";
    }
}
