using System.Data;
using System.Data.Common;
using Dapper;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Students;

public sealed class StudentRepository : IStudentRepository
{
    private const string StudentSummarySp = "spRptStudentSummary";
    private const string GuardianStudentsSp = "GetStudentInfoByGuardianIDMeal_New";
    private const string GetAllGradesSp = "spGetAllGrades";
    private const string SchoolsByCountrySp = "spSelectSchoolInfoByCountryID_New";
    private const string InsertStudentInfoSp = "spInsertStudentInfo";
    private const string StudentSchoolIdSql = """
        SELECT TOP (1)
            CAST(ISNULL(sl.StudSchoolId, 0) AS INT) AS SchoolId
        FROM StudentLogin sl
        WHERE sl.UserId = @StudentId;
        """;

    private const string StudentStudStdSql = """
        SELECT TOP (1)
            LTRIM(RTRIM(ISNULL(sl.StudStd, ''))) AS StudStd
        FROM StudentLogin sl
        WHERE sl.UserId = @StudentId;
        """;

    private const string SchoolCodeByIdSql = """
        SELECT TOP (1)
            LTRIM(RTRIM(ISNULL(s.Schoolcode, ''))) AS SchoolCode
        FROM SchoolInfo s
        WHERE s.SchoolId = @SchoolId;
        """;

    private const string StudentMinimumTopupSql = """
        SELECT CAST(ISNULL(s.MinimumTopup, 0) AS decimal(18,2))
        FROM StudentLogin sl
        INNER JOIN SchoolInfo s ON s.SchoolId = sl.StudSchoolId
        WHERE sl.UserId = @StudentId;
        """;

    private const string StudentCardBalanceMetaSql = """
        SELECT
            LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) AS CustomerId,
            CAST(ISNULL(s.MinimumTopup, 0) AS decimal(18,2)) AS MinimumTopupAmount,
            LTRIM(RTRIM(ISNULL(s.SchoolLogo, ''))) AS SchoolLogoFileName
        FROM StudentLogin sl
        LEFT JOIN SchoolInfo s
            ON s.SchoolId = TRY_CONVERT(INT, NULLIF(LTRIM(RTRIM(CONVERT(varchar(50), sl.StudSchoolId))), ''))
            OR (
                LTRIM(RTRIM(ISNULL(s.Schoolcode, ''))) <> ''
                AND LTRIM(RTRIM(s.Schoolcode)) = LTRIM(RTRIM(CONVERT(varchar(50), sl.StudSchoolId)))
            )
        WHERE sl.UserId = @StudentId;
        """;

    private const string SchoolLogoByNameSql = """
        SELECT TOP (1)
            LTRIM(RTRIM(ISNULL(s.SchoolLogo, ''))) AS SchoolLogoFileName
        FROM SchoolInfo s
        WHERE LTRIM(RTRIM(ISNULL(s.SchoolName, ''))) = LTRIM(RTRIM(@SchoolName))
          AND LTRIM(RTRIM(ISNULL(s.SchoolLogo, ''))) <> '';
        """;

    private const string GuardianBasicByStudentSql = """
        SELECT TOP (1)
            sl.GrdId AS GuardianId,
            LTRIM(RTRIM(ISNULL(g.Email, ''))) AS Email,
            LTRIM(RTRIM(ISNULL(g.FirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(g.LastName, ''))) AS GuardianName,
            LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) AS CustomerId
        FROM StudentLogin sl
        INNER JOIN GuardianInfo g ON g.GrdID = sl.GrdId
        WHERE sl.UserId = @StudentId;
        """;

    private const string GuardianBasicByCustomerSql = """
        SELECT TOP (1)
            sl.GrdId AS GuardianId,
            LTRIM(RTRIM(ISNULL(g.Email, ''))) AS Email,
            LTRIM(RTRIM(ISNULL(g.FirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(g.LastName, ''))) AS GuardianName,
            LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) AS CustomerId
        FROM StudentLogin sl
        INNER JOIN GuardianInfo g ON g.GrdID = sl.GrdId
        WHERE sl.CustomerId = @CustomerId;
        """;

