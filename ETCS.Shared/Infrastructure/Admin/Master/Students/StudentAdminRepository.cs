using System.Data.Common;
using Dapper;
using ETCS.Shared.Application.Students;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Admin.Master.Schools;
using ETCS.Shared.Infrastructure.Data;
using ETCS.Shared.Infrastructure.Students;

using static ETCS.Shared.Infrastructure.Admin.DataTablePagingHelper;
using static ETCS.Shared.Infrastructure.Admin.SchoolScopeFilterHelper;

namespace ETCS.Shared.Infrastructure.Admin.Master.Students;

public sealed class StudentAdminRepository : IStudentAdminRepository
{
    /// <summary>Active prepaid balance from IdMember (IdCardStatus = 1), keyed by StudentLogin.CustomerId.</summary>
    private const string BalanceSql = """
        ISNULL((
            SELECT TOP (1) m.BalPrepaid
            FROM IdMember m
            WHERE m.IdCardStatus = 1
              AND LTRIM(RTRIM(m.CustomerID)) = LTRIM(RTRIM(ISNULL(sl.CustomerId, '')))
              AND LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) <> ''
        ), 0)
        """;

    private static readonly string SelectSql = $"""
        SELECT
            sl.UserId,
            LTRIM(RTRIM(ISNULL(sl.StudCode, ''))) AS StudCode,
            LTRIM(RTRIM(ISNULL(sl.StudFirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(sl.StudLastName, ''))) AS Name,
            LTRIM(RTRIM(ISNULL(sch.SchoolName, ''))) AS SchoolName,
            LTRIM(RTRIM(ISNULL(g.FirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(g.LastName, ''))) AS GuardianName,
            CAST(AddDate as datetime) AS CreatedAt,
            {BalanceSql} AS Balance
        """;

    private const string FromSql = """
        FROM StudentLogin sl
        LEFT JOIN SchoolInfo sch ON sch.SchoolId = sl.StudSchoolId
        LEFT JOIN GuardianInfo g ON g.GrdID = sl.GrdID
        """;

    private static readonly string SearchFilterSql = $"""
        LTRIM(RTRIM(ISNULL(sl.StudCode, ''))) LIKE '%' + @Search + '%'
        OR LTRIM(RTRIM(ISNULL(sl.StudFirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(sl.StudLastName, ''))) LIKE '%' + @Search + '%'
        OR LTRIM(RTRIM(ISNULL(sch.SchoolName, ''))) LIKE '%' + @Search + '%'
        OR LTRIM(RTRIM(ISNULL(g.FirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(g.LastName, ''))) LIKE '%' + @Search + '%'
        OR CAST({BalanceSql} AS varchar(30)) LIKE '%' + @Search + '%'
        """;

