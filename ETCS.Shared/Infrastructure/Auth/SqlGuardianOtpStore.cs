using System.Data.Common;
using System.Security.Cryptography;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Auth;

public sealed class SqlGuardianOtpStore : IGuardianOtpStore
{
    private const string QualifiedOtpTable = "[dbo].[GuardianOtp]";
    private const string QualifiedTokenTable = "[dbo].[GuardianOtpVerificationToken]";

    private readonly IDbConnectionFactory _connectionFactory;

    public SqlGuardianOtpStore(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task StoreOtpAsync(
        string purpose,
        string email,
        string otpHash,
        TimeSpan lifetime,
        int? guardianId = null,
        CancellationToken cancellationToken = default)
    {
        var normalizedPurpose = NormalizePurpose(purpose);
        var normalizedEmail = NormalizeEmail(email);

        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        await db.ExecuteAsync(
            new CommandDefinition(
                $"""
                UPDATE {QualifiedOtpTable}
                SET UsedAtUtc = SYSUTCDATETIME()
                WHERE Purpose = @Purpose
                  AND Email = @Email
                  AND UsedAtUtc IS NULL;
                """,
                new { Purpose = normalizedPurpose, Email = normalizedEmail },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        await db.ExecuteAsync(
            new CommandDefinition(
                $"""
                INSERT INTO {QualifiedOtpTable}
                    (Purpose, Email, GuardianId, OtpHash, ExpiresAtUtc, AttemptCount, CreatedAtUtc)
                VALUES
                    (@Purpose, @Email, @GuardianId, @OtpHash, @ExpiresAtUtc, 0, SYSUTCDATETIME());
                """,
                new
                {
                    Purpose = normalizedPurpose,
                    Email = normalizedEmail,
                    GuardianId = guardianId,
                    OtpHash = otpHash,
                    ExpiresAtUtc = DateTime.UtcNow.Add(lifetime)
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<GuardianOtpRecord?> GetActiveOtpAsync(
        string purpose,
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedPurpose = NormalizePurpose(purpose);
        var normalizedEmail = NormalizeEmail(email);
        if (string.IsNullOrWhiteSpace(normalizedPurpose) || string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        var row = await db.QuerySingleOrDefaultAsync<OtpRow>(
            new CommandDefinition(
                $"""
                SELECT TOP (1) Id, Purpose, Email, GuardianId, OtpHash, ExpiresAtUtc, AttemptCount, UsedAtUtc
                FROM {QualifiedOtpTable}
                WHERE Purpose = @Purpose
                  AND Email = @Email
                  AND UsedAtUtc IS NULL
                ORDER BY Id DESC;
                """,
                new { Purpose = normalizedPurpose, Email = normalizedEmail },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (row is null)
        {
            return null;
        }

        return new GuardianOtpRecord
        {
            Id = row.Id,
            Purpose = row.Purpose,
            Email = row.Email,
            GuardianId = row.GuardianId,
            OtpHash = row.OtpHash,
            ExpiresAtUtc = row.ExpiresAtUtc,
            AttemptCount = row.AttemptCount,
            UsedAtUtc = row.UsedAtUtc
        };
    }

    public async Task IncrementAttemptAsync(long otpId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        await db.ExecuteAsync(
            new CommandDefinition(
                $"""
                UPDATE {QualifiedOtpTable}
                SET AttemptCount = AttemptCount + 1
                WHERE Id = @Id;
                """,
                new { Id = otpId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task MarkOtpUsedAsync(long otpId, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        await db.ExecuteAsync(
            new CommandDefinition(
                $"""
                UPDATE {QualifiedOtpTable}
                SET UsedAtUtc = SYSUTCDATETIME()
                WHERE Id = @Id
                  AND UsedAtUtc IS NULL;
                """,
                new { Id = otpId },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task InvalidateOtpsAsync(
        string purpose,
        string email,
        CancellationToken cancellationToken = default)
    {
        var normalizedPurpose = NormalizePurpose(purpose);
        var normalizedEmail = NormalizeEmail(email);

        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        await db.ExecuteAsync(
            new CommandDefinition(
                $"""
                UPDATE {QualifiedOtpTable}
                SET UsedAtUtc = SYSUTCDATETIME()
                WHERE Purpose = @Purpose
                  AND Email = @Email
                  AND UsedAtUtc IS NULL;
                """,
                new { Purpose = normalizedPurpose, Email = normalizedEmail },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    public async Task<string> CreateVerificationTokenAsync(
        string purpose,
        string email,
        TimeSpan lifetime,
        CancellationToken cancellationToken = default)
    {
        var normalizedPurpose = NormalizePurpose(purpose);
        var normalizedEmail = NormalizeEmail(email);
        var token = CreateSecureToken();

        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        await db.ExecuteAsync(
            new CommandDefinition(
                $"""
                UPDATE {QualifiedTokenTable}
                SET UsedAtUtc = SYSUTCDATETIME()
                WHERE Purpose = @Purpose
                  AND Email = @Email
                  AND UsedAtUtc IS NULL;
                """,
                new { Purpose = normalizedPurpose, Email = normalizedEmail },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        await db.ExecuteAsync(
            new CommandDefinition(
                $"""
                INSERT INTO {QualifiedTokenTable} (Token, Purpose, Email, ExpiresAtUtc, CreatedAtUtc)
                VALUES (@Token, @Purpose, @Email, @ExpiresAtUtc, SYSUTCDATETIME());
                """,
                new
                {
                    Token = token,
                    Purpose = normalizedPurpose,
                    Email = normalizedEmail,
                    ExpiresAtUtc = DateTime.UtcNow.Add(lifetime)
                },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        return token;
    }

    public async Task<GuardianOtpVerificationTokenRecord?> GetValidVerificationTokenAsync(
        string purpose,
        string token,
        CancellationToken cancellationToken = default)
    {
        var normalizedPurpose = NormalizePurpose(purpose);
        if (string.IsNullOrWhiteSpace(normalizedPurpose) || string.IsNullOrWhiteSpace(token))
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        var row = await db.QuerySingleOrDefaultAsync<TokenRow>(
            new CommandDefinition(
                $"""
                SELECT Token, Purpose, Email, ExpiresAtUtc, UsedAtUtc
                FROM {QualifiedTokenTable}
                WHERE Token = @Token
                  AND Purpose = @Purpose;
                """,
                new { Token = token.Trim(), Purpose = normalizedPurpose },
                cancellationToken: cancellationToken)).ConfigureAwait(false);

        if (row is null || row.UsedAtUtc is not null || row.ExpiresAtUtc <= DateTime.UtcNow)
        {
            return null;
        }

        return new GuardianOtpVerificationTokenRecord
        {
            Token = row.Token,
            Purpose = row.Purpose,
            Email = row.Email,
            ExpiresAtUtc = row.ExpiresAtUtc,
            UsedAtUtc = row.UsedAtUtc
        };
    }

    public async Task MarkVerificationTokenUsedAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return;
        }

        using var connection = _connectionFactory.CreateConnection();
        var db = (DbConnection)connection;
        await db.OpenAsync(cancellationToken).ConfigureAwait(false);

        await db.ExecuteAsync(
            new CommandDefinition(
                $"""
                UPDATE {QualifiedTokenTable}
                SET UsedAtUtc = SYSUTCDATETIME()
                WHERE Token = @Token
                  AND UsedAtUtc IS NULL;
                """,
                new { Token = token.Trim() },
                cancellationToken: cancellationToken)).ConfigureAwait(false);
    }

    private static string NormalizePurpose(string purpose) =>
        (purpose ?? string.Empty).Trim();

    private static string NormalizeEmail(string email) =>
        (email ?? string.Empty).Trim().ToLowerInvariant();

    private static string CreateSecureToken()
    {
        Span<byte> bytes = stackalloc byte[32];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private sealed class OtpRow
    {
        public long Id { get; init; }

        public string Purpose { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;

        public int? GuardianId { get; init; }

        public string OtpHash { get; init; } = string.Empty;

        public DateTime ExpiresAtUtc { get; init; }

        public int AttemptCount { get; init; }

        public DateTime? UsedAtUtc { get; init; }
    }

    private sealed class TokenRow
    {
        public string Token { get; init; } = string.Empty;

        public string Purpose { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;

        public DateTime ExpiresAtUtc { get; init; }

        public DateTime? UsedAtUtc { get; init; }
    }
}
