using Dapper;
using DocumentFormat.OpenXml.Spreadsheet;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Admin.Master.Students;
using ETCS.Shared.Infrastructure.Admin.Models;
using ETCS.Shared.Infrastructure.Data;
using System.Data.Common;
using System.Globalization;

namespace ETCS.Shared.Infrastructure.Students;

public sealed class GuardianChildEnrollmentService : IGuardianChildEnrollmentService
{
    private const int DefaultCountryId = 1;

    private readonly IDbConnectionFactory _connectionFactory;
    private readonly IStudentRepository _studentRepository;
    private readonly IStudentAllergyAdminRepository _allergyRepository;
    private readonly IMealEnumAdminRepository _mealEnumRepository;

    public GuardianChildEnrollmentService(
        IDbConnectionFactory connectionFactory,
        IStudentRepository studentRepository,
        IStudentAllergyAdminRepository allergyRepository,
        IMealEnumAdminRepository mealEnumRepository)
    {
        _connectionFactory = connectionFactory;
        _studentRepository = studentRepository;
        _allergyRepository = allergyRepository;
        _mealEnumRepository = mealEnumRepository;
    }

    public async Task<ChildFormLookupsDto> GetAddChildFormAsync(CancellationToken cancellationToken = default)
    {
        var lookups = await LoadLookupsAsync(cancellationToken);
        return lookups;
    }

