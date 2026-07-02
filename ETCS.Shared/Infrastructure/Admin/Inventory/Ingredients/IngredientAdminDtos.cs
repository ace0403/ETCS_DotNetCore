using System.ComponentModel.DataAnnotations;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.Ingredients;

public sealed class IngredientListItemDto
{
    public int Id { get; init; }

    public string IngredientName { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public bool IsActive { get; init; }
}

public sealed class IngredientSaveRequest
{
    public int Id { get; set; }

    [Display(Name = "Ingredient Name")]
    [Required(ErrorMessage = "Ingredient name is required.")]
    [MaxLength(200)]
    public string IngredientName { get; set; } = string.Empty;

    [Display(Name = "Description")]
    [MaxLength(500)]
    public string? Description { get; set; }

    [Display(Name = "Sort Order")]
    public int SortOrder { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public int? CreatedBy { get; set; }

    public int? UpdatedBy { get; set; }
}