    private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["UserId"] = "sl.UserId",
        ["StudCode"] = "sl.StudCode",
        ["Name"] = "sl.StudFirstName",
        ["SchoolName"] = "sch.SchoolName",
        ["GuardianName"] = "g.FirstName",
        ["Balance"] = BalanceSql
    };

    private const string GetSql = """
        SELECT TOP (1)
            sl.UserId,
            LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(sl.CustomerId)), ''), sl.StudCode))) AS StudCode,
            LTRIM(RTRIM(ISNULL(sl.StudFirstName, ''))) AS FirstName,
            LTRIM(RTRIM(ISNULL(sl.StudLastName, ''))) AS LastName,
            ISNULL(sl.StudSchoolId, 0) AS SchoolId,
            ISNULL(
                NULLIF(sl.GrdID, 0),
                (SELECT TOP (1) CAST(gm.GrdID AS int) FROM GuardianMaster gm WHERE gm.StudentCardNo = sl.CustomerId)
            ) AS GuardianId,
            LTRIM(RTRIM(ISNULL(sl.StudGender, 'Male'))) AS Gender,
            LTRIM(RTRIM(ISNULL(sl.StudStd, ''))) AS StudStd,
            LTRIM(RTRIM(ISNULL(sl.StudDiv, ''))) AS Division,
            sl.StudDateOfBirth AS DateOfBirth,
            sl.DailyLimit AS DailySpendLimit,
            sl.WeeklyLimit AS WeeklySpendLimit,
            CAST(ISNULL(sl.IsUnsubscribeLowBalNoti, 0) AS bit) AS IsUnsubscribeLowBalNoti,
            CASE WHEN ISNULL(sl.BlackList, 0) = 1 THEN 0 ELSE 1 END AS IsActive
        FROM StudentLogin sl
        WHERE sl.UserId = @UserId;
        """;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IStudentRepository _studentRepository;
    private readonly IStudentAllergyAdminRepository _allergyRepository;
    private readonly IStudentOrderTypeAdminRepository _orderTypeRepository;
    private readonly ISchoolOrderTypeAdminRepository _schoolOrderTypeRepository;

    public StudentAdminRepository(
        IDbConnectionFactory connectionFactory,
        IStudentRepository studentRepository,
        IStudentAllergyAdminRepository allergyRepository,
        IStudentOrderTypeAdminRepository orderTypeRepository,
        ISchoolOrderTypeAdminRepository schoolOrderTypeRepository)
    {
        _connectionFactory = connectionFactory;
        _studentRepository = studentRepository;
        _allergyRepository = allergyRepository;
        _orderTypeRepository = orderTypeRepository;
        _schoolOrderTypeRepository = schoolOrderTypeRepository;
    }

    public async Task<DataTableResponse<StudentAdminListItemDto>> GetDataAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var (schoolFilterSql, schoolFilterParams) = BuildSchoolIdFilter(request, "sl.StudSchoolId");

        return await QueryPagedAsync<StudentAdminListItemDto>(
            dbConnection,
            SelectSql,
            FromSql,
            schoolFilterSql,
            SearchFilterSql,
            SortColumns,
            "sl.UserId",
            request,
            schoolFilterParams,
            cancellationToken: cancellationToken,
            defaultSortDirection: "DESC");
    }

    public async Task<StudentAdminSaveRequest?> GetAsync(decimal userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0) return null;
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var row = await dbConnection.QuerySingleOrDefaultAsync<StudentAdminRow>(
            new CommandDefinition(GetSql, new { UserId = userId }, cancellationToken: cancellationToken));
        if (row is null) return null;

        var grades = await _studentRepository.GetAllGradesAsync(cancellationToken);
        var gradeId = grades.FirstOrDefault(g =>
            string.Equals(g.Grade, row.StudStd, StringComparison.OrdinalIgnoreCase))?.Id ?? 0;

        var allergyIds = await _allergyRepository.GetAllergyIdsAsync(userId, cancellationToken);
        var orderTypeIds = await _orderTypeRepository.GetOrderTypeIdsAsync(userId, cancellationToken);

        return new StudentAdminSaveRequest
        {
            UserId = row.UserId,
            StudCode = row.StudCode,
            FirstName = row.FirstName,
            LastName = row.LastName,
            SchoolId = row.SchoolId,
            GuardianId = row.GuardianId,
            Gender = row.Gender,
            GradeId = gradeId,
            Division = row.Division,
            DateOfBirth = row.DateOfBirth,
            DailySpendLimit = row.DailySpendLimit,
            WeeklySpendLimit = row.WeeklySpendLimit,
            LowBalanceEmailNotification = !row.IsUnsubscribeLowBalNoti,
            AllergyItemIds = allergyIds.ToList(),
            OrderTypeIds = orderTypeIds.ToList(),
            IsActive = row.IsActive
        };
    }

    public async Task<IReadOnlyList<GuardianLookupDto>> GuardianLookupsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.QueryAsync<GuardianLookupDto>(
            new CommandDefinition(
                """
                SELECT CAST(g.GrdID AS int) AS Id,
                    LTRIM(RTRIM(ISNULL(g.FirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(g.LastName, ''))) AS Name
                FROM GuardianInfo g
                ORDER BY g.FirstName, g.LastName;
                """,
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<SchoolLookupDto>> SchoolLookupsAsync(CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var rows = await dbConnection.QueryAsync<SchoolLookupDto>(
            new CommandDefinition(
                """
                SELECT
                    SchoolId AS Id,
                    LTRIM(RTRIM(SchoolName)) AS Name,
                    LTRIM(RTRIM(ISNULL(Schoolcode, ''))) AS Code
                FROM SchoolInfo
                ORDER BY SchoolName;
                """,
                cancellationToken: cancellationToken));
        return rows.ToList();
    }

    public async Task<IReadOnlyList<GradeLookupDto>> GradeLookupsAsync(CancellationToken cancellationToken = default)
    {
        var grades = await _studentRepository.GetAllGradesAsync(cancellationToken);
        return grades.Select(g => new GradeLookupDto { Id = g.Id, Grade = g.Grade }).ToList();
    }

    public async Task<AdminOperationResult> SaveAsync(StudentAdminSaveRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var grades = await _studentRepository.GetAllGradesAsync(cancellationToken);
        var grade = grades.FirstOrDefault(g => g.Id == request.GradeId);
        if (grade is null)
            return AdminOperationResult.Fail("Selected standard was not found.");

        var school = await dbConnection.QuerySingleOrDefaultAsync<SchoolRow>(
            new CommandDefinition(
                """
                SELECT TOP (1)
                    SchoolId,
                    ISNULL(CountryId, 1) AS CountryId,
                    LTRIM(RTRIM(ISNULL(SchoolCode, ''))) AS SchoolCode
                FROM SchoolInfo
                WHERE SchoolId = @SchoolId;
                """,
                new { request.SchoolId },
                cancellationToken: cancellationToken));
        if (school is null)
            return AdminOperationResult.Fail("Selected school was not found.");

        var schoolOrderTypeError = StudentOrderTypeValidation.ValidateAgainstSchool(
            await _schoolOrderTypeRepository.GetOrderTypeIdsAsync(request.SchoolId, cancellationToken),
            request.OrderTypeIds);
        if (schoolOrderTypeError is not null)
            return AdminOperationResult.Fail(schoolOrderTypeError);

        var cardError = request.UserId > 0
            ? StudentCardNumber.ValidateForEdit(request.StudCode, out var studCode)
            : StudentCardNumber.ResolveForCreate(request.StudCode, school.SchoolCode, out studCode);
        if (cardError is not null)
            return AdminOperationResult.Fail(cardError);

        if (request.UserId > 0)
        {
            var linkedStudent = await dbConnection.QuerySingleOrDefaultAsync<LinkedStudentRow>(
                new CommandDefinition(
                    "SELECT TOP (1) UserId, LTRIM(RTRIM(ISNULL(CustomerId, ''))) AS CustomerId FROM StudentLogin WHERE UserId = @UserId;",
                    new { request.UserId },
                    cancellationToken: cancellationToken));
            if (linkedStudent is null)
                return AdminOperationResult.Fail("Student was not found.");

            if (await StudentCardNumber.IsTakenAsync(dbConnection, studCode, request.UserId, cancellationToken))
                return AdminOperationResult.Fail(StudentCardNumber.DuplicateMessage);

            var oldCustomerId = linkedStudent.CustomerId;
            var blackList = request.IsActive ? 0 : 1;

            await using var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);
            try
            {
                const string updateSql = """
                    UPDATE StudentLogin
                    SET StudCode = @StudCode,
                        StudUserName = @StudCode,
                        CustomerId = @StudCode,
                        StudFirstName = @FirstName,
                        StudLastName = @LastName,
                        StudSchoolId = @SchoolId,
                        GrdID = NULLIF(@GuardianId, 0),
                        StudGender = @Gender,
                        StudStd = @StudStd,
                        StudDiv = @Division,
                        StudDateOfBirth = @DateOfBirth,
                        DailyLimit = @DailySpendLimit,
                        WeeklyLimit = @WeeklySpendLimit,
                        IsUnsubscribeLowBalNoti = @IsUnsubscribeLowBalNoti,
                        BlackList = @BlackList
                    WHERE UserId = @UserId;
                    """;
                var rows = await dbConnection.ExecuteAsync(
                    new CommandDefinition(
                        updateSql,
                        new
                        {
                            request.UserId,
                            StudCode = studCode,
                            request.FirstName,
                            LastName = request.LastName ?? string.Empty,
                            request.SchoolId,
                            request.GuardianId,
                            Gender = request.Gender.Trim(),
                            StudStd = grade.Grade,
                            request.Division,
                            request.DateOfBirth,
                            DailySpendLimit = request.DailySpendLimit ?? 0m,
                            WeeklySpendLimit = request.WeeklySpendLimit ?? 0m,
                            IsUnsubscribeLowBalNoti = !request.LowBalanceEmailNotification,
                            BlackList = blackList
                        },
                        transaction: transaction,
                        cancellationToken: cancellationToken));

                if (rows <= 0)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    return AdminOperationResult.Fail("Student was not updated.");
                }

                await StudentIdMemberCustomerIdSync.UpdateCustomerIdAsync(
                    dbConnection,
                    oldCustomerId,
                    studCode,
                    cancellationToken,
                    transaction);

                if (request.GuardianId > 0)
                {
                    var newCustomerId = await dbConnection.ExecuteScalarAsync<string>(
                        new CommandDefinition(
                            "SELECT TOP (1) CustomerId FROM StudentLogin WHERE UserId = @UserId;",
                            new { request.UserId },
                            transaction: transaction,
                            cancellationToken: cancellationToken));

                    if (!string.IsNullOrWhiteSpace(newCustomerId))
                    {
                        await dbConnection.ExecuteAsync(
                            new CommandDefinition(
                                """
                                UPDATE GuardianMaster
                                SET StudentCardNo = @NewCustomerId
                                WHERE GrdID = @GuardianId
                                  AND StudentCardNo = @OldCustomerId;
                                """,
                                new
                                {
                                    request.GuardianId,
                                    OldCustomerId = oldCustomerId,
                                    NewCustomerId = newCustomerId
                                },
                                transaction: transaction,
                                cancellationToken: cancellationToken));

                        var mappingExists = await dbConnection.ExecuteScalarAsync<int?>(
                            new CommandDefinition(
                                """
                                SELECT TOP (1) CAST(gm.ID AS int)
                                FROM GuardianMaster gm
                                INNER JOIN StudentLogin sl ON sl.CustomerId = gm.StudentCardNo
                                WHERE gm.GrdID = @GuardianId
                                  AND sl.UserId = @UserId;
                                """,
                                new { request.GuardianId, request.UserId },
                                transaction: transaction,
                                cancellationToken: cancellationToken));
                        if (!mappingExists.HasValue)
                        {
                            await dbConnection.ExecuteAsync(
                                new CommandDefinition(
                                    """
                                    INSERT INTO GuardianMaster (GrdID, StudentCardNo)
                                    SELECT @GuardianId, sl.CustomerId
                                    FROM StudentLogin sl
                                    WHERE sl.UserId = @UserId;
                                    """,
                                    new { request.GuardianId, request.UserId },
                                    transaction: transaction,
                                    cancellationToken: cancellationToken));
                        }
                    }
                }

                await transaction.CommitAsync(cancellationToken);
            }
            catch
            {
                await transaction.RollbackAsync(cancellationToken);
                throw;
            }

            await _allergyRepository.SaveAllergiesAsync(request.UserId, request.AllergyItemIds ?? [], cancellationToken);
            await _orderTypeRepository.SaveOrderTypesAsync(request.UserId, request.OrderTypeIds ?? [], cancellationToken);
            return AdminOperationResult.Ok("Student updated successfully.");
        }

        if (await StudentCardNumber.IsTakenAsync(dbConnection, studCode, excludeUserId: null, cancellationToken))
            return AdminOperationResult.Fail(StudentCardNumber.DuplicateMessage);

        var upsert = BuildUpsertRequest(request, grade.Grade, school, studCode);
        try
        {
            await _studentRepository.SaveStudentAsync(upsert, isInsert: true, cancellationToken);
        }
        catch (Exception ex)
        {
            return AdminOperationResult.Fail(StudentCardNumber.MessageOrDuplicate(ex));
        }

        var userId = await dbConnection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(
                """
                SELECT TOP (1) UserId
                FROM StudentLogin
                WHERE LTRIM(RTRIM(ISNULL(CustomerId, ''))) = @StudCode
                   OR LTRIM(RTRIM(ISNULL(StudCode, ''))) = @StudCode
                ORDER BY UserId DESC;
                """,
                new { StudCode = studCode },
                cancellationToken: cancellationToken));
        if (!userId.HasValue)
            return AdminOperationResult.Fail("Student was created but could not be loaded.");

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE StudentLogin
                SET DailyLimit = @DailySpendLimit,
                    WeeklyLimit = @WeeklySpendLimit,
                    IsUnsubscribeLowBalNoti = @IsUnsubscribeLowBalNoti,
                    StudDateOfBirth = @DateOfBirth
                WHERE UserId = @UserId;
                """,
                new
                {
                    UserId = userId.Value,
                    DailySpendLimit = request.DailySpendLimit ?? 0m,
                    WeeklySpendLimit = request.WeeklySpendLimit ?? 0m,
                    IsUnsubscribeLowBalNoti = !request.LowBalanceEmailNotification,
                    request.DateOfBirth
                },
                cancellationToken: cancellationToken));

        await _allergyRepository.SaveAllergiesAsync(userId.Value, request.AllergyItemIds ?? [], cancellationToken);
        await _orderTypeRepository.SaveOrderTypesAsync(userId.Value, request.OrderTypeIds ?? [], cancellationToken);

        if (request.GuardianId > 0)
        {
            var mappingExists = await dbConnection.ExecuteScalarAsync<int?>(
                new CommandDefinition(
                    """
                    SELECT TOP (1) CAST(gm.ID AS int)
                    FROM GuardianMaster gm
                    INNER JOIN StudentLogin sl ON sl.CustomerId = gm.StudentCardNo
                    WHERE gm.GrdID = @GuardianId
                      AND sl.UserId = @UserId;
                    """,
                    new { GuardianId = request.GuardianId, UserId = userId.Value },
                    cancellationToken: cancellationToken));
            if (!mappingExists.HasValue)
            {
                await dbConnection.ExecuteAsync(
                    new CommandDefinition(
                        """
                        INSERT INTO GuardianMaster (GrdID, StudentCardNo)
                        SELECT @GuardianId, sl.CustomerId
                        FROM StudentLogin sl
                        WHERE sl.UserId = @UserId;
                        """,
                        new { GuardianId = request.GuardianId, UserId = userId.Value },
                        cancellationToken: cancellationToken));
            }
        }

        return AdminOperationResult.Ok("Student added successfully.");
    }

    public async Task<AdminOperationResult> DeleteAsync(decimal userId, CancellationToken cancellationToken = default)
    {
        if (userId <= 0) return AdminOperationResult.Fail("Id is required.");
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        try
        {
            await _allergyRepository.DeleteAllergiesAsync(userId, cancellationToken);
            await _orderTypeRepository.DeleteOrderTypesAsync(userId, cancellationToken);
            var rows = await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    "DELETE FROM StudentLogin WHERE UserId = @UserId;",
                    new { UserId = userId },
                    cancellationToken: cancellationToken));
            return rows > 0
                ? AdminOperationResult.Ok("Record deleted successfully.")
                : AdminOperationResult.Fail("Record was not deleted.");
        }
        catch
        {
            return AdminOperationResult.Fail("Record could not be deleted. It may be in use.");
        }
    }

    private static UpsertStudentRequest BuildUpsertRequest(
        StudentAdminSaveRequest request,
        string gradeText,
        SchoolRow school,
        string studCode)
    {
        var dob = request.DateOfBirth.HasValue
            ? request.DateOfBirth.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;

        return new UpsertStudentRequest
        {
            StudCode = studCode,
            StudUserName = studCode,
            StudPassword = string.IsNullOrWhiteSpace(request.Password) ? studCode : request.Password,
            StudCountryID = school.CountryId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StudSchoolID = school.SchoolId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StudStd = gradeText,
            StudDiv = request.Division.Trim(),
            Year = DateTime.Now.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StudFirstName = request.FirstName.Trim(),
            StudLastName = request.LastName?.Trim() ?? string.Empty,
            StudGender = request.Gender.Trim(),
            StudDOB = dob,
            CustomerId = studCode,
            GuardianID = request.GuardianId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SchoolCode = school.SchoolCode,
            IDCardStatus = "1"
        };
    }

    private sealed class LinkedStudentRow
    {
        public decimal UserId { get; init; }
        public string CustomerId { get; init; } = string.Empty;
    }

    private sealed class StudentAdminRow
    {
        public decimal UserId { get; init; }
        public string StudCode { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public int SchoolId { get; init; }
        public int GuardianId { get; init; }
        public string Gender { get; init; } = string.Empty;
        public string StudStd { get; init; } = string.Empty;
        public string Division { get; init; } = string.Empty;
        public DateTime? DateOfBirth { get; init; }
        public decimal? DailySpendLimit { get; init; }
        public decimal? WeeklySpendLimit { get; init; }
        public bool IsUnsubscribeLowBalNoti { get; init; }
        public bool IsActive { get; init; }
    }

    private sealed class SchoolRow
    {
        public int SchoolId { get; init; }
        public int CountryId { get; init; }
        public string SchoolCode { get; init; } = string.Empty;
    }
}
