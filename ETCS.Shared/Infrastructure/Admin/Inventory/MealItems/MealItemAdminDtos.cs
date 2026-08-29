using System.ComponentModel.DataAnnotations;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;

public sealed class MealItemListDto
{
    public int Id { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public string SchoolNames { get; init; } = string.Empty;
    public string OrderTypeNames { get; init; } = string.Empty;
    public int SchoolId { get; init; }
    public int MealSessionId { get; init; }
    public int MealTypeId { get; init; }
    public int? MealCategoryId { get; init; }
    public decimal Price { get; init; }
    public bool IsActive { get; init; }
}

public sealed class MealItemSaveRequest
{
    public int Id { get; set; }

    [Display(Name = "School")]
    public List<int> SchoolIds { get; set; } = [];

    /// <summary>Denormalized primary school for legacy MealItem.SchoolId column.</summary>
    public int SchoolId { get; set; }

    [Required(ErrorMessage = "Meal session is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Meal session is required.")]
    public int MealSessionId { get; set; }

    [Required(ErrorMessage = "Meal type is required.")]
    [Range(1, int.MaxValue, ErrorMessage = "Meal type is required.")]
    public int MealTypeId { get; set; }

    public int? MealCategoryId { get; set; }

    [Required(ErrorMessage = "Item name is required.")]
    [MaxLength(200)]
    public string ItemName { get; set; } = string.Empty;

    [MaxLength(2000)]
    public string? Detail { get; set; }

    [Required(ErrorMessage = "Price is required.")]
    [Range(0, double.MaxValue, ErrorMessage = "Price cannot be negative.")]
    public decimal? Price { get; set; }

    public string? ImageName { get; set; }

    public List<int> IngredientIds { get; set; } = [];

    public List<int> WeekNos { get; set; } = [];

    public List<int> DayIds { get; set; } = [];

    public List<MealItemNutritionLineDto> NutritionLines { get; set; } = [];

    [Display(Name = "Order type")]
    public List<int> OrderTypeIds { get; set; } = [];

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
}

public sealed class MealItemNutritionLineDto
{
    public int NutritionId { get; set; }

    public decimal MeasureValue { get; set; }

    public int MeasureTypeId { get; set; }
}
