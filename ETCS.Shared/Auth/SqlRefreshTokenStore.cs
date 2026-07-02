using System.Data;
using System.Data.Common;
using System.Text.RegularExpressions;
using System.Threading;
using Dapper;
using ETCS.Shared.Infrastructure.Data;
using ETCS.Shared.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ETCS.Shared.Auth;

public sealed class SqlRefreshTokenStore : IRefreshTokenStore
{
    private static readonly Regex SafeIdentifier = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly string _table;
    private readonly string _qualifiedTableName;
    private readonly ILogger<SqlRefreshTokenStore> _logger;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private int _schemaEnsured;

    public SqlRefreshTokenStore(
        IDbConnectionFactory connectionFactory,
        IOptions<RefreshTokenStoreOptions> options,
        ILogger<SqlRefreshTokenStore> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
        var table = options.Value.TableName;
        if (string.IsNullOrWhiteSpace(table) || !SafeIdentifier.IsMatch(table))
        {
            throw new InvalidOperationException(
                "RefreshTokenStore:TableName must match [A-Za-z_][A-Za-z0-9_]* (e.g. ETCS_ApiRefreshTokens).");
        }

        _table = table;
        _qualifiedTableName = "[dbo].[" + table + "]";
    }

    public async Task SaveAsync(string refreshToken, RefreshTokenRecord record, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        var sql = $"""
            MERGE {_qualifiedTableName} AS t
            USING (SELECT @Token AS Token) AS s
            ON (t.Token = s.Token)
            WHEN MATCHED THEN
              UPDATE SET UserId = @UserId, Username = @Username, ExpiresAtUtc = @ExpiresAtUtc, RevokedAtUtc = NULL
            WHEN NOT MATCHED THEN
              INSERT (Token, UserId, Username, ExpiresAtUtc, RevokedAtUtc)
              VALUES (@Token, @UserId, @Username, @ExpiresAtUtc, NULL);
            """;

        await db.ExecuteAsync(
            new CommandDefinition(
                sql,
                new
                {
                    Token = refreshToken,
                    UserId = record.Id,
                    Username = record.Username,
                    ExpiresAtUtc = record.ExpiresAtUtc
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<RefreshTokenRecord?> GetAsync(string refreshToken, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        var sql = $"""
            SELECT UserId AS Id, Username, ExpiresAtUtc, RevokedAtUtc
            FROM {_qualifiedTableName}
            WHERE Token = @Token;
            """;

        var row = await db.QuerySingleOrDefaultAsync<RefreshTokenRow>(
            new CommandDefinition(sql, new { Token = refreshToken }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        return new RefreshTokenRecord
        {
            Id = row.Id,
            Username = row.Username,
            ExpiresAtUtc = row.ExpiresAtUtc,
            RevokedAtUtc = row.RevokedAtUtc
        };
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        var sql = $"""
            UPDATE {_qualifiedTableName}
            SET RevokedAtUtc = SYSUTCDATETIME()
            WHERE Token = @Token AND RevokedAtUtc IS NULL;
            """;

        await db.ExecuteAsync(new CommandDefinition(sql, new { Token = refreshToken }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    public async Task RemoveAsync(string refreshToken, CancellationToken cancellationToken)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        var sql = $"DELETE FROM {_qualifiedTableName} WHERE Token = @Token;";

        await db.ExecuteAsync(new CommandDefinition(sql, new { Token = refreshToken }, cancellationToken: cancellationToken))
            .ConfigureAwait(false);
    }

    private async Task EnsureSchemaAsync(CancellationToken cancellationToken)
    {
        if (Volatile.Read(ref _schemaEnsured) == 1)
        {
            return;
        }

        await _schemaLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_schemaEnsured == 1)
            {
                return;
            }

            using var connection = _connectionFactory.CreateConnection();
            var db = (DbConnection)connection;
            await db.OpenAsync(cancellationToken).ConfigureAwait(false);

            var ddl = $"""
                IF OBJECT_ID(N'dbo.{_table}', N'U') IS NULL
                BEGIN
                    CREATE TABLE {_qualifiedTableName} (
                        [Token] NVARCHAR(900) NOT NULL PRIMARY KEY,
                        [UserId] INT NOT NULL,
                        [Username] NVARCHAR(256) NOT NULL,
                        [ExpiresAtUtc] DATETIME2 NOT NULL,
                        [RevokedAtUtc] DATETIME2 NULL
                    );
                END
                """;

            await db.ExecuteAsync(new CommandDefinition(ddl, cancellationToken: cancellationToken)).ConfigureAwait(false);
            Volatile.Write(ref _schemaEnsured, 1);
            _logger.LogInformation("Refresh token table {Table} is ready.", _qualifiedTableName);
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    private sealed class RefreshTokenRow
    {
        public int Id { get; init; }

        public string Username { get; init; } = string.Empty;

        public DateTime ExpiresAtUtc { get; init; }

        public DateTime? RevokedAtUtc { get; init; }
    }
}
