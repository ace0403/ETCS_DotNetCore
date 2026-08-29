namespace ETCS.Shared.Infrastructure.Auth.Models;

public sealed record SendOtpResponse(string Message, int ExpiresInSeconds);
