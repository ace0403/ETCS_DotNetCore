using System.Data;
using System.Data.Common;
using Dapper;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Auth.Models;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Auth;

public sealed class ParentLoginRepository : IParentLoginRepository
{
    private const string LoginProcedureName = "spParentLogin";
    private const string ExistingGuardianByEmailSql = """
        SELECT TOP (1)
            g.GrdID AS GuardianId,
            LTRIM(RTRIM(ISNULL(g.FirstName, ''))) AS FirstName,
            LTRIM(RTRIM(ISNULL(g.LastName, ''))) AS LastName,
            LTRIM(RTRIM(ISNULL(g.Email, ''))) AS Email
        FROM GuardianInfo g
        WHERE LOWER(LTRIM(RTRIM(ISNULL(g.Email, '')))) = @Email;
        """;
    private const string ExistingGuardianIdByEmailSql = """
        SELECT TOP (1) g.GrdID
        FROM GuardianInfo g
        WHERE LOWER(LTRIM(RTRIM(ISNULL(g.Email, '')))) = @Email;
        """;
    private const string ExistingGuardianIdByUsernameSql = """
        SELECT TOP (1) g.GrdID
        FROM GuardianInfo g
        WHERE LOWER(LTRIM(RTRIM(ISNULL(g.UserName, '')))) = @Username;
        """;
    private const string InsertGuardianRegistrationSql = """
        INSERT INTO GuardianInfo (FirstName, LastName, Email, MobileNo, UserName, Password, Blacklist, Status, GUID, RoleId)
        VALUES (@FirstName, @LastName, @Email, @MobileNumber, @Username, @PasswordHash, 0, 1, NEWID(), 5);

        SELECT CAST(SCOPE_IDENTITY() AS INT);
        """;
    private const string GetPasswordSql = """
        SELECT TOP (1) Password
        FROM GuardianInfo
        WHERE GrdID = @GuardianId;
        """;
    private const string UpdatePasswordSql = """
        UPDATE GuardianInfo
        SET Password = @PasswordHash
        WHERE GrdID = @GuardianId;
        """;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IGuardianPasswordResetTokenStore _passwordResetTokenStore;

    public ParentLoginRepository(
        IDbConnectionFactory connectionFactory,
        IGuardianPasswordResetTokenStore passwordResetTokenStore)
    {
        _connectionFactory = connectionFactory;
        _passwordResetTokenStore = passwordResetTokenStore;
    }

