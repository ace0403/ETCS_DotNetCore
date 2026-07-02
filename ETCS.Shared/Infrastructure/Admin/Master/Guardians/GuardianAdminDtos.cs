using System.ComponentModel.DataAnnotations;

namespace ETCS.Shared.Infrastructure.Admin.Master.Guardians;

public sealed class GuardianListItemDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string? MobileNo { get; init; }
    public string? Username { get; init; }
    public bool IsActive { get; init; }
}

public sealed class GuardianSaveRequest
{
    public int Id { get; set; }

    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? MobileNo { get; set; }

    [MaxLength(100)]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
}

public sealed class GuardianTransferViewModel
{
    public int GuardianId { get; set; }

    [Required(ErrorMessage = "Select from student.")]
    [Range(0.0000001, double.MaxValue, ErrorMessage = "Select from student.")]
    public decimal FromUserId { get; set; }

    [Required(ErrorMessage = "Select to student.")]
    [Range(0.0000001, double.MaxValue, ErrorMessage = "Select to student.")]
    public decimal ToUserId { get; set; }

    [Required(ErrorMessage = "Amount is required.")]
    [Range(10, double.MaxValue, ErrorMessage = "Enter an amount greater than zero.")]
    public decimal Amount { get; set; }

    public IReadOnlyList<GuardianChildTransferItemDto> Children { get; init; } = [];
}

public sealed class GuardianChildTransferItemDto
{
    public decimal UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public decimal Balance { get; init; }
}

public sealed class GuardianChildrenViewModel
{
    public int GuardianId { get; set; }
    public string GuardianName { get; set; } = string.Empty;
    public IReadOnlyList<GuardianChildListItemDto> Children { get; init; } = [];
}

public sealed class GuardianChildListItemDto
{
    public decimal UserId { get; init; }
    public string Name { get; init; } = string.Empty;
    public string CardNo { get; init; } = string.Empty;
    public string Std { get; init; } = string.Empty;
    public string SchoolName { get; init; } = string.Empty;
    public decimal Balance { get; init; }
    public string Status { get; init; } = string.Empty;
}

public sealed class GuardianBalanceTransferRequest
{
    public int GuardianId { get; set; }
    public decimal FromUserId { get; set; }
    public decimal ToUserId { get; set; }
    public decimal Amount { get; set; }
}

public sealed class GuardianAddStudentViewModel
{
    public int GuardianId { get; set; }
    public IReadOnlyList<GuardianAddStudentGradeOption> Grades { get; init; } = [];
    public IReadOnlyList<GuardianAddStudentSchoolOption> Schools { get; init; } = [];
}

public sealed class GuardianAddStudentGradeOption
{
    public int Id { get; init; }
    public string Grade { get; init; } = string.Empty;
}

public sealed class GuardianAddStudentSchoolOption
{
    public int SchoolId { get; init; }
    public string SchoolName { get; init; } = string.Empty;
    public string SchoolCode { get; init; } = string.Empty;
    public int CountryId { get; init; }
}

public class GuardianAddStudentRequest
{
    public int GuardianId { get; set; }

    [Required(ErrorMessage = "Student card number is required.")]
    [Display(Name = "Student Card No")]
    [MaxLength(50)]
    public string StudentCardNo { get; set; } = string.Empty;

    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(50)]
    public string FirstName { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? LastName { get; set; } = string.Empty;

    [Required(ErrorMessage = "Class / grade is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Class / grade is required.")]
    public int GradeId { get; set; }

    [Required(ErrorMessage = "Division is required.")]
    [MaxLength(128)]
    public string Division { get; set; } = string.Empty;

    [Required(ErrorMessage = "Gender is required.")]
    public string Gender { get; set; } = "Male";

    [Required(ErrorMessage = "School is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "School is required.")]
    public int SchoolId { get; set; }

    [Display(Name = "Date of birth")]
    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    [Display(Name = "Allergies")]
    public List<int>? AllergyItemIds { get; set; }

    [Display(Name = "Daily Spend Limit")]
    [Range(0, double.MaxValue, ErrorMessage = "Daily spend limit cannot be negative.")]
    public decimal? DailySpendLimit { get; set; }

    [Display(Name = "Weekly Spend Limit")]
    [Range(0, double.MaxValue, ErrorMessage = "Weekly spend limit cannot be negative.")]
    public decimal? WeeklySpendLimit { get; set; }
}

public sealed class GuardianEditStudentRequest : GuardianAddStudentRequest
{
    [Range(0.0000001, double.MaxValue, ErrorMessage = "Student is required.")]
    public decimal UserId { get; set; }
}

public sealed class GuardianEditStudentViewModel
{
    public GuardianEditStudentRequest Student { get; init; } = new();
    public IReadOnlyList<GuardianAddStudentGradeOption> Grades { get; init; } = [];
    public IReadOnlyList<GuardianAddStudentSchoolOption> Schools { get; init; } = [];
}
