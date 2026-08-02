using System.Data.Common;
using System.Security.Cryptography;
using System.Threading;
using Dapper;
using ETCS.Shared.Infrastructure.Data;
using Microsoft.Extensions.Logging;

namespace ETCS.Shared.Infrastructure.Auth;

public sealed class SqlGuardianPasswordResetTokenStore : IGuardianPasswordResetTokenStore
{
    private const string TableName = "GuardianPasswordResetToken";
    private const string QualifiedTableName = "[dbo].[GuardianPasswordResetToken]";

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly ILogger<SqlGuardianPasswordResetTokenStore> _logger;
    private readonly SemaphoreSlim _schemaLock = new(1, 1);
    private int _schemaEnsured;

    public SqlGuardianPasswordResetTokenStore(
        IDbConnectionFactory connectionFactory,
        ILogger<SqlGuardianPasswordResetTokenStore> logger)
    {
        _connectionFactory = connectionFactory;
        _logger = logger;
    }

    public async Task<string> CreateAsync(int guardianId, TimeSpan lifetime, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        var token = CreateSecureToken();
        var expiresAtUtc = DateTime.UtcNow.Add(lifetime);

        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        await db.ExecuteAsync(
            new CommandDefinition(
                $"""
                UPDATE {QualifiedTableName}
                SET UsedAtUtc = SYSUTCDATETIME()
                WHERE GuardianId = @GuardianId
                  AND UsedAtUtc IS NULL;
                """,
                new { GuardianId = guardianId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        await db.ExecuteAsync(
            new CommandDefinition(
                $"""
                INSERT INTO {QualifiedTableName} (Token, GuardianId, ExpiresAtUtc, CreatedAtUtc)
                VALUES (@Token, @GuardianId, @ExpiresAtUtc, SYSUTCDATETIME());
                """,
                new
                {
                    Token = token,
                    GuardianId = guardianId,
                    ExpiresAtUtc = expiresAtUtc
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return token;
    }

    public async Task<GuardianPasswordResetTokenRecord?> GetValidAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        var row = await db.QuerySingleOrDefaultAsync<TokenRow>(
            new CommandDefinition(
                $"""
                SELECT Token, GuardianId, ExpiresAtUtc, UsedAtUtc
                FROM {QualifiedTableName}
                WHERE Token = @Token;
                """,
                new { Token = token.Trim() },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        if (row.UsedAtUtc is not null || row.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return null;
        }

        return new GuardianPasswordResetTokenRecord
        {
            Token = row.Token,
            GuardianId = row.GuardianId,
            ExpiresAtUtc = row.ExpiresAtUtc,
            UsedAtUtc = row.UsedAtUtc
        };
    }

    public async Task MarkUsedAsync(string token, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        await db.ExecuteAsync(
            new CommandDefinition(
                $"""
                UPDATE {QualifiedTableName}
                SET UsedAtUtc = SYSUTCDATETIME()
                WHERE Token = @Token
                  AND UsedAtUtc IS NULL;
                """,
                new { Token = token.Trim() },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task RevokeUnusedForGuardianAsync(int guardianId, CancellationToken cancellationToken = default)
    {
        await EnsureSchemaAsync(cancellationToken).ConfigureAwait(false);

        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        await db.ExecuteAsync(
            new CommandDefinition(
                $"""
                UPDATE {QualifiedTableName}
                SET UsedAtUtc = SYSUTCDATETIME()
                WHERE GuardianId = @GuardianId
                  AND UsedAtUtc IS NULL;
                """,
                new { GuardianId = guardianId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
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
                IF OBJECT_ID(N'dbo.{TableName}', N'U') IS NULL
                BEGIN
                    CREATE TABLE {QualifiedTableName} (
                        [Token] NVARCHAR(128) NOT NULL PRIMARY KEY,
                        [GuardianId] INT NOT NULL,
                        [ExpiresAtUtc] DATETIME2 NOT NULL,
                        [UsedAtUtc] DATETIME2 NULL,
                        [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT DF_{TableName}_CreatedAtUtc DEFAULT (SYSUTCDATETIME())
                    );

                    CREATE INDEX IX_{TableName}_GuardianId
                        ON {QualifiedTableName} ([GuardianId])
                        INCLUDE ([UsedAtUtc], [ExpiresAtUtc]);
                END
                """;

            await db.ExecuteAsync(new CommandDefinition(ddl, cancellationToken: cancellationToken)).ConfigureAwait(false);
            Volatile.Write(ref _schemaEnsured, 1);
            _logger.LogInformation("Password reset token table {Table} is ready.", QualifiedTableName);
        }
        finally
        {
            _schemaLock.Release();
        }
    }

    private static string CreateSecureToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed class TokenRow
    {
        public string Token { get; init; } = string.Empty;

        public int GuardianId { get; init; }

        public DateTime ExpiresAtUtc { get; init; }

        public DateTime? UsedAtUtc { get; init; }
    }
}
