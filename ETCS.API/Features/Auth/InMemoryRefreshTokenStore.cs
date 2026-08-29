using System.Collections.Concurrent;
using ETCS.Shared.Auth;

namespace ETCS.API.Features.Auth;

public sealed class InMemoryRefreshTokenStore : IRefreshTokenStore
{
    private readonly ConcurrentDictionary<string, RefreshTokenRecord> _store = new();

    public Task SaveAsync(string refreshToken, RefreshTokenRecord record, CancellationToken cancellationToken)
    {
        _store[refreshToken] = record;
        return Task.CompletedTask;
    }

    public Task<RefreshTokenRecord?> GetAsync(string refreshToken, CancellationToken cancellationToken)
    {
        _store.TryGetValue(refreshToken, out var record);
        return Task.FromResult(record);
    }

    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        if (_store.TryGetValue(refreshToken, out var record))
        {
            record.RevokedAtUtc = DateTime.UtcNow;
        }

        return Task.CompletedTask;
    }

    public Task RevokeAllByUserIdAsync(int userId, CancellationToken cancellationToken)
    {
        if (userId <= 0)
        {
            return Task.CompletedTask;
        }

        var now = DateTime.UtcNow;
        foreach (var pair in _store)
        {
            if (pair.Value.Id == userId && pair.Value.RevokedAtUtc is null)
            {
                pair.Value.RevokedAtUtc = now;
            }
        }

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string refreshToken, CancellationToken cancellationToken)
    {
        _store.TryRemove(refreshToken, out _);
        return Task.CompletedTask;
    }
}
