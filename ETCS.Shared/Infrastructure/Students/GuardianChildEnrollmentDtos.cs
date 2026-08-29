namespace ETCS.Shared.Infrastructure.Students;

public sealed class ChildFormOptionDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;
}

public sealed class ChildFormSchoolOptionDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string SchoolCode { get; init; } = string.Empty;

    public int CountryId { get; init; }
}

public sealed class ChildFormLookupsDto
{
    public IReadOnlyList<ChildFormOptionDto> Grades { get; init; } = [];

    public IReadOnlyList<ChildFormSchoolOptionDto> Schools { get; init; } = [];

    public IReadOnlyList<ChildFormOptionDto> Allergies { get; init; } = [];

    public IReadOnlyList<string> Genders { get; init; } = ["Male", "Female"];
}

public sealed class GuardianChildFormStudentDto
{
    public decimal UserId { get; init; }

    public string StudentCardNo { get; init; } = string.Empty;

    public string FirstName { get; init; } = string.Empty;

    public string LastName { get; init; } = string.Empty;

    public int GradeId { get; init; }

    public string Division { get; init; } = string.Empty;

    public string Gender { get; init; } = "Male";

    public int SchoolId { get; init; }

    public DateTime? DateOfBirth { get; init; }

    public IReadOnlyList<int> AllergyItemIds { get; init; } = [];

    public decimal? DailySpendLimit { get; init; }

    public decimal? WeeklySpendLimit { get; init; }

    public bool LowBalanceEmailNotification { get; init; } = true;
}

public sealed class GuardianChildEditFormResponse
{
    public required GuardianChildFormStudentDto Student { get; init; }

    public IReadOnlyList<ChildFormOptionDto> Grades { get; init; } = [];

    public IReadOnlyList<ChildFormSchoolOptionDto> Schools { get; init; } = [];

    public IReadOnlyList<ChildFormOptionDto> Allergies { get; init; } = [];

    public IReadOnlyList<string> Genders { get; init; } = ["Male", "Female"];
}

public sealed class GuardianChildUpsertRequest
{
    /// <summary>Required for update. Ignored on create.</summary>
    public decimal? UserId { get; set; }

    public string StudentCardNo { get; set; } = string.Empty;

    public string FirstName { get; set; } = string.Empty;

    public string? LastName { get; set; }

    public int GradeId { get; set; }

    public string Division { get; set; } = string.Empty;

    public string Gender { get; set; } = "Male";

    public int SchoolId { get; set; }

    public DateTime? DateOfBirth { get; set; }

    public List<int>? AllergyItemIds { get; set; }

    public decimal? DailySpendLimit { get; set; }

    public decimal? WeeklySpendLimit { get; set; }

    public bool LowBalanceEmailNotification { get; set; } = true;
}

public sealed class GuardianChildOperationResult
{
    public bool Success { get; init; }

    public string Message { get; init; } = string.Empty;

    public static GuardianChildOperationResult Ok(string message) =>
        new() { Success = true, Message = message };

    public static GuardianChildOperationResult Fail(string message) =>
        new() { Success = false, Message = message };
}
