using System.ComponentModel.DataAnnotations;

namespace ETCS.Shared.Infrastructure.Admin.Master.Staff;

public sealed class StaffListItemDto
{
    public int Id { get; init; }
    public string StaffId { get; init; } = string.Empty;
    public string LoginName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string RoleName { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}

public sealed class StaffSaveRequest
{
    public int Id { get; set; }

    [Display(Name = "Username")]
    [Required(ErrorMessage = "Username is required.")]
    [MaxLength(50)]
    public string LoginName { get; set; } = string.Empty;

    [Display(Name = "Password")]
    [MaxLength(100)]
    [DataType(DataType.Password)]
    public string? Password { get; set; }

    [Display(Name = "First Name")]
    [Required(ErrorMessage = "First name is required.")]
    [MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "Last Name")]
    [MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "Staff ID")]
    [Required(ErrorMessage = "Staff ID is required.")]
    [MaxLength(50)]
    public string StaffId { get; set; } = string.Empty;

    [Display(Name = "Email")]
    [Required(ErrorMessage = "Email is required.")]
    [EmailAddress(ErrorMessage = "Enter a valid email address.")]
    [MaxLength(100)]
    public string Email { get; set; } = string.Empty;

    [Display(Name = "Date of Birth")]
    [Required(ErrorMessage = "Date of birth is required.")]
    [DataType(DataType.Date)]
    public DateTime? DateOfBirth { get; set; }

    [Display(Name = "Country")]
    [Required(ErrorMessage = "Country is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Country is required.")]
    public int CountryId { get; set; }

    [Display(Name = "Schools")]
    public List<int> SchoolIds { get; set; } = [];

    public int SchoolId { get; set; }

    [Display(Name = "Roles")]
    public List<int> RoleIds { get; set; } = [];

    [Display(Name = "Default Role")]
    public int? DefaultRoleId { get; set; }

    [Display(Name = "Role")]
    [Range(1, int.MaxValue, ErrorMessage = "Role is required.")]
    public int RoleId { get; set; }

    [Display(Name = "Security Question")]
    [Required(ErrorMessage = "Security question is required.")]
    [MaxLength(200)]
    public string SecurityQuestion { get; set; } = string.Empty;

    [Display(Name = "Security Answer")]
    [Required(ErrorMessage = "Security answer is required.")]
    [MaxLength(200)]
    public string SecurityAnswer { get; set; } = string.Empty;

    public bool IsNew { get; set; }
}

public sealed class StaffRoleLookupDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class StaffCountryLookupDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class StaffSchoolLookupDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
