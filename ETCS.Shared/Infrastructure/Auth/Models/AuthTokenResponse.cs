namespace ETCS.Shared.Infrastructure.Auth.Models;

public sealed record AuthTokenResponse(
    string AccessToken,
    string TokenType,
    DateTime ExpiresAtUtc,
    string RefreshToken,
    DateTime RefreshExpiresAtUtc,
    UserResponse? User);
