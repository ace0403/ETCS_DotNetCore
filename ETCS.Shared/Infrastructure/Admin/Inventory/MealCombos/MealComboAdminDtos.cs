using System.ComponentModel.DataAnnotations;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealCombos;

public sealed class MealComboListDto
{
    public int Id { get; init; }
    public string PackageName { get; init; } = string.Empty;
    public int SchoolId { get; init; }
    public decimal Price { get; init; }
    public decimal ProcessingFee { get; init; }
    public decimal TotalPrice => Price + ProcessingFee;
    public int ItemCount { get; init; }
    public bool IsActive { get; init; }
}

public sealed class MealComboSaveRequest
{
    public int Id { get; set; }

    [Required(ErrorMessage = "School is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "School is required.")]
    public int SchoolId { get; set; }

    public int? MealTypeId { get; set; }
    public int? MealCategoryId { get; set; }

    [Display(Name = "Combo Name")]
    [Required(ErrorMessage = "Package name is required.")]
    [MaxLength(200)]
    public string PackageName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Detail { get; set; }

    [Required(ErrorMessage = "Price is required.")]
    [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative.")]
    public decimal Price { get; set; }

    [Display(Name = "Processing Fee")]
    [Range(0, double.MaxValue, ErrorMessage = "Processing fee cannot be negative.")]
    public decimal ProcessingFee { get; set; }

    public string? ImageName { get; set; }

    public List<int> WeekNos { get; set; } = [];

    public List<int> DayIds { get; set; } = [];

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
    public List<int> MealItemIds { get; set; } = [];
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
}
