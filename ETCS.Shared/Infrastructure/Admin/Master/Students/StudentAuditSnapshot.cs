using System.Data.Common;
using System.Text.Json.Serialization;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Master.Guardians;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Master.Students;

internal static class StudentAuditSnapshot
{
    public static Task<StudentAuditValues?> CaptureAsync(
        IDbConnectionFactory schoolConnectionFactory,
        IMealDbConnectionFactory mealConnectionFactory,
        StudentAdminSaveRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return Task.FromResult<StudentAuditValues?>(null);
        }

        return CaptureAsync(
            schoolConnectionFactory,
            mealConnectionFactory,
            new StudentAuditInput(
                request.UserId,
                request.StudCode,
                request.FirstName,
                request.LastName,
                request.SchoolId,
                request.GuardianId,
                request.Gender,
                request.GradeId,
                request.Division,
                request.DateOfBirth,
                request.AllergyItemIds,
                request.OrderTypeIds,
                request.DailySpendLimit,
                request.WeeklySpendLimit,
                request.LowBalanceEmailNotification,
                request.IsActive),
            cancellationToken);
    }

    public static Task<StudentAuditValues?> CaptureAsync(
        IDbConnectionFactory schoolConnectionFactory,
        IMealDbConnectionFactory mealConnectionFactory,
        GuardianAddStudentRequest? request,
        CancellationToken cancellationToken,
        decimal? userId = null)
    {
        if (request is null)
        {
            return Task.FromResult<StudentAuditValues?>(null);
        }

        bool? isActive = request is GuardianEditStudentRequest ? null : true;
        var resolvedUserId = userId
            ?? (request is GuardianEditStudentRequest edit ? edit.UserId : 0m);
        return CaptureAsync(
            schoolConnectionFactory,
            mealConnectionFactory,
            new StudentAuditInput(
                resolvedUserId,
                request.StudentCardNo,
                request.FirstName,
                request.LastName,
                request.SchoolId,
                request.GuardianId,
                request.Gender,
                request.GradeId,
                request.Division,
                request.DateOfBirth,
                request.AllergyItemIds,
                request.OrderTypeIds,
                request.DailySpendLimit,
                request.WeeklySpendLimit,
                request.LowBalanceEmailNotification,
                isActive),
            cancellationToken);
    }

    private static async Task<StudentAuditValues?> CaptureAsync(
        IDbConnectionFactory schoolConnectionFactory,
        IMealDbConnectionFactory mealConnectionFactory,
        StudentAuditInput input,
        CancellationToken cancellationToken)
    {
        var schools = await LoadMapAsync(
            schoolConnectionFactory.CreateConnection,
            """
            SELECT SchoolId AS Id, LTRIM(RTRIM(ISNULL(SchoolName, ''))) AS Name
            FROM SchoolInfo
            WHERE SchoolId IN @Ids;
            """,
            [input.SchoolId],
            cancellationToken);
        var parents = await LoadMapAsync(
            schoolConnectionFactory.CreateConnection,
            """
            SELECT CAST(GrdID AS int) AS Id,
                   LTRIM(RTRIM(ISNULL(FirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(LastName, ''))) AS Name
            FROM GuardianInfo
            WHERE GrdID IN @Ids;
            """,
            [input.GuardianId],
            cancellationToken);
        var grades = await LoadMapAsync(
            schoolConnectionFactory.CreateConnection,
            """
            SELECT Id, LTRIM(RTRIM(ISNULL(Grade, ''))) AS Name
            FROM SchoolGrades
            WHERE Id IN @Ids;
            """,
            [input.GradeId],
            cancellationToken);
        var enums = await LoadMapAsync(
            mealConnectionFactory.CreateConnection,
            """
            SELECT Id, LTRIM(RTRIM(ISNULL(EnumValue, ''))) AS Name
            FROM Enums
            WHERE Id IN @Ids;
            """,
            (input.AllergyItemIds ?? []).Concat(input.OrderTypeIds ?? []),
            cancellationToken);

        return new StudentAuditValues
        {
            UserId = input.UserId,
            StudentIdNo = input.StudentIdNo?.Trim() ?? string.Empty,
            FirstName = input.FirstName?.Trim() ?? string.Empty,
            LastName = input.LastName?.Trim(),
            School = Label(schools, input.SchoolId),
            Parent = Label(parents, input.GuardianId),
            Gender = input.Gender?.Trim(),
            Standard = Label(grades, input.GradeId),
            Division = input.Division?.Trim(),
            DateOfBirth = input.DateOfBirth,
            Allergies = Labels(enums, input.AllergyItemIds),
            OrderTypes = Labels(enums, input.OrderTypeIds),
            DailySpendLimit = input.DailySpendLimit,
            WeeklySpendLimit = input.WeeklySpendLimit,
            LowBalanceEmailNotification = input.LowBalanceEmailNotification,
            IsActive = input.IsActive
        };
    }

    private static async Task<Dictionary<int, string>> LoadMapAsync(
        Func<System.Data.IDbConnection> createConnection,
        string sql,
        IEnumerable<int> ids,
        CancellationToken cancellationToken)
    {
        var distinct = ids.Where(id => id > 0).Distinct().ToArray();
        if (distinct.Length == 0)
        {
            return [];
        }

        try
        {
            using var connection = createConnection();
            var dbConnection = (DbConnection)connection;
            await dbConnection.OpenAsync(cancellationToken);
            var rows = await dbConnection.QueryAsync<IdNameRow>(
                new CommandDefinition(sql, new { Ids = distinct }, cancellationToken: cancellationToken));
            return rows
                .GroupBy(row => row.Id)
                .ToDictionary(group => group.Key, group => group.First().Name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [];
        }
    }

    private static string? Label(IReadOnlyDictionary<int, string> names, int id)
    {
        if (id <= 0)
        {
            return null;
        }

        return names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name.Trim()
            : id.ToString();
    }

    private static string[] Labels(IReadOnlyDictionary<int, string> names, IEnumerable<int>? ids)
    {
        if (ids is null)
        {
            return [];
        }

        return ids
            .Where(id => id > 0)
            .Distinct()
            .Select(id => Label(names, id) ?? id.ToString())
            .ToArray();
    }

    private sealed record StudentAuditInput(
        decimal UserId,
        string? StudentIdNo,
        string? FirstName,
        string? LastName,
        int SchoolId,
        int GuardianId,
        string? Gender,
        int GradeId,
        string? Division,
        DateTime? DateOfBirth,
        IReadOnlyList<int>? AllergyItemIds,
        IReadOnlyList<int>? OrderTypeIds,
        decimal? DailySpendLimit,
        decimal? WeeklySpendLimit,
        bool LowBalanceEmailNotification,
        bool? IsActive);

    private sealed class IdNameRow
    {
        public int Id { get; set; }

        public string Name { get; init; } = string.Empty;
    }
}

internal sealed class StudentAuditValues
{
    public decimal UserId { get; init; }

    public string StudentIdNo { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string? LastName { get; init; }

    public string? School { get; init; }

    public string? Parent { get; init; }

    public string? Gender { get; init; }

    public string? Standard { get; init; }

    public string? Division { get; init; }

    public DateTime? DateOfBirth { get; init; }

    public IReadOnlyList<string> Allergies { get; init; } = [];

    public IReadOnlyList<string> OrderTypes { get; init; } = [];

    public decimal? DailySpendLimit { get; init; }

    public decimal? WeeklySpendLimit { get; init; }

    public bool LowBalanceEmailNotification { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? IsActive { get; init; }
}
