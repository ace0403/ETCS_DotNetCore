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
        WHERE LTRIM(RTRIM(ISNULL(g.Email, ''))) = @Email;
        """;
    private const string UpdateGuardianRegistrationSql = """
        UPDATE GuardianInfo
        SET
            FirstName = @FirstName,
            LastName = @LastName,
            Email = @Email,
            MobileNo = @MobileNumber,
            Password = @PasswordHash
        WHERE GrdID = @GuardianId;
        """;
    private const string InsertGuardianRegistrationSql = """
        INSERT INTO GuardianInfo (FirstName, LastName, Email, MobileNo, Username, Password, Blacklist, Status, GUID, RoleId)
        VALUES (@FirstName, @LastName, @Email, @MobileNumber, @Username, @PasswordHash, 0, 1, NEWID(), 5);

        SELECT CAST(SCOPE_IDENTITY() AS INT);
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public ParentLoginRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
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
        var passwordHash = SecurityHelper.GetMd5Hash(request.Password);

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var existing = await dbConnection.QueryFirstOrDefaultAsync<GuardianLookupRow>(
            new CommandDefinition(
                ExistingGuardianByEmailSql,
                new { Email = normalizedEmail },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

        if (existing is not null && existing.GuardianId > 0)
        {
            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    UpdateGuardianRegistrationSql,
                    new
                    {
                        existing.GuardianId,
                        FirstName = request.FirstName.Trim(),
                        LastName = request.LastName.Trim(),
                        Email = normalizedEmail,
                        MobileNumber = request.MobileNumber.Trim(),
                        PasswordHash = passwordHash
                    },
                    commandType: CommandType.Text,
                    cancellationToken: cancellationToken));

            var updatedUser = new UserResponse(
                existing.GuardianId,
                $"{request.FirstName.Trim()} {request.LastName.Trim()}".Trim(),
                normalizedEmail);

            return new ParentRegistrationResult(true, existing.GuardianId, "Registration completed.", updatedUser);
        }

        var newGuardianId = await dbConnection.ExecuteScalarAsync<int>(
            new CommandDefinition(
                InsertGuardianRegistrationSql,
                new
                {
                    FirstName = request.FirstName.Trim(),
                    LastName = request.LastName.Trim(),
                    Email = normalizedEmail,
                    Username = normalizedEmail,
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

    private sealed class GuardianLookupRow
    {
        public int GuardianId { get; init; }
    }
}