    public async Task<GuardianChildEditFormResponse?> GetEditChildFormAsync(
        int guardianId,
        decimal studentUserId,
        CancellationToken cancellationToken = default)
    {
        if (guardianId <= 0 || studentUserId <= 0)
        {
            return null;
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

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
                INNER JOIN GuardianMaster gm
                    ON LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) = LTRIM(RTRIM(ISNULL(gm.StudentCardNo, '')))
                WHERE gm.GrdID = @GuardianId
                  AND sl.UserId = @UserId;
                """,
                new { GuardianId = guardianId, UserId = studentUserId },
                cancellationToken: cancellationToken));

        if (row is null)
        {
            return null;
        }

        var lookups = await LoadLookupsAsync(cancellationToken);
        var gradeId = lookups.Grades.FirstOrDefault(g =>
            string.Equals(g.Name, row.StudStd, StringComparison.OrdinalIgnoreCase))?.Id ?? 0;
        var allergyIds = await _allergyRepository.GetAllergyIdsAsync(studentUserId, cancellationToken);

        return new GuardianChildEditFormResponse
        {
            Student = new GuardianChildFormStudentDto
            {
                UserId = row.UserId,
                StudentCardNo = row.StudentCardNo,
                FirstName = row.FirstName,
                LastName = row.LastName,
                GradeId = gradeId,
                Division = row.Division,
                Gender = string.IsNullOrWhiteSpace(row.Gender) ? "Male" : row.Gender,
                SchoolId = row.SchoolId,
                DateOfBirth = row.DateOfBirth,
                AllergyItemIds = allergyIds,
                DailySpendLimit = row.DailySpendLimit,
                WeeklySpendLimit = row.WeeklySpendLimit,
                LowBalanceEmailNotification = !row.IsUnsubscribeLowBalNoti
            },
            Grades = lookups.Grades,
            Schools = lookups.Schools,
            Allergies = lookups.Allergies,
            Genders = lookups.Genders
        };
    }

    public async Task<GuardianChildOperationResult> CreateAsync(
        int guardianId,
        GuardianChildUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        var validated = await ValidateAsync(guardianId, request, existingUserId: null, cancellationToken);
        if (!validated.Success || validated.Context is null)
        {
            return GuardianChildOperationResult.Fail(validated.Message);
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var context = validated.Context;
        var upsert = BuildUpsertRequest(guardianId, context, includePassword: true);

        #region Student Code Duplication Check
        if (await StudentCardNumber.IsTakenAsync(dbConnection, upsert.StudCode, excludeUserId: null, cancellationToken))
            return GuardianChildOperationResult.Fail(StudentCardNumber.DuplicateMessage);
        #endregion

        try
        {
            await _studentRepository.SaveStudentAsync(upsert, isInsert: true, cancellationToken);
        }
        catch (Exception ex)
        {
            return GuardianChildOperationResult.Fail(StudentCardNumber.MessageOrDuplicate(ex));
        }

        var userId = await ResolveUserIdByCardAsync(context.CardNo, cancellationToken);
        if (!userId.HasValue)
        {
            return GuardianChildOperationResult.Fail("Student was created but could not be loaded.");
        }

        await ApplyPostSaveAsync(guardianId, userId.Value, context, oldCustomerId: null, cancellationToken);
        return GuardianChildOperationResult.Ok("Student created.");
    }

    public async Task<GuardianChildOperationResult> UpdateAsync(
        int guardianId,
        GuardianChildUpsertRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!request.UserId.HasValue || request.UserId.Value <= 0)
        {
            return GuardianChildOperationResult.Fail("Student is required.");
        }

        var linked = await GetLinkedStudentAsync(guardianId, request.UserId.Value, cancellationToken);
        if (linked is null)
        {
            return GuardianChildOperationResult.Fail("Student was not found for this parent.");
        }

        // Card number is immutable on edit — always use the stored value (UI no longer sends changes).
        var cardNo = !string.IsNullOrWhiteSpace(linked.CustomerId)
            ? linked.CustomerId.Trim()
            : linked.StudCode.Trim();
        if (string.IsNullOrWhiteSpace(cardNo))
        {
            return GuardianChildOperationResult.Fail("Student card number is missing.");
        }

        request.StudentCardNo = cardNo;

        var validated = await ValidateAsync(
            guardianId,
            request,
            request.UserId.Value,
            cancellationToken,
            existingCardNo: cardNo);
        if (!validated.Success || validated.Context is null)
        {
            return GuardianChildOperationResult.Fail(validated.Message);
        }

        // Do not call spInsertStudentInfo on update — it always INSERTs into IDMember and
        // fails with PK_IDMember when the card already exists. Mirror portal EditStudent:
        // UPDATE StudentLogin (+ guardian mapping / allergies) only.
        try
        {
            await ApplyPostSaveAsync(
                guardianId,
                request.UserId.Value,
                validated.Context,
                oldCustomerId: linked.CustomerId,
                cancellationToken);
        }
        catch (Exception ex)
        {
            return GuardianChildOperationResult.Fail(ex.Message);
        }

        return GuardianChildOperationResult.Ok("Student updated.");
    }

    private async Task<ChildFormLookupsDto> LoadLookupsAsync(CancellationToken cancellationToken)
    {
        var gradesTask = _studentRepository.GetAllGradesAsync(cancellationToken);
        var allergiesTask = _mealEnumRepository.GetByTypeIdAsync(MealEnumTypeIds.FoodAllergy, cancellationToken);
        var schoolsTask = LoadSchoolsAsync(cancellationToken);

        await Task.WhenAll(gradesTask, allergiesTask, schoolsTask);

        var grades = (await gradesTask)
            .Select(g => new ChildFormOptionDto { Id = g.Id, Name = g.Grade })
            .ToList();

        var allergies = (await allergiesTask)
            .Select(a => new ChildFormOptionDto
            {
                Id = a.Id,
                Name = string.IsNullOrWhiteSpace(a.Description) ? a.Name : a.Description
            })
            .ToList();

        return new ChildFormLookupsDto
        {
            Grades = grades,
            Schools = await schoolsTask,
            Allergies = allergies,
            Genders = ["Male", "Female"]
        };
    }

    private async Task<IReadOnlyList<ChildFormSchoolOptionDto>> LoadSchoolsAsync(
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<ChildFormSchoolOptionDto>(
            new CommandDefinition(
                """
                SELECT
                    SchoolId AS Id,
                    LTRIM(RTRIM(SchoolName)) AS Name,
                    LTRIM(RTRIM(ISNULL(SchoolCode, ''))) AS SchoolCode,
                    ISNULL(CountryId, @DefaultCountryId) AS CountryId
                FROM SchoolInfo
                ORDER BY SchoolName;
                """,
                new { DefaultCountryId },
                cancellationToken: cancellationToken));

        return rows.ToList();
    }

    private async Task<(bool Success, string Message, UpsertContext? Context)> ValidateAsync(
        int guardianId,
        GuardianChildUpsertRequest request,
        decimal? existingUserId,
        CancellationToken cancellationToken,
        string? existingCardNo = null)
    {
        if (guardianId <= 0)
        {
            return (false, "Parent is required.", null);
        }

        if (string.IsNullOrWhiteSpace(request.StudentCardNo))
        {
            return (false, "Student card number is required.", null);
        }

        if (string.IsNullOrWhiteSpace(request.FirstName))
        {
            return (false, "First name is required.", null);
        }

        if (request.GradeId <= 0)
        {
            return (false, "Class / grade is required.", null);
        }

        if (string.IsNullOrWhiteSpace(request.Division))
        {
            return (false, "Division is required.", null);
        }

        if (request.SchoolId <= 0)
        {
            return (false, "School is required.", null);
        }

        if (request.DailySpendLimit is < 0 || request.WeeklySpendLimit is < 0)
        {
            return (false, "Spend limits cannot be negative.", null);
        }

        var cardNo = request.StudentCardNo.Trim();
        if (cardNo.Length > 50)
        {
            return (false, "Student card number is too long.", null);
        }

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var guardianExists = await dbConnection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                "SELECT TOP (1) CAST(g.GrdID AS int) FROM GuardianInfo g WHERE g.GrdID = @GuardianId;",
                new { GuardianId = guardianId },
                cancellationToken: cancellationToken));
        if (!guardianExists.HasValue)
        {
            return (false, "Parent was not found.", null);
        }

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
        {
            return (false, "Selected school was not found.", null);
        }

        #region School Code Validation
        var cardError = request.UserId > 0
            ? StudentCardNumber.ValidateForEdit(request.StudentCardNo, out var studCode)
            : StudentCardNumber.ResolveForCreate(request.StudentCardNo, school.SchoolCode, out studCode);
        if (cardError is not null)
            return (false, cardError, null);
        #endregion

        var cardUnchanged = existingUserId.HasValue &&
            !string.IsNullOrWhiteSpace(existingCardNo) &&
            string.Equals(studCode, existingCardNo.Trim(), StringComparison.OrdinalIgnoreCase);

        if (!cardUnchanged
            && await StudentCardNumber.IsTakenAsync(dbConnection, studCode, existingUserId, cancellationToken))
        {
            return (false, StudentCardNumber.DuplicateMessage, null);
        }

        var grades = await _studentRepository.GetAllGradesAsync(cancellationToken);
        var grade = grades.FirstOrDefault(g => g.Id == request.GradeId);
        if (grade is null)
        {
            return (false, "Selected class / grade was not found.", null);
        }

        var gender = string.IsNullOrWhiteSpace(request.Gender) ? "Male" : request.Gender.Trim();
        return (true, string.Empty, new UpsertContext(
            studCode,
            request.FirstName.Trim(),
            request.LastName?.Trim() ?? string.Empty,
            request.Division.Trim(),
            gender,
            grade.Grade,
            school,
            request.DateOfBirth,
            request.AllergyItemIds ?? [],
            request.DailySpendLimit ?? 0m,
            request.WeeklySpendLimit ?? 0m,
            request.LowBalanceEmailNotification));
    }

    private static UpsertStudentRequest BuildUpsertRequest(
        int guardianId,
        UpsertContext context,
        bool includePassword)
    {
        var dob = context.DateOfBirth.HasValue
            ? context.DateOfBirth.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)
            : string.Empty;

        return new UpsertStudentRequest
        {
            StudCode = context.CardNo,
            StudUserName = context.CardNo,
            StudPassword = includePassword ? context.CardNo : null,
            StudCountryID = context.School.CountryId.ToString(CultureInfo.InvariantCulture),
            StudSchoolID = context.School.SchoolId.ToString(CultureInfo.InvariantCulture),
            StudStd = context.GradeName,
            StudDiv = context.Division,
            Year = DateTime.Now.Year.ToString(CultureInfo.InvariantCulture),
            StudFirstName = context.FirstName,
            StudLastName = context.LastName,
            StudGender = context.Gender,
            StudDOB = dob,
            CustomerId = context.CardNo,
            GuardianID = guardianId.ToString(CultureInfo.InvariantCulture),
            SchoolCode = context.School.SchoolCode,
            IDCardStatus = "1"
        };
    }

    private async Task ApplyPostSaveAsync(
        int guardianId,
        decimal userId,
        UpsertContext context,
        string? oldCustomerId,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        await dbConnection.ExecuteAsync(
            new CommandDefinition(
                """
                UPDATE StudentLogin
                SET DailyLimit = @DailySpendLimit,
                    WeeklyLimit = @WeeklySpendLimit,
                    IsUnsubscribeLowBalNoti = @IsUnsubscribeLowBalNoti,
                    StudDateOfBirth = @DateOfBirth,
                    StudCode = @CardNo,
                    StudUserName = @CardNo,
                    CustomerId = @CardNo,
                    StudFirstName = @FirstName,
                    StudLastName = @LastName,
                    StudSchoolId = @SchoolId,
                    StudGender = @Gender,
                    StudStd = @StudStd,
                    StudDiv = @Division,
                    GrdID = @GuardianId
                WHERE UserId = @UserId;
                """,
                new
                {
                    UserId = userId,
                    GuardianId = guardianId,
                    CardNo = context.CardNo,
                    context.FirstName,
                    context.LastName,
                    SchoolId = context.School.SchoolId,
                    context.Gender,
                    StudStd = context.GradeName,
                    context.Division,
                    context.DateOfBirth,
                    DailySpendLimit = context.DailySpendLimit,
                    WeeklySpendLimit = context.WeeklySpendLimit,
                    IsUnsubscribeLowBalNoti = !context.LowBalanceEmailNotification
                },
                cancellationToken: cancellationToken));

        if (!string.IsNullOrWhiteSpace(oldCustomerId) &&
            !string.Equals(oldCustomerId, context.CardNo, StringComparison.OrdinalIgnoreCase))
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
                        GuardianId = guardianId,
                        OldCustomerId = oldCustomerId,
                        NewCustomerId = context.CardNo
                    },
                    cancellationToken: cancellationToken));
        }

        var mappingExists = await dbConnection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                """
                SELECT TOP (1) CAST(gm.ID AS int)
                FROM GuardianMaster gm
                WHERE gm.GrdID = @GuardianId
                  AND LTRIM(RTRIM(ISNULL(gm.StudentCardNo, ''))) = @CardNo;
                """,
                new { GuardianId = guardianId, CardNo = context.CardNo },
                cancellationToken: cancellationToken));

        if (!mappingExists.HasValue)
        {
            await dbConnection.ExecuteAsync(
                new CommandDefinition(
                    """
                    INSERT INTO GuardianMaster (GrdID, StudentCardNo)
                    VALUES (@GuardianId, @CardNo);
                    """,
                    new { GuardianId = guardianId, CardNo = context.CardNo },
                    cancellationToken: cancellationToken));
        }

        await _allergyRepository.SaveAllergiesAsync(userId, context.AllergyItemIds, cancellationToken);
    }

    private async Task<decimal?> ResolveUserIdByCardAsync(string cardNo, CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.ExecuteScalarAsync<decimal?>(
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
    }

    private async Task<LinkedStudentRow?> GetLinkedStudentAsync(
        int guardianId,
        decimal userId,
        CancellationToken cancellationToken)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        return await dbConnection.QuerySingleOrDefaultAsync<LinkedStudentRow>(
            new CommandDefinition(
                """
                SELECT TOP (1)
                    sl.UserId,
                    LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) AS CustomerId,
                    LTRIM(RTRIM(ISNULL(sl.StudCode, ''))) AS StudCode
                FROM StudentLogin sl
                INNER JOIN GuardianMaster gm
                    ON LTRIM(RTRIM(ISNULL(sl.CustomerId, ''))) = LTRIM(RTRIM(ISNULL(gm.StudentCardNo, '')))
                WHERE gm.GrdID = @GuardianId
                  AND sl.UserId = @UserId;
                """,
                new { GuardianId = guardianId, UserId = userId },
                cancellationToken: cancellationToken));
    }

    private sealed record UpsertContext(
        string CardNo,
        string FirstName,
        string LastName,
        string Division,
        string Gender,
        string GradeName,
        SchoolRow School,
        DateTime? DateOfBirth,
        IReadOnlyList<int> AllergyItemIds,
        decimal DailySpendLimit,
        decimal WeeklySpendLimit,
        bool LowBalanceEmailNotification);

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
        public string StudCode { get; init; } = string.Empty;
    }

    private sealed class SchoolRow
    {
        public int SchoolId { get; init; }
        public int CountryId { get; init; }
        public string SchoolCode { get; init; } = string.Empty;
    }
}
