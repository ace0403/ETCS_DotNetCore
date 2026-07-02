using System.ComponentModel.DataAnnotations;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealServingPeriods;

public sealed class MealServingPeriodListDto
{
    public int Id { get; init; }
    public int SchoolId { get; init; }
    public string SchoolName { get; set; } = string.Empty;
    public DateTime? StartDate { get; init; }
    public DateTime? CutoffDate { get; init; }
}

public sealed class MealServingPeriodSaveRequest
{
    public int Id { get; set; }

    [Required(ErrorMessage = "School is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "School is required.")]
    public int SchoolId { get; set; }

    [Display(Name = "Start Date (optional)")]
    public DateTime? StartDate { get; set; }

    [Display(Name = "Cutoff Date (optional)")]
    public DateTime? CutoffDate { get; set; }
}
