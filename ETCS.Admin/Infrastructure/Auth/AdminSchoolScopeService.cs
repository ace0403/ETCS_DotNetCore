using System.Data.Common;
using System.Security.Claims;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Admin.Reports.CanteenTransactions;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Admin.Infrastructure.Auth;

public sealed class AdminSchoolScopeService : IAdminSchoolScopeService
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IDbConnectionFactory _connectionFactory;

    public AdminSchoolScopeService(
        IHttpContextAccessor httpContextAccessor,
        IDbConnectionFactory connectionFactory)
    {
        _httpContextAccessor = httpContextAccessor;
        _connectionFactory = connectionFactory;
    }

    private ClaimsPrincipal User =>
        _httpContextAccessor.HttpContext?.User
        ?? throw new InvalidOperationException("HttpContext is not available.");

    public bool IsUnrestricted => User.IsAdminRole() || !User.IsSchoolScoped();

    public IReadOnlyList<int> GetAllowedSchoolIds() =>
        IsUnrestricted ? [] : User.GetAssignedSchoolIds();

    public async Task<IReadOnlyList<string>> GetAllowedSchoolCodesAsync(CancellationToken cancellationToken = default)
    {
        if (IsUnrestricted)
        {
            return [];
        }

        var schoolIds = GetAllowedSchoolIds();
        if (schoolIds.Count == 0)
        {
            return [];
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<string>(
            new CommandDefinition(
                """
                SELECT LTRIM(RTRIM(ISNULL(Schoolcode, '')))
                FROM SchoolInfo
                WHERE SchoolId IN @SchoolIds;
                """,
                new { SchoolIds = schoolIds },
                cancellationToken: cancellationToken));

        return rows
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<SchoolCodeLookupDto>> FilterSchoolCodesAsync(
        IReadOnlyList<SchoolCodeLookupDto> schools,
        CancellationToken cancellationToken = default)
    {
        if (IsUnrestricted)
        {
            return schools;
        }

        var allowedCodes = new HashSet<string>(
            await GetAllowedSchoolCodesAsync(cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        return schools
            .Where(s => allowedCodes.Contains(s.Code))
            .ToList();
    }

    public void ApplyListScope(DataTableRequest request)
    {
        if (IsUnrestricted)
        {
            return;
        }

        var allowed = GetAllowedSchoolIds();
        if (allowed.Count == 0)
        {
            return;
        }

        if (allowed.Count == 1)
        {
            request.SchoolId = allowed[0];
            return;
        }

        if (request.SchoolId is > 0 && !allowed.Contains(request.SchoolId.Value))
        {
            request.SchoolId = allowed[0];
        }
    }

    public void EnsureInScope(int? schoolId)
    {
        if (IsUnrestricted || schoolId is null or <= 0)
        {
            return;
        }

        EnsureInScope(schoolId.Value);
    }

    public void EnsureInScope(int schoolId)
    {
        if (IsUnrestricted)
        {
            return;
        }

        var allowed = GetAllowedSchoolIds();
        if (allowed.Count == 0 || allowed.Contains(schoolId))
        {
            return;
        }

        throw new UnauthorizedAccessException("School is outside your assigned scope.");
    }

    public async Task EnsureReportSchoolCodeInScopeAsync(
        string? schoolCode,
        CancellationToken cancellationToken = default)
    {
        if (IsUnrestricted)
        {
            return;
        }

        var allowedCodes = await GetAllowedSchoolCodesAsync(cancellationToken);
        if (allowedCodes.Count == 0)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(schoolCode))
        {
            if (allowedCodes.Count == 1)
            {
                return;
            }

            throw new UnauthorizedAccessException("School is required for your account.");
        }

        if (!allowedCodes.Contains(schoolCode, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("School is outside your assigned scope.");
        }
    }

    public void EnsureReportSchoolIdInScope(string? schoolId)
    {
        if (IsUnrestricted || string.IsNullOrWhiteSpace(schoolId))
        {
            return;
        }

        if (int.TryParse(schoolId, out var parsedId))
        {
            EnsureInScope(parsedId);
            return;
        }

        var allowedIds = new HashSet<string>(
            GetAllowedSchoolIds().Select(id => id.ToString()),
            StringComparer.OrdinalIgnoreCase);

        if (!allowedIds.Contains(schoolId))
        {
            throw new UnauthorizedAccessException("School is outside your assigned scope.");
        }
    }

    public IReadOnlyList<T> FilterMealOrderSchools<T>(IEnumerable<T> schools, Func<T, string> idSelector)
        where T : class
    {
        var list = schools.ToList();
        if (IsUnrestricted)
        {
            return list;
        }

        var allowedIds = new HashSet<string>(
            GetAllowedSchoolIds().Select(id => id.ToString()),
            StringComparer.OrdinalIgnoreCase);

        return list.Where(s => allowedIds.Contains(idSelector(s))).ToList();
    }

    public IReadOnlyList<T> FilterSchools<T>(IEnumerable<T> schools, Func<T, int> idSelector)
    {
        var list = schools.ToList();
        if (IsUnrestricted)
        {
            return list;
        }

        var allowed = new HashSet<int>(GetAllowedSchoolIds());
        return list.Where(s => allowed.Contains(idSelector(s))).ToList();
    }
}
