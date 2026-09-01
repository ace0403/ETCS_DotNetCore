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

    public async Task<IReadOnlyList<string>> ResolveSchoolCodesForQueryAsync(
        string? requestedSchoolCode,
        CancellationToken cancellationToken = default)
    {
        if (IsUnrestricted)
        {
            if (string.IsNullOrWhiteSpace(requestedSchoolCode))
            {
                return [];
            }

            return [requestedSchoolCode.Trim()];
        }

        var allowedCodes = await GetAllowedSchoolCodesAsync(cancellationToken);
        if (allowedCodes.Count == 0)
        {
            throw new UnauthorizedAccessException("No schools assigned to your account.");
        }

        if (string.IsNullOrWhiteSpace(requestedSchoolCode))
        {
            return allowedCodes;
        }

        var normalized = requestedSchoolCode.Trim();
        if (!allowedCodes.Contains(normalized, StringComparer.OrdinalIgnoreCase))
        {
            throw new UnauthorizedAccessException("School is outside your assigned scope.");
        }

        return [normalized];
    }

    public async Task<(string SchoolCode, string SchoolCodesCsv)> ResolveAdminSchoolParamsFromIdQueryAsync(
        string? requestedSchoolId,
        CancellationToken cancellationToken = default)
    {
        var ids = ResolveSchoolIdsForQuery(requestedSchoolId);
        if (IsUnrestricted && ids.Count == 0)
        {
            return (string.Empty, string.Empty);
        }

        if (ids.Count == 0)
        {
            throw new UnauthorizedAccessException("No schools assigned to your account.");
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var codes = (await dbConnection.QueryAsync<string>(
            new CommandDefinition(
                """
                SELECT LTRIM(RTRIM(ISNULL(Schoolcode, '')))
                FROM SchoolInfo
                WHERE SchoolId IN @SchoolIds;
                """,
                new { SchoolIds = ids },
                cancellationToken: cancellationToken)))
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (codes.Count == 0)
        {
            throw new UnauthorizedAccessException("School is outside your assigned scope.");
        }

        return (
            codes.Count == 1 ? codes[0] : string.Empty,
            string.Join(",", codes));
    }

    public IReadOnlyList<int> ResolveSchoolIdsForList(int? requestedSchoolId)
    {
        if (IsUnrestricted)
        {
            return [];
        }

        var allowed = GetAllowedSchoolIds();
        if (allowed.Count == 0)
        {
            throw new UnauthorizedAccessException("No schools assigned to your account.");
        }

        if (requestedSchoolId is null or <= 0)
        {
            return allowed;
        }

        if (!allowed.Contains(requestedSchoolId.Value))
        {
            throw new UnauthorizedAccessException("School is outside your assigned scope.");
        }

        return [requestedSchoolId.Value];
    }

    public IReadOnlyList<int> ResolveSchoolIdsForQuery(string? requestedSchoolId)
    {
        int? parsed = null;
        if (!string.IsNullOrWhiteSpace(requestedSchoolId)
            && !string.Equals(requestedSchoolId.Trim(), "All", StringComparison.OrdinalIgnoreCase))
        {
            if (!int.TryParse(requestedSchoolId.Trim(), out var id))
            {
                throw new UnauthorizedAccessException("Invalid school.");
            }

            parsed = id;
        }

        if (IsUnrestricted)
        {
            return parsed is > 0 ? [parsed.Value] : [];
        }

        return ResolveSchoolIdsForList(parsed);
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
            throw new UnauthorizedAccessException("No schools assigned to your account.");
        }

        if (request.SchoolId is > 0)
        {
            if (!allowed.Contains(request.SchoolId.Value))
            {
                throw new UnauthorizedAccessException("School is outside your assigned scope.");
            }

            return;
        }

        if (allowed.Count == 1)
        {
            request.SchoolId = allowed[0];
            return;
        }

        request.ScopedSchoolIds = allowed.ToList();
        request.SchoolId = null;
    }

    public void EnsureInScope(int? schoolId)
    {
        if (IsUnrestricted)
        {
            return;
        }

        var allowed = GetAllowedSchoolIds();
        if (allowed.Count == 0)
        {
            throw new UnauthorizedAccessException("No schools assigned to your account.");
        }

        if (schoolId is null or <= 0)
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
        if (allowed.Count == 0)
        {
            throw new UnauthorizedAccessException("No schools assigned to your account.");
        }

        if (!allowed.Contains(schoolId))
        {
            throw new UnauthorizedAccessException("School is outside your assigned scope.");
        }
    }

    public async Task EnsureReportSchoolCodeInScopeAsync(
        string? schoolCode,
        CancellationToken cancellationToken = default)
    {
        _ = await ResolveSchoolCodesForQueryAsync(schoolCode, cancellationToken);
    }

    public void EnsureReportSchoolIdInScope(string? schoolId)
    {
        _ = ResolveSchoolIdsForQuery(schoolId);
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
