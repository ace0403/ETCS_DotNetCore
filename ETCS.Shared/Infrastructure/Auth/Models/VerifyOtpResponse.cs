namespace ETCS.Shared.Infrastructure.Auth.Models;

public sealed record VerifyOtpResponse(string VerificationToken, int ExpiresInSeconds);
