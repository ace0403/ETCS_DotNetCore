using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;

namespace ETCS.Shared.Infrastructure.Legal;

public sealed class LegalContentRepository : ILegalContentRepository
{
    private static readonly TimeSpan CacheTtl = TimeSpan.FromHours(24);

    private const string GetByKeySql = """
        SELECT TOP (1)
            ContentKey,
            Title,
            BodyHtml,
            LastUpdatedOn
        FROM dbo.LegalContent
        WHERE ContentKey = @ContentKey
          AND IsActive = 1;
        """;

    private const string GetAllActiveSql = """
        SELECT
            ContentKey,
            Title,
            BodyHtml,
            LastUpdatedOn
        FROM dbo.LegalContent
        WHERE IsActive = 1
        ORDER BY
            CASE ContentKey
                WHEN N'Terms' THEN 1
                WHEN N'Privacy' THEN 2
                WHEN N'Cancellation' THEN 3
                ELSE 99
            END,
            Title;
        """;

    private readonly IMealDbConnectionFactory _connectionFactory;
    private readonly IMemoryCache _cache;

    public LegalContentRepository(
        IMealDbConnectionFactory connectionFactory,
        IMemoryCache cache)
    {
        _connectionFactory = connectionFactory;
        _cache = cache;
    }

    public async Task<LegalContentDto?> GetByKeyAsync(
        string contentKey,
        CancellationToken cancellationToken = default)
    {
        var normalizedKey = LegalContentKeys.Normalize(contentKey);
        if (normalizedKey is null)
        {
            return null;
        }

        var cacheKey = LegalContentKeys.CacheKeyFor(normalizedKey);
        if (_cache.TryGetValue(cacheKey, out LegalContentDto? cached))
        {
            return cached;
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var row = await dbConnection.QuerySingleOrDefaultAsync<LegalContentDto>(
            new CommandDefinition(
                GetByKeySql,
                new { ContentKey = normalizedKey },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (row is not null)
        {
            _cache.Set(cacheKey, row, CacheTtl);
        }

        return row;
    }

    public async Task<IReadOnlyList<LegalContentDto>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        if (_cache.TryGetValue(LegalContentKeys.CacheKeyAll, out IReadOnlyList<LegalContentDto>? cached)
            && cached is not null)
        {
            return cached;
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken).ConfigureAwait(false);

        var rows = (await dbConnection.QueryAsync<LegalContentDto>(
            new CommandDefinition(
                GetAllActiveSql,
                cancellationToken: cancellationToken)).ConfigureAwait(false)).AsList();

        IReadOnlyList<LegalContentDto> result = rows;
        _cache.Set(LegalContentKeys.CacheKeyAll, result, CacheTtl);

        foreach (var row in rows)
        {
            _cache.Set(LegalContentKeys.CacheKeyFor(row.ContentKey), row, CacheTtl);
        }

        return result;
    }

    public void ClearCache()
    {
        _cache.Remove(LegalContentKeys.CacheKeyAll);
        foreach (var key in LegalContentKeys.All)
        {
            _cache.Remove(LegalContentKeys.CacheKeyFor(key));
        }
    }
}
