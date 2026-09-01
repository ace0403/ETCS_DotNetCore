using System.Data;
using System.Data.Common;
using Dapper;
using ETCS.Shared.Application.Students;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Admin.Master.Schools;
using ETCS.Shared.Infrastructure.Data;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Admin.Master.Students;

using static ETCS.Shared.Infrastructure.Admin.DataTablePagingHelper;

namespace ETCS.Shared.Infrastructure.Admin.Master.Guardians;

public sealed class GuardianAdminRepository : IGuardianAdminRepository
{
    private const string GetAccountBalanceSp = "spGetAccountBalance";
    private const string TransferBalanceSp = "spTransferBalanceByCustomer";

    private const string SelectSql = """
        SELECT
            CAST(g.GrdID AS int) AS Id,
            CASE 
                WHEN ISNULL(g.FirstName,'') != '' THEN LTRIM(RTRIM(ISNULL(g.FirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(g.LastName, '')))
                ELSE LTRIM(RTRIM(ISNULL(g.Email, '')))
            END AS Name,
            LTRIM(RTRIM(ISNULL(g.Email, ''))) AS Email,
            g.MobileNo,
            LTRIM(RTRIM(ISNULL(g.UserName, g.Email))) AS Username,
            CASE WHEN ISNULL(g.Status, 1) = 1 AND ISNULL(g.Blacklist, 0) = 0 THEN 1 ELSE 0 END AS IsActive
        """;

    private const string FromSql = "FROM GuardianInfo g";

    private const string SearchFilterSql = """
        LTRIM(RTRIM(ISNULL(g.FirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(g.LastName, ''))) LIKE '%' + @Search + '%'
        OR LTRIM(RTRIM(ISNULL(g.Email, ''))) LIKE '%' + @Search + '%'
        OR LTRIM(RTRIM(ISNULL(g.UserName, g.Email))) LIKE '%' + @Search + '%'
        OR LTRIM(RTRIM(ISNULL(g.MobileNo, ''))) LIKE '%' + @Search + '%'
        """;