    public async Task<ParentLoginResult> GetLoginAsync(string loginName, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync(
            new CommandDefinition(
                LoginProcedureName,
                new { LoginName = loginName },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        var first = rows.FirstOrDefault();
        if (first is null)
        {
            return new ParentLoginResult(false, 0, null, null);
        }

        var row = (IDictionary<string, object>)first;

        int.TryParse(row["GrdID"].ToString(), out var id);
        var storedPassword = row["Password"]?.ToString();

        var firstName = row.TryGetValue("FirstName", out var firstNameValue)
            ? firstNameValue?.ToString()?.Trim()
            : null;
        var lastName = row.TryGetValue("LastName", out var lastNameValue)
            ? lastNameValue?.ToString()?.Trim()
            : null;
        var email = row.TryGetValue("Email", out var emailValue)
            ? emailValue?.ToString()?.Trim()
            : null;

        var displayName = string.Join(
            " ",
            new[] { firstName, lastName }.Where(part => !string.IsNullOrWhiteSpace(part)));

        var user = new UserResponse(
            id,
            string.IsNullOrWhiteSpace(displayName) ? loginName : displayName,
            email);

        return new ParentLoginResult(true, id, storedPassword, user);
    }

    public async Task<ParentRegistrationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        var normalizedEmail = request.Email.Trim().ToLowerInvariant();
        var normalizedUsername = request.Username.Trim();
        var passwordHash = SecurityHelper.GetMd5Hash(request.Password);

        if (string.IsNullOrWhiteSpace(normalizedUsername))
        {
            return new ParentRegistrationResult(false, 0, "Username is required.", null);
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var existingEmailId = await dbConnection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                ExistingGuardianIdByEmailSql,
                new { Email = normalizedEmail },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

        if (existingEmailId is > 0)
        {
            return new ParentRegistrationResult(false, 0, "An account with this email already exists.", null);
        }

        var existingUsernameId = await dbConnection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                ExistingGuardianIdByUsernameSql,
                new { Username = normalizedUsername.ToLowerInvariant() },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

        if (existingUsernameId is > 0)
        {
            return new ParentRegistrationResult(false, 0, "This username is already taken.", null);
        }

        var newGuardianId = await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                InsertGuardianRegistrationSql,
                new
                {
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = normalizedEmail,
                    Username = normalizedUsername,
                    MobileNumber = request.MobileNumber.Trim(),
                    PasswordHash = passwordHash
                },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

        var newUser = new UserResponse(
            newGuardianId,
            $"{request.FirstName.Trim()} {request.LastName.Trim()}".Trim(),
            normalizedEmail);

        return new ParentRegistrationResult(true, newGuardianId, "Registration completed.", newUser);
    }

    public async Task<ParentChangePasswordResult> ChangePasswordAsync(
        int guardianId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var stored = await dbConnection.QuerySingleOrDefaultAsync<string>(
            new CommandDefinition(
                GetPasswordSql,
                new { GuardianId = guardianId },
                cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(stored))
        {
            return new ParentChangePasswordResult(false, "Account not found.");
        }

        var currentHash = SecurityHelper.GetMd5Hash(currentPassword);
        if (!PasswordMatches(stored, currentHash, currentPassword))
        {
            return new ParentChangePasswordResult(false, "Current password is incorrect.");
        }

        var newHash = SecurityHelper.GetMd5Hash(newPassword);
        if (PasswordMatches(stored, newHash, newPassword))
        {
            return new ParentChangePasswordResult(false, "New password must be different from the current password.");
        }

        var rows = await dbConnection.ExecuteAsync(
            new CommandDefinition(
                UpdatePasswordSql,
                new { GuardianId = guardianId, PasswordHash = newHash },
                cancellationToken: cancellationToken));

        if (rows <= 0)
        {
            return new ParentChangePasswordResult(false, "Password was not updated.");
        }

        await _passwordResetTokenStore.RevokeUnusedForGuardianAsync(guardianId, cancellationToken);
        return new ParentChangePasswordResult(true, "Password updated successfully.");
    }

    public async Task<ParentPasswordResetRequestResult> RequestPasswordResetAsync(
        string email,
        TimeSpan tokenLifetime,
        CancellationToken cancellationToken = default)
    {
        var normalizedEmail = email.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            return new ParentPasswordResetRequestResult(false, 0, string.Empty, string.Empty, null);
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var guardian = await dbConnection.QueryFirstOrDefaultAsync<GuardianLookupRow>(
            new CommandDefinition(
                ExistingGuardianByEmailSql,
                new { Email = normalizedEmail },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

        if (guardian is null || guardian.GuardianId <= 0 || string.IsNullOrWhiteSpace(guardian.Email))
        {
            return new ParentPasswordResetRequestResult(false, 0, string.Empty, string.Empty, null);
        }

        var displayName = string.Join(
            " ",
            new[] { guardian.FirstName, guardian.LastName }.Where(part => !string.IsNullOrWhiteSpace(part))).Trim();
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = guardian.Email;
        }

        var token = await _passwordResetTokenStore.CreateAsync(guardian.GuardianId, tokenLifetime, cancellationToken);
        return new ParentPasswordResetRequestResult(
            true,
            guardian.GuardianId,
            guardian.Email.Trim(),
            displayName,
            token);
    }

    public async Task<ParentPasswordResetValidateResult> ValidatePasswordResetTokenAsync(
        string token,
        CancellationToken cancellationToken = default)
    {
        var record = await _passwordResetTokenStore.GetValidAsync(token, cancellationToken);
        if (record is null)
        {
            return new ParentPasswordResetValidateResult(false, 0, "This reset link is invalid or has expired.");
        }

        return new ParentPasswordResetValidateResult(true, record.GuardianId, string.Empty);
    }

    public async Task<ParentChangePasswordResult> CompletePasswordResetAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var record = await _passwordResetTokenStore.GetValidAsync(token, cancellationToken);
        if (record is null)
        {
            return new ParentChangePasswordResult(false, "This reset link is invalid or has expired.");
        }

        var newHash = SecurityHelper.GetMd5Hash(newPassword);

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.ExecuteAsync(
            new CommandDefinition(
                UpdatePasswordSql,
                new { GuardianId = record.GuardianId, PasswordHash = newHash },
                cancellationToken: cancellationToken));

        if (rows <= 0)
        {
            return new ParentChangePasswordResult(false, "Password was not updated.");
        }

        await _passwordResetTokenStore.MarkUsedAsync(token, cancellationToken);
        await _passwordResetTokenStore.RevokeUnusedForGuardianAsync(record.GuardianId, cancellationToken);
        return new ParentChangePasswordResult(true, "Password updated successfully.");
    }

    private static bool PasswordMatches(string stored, string md5Hash, string plainPassword) =>
        string.Equals(stored.Trim(), md5Hash, StringComparison.OrdinalIgnoreCase)
        || string.Equals(stored.Trim(), plainPassword, StringComparison.Ordinal);

    private sealed class GuardianLookupRow
    {
        public int GuardianId { get; init; }

        public string FirstName { get; init; } = string.Empty;

        public string LastName { get; init; } = string.Empty;

        public string Email { get; init; } = string.Empty;
    }
}