    private const string StudentIdentityByCustomerSql = """
        SELECT TOP (1)
            CAST(sl.UserId AS INT) AS UserId,
            sl.GrdId AS GuardianId,
            LTRIM(RTRIM(ISNULL(g.Email, ''))) AS Email,
            LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) AS CustomerId,
            LTRIM(RTRIM(ISNULL(sl.StudFirstName, ''))) + ' ' + LTRIM(RTRIM(ISNULL(sl.StudLastName, ''))) AS StudentName
        FROM StudentLogin sl
        INNER JOIN GuardianInfo g ON g.GrdID = sl.GrdId
        WHERE sl.CustomerId = @CustomerId;
        """;

    private readonly IDbConnectionFactory _connectionFactory;

    public StudentRepository(IDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<List<StudentSummaryDto>> GetStudentSummaryAsync(
        string? studentId,
        int guardianId,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = (await dbConnection.QueryAsync<StudentSummaryDto>(
            new CommandDefinition(
                StudentSummarySp,
                new { StudID = studentId, GrdID = guardianId },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken))).ToList();

        return rows.Select(TrimSummaryStrings).ToList();
    }

    public async Task<List<StudentListingDto>> GetStudentsByGuardianAsync(
        int guardianId,
        string? customerId,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = (await dbConnection.QueryAsync<StudentListingDto>(
            new CommandDefinition(
                GuardianStudentsSp,
                new { GuardianID = guardianId, CustomerID = customerId ?? string.Empty },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken))).ToList();

        return rows.Select(TrimListingStrings).ToList();
    }

    public async Task<IReadOnlyList<StudentBasicListItemDto>> GetStudentBasicListByGuardianAsync(
        int guardianId,
        CancellationToken cancellationToken)
    {
        var rows = await GetStudentsByGuardianAsync(guardianId, null, cancellationToken);
        return rows
            .Select(row => new StudentBasicListItemDto(
                row.UserId,
                string.IsNullOrWhiteSpace(row.StudCode)
                    ? row.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                    : row.StudCode.Trim(),
                guardianId,
                row.Name?.Trim() ?? string.Empty))
            .ToList();
    }

    public async Task<int?> GetStudentSchoolIdAsync(int studentId, CancellationToken cancellationToken = default)
    {
        if (studentId <= 0)
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var schoolId = await dbConnection.QueryFirstOrDefaultAsync<int?>(
            new CommandDefinition(
                StudentSchoolIdSql,
                new { StudentId = studentId },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

        return schoolId is > 0 ? schoolId : null;
    }

    public async Task<int?> ResolveStudentGradeIdAsync(int studentId, CancellationToken cancellationToken = default)
    {
        if (studentId <= 0)
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var studStd = await dbConnection.QueryFirstOrDefaultAsync<string>(
            new CommandDefinition(
                StudentStudStdSql,
                new { StudentId = studentId },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

        if (string.IsNullOrWhiteSpace(studStd))
        {
            return null;
        }

        var schoolId = await GetStudentSchoolIdAsync(studentId, cancellationToken);
        int? schoolCode = null;
        if (schoolId is > 0)
        {
            var schoolCodeText = await dbConnection.QueryFirstOrDefaultAsync<string>(
                new CommandDefinition(
                    SchoolCodeByIdSql,
                    new { SchoolId = schoolId.Value },
                    cancellationToken: cancellationToken));

            if (!string.IsNullOrWhiteSpace(schoolCodeText)
                && int.TryParse(schoolCodeText.Trim(), out var parsedSchoolCode))
            {
                schoolCode = parsedSchoolCode;
            }
        }

        var grades = await GetAllGradesAsync(cancellationToken);
        var normalizedStudStd = studStd.Trim();
        var match = grades.FirstOrDefault(grade =>
            string.Equals(grade.Grade.Trim(), normalizedStudStd, StringComparison.OrdinalIgnoreCase)
            && (grade.SchoolCode is null || schoolCode is null || grade.SchoolCode == schoolCode));

        return match?.Id is > 0 ? match.Id : null;
    }

    public async Task<decimal?> GetStudentMinimumTopupAsync(int studentId, CancellationToken cancellationToken = default)
    {
        if (studentId <= 0)
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.QueryFirstOrDefaultAsync<decimal?>(
            new CommandDefinition(
                StudentMinimumTopupSql,
                new { StudentId = studentId },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));
    }

    public async Task<StudentCardBalanceMetaDto?> GetStudentCardBalanceMetaAsync(
        int studentId,
        CancellationToken cancellationToken = default)
    {
        if (studentId <= 0)
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var row = await dbConnection.QueryFirstOrDefaultAsync<StudentCardBalanceMetaRow>(
            new CommandDefinition(
                StudentCardBalanceMetaSql,
                new { StudentId = studentId },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        return new StudentCardBalanceMetaDto(
            string.IsNullOrWhiteSpace(row.CustomerId) ? null : row.CustomerId.Trim(),
            row.MinimumTopupAmount,
            string.IsNullOrWhiteSpace(row.SchoolLogoFileName) ? null : row.SchoolLogoFileName.Trim());
    }

    public async Task<string?> GetSchoolLogoFileNameByNameAsync(
        string schoolName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(schoolName))
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var fileName = await dbConnection.QueryFirstOrDefaultAsync<string?>(
            new CommandDefinition(
                SchoolLogoByNameSql,
                new { SchoolName = schoolName.Trim() },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

        return string.IsNullOrWhiteSpace(fileName) ? null : fileName.Trim();
    }

    private sealed class StudentCardBalanceMetaRow
    {
        public string? CustomerId { get; init; }

        public decimal? MinimumTopupAmount { get; init; }

        public string? SchoolLogoFileName { get; init; }
    }

    public async Task<bool?> GetSchoolEmailAlertsEnabledAsync(int schoolId, CancellationToken cancellationToken = default)
    {
        if (schoolId <= 0)
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.QueryFirstOrDefaultAsync<bool?>(
            new CommandDefinition(
                """
                SELECT CAST(ISNULL(s.EmailAlterts, 0) AS bit)
                FROM SchoolInfo s
                WHERE s.SchoolId = @SchoolId
                """,
                new { SchoolId = schoolId },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));
    }

    public async Task<StudentGuardianBasicDetailDto?> GetGuardianBasicDetailByStudentIdAsync(
        string studentId,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var row = await dbConnection.QueryFirstOrDefaultAsync<StudentGuardianBasicDetailDto>(
            new CommandDefinition(
                GuardianBasicByStudentSql,
                new { StudentId = studentId.Trim() },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        return row with
        {
            Email = row.Email.Trim(),
            GuardianName = row.GuardianName.Trim(),
            CustomerId = row.CustomerId.Trim()
        };
    }

    public async Task<StudentGuardianBasicDetailDto?> GetGuardianBasicDetailByCustomerIdAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var row = await dbConnection.QueryFirstOrDefaultAsync<StudentGuardianBasicDetailDto>(
            new CommandDefinition(
                GuardianBasicByCustomerSql,
                new { CustomerId = customerId.Trim() },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        return row with
        {
            Email = row.Email.Trim(),
            GuardianName = row.GuardianName.Trim(),
            CustomerId = row.CustomerId.Trim()
        };
    }

    public async Task<StudentIdentityByCustomerDto?> GetStudentIdentityByCustomerIdAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var row = await dbConnection.QueryFirstOrDefaultAsync<StudentIdentityByCustomerDto>(
            new CommandDefinition(
                StudentIdentityByCustomerSql,
                new { CustomerId = customerId.Trim() },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        return row with
        {
            Email = row.Email.Trim(),
            CustomerId = row.CustomerId.Trim(),
            StudentName = row.StudentName.Trim()
        };
    }

    public async Task<decimal> GetPrepaidBalanceByCustomerIdAsync(
        string customerId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(customerId))
        {
            return 0m;
        }

        const string sql = """
            SELECT TOP (1)
                CAST(ISNULL(m.BalPrepaid, 0) AS decimal(18,2))
            FROM IdMember m
            WHERE m.IdCardStatus = 1
              AND LTRIM(RTRIM(m.CustomerID)) = LTRIM(RTRIM(@CustomerId));
            """;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.ExecuteScalarAsync<decimal?>(
            new CommandDefinition(
                sql,
                new { CustomerId = customerId.Trim() },
                commandType: CommandType.Text,
                cancellationToken: cancellationToken)) ?? 0m;
    }

    public async Task<IReadOnlyList<GradeListItemDto>> GetAllGradesAsync(CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = (await dbConnection.QueryAsync<GradeListItemDto>(
            new CommandDefinition(
                GetAllGradesSp,
                parameters: null,
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken))).ToList();

        return rows
            .Select(g => new GradeListItemDto(g.Id, g.Grade.Trim(), g.SchoolCode))
            .ToList();
    }

    public async Task<IReadOnlyList<SchoolListItemDto>> GetSchoolsByCountryAsync(
        int countryId,
        string? schoolId,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = (await dbConnection.QueryAsync<SchoolListItemDto>(
            new CommandDefinition(
                SchoolsByCountrySp,
                new { CountryId = countryId, SchoolId = schoolId ?? string.Empty },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken))).ToList();

        return rows.Select(TrimSchoolRow).ToList();
    }

    public async Task SaveStudentAsync(UpsertStudentRequest request, bool isInsert, CancellationToken cancellationToken)
    {
        if (!isInsert)
        {
            throw new InvalidOperationException(
                "spInsertStudentInfo only supports create. Use a direct StudentLogin UPDATE for edits.");
        }

        if (string.IsNullOrWhiteSpace(request.StudPassword))
        {
            throw new ArgumentException("StudPassword is required when creating a student.", nameof(request));
        }

        object passwordValue = DBNull.Value;
        if (!string.IsNullOrWhiteSpace(request.StudPassword))
        {
            passwordValue = SecurityHelper.GetMd5Hash(request.StudPassword);
        }

        var p = new DynamicParameters();
        p.Add("StudCode", TrimFixed(request.StudCode, 50), DbType.StringFixedLength, size: 50);
        p.Add("StudUserName", TrimFixed(request.StudUserName, 50), DbType.StringFixedLength, size: 50);
        p.Add("StudPassword", passwordValue, DbType.String, size: 128);
        p.Add("StudCountryID", Trim(request.StudCountryID, 128), DbType.String, size: 128);
        p.Add("StudSchoolID", Trim(request.StudSchoolID, 128), DbType.String, size: 128);
        p.Add("StudStd", Trim(request.StudStd, 128), DbType.String, size: 128);
        p.Add("StudDiv", Trim(request.StudDiv, 128), DbType.String, size: 128);
        p.Add("Year", Trim(request.Year, 4), DbType.String, size: 4);
        p.Add("StudFirstName", Trim(request.StudFirstName, 128), DbType.String, size: 128);
        p.Add("StudLastName", Trim(request.StudLastName, 128), DbType.String, size: 128);
        p.Add("StudAdd1", Trim(request.StudAdd1, 128), DbType.String, size: 128);
        p.Add("StudAdd2", Trim(request.StudAdd2, 128), DbType.String, size: 128);
        p.Add("StudCity", Trim(request.StudCity, 128), DbType.String, size: 128);
        p.Add("StudState", Trim(request.StudState, 128), DbType.String, size: 128);
        p.Add("StudCountry", Trim(request.StudCountry, 128), DbType.String, size: 128);
        p.Add("StudDOB", Trim(request.StudDOB, 128), DbType.String, size: 128);
        p.Add("StudGender", Trim(request.StudGender, 128), DbType.String, size: 128);
        p.Add("StudEmailId", Trim(request.StudEmailId, 40), DbType.AnsiString, size: 40);
        p.Add("StudSecutityQue", TrimFixed(request.StudSecutityQue, 40), DbType.StringFixedLength, size: 40);
        p.Add("StudSecurityAns", TrimFixed(request.StudSecurityAns, 40), DbType.StringFixedLength, size: 40);
        p.Add("StudMobile", TrimFixed(request.StudMobile, 40), DbType.StringFixedLength, size: 40);
        p.Add("BlackListReason", Trim(request.BlackListReason, 500), DbType.AnsiString, size: 500);
        p.Add("CustomerId", TrimFixed(request.CustomerId, 50), DbType.AnsiStringFixedLength, size: 50);
        var idCardStatus = int.TryParse(request.IDCardStatus?.Trim(), out var parsedStatus) ? parsedStatus : 1;
        p.Add("IDCardStatus", idCardStatus, DbType.Int32);
        p.Add("SchoolCode", TrimFixed(request.SchoolCode, 50), DbType.AnsiStringFixedLength, size: 50);
        p.Add("GuardianID", Trim(request.GuardianID, 50), DbType.String, size: 50);

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var cardNo = string.IsNullOrWhiteSpace(request.CustomerId) ? request.StudCode : request.CustomerId;
        if (await StudentCardNumber.IsTakenAsync(dbConnection, cardNo, excludeUserId: null, cancellationToken))
        {
            throw new InvalidOperationException(StudentCardNumber.DuplicateMessage);
        }

        try
        {
            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    InsertStudentInfoSp,
                    p,
                    commandType: CommandType.StoredProcedure,
                    cancellationToken: cancellationToken));
        }
        catch (Exception ex) when (StudentCardNumber.IsDuplicateConflict(ex))
        {
            throw new InvalidOperationException(StudentCardNumber.DuplicateMessage, ex);
        }
    }

    private static SchoolListItemDto TrimSchoolRow(SchoolListItemDto row)
    {
        return new SchoolListItemDto(
            row.SchoolId,
            row.CountryId,
            row.SchoolCode?.Trim() ?? string.Empty,
            row.SchoolName?.Trim() ?? string.Empty,
            row.SchoolLogo?.Trim(),
            row.MinimumTopup,
            row.PdfPath?.Trim(),
            row.EmailAlerts);
    }

    private static string Trim(string value, int maxLen)
    {
        var t = value.Trim();
        return t.Length <= maxLen ? t : t[..maxLen];
    }

    private static string TrimFixed(string value, int maxLen)
    {
        var t = value.Trim();
        return t.Length <= maxLen ? t : t[..maxLen];
    }

    private static StudentSummaryDto TrimSummaryStrings(StudentSummaryDto row)
    {
        return new StudentSummaryDto(
            row.Name?.Trim() ?? string.Empty,
            row.Balance,
            row.Customerid?.Trim() ?? string.Empty);
    }

    private static StudentListingDto TrimListingStrings(StudentListingDto row)
    {
        return new StudentListingDto(
            row.UserId,
            row.StudCode?.Trim() ?? string.Empty,
            row.Name?.Trim() ?? string.Empty,
            row.UserName?.Trim() ?? string.Empty,
            row.Std?.Trim() ?? string.Empty,
            row.SchoolName?.Trim() ?? string.Empty,
            row.Cardid?.Trim() ?? string.Empty,
            row.Status?.Trim() ?? string.Empty,
            row.DateOfBirth,
            row.Balprepaid,
            row.ClassName?.Trim() ?? string.Empty,
            row.GroupName?.Trim() ?? string.Empty,
            row.Year,
            row.Email?.Trim() ?? string.Empty,
            row.IsNoService ?? 0);
    }
}