    private static readonly IReadOnlyDictionary<string, string> SortColumns = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        ["Id"] = "g.GrdID",
        ["Name"] = "g.FirstName",
        ["Email"] = "g.Email",
        ["MobileNo"] = "g.MobileNo",
        ["Username"] = "g.UserName",
        ["IsActive"] = "g.Status"
    };

    private const string GetSql = """
        SELECT TOP (1)
            CAST(g.GrdID AS int) AS Id,
            g.FirstName,
            g.LastName,
            g.Email,
            g.MobileNo,
            CASE WHEN ISNULL(g.Status, 1) = 1 AND ISNULL(g.Blacklist, 0) = 0 THEN 1 ELSE 0 END AS IsActive
        FROM GuardianInfo g
        WHERE g.GrdID = @Id;
        """;

    private const int DefaultCountryId = 1;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IStudentRepository _studentRepository;
    private readonly IStudentAllergyAdminRepository _allergyRepository;
    private readonly IStudentOrderTypeAdminRepository _orderTypeRepository;
    private readonly ISchoolOrderTypeAdminRepository _schoolOrderTypeRepository;

    public GuardianAdminRepository(
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

    public async Task<DataTableResponse<GuardianListItemDto>> GetDataAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        return await QueryPagedAsync<GuardianListItemDto>(
            dbConnection,
            SelectSql,
            FromSql,
            baseFilterSql: null,
            SearchFilterSql,
            SortColumns,
            "g.GrdID",
            request,
            cancellationToken: cancellationToken,
            defaultSortDirection: "DESC");
    }

    public async Task<GuardianSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return null;
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        var row = await dbConnection.QuerySingleOrDefaultAsync<GuardianSaveRequest>(
            new CommandDefinition(GetSql, new { Id = id }, cancellationToken: cancellationToken));
        if (row is not null)
        {
            row.Password = null;
        }
        return row;
    }

    public async Task<AdminOperationResult> SaveAsync(GuardianSaveRequest request, CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var userName = request.Email.Trim();

        if (request.Id > 0)
        {
            const string updateSql = """
                UPDATE GuardianInfo
                SET FirstName = @FirstName,
                    LastName = @LastName,
                    Email = @Email,
                    MobileNo = @MobileNo,
                    UserName = @UserName,
                    Password = CASE WHEN @PasswordHash IS NOT NULL THEN @PasswordHash ELSE Password END,
                    Status = CASE WHEN @IsActive = 1 THEN 1 ELSE 0 END,
                    Blacklist = CASE WHEN @IsActive = 1 THEN 0 ELSE 1 END
                WHERE GrdID = @Id;
                """;
            var passwordHash = string.IsNullOrWhiteSpace(request.Password)
                ? null
                : SecurityHelper.GetMd5Hash(request.Password);
            var rows = await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    updateSql,
                    new
                    {
                        request.Id,
                        request.FirstName,
                        request.LastName,
                        request.Email,
                        request.MobileNo,
                        UserName = userName,
                        PasswordHash = passwordHash,
                        request.IsActive
                    },
                    cancellationToken: cancellationToken));
            return rows > 0
                ? AdminOperationResult.Ok("Parent updated successfully.")
                : AdminOperationResult.Fail("Parent was not updated.");
        }

        const string existsSql = """
            SELECT TOP (1) CAST(GrdID AS int) FROM GuardianInfo
            WHERE LTRIM(RTRIM(ISNULL(Email, ''))) = @Email;
            """;
        var existing = await dbConnection.ExecuteScalarAsync<int?>(
            new CommandDefinition(existsSql, new { request.Email }, cancellationToken: cancellationToken));
        if (existing.HasValue)
        {
            return AdminOperationResult.Fail("Parent with this email already exists.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
            return AdminOperationResult.Fail("Password is required.");

        const string insertSql = """
            INSERT INTO GuardianInfo (FirstName, LastName, Email, MobileNo, UserName, Password, Blacklist, Status, GUID, RoleId)
            VALUES (@FirstName, @LastName, @Email, @MobileNo, @UserName, @PasswordHash, 0, 1, NEWID(), 5);
            """;
        var hash = SecurityHelper.GetMd5Hash(request.Password);
        var inserted = await dbConnection.ExecuteAsync(
            new CommandDefinition(
                insertSql,
                new
                {
                    request.FirstName,
                    request.LastName,
                    request.Email,
                    request.MobileNo,
                    UserName = userName,
                    PasswordHash = hash
                },
                cancellationToken: cancellationToken));
        return inserted > 0
            ? AdminOperationResult.Ok("Parent added successfully.")
            : AdminOperationResult.Fail("Parent was not added.");
    }

    public async Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default)
    {
        if (id <= 0) return AdminOperationResult.Fail("Id is required.");
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);
        try
        {
            var rows = await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    "DELETE FROM GuardianInfo WHERE GrdID = @Id;",
                    new { Id = id },
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

    public async Task<GuardianChildrenViewModel?> GetChildrenViewAsync(
        int guardianId,
        CancellationToken cancellationToken = default)
    {
        if (guardianId <= 0) return null;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        const string guardianSql = """
            SELECT TOP (1)
                CAST(g.GrdID AS int) AS GuardianId,
                CASE
                    WHEN ISNULL(g.FirstName, '') != '' THEN LTRIM(RTRIM(ISNULL(g.FirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(g.LastName, '')))
                    ELSE LTRIM(RTRIM(ISNULL(g.Email, '')))
                END AS GuardianName
            FROM GuardianInfo g
            WHERE g.GrdID = @GuardianId;
            """;

        var guardian = await dbConnection.QuerySingleOrDefaultAsync<GuardianChildrenViewModel>(
            new CommandDefinition(guardianSql, new { GuardianId = guardianId }, cancellationToken: cancellationToken));
        if (guardian is null) return null;

        var students = await _studentRepository.GetStudentsByGuardianAsync(guardianId, null, cancellationToken);
        var allergyNamesByStudent = await _allergyRepository.GetAllergyNamesByStudentIdsAsync(
            students.Select(s => s.UserId).ToList(),
            cancellationToken);
        var children = new List<GuardianChildListItemDto>(students.Count);
        foreach (var student in students)
        {
            var cardNo = !string.IsNullOrWhiteSpace(student.Cardid)
                ? student.Cardid.Trim()
                : student.StudCode?.Trim() ?? string.Empty;
            var balance = string.IsNullOrWhiteSpace(cardNo)
                ? 0m
                : await GetAccountBalanceAsync(dbConnection, cardNo, cancellationToken);

            allergyNamesByStudent.TryGetValue(student.UserId, out var allergies);

            children.Add(new GuardianChildListItemDto
            {
                UserId = student.UserId,
                Name = student.Name?.Trim() ?? string.Empty,
                CardNo = cardNo,
                Std = student.Std?.Trim() ?? string.Empty,
                SchoolName = student.SchoolName?.Trim() ?? string.Empty,
                Balance = balance,
                Status = student.Status?.Trim() ?? string.Empty,
                Allergies = allergies ?? []
            });
        }

        return new GuardianChildrenViewModel
        {
            GuardianId = guardian.GuardianId,
            GuardianName = guardian.GuardianName,
            Children = children
        };
    }

    public async Task<GuardianTransferViewModel?> GetTransferViewAsync(int guardianId, CancellationToken cancellationToken = default)
    {
        if (guardianId <= 0) return null;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var exists = await dbConnection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT TOP (1) CAST(GrdID AS int) FROM GuardianInfo WHERE GrdID = @GuardianId;",
                new { GuardianId = guardianId },
                cancellationToken: cancellationToken));
        if (!exists.HasValue) return null;

        var children = await GetChildrenForTransferAsync(dbConnection, guardianId, cancellationToken);
        return new GuardianTransferViewModel
        {
            GuardianId = guardianId,
            Children = children
        };
    }

    public async Task<AdminOperationResult> TransferBalanceAsync(
        GuardianBalanceTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.GuardianId <= 0)
            return AdminOperationResult.Fail("Parent is required.");
        if (request.FromUserId <= 0 || request.ToUserId <= 0)
            return AdminOperationResult.Fail("From and to student are required.");
        if (request.FromUserId == request.ToUserId)
            return AdminOperationResult.Fail("From and to student must be different.");
        if (request.Amount <= 0)
            return AdminOperationResult.Fail("Amount must be greater than zero.");

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var fromStudent = await GetTransferStudentAsync(dbConnection, request.GuardianId, request.FromUserId, cancellationToken);
        var toStudent = await GetTransferStudentAsync(dbConnection, request.GuardianId, request.ToUserId, cancellationToken);

        if (fromStudent is null || toStudent is null)
            return AdminOperationResult.Fail("Selected students were not found for this parent.");

        if (string.IsNullOrWhiteSpace(fromStudent.CustomerId) || string.IsNullOrWhiteSpace(toStudent.CustomerId))
            return AdminOperationResult.Fail("Student card account is not configured.");

        var fromCustomerId = fromStudent.CustomerId.Trim();
        var toCustomerId = toStudent.CustomerId.Trim();
        if (fromCustomerId.Length > 20 || toCustomerId.Length > 20)
            return AdminOperationResult.Fail("Customer ID exceeds allowed length.");

        var transactionId = $"BT{DateTime.UtcNow:yyyyMMddHHmmss}";
        if (transactionId.Length > 20)
            transactionId = transactionId[..20];

        var parameters = new DynamicParameters();
        parameters.Add("@FromCustomerID", fromCustomerId, DbType.String, size: 20);
        parameters.Add("@ToCustomerID", toCustomerId, DbType.String, size: 20);
        parameters.Add("@Amount", request.Amount, DbType.Decimal);
        parameters.Add("@TransactionID", transactionId, DbType.String, size: 20);
        parameters.Add("@Message", dbType: DbType.String, size: 200, direction: ParameterDirection.Output);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                TransferBalanceSp,
                parameters,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        var message = parameters.Get<string>("@Message")?.Trim();
        if (!string.IsNullOrEmpty(message)
            && message.Contains("successful", StringComparison.OrdinalIgnoreCase))
        {
            return AdminOperationResult.Ok(message);
        }

        return AdminOperationResult.Fail(
            string.IsNullOrWhiteSpace(message) ? "Balance transfer failed." : message);
    }

    private static async Task<IReadOnlyList<GuardianChildTransferItemDto>> GetChildrenForTransferAsync(
        DbConnection dbConnection,
        int guardianId,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT
                sl.UserId,
                LTRIM(RTRIM(ISNULL(sl.StudFirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(sl.StudLastName, ''))) AS Name,
                LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) AS CustomerId
            FROM StudentLogin sl
            WHERE sl.GrdID = @GuardianId
              AND ISNULL(sl.BlackList, 0) = 0
            ORDER BY sl.StudFirstName, sl.StudLastName;
            """;

        var students = (await dbConnection.QueryAsync<TransferStudentRow>(
            new CommandDefinition(sql, new { GuardianId = guardianId }, cancellationToken: cancellationToken))).ToList();

        var children = new List<GuardianChildTransferItemDto>(students.Count);
        foreach (var student in students)
        {
            var balance = string.IsNullOrWhiteSpace(student.CustomerId)
                ? 0m
                : await GetAccountBalanceAsync(dbConnection, student.CustomerId, cancellationToken);

            children.Add(new GuardianChildTransferItemDto
            {
                UserId = student.UserId,
                Name = student.Name,
                Balance = balance
            });
        }

        return children;
    }

    private sealed class TransferStudentRow
    {
        public decimal UserId { get; init; }
        public string Name { get; init; } = string.Empty;
        public string CustomerId { get; init; } = string.Empty;
    }

    private static async Task<TransferStudentRow?> GetTransferStudentAsync(
        DbConnection dbConnection,
        int guardianId,
        decimal userId,
        CancellationToken cancellationToken,
        DbTransaction? transaction = null)
    {
        const string sql = """
            SELECT TOP (1)
                sl.UserId,
                LTRIM(RTRIM(ISNULL(sl.StudFirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(sl.StudLastName, ''))) AS Name,
                LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) AS CustomerId
            FROM StudentLogin sl
            WHERE sl.GrdID = @GuardianId
              AND sl.UserId = @UserId
              AND ISNULL(sl.BlackList, 0) = 0;
            """;

        return await dbConnection.QuerySingleOrDefaultAsync<TransferStudentRow>(
            new CommandDefinition(
                sql,
                new { GuardianId = guardianId, UserId = userId },
                transaction: transaction,
                cancellationToken: cancellationToken));
    }

    private static async Task<decimal> GetAccountBalanceAsync(
        DbConnection dbConnection,
        string customerId,
        CancellationToken cancellationToken)
    {
        var row = await dbConnection.QueryFirstOrDefaultAsync<AccountBalanceRow>(
            new CommandDefinition(
                GetAccountBalanceSp,
                new { CustomerID = customerId.Trim() },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken));

        return row?.BalPrepaid ?? 0m;
    }

    private sealed class AccountBalanceRow
    {
        public decimal BalPrepaid { get; init; }
    }

    public async Task<GuardianAddStudentViewModel?> GetAddStudentViewAsync(
        int guardianId,
        CancellationToken cancellationToken = default)
    {
        if (guardianId <= 0) return null;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        const string guardianSql = """
            SELECT TOP (1)
                CAST(g.GrdID AS int) AS GuardianId
            FROM GuardianInfo g
            WHERE g.GrdID = @GuardianId;
            """;

        var guardian = await dbConnection.QuerySingleOrDefaultAsync<GuardianAddStudentViewModel>(
            new CommandDefinition(guardianSql, new { GuardianId = guardianId }, cancellationToken: cancellationToken));
        if (guardian is null) return null;

        var grades = (await _studentRepository.GetAllGradesAsync(cancellationToken))
            .Select(g => new GuardianAddStudentGradeOption { Id = g.Id, Grade = g.Grade })
            .ToList();

        var schools = (await dbConnection.QueryAsync<GuardianAddStudentSchoolOption>(
            new CommandDefinition(
                """
                SELECT
                    SchoolId,
                    LTRIM(RTRIM(SchoolName)) AS SchoolName,
                    LTRIM(RTRIM(ISNULL(SchoolCode, ''))) AS SchoolCode,
                    ISNULL(CountryId, @DefaultCountryId) AS CountryId
                FROM SchoolInfo
                ORDER BY SchoolName;
                """,
                new { DefaultCountryId },
                cancellationToken: cancellationToken))).ToList();

        return new GuardianAddStudentViewModel
        {
            GuardianId = guardian.GuardianId,
            Grades = grades,
            Schools = schools
        };
    }

    public async Task<AdminOperationResult> AddStudentAsync(
        GuardianAddStudentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.GuardianId <= 0)
            return AdminOperationResult.Fail("Parent is required.");
        if (string.IsNullOrWhiteSpace(request.StudentCardNo))
            return AdminOperationResult.Fail("Student card number is required.");
        if (string.IsNullOrWhiteSpace(request.FirstName))
            return AdminOperationResult.Fail("First name is required.");
        if (request.GradeId <= 0)
            return AdminOperationResult.Fail("Class / grade is required.");
        if (string.IsNullOrWhiteSpace(request.Division))
            return AdminOperationResult.Fail("Division is required.");
        if (request.SchoolId <= 0)
            return AdminOperationResult.Fail("School is required.");

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var guardianRow = await dbConnection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                """
                SELECT TOP (1) CAST(g.GrdID AS int)
                FROM GuardianInfo g
                WHERE g.GrdID = @GuardianId;
                """,
                new { request.GuardianId },
                cancellationToken: cancellationToken));
        if (!guardianRow.HasValue)
            return AdminOperationResult.Fail("Parent was not found.");

        var grades = await _studentRepository.GetAllGradesAsync(cancellationToken);
        var grade = grades.FirstOrDefault(g => g.Id == request.GradeId);
        if (grade is null)
            return AdminOperationResult.Fail("Selected class / grade was not found.");

        var school = await dbConnection.QuerySingleOrDefaultAsync<SchoolRow>(
            new CommandDefinition(
                """
                SELECT TOP (1)
                    SchoolId,
                    ISNULL(CountryId, @DefaultCountryId) AS CountryId,
                    LTRIM(RTRIM(ISNULL(SchoolCode, ''))) AS SchoolCode
                FROM SchoolInfo
                WHERE SchoolId = @SchoolId;
                """,
                new { request.SchoolId, DefaultCountryId },
                cancellationToken: cancellationToken));
        if (school is null)
            return AdminOperationResult.Fail("Selected school was not found.");

        var cardError = StudentCardNumber.ResolveForCreate(request.StudentCardNo, school.SchoolCode, out var cardNo);
        if (cardError is not null)
            return AdminOperationResult.Fail(cardError);

        if (await StudentCardNumber.IsTakenAsync(dbConnection, cardNo, excludeUserId: null, cancellationToken))
            return AdminOperationResult.Fail(StudentCardNumber.DuplicateMessage);

        var schoolOrderTypeError = StudentOrderTypeValidation.ValidateAgainstSchool(
            await _schoolOrderTypeRepository.GetOrderTypeIdsAsync(request.SchoolId, cancellationToken),
            request.OrderTypeIds);
        if (schoolOrderTypeError is not null)
            return AdminOperationResult.Fail(schoolOrderTypeError);

        var gender = string.IsNullOrWhiteSpace(request.Gender) ? "Male" : request.Gender.Trim();
        var dob = request.DateOfBirth.HasValue
            ? request.DateOfBirth.Value.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)
            : string.Empty;

        var upsert = new UpsertStudentRequest
        {
            StudCode = cardNo,
            StudUserName = cardNo,
            StudPassword = cardNo,
            StudCountryID = school.CountryId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StudSchoolID = school.SchoolId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StudStd = grade.Grade,
            StudDiv = request.Division.Trim(),
            Year = DateTime.Now.Year.ToString(System.Globalization.CultureInfo.InvariantCulture),
            StudFirstName = request.FirstName.Trim(),
            StudLastName = request.LastName?.Trim() ?? string.Empty,
            StudGender = gender,
            StudDOB = dob,
            CustomerId = cardNo,
            GuardianID = request.GuardianId.ToString(System.Globalization.CultureInfo.InvariantCulture),
            SchoolCode = school.SchoolCode,
            IDCardStatus = "1"
        };

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
                WHERE LTRIM(RTRIM(ISNULL(CustomerId, ''))) = @CardNo
                   OR LTRIM(RTRIM(ISNULL(StudCode, ''))) = @CardNo
                ORDER BY UserId DESC;
                """,
                new { CardNo = cardNo },
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

        return AdminOperationResult.Ok("Student added successfully.");
    }

    public async Task<GuardianEditStudentViewModel?> GetEditStudentViewAsync(
        int guardianId,
        decimal userId,
        CancellationToken cancellationToken = default)
    {
        if (guardianId <= 0 || userId <= 0) return null;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var guardianExists = await dbConnection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT TOP (1) CAST(g.GrdID AS int) FROM GuardianInfo g WHERE g.GrdID = @GuardianId;",
                new { GuardianId = guardianId },
                cancellationToken: cancellationToken));
        if (!guardianExists.HasValue) return null;

        var row = await dbConnection.QuerySingleOrDefaultAsync<EditStudentRow>(
            new CommandDefinition(
                """
                SELECT TOP (1)
                    sl.UserId,
                    LTRIM(RTRIM(ISNULL(NULLIF(LTRIM(RTRIM(sl.CustomerId)), ''), sl.StudCode))) AS StudentCardNo,
                    LTRIM(RTRIM(ISNULL(sl.StudFirstName, ''))) AS FirstName,
                    LTRIM(RTRIM(ISNULL(sl.StudLastName, ''))) AS LastName,
                    ISNULL(sl.StudSchoolId, 0) AS SchoolId,
                    LTRIM(RTRIM(ISNULL(sl.StudGender, 'Male'))) AS Gender,
                    LTRIM(RTRIM(ISNULL(sl.StudStd, ''))) AS StudStd,
                    LTRIM(RTRIM(ISNULL(sl.StudDiv, ''))) AS Division,
                    sl.StudDateOfBirth AS DateOfBirth,
                    sl.DailyLimit AS DailySpendLimit,
                    sl.WeeklyLimit AS WeeklySpendLimit,
                    CAST(ISNULL(sl.IsUnsubscribeLowBalNoti, 0) AS bit) AS IsUnsubscribeLowBalNoti
                FROM StudentLogin sl
                INNER JOIN GuardianMaster gm ON sl.CustomerId = gm.StudentCardNo
                WHERE gm.GrdID = @GuardianId
                  AND sl.UserId = @UserId;
                """,
                new { GuardianId = guardianId, UserId = userId },
                cancellationToken: cancellationToken));
        if (row is null) return null;

        var grades = (await _studentRepository.GetAllGradesAsync(cancellationToken))
            .Select(g => new GuardianAddStudentGradeOption { Id = g.Id, Grade = g.Grade })
            .ToList();
        var gradeId = grades.FirstOrDefault(g =>
            string.Equals(g.Grade, row.StudStd, StringComparison.OrdinalIgnoreCase))?.Id ?? 0;

        var schools = (await dbConnection.QueryAsync<GuardianAddStudentSchoolOption>(
            new CommandDefinition(
                """
                SELECT
                    SchoolId,
                    LTRIM(RTRIM(SchoolName)) AS SchoolName,
                    LTRIM(RTRIM(ISNULL(SchoolCode, ''))) AS SchoolCode,
                    ISNULL(CountryId, @DefaultCountryId) AS CountryId
                FROM SchoolInfo
                ORDER BY SchoolName;
                """,
                new { DefaultCountryId },
                cancellationToken: cancellationToken))).ToList();

        var allergyIds = await _allergyRepository.GetAllergyIdsAsync(userId, cancellationToken);
        var orderTypeIds = await _orderTypeRepository.GetOrderTypeIdsAsync(userId, cancellationToken);

        return new GuardianEditStudentViewModel
        {
            Student = new GuardianEditStudentRequest
            {
                UserId = row.UserId,
                GuardianId = guardianId,
                StudentCardNo = row.StudentCardNo,
                FirstName = row.FirstName,
                LastName = row.LastName,
                SchoolId = row.SchoolId,
                Gender = row.Gender,
                GradeId = gradeId,
                Division = row.Division,
                DateOfBirth = row.DateOfBirth,
                DailySpendLimit = row.DailySpendLimit,
                WeeklySpendLimit = row.WeeklySpendLimit,
                LowBalanceEmailNotification = !row.IsUnsubscribeLowBalNoti,
                AllergyItemIds = allergyIds.ToList(),
                OrderTypeIds = orderTypeIds.ToList()
            },
            Grades = grades,
            Schools = schools
        };
    }

    public async Task<AdminOperationResult> EditStudentAsync(
        GuardianEditStudentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.GuardianId <= 0)
            return AdminOperationResult.Fail("Parent is required.");
        if (request.UserId <= 0)
            return AdminOperationResult.Fail("Student is required.");
        if (string.IsNullOrWhiteSpace(request.StudentCardNo))
            return AdminOperationResult.Fail("Student card number is required.");
        if (string.IsNullOrWhiteSpace(request.FirstName))
            return AdminOperationResult.Fail("First name is required.");
        if (request.GradeId <= 0)
            return AdminOperationResult.Fail("Class / grade is required.");
        if (string.IsNullOrWhiteSpace(request.Division))
            return AdminOperationResult.Fail("Division is required.");
        if (request.SchoolId <= 0)
            return AdminOperationResult.Fail("School is required.");

        var cardError = StudentCardNumber.ValidateForEdit(request.StudentCardNo, out var cardNo);
        if (cardError is not null)
            return AdminOperationResult.Fail(cardError);

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var linkedStudent = await dbConnection.QuerySingleOrDefaultAsync<LinkedStudentRow>(
            new CommandDefinition(
                """
                SELECT TOP (1)
                    sl.UserId,
                    LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) AS CustomerId
                FROM StudentLogin sl
                INNER JOIN GuardianMaster gm ON sl.CustomerId = gm.StudentCardNo
                WHERE gm.GrdID = @GuardianId
                  AND sl.UserId = @UserId;
                """,
                new { GuardianId = request.GuardianId, UserId = request.UserId },
                cancellationToken: cancellationToken));
        if (linkedStudent is null)
            return AdminOperationResult.Fail("Student was not found for this parent.");

        if (await StudentCardNumber.IsTakenAsync(dbConnection, cardNo, request.UserId, cancellationToken))
            return AdminOperationResult.Fail(StudentCardNumber.DuplicateMessage);

        var grades = await _studentRepository.GetAllGradesAsync(cancellationToken);
        var grade = grades.FirstOrDefault(g => g.Id == request.GradeId);
        if (grade is null)
            return AdminOperationResult.Fail("Selected class / grade was not found.");

        var school = await dbConnection.QuerySingleOrDefaultAsync<SchoolRow>(
            new CommandDefinition(
                """
                SELECT TOP (1)
                    SchoolId,
                    ISNULL(CountryId, @DefaultCountryId) AS CountryId,
                    LTRIM(RTRIM(ISNULL(SchoolCode, ''))) AS SchoolCode
                FROM SchoolInfo
                WHERE SchoolId = @SchoolId;
                """,
                new { request.SchoolId, DefaultCountryId },
                cancellationToken: cancellationToken));
        if (school is null)
            return AdminOperationResult.Fail("Selected school was not found.");

        var schoolOrderTypeError = StudentOrderTypeValidation.ValidateAgainstSchool(
            await _schoolOrderTypeRepository.GetOrderTypeIdsAsync(request.SchoolId, cancellationToken),
            request.OrderTypeIds);
        if (schoolOrderTypeError is not null)
            return AdminOperationResult.Fail(schoolOrderTypeError);

        var gender = string.IsNullOrWhiteSpace(request.Gender) ? "Male" : request.Gender.Trim();
        var oldCustomerId = linkedStudent.CustomerId;

        await using var transaction = await dbConnection.BeginTransactionAsync(cancellationToken);
        try
        {
            var rows = await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    """
                    UPDATE StudentLogin
                    SET StudCode = @StudCode,
                        StudUserName = @StudCode,
                        CustomerId = @StudCode,
                        StudFirstName = @FirstName,
                        StudLastName = @LastName,
                        StudSchoolId = @SchoolId,
                        GrdID = @GuardianId,
                        StudGender = @Gender,
                        StudStd = @StudStd,
                        StudDiv = @Division,
                        StudDateOfBirth = @DateOfBirth,
                        DailyLimit = @DailySpendLimit,
                        WeeklyLimit = @WeeklySpendLimit,
                        IsUnsubscribeLowBalNoti = @IsUnsubscribeLowBalNoti
                    WHERE UserId = @UserId;
                    """,
                    new
                    {
                        request.UserId,
                        StudCode = cardNo,
                        request.FirstName,
                        LastName = request.LastName ?? string.Empty,
                        request.SchoolId,
                        request.GuardianId,
                        Gender = gender,
                        StudStd = grade.Grade,
                        request.Division,
                        request.DateOfBirth,
                        DailySpendLimit = request.DailySpendLimit ?? 0m,
                        WeeklySpendLimit = request.WeeklySpendLimit ?? 0m,
                        IsUnsubscribeLowBalNoti = !request.LowBalanceEmailNotification
                    },
                    transaction: transaction,
                    cancellationToken: cancellationToken));
            if (rows <= 0)
            {
                await transaction.RollbackAsync(cancellationToken);
                return AdminOperationResult.Fail("Student was not updated.");
            }

            var newCustomerId = await dbConnection.ExecuteScalarAsync<string>(
                new CommandDefinition(
                    "SELECT TOP (1) CustomerId FROM StudentLogin WHERE UserId = @UserId;",
                    new { request.UserId },
                    transaction: transaction,
                    cancellationToken: cancellationToken));

            await StudentIdMemberCustomerIdSync.UpdateCustomerIdAsync(
                dbConnection,
                oldCustomerId,
                newCustomerId ?? string.Empty,
                cancellationToken,
                transaction);

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

    private sealed class EditStudentRow
    {
        public decimal UserId { get; init; }
        public string StudentCardNo { get; init; } = string.Empty;
        public string FirstName { get; init; } = string.Empty;
        public string LastName { get; init; } = string.Empty;
        public int SchoolId { get; init; }
        public string Gender { get; init; } = "Male";
        public string StudStd { get; init; } = string.Empty;
        public string Division { get; init; } = string.Empty;
        public DateTime? DateOfBirth { get; init; }
        public decimal? DailySpendLimit { get; init; }
        public decimal? WeeklySpendLimit { get; init; }
        public bool IsUnsubscribeLowBalNoti { get; init; }
    }

    private sealed class LinkedStudentRow
    {
        public decimal UserId { get; init; }
        public string CustomerId { get; init; } = string.Empty;
    }

    private sealed class SchoolRow
    {
        public int SchoolId { get; init; }
        public int CountryId { get; init; }
        public string SchoolCode { get; init; } = string.Empty;
    }
}
