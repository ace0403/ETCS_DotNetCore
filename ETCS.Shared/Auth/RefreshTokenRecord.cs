namespace ETCS.Shared.Auth;

public sealed class RefreshTokenRecord
{
    public required int Id { get; init; }

    public required string Username { get; init; }

    public required DateTime ExpiresAtUtc { get; init; }

    public DateTime? RevokedAtUtc { get; set; }

    public bool IsActive => RevokedAtUtc is null && ExpiresAtUtc > DateTime.UtcNow;
}
