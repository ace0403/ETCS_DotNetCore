namespace ETCS.Shared.Auth;

public interface IRefreshTokenStore
{
    Task SaveAsync(string refreshToken, RefreshTokenRecord record, CancellationToken cancellationToken);

    Task<RefreshTokenRecord?> GetAsync(string refreshToken, CancellationToken cancellationToken);

    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);

    Task RemoveAsync(string refreshToken, CancellationToken cancellationToken);
}
