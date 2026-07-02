using System.ComponentModel.DataAnnotations;

namespace ETCS.Shared.Infrastructure.Admin.Master.Schools;

public sealed class SchoolCountryLookupDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}

public sealed class SchoolListItemDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string CountryName { get; init; } = string.Empty;
    public decimal MinimumTopupAmount { get; init; }
    public bool HasEmailNotification { get; init; }
}

public sealed class SchoolSaveRequest
{
    public int Id { get; set; }

    [Required(ErrorMessage = "School name is required.")]
    [MaxLength(200)]
    [Display(Name = "School Name")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "School code is required.")]
    [MaxLength(50)]
    [Display(Name = "School Code")]
    public string Code { get; set; } = string.Empty;

    [Required(ErrorMessage = "Country is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Country is required.")]
    [Display(Name = "Country")]
    public int CountryId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Minimum topup cannot be negative.")]
    [Display(Name = "Minimum Topup")]
    public decimal? MinimumTopupAmount { get; set; }

    [Display(Name = "Email Alerts")]
    public bool HasEmailNotification { get; set; }
    public string? LogoFileName { get; set; }
    public string? PdfPath { get; set; }
}
