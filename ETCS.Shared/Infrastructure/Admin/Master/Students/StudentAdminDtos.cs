using System.ComponentModel.DataAnnotations;



namespace ETCS.Shared.Infrastructure.Admin.Master.Students;



public sealed class StudentAdminListItemDto

{

    public decimal UserId { get; init; }

    public string? StudCode { get; init; }

    public string Name { get; init; } = string.Empty;

    public string? SchoolName { get; init; }

    public string? GuardianName { get; init; }

    public decimal Balance { get; init; }

    public DateTime? CreatedAt { get; init; }
}



public sealed class StudentAdminSaveRequest

{

    public decimal UserId { get; set; }



    [Required(ErrorMessage = "Student card number is required.")]
    [Display(Name = "Student Card No")]
    [MaxLength(50)]
    [RegularExpression(@"^\d+$", ErrorMessage = "Student card number must contain digits only.")]
    public string StudCode { get; set; } = string.Empty;



    [Required(ErrorMessage = "First name is required.")]

    [Display(Name = "First name")]

    [MaxLength(50)]

    public string FirstName { get; set; } = string.Empty;



    [MaxLength(50)]

    [Display(Name = "Last name")]

    public string? LastName { get; set; }



    [Required(ErrorMessage = "School is required.")]

    [Display(Name = "School")]

    [Range(1, int.MaxValue, ErrorMessage = "School is required.")]

    public int SchoolId { get; set; }



    [Required(ErrorMessage = "Parent is required.")]

    [Display(Name = "Parent")]

    [Range(1, int.MaxValue, ErrorMessage = "Parent is required.")]

    public int GuardianId { get; set; }



    [Required(ErrorMessage = "Gender is required.")]

    [Display(Name = "Gender")]

    public string Gender { get; set; } = "Male";



    [Required(ErrorMessage = "Standard is required.")]

    [Display(Name = "Standard")]

    [Range(1, int.MaxValue, ErrorMessage = "Standard is required.")]

    public int GradeId { get; set; }



    [Required(ErrorMessage = "Division is required.")]

    [Display(Name = "Division")]

    [MaxLength(128)]

    public string Division { get; set; } = string.Empty;



    [Display(Name = "Date of birth")]

    [DataType(DataType.Date)]

    public DateTime? DateOfBirth { get; set; }



    [Display(Name = "Allergies")]
    public List<int>? AllergyItemIds { get; set; }

    [Display(Name = "Order Types")]
    public List<int>? OrderTypeIds { get; set; }



    [Display(Name = "Daily Spend Limit")]

    [Range(0, double.MaxValue, ErrorMessage = "Daily spend limit cannot be negative.")]

    public decimal? DailySpendLimit { get; set; }



    [Display(Name = "Weekly Spend Limit")]

    [Range(0, double.MaxValue, ErrorMessage = "Weekly spend limit cannot be negative.")]

    public decimal? WeeklySpendLimit { get; set; }



    [Display(Name = "Low balance email notification")]

    public bool LowBalanceEmailNotification { get; set; } = true;



    [MaxLength(100)]

    [DataType(DataType.Password)]

    public string? Password { get; set; }



    public bool IsActive { get; set; } = true;

}



public sealed class GuardianLookupDto

{

    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

}



public sealed class SchoolLookupDto

{

    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Code { get; init; } = string.Empty;

}



public sealed class GradeLookupDto

{

    public int Id { get; init; }

    public string Grade { get; init; } = string.Empty;

}



public sealed class AllergyLookupDto

{

    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

}

