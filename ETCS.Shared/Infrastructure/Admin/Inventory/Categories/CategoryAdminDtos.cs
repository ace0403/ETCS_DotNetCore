using System.ComponentModel.DataAnnotations;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.Categories;

public sealed class CategoryListItemDto
{
    public int Id { get; init; }
    public string CategoryName { get; init; } = string.Empty;
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

public sealed class CategorySaveRequest
{
    public int Id { get; set; }

    [Display(Name = "Category Name")]
    [Required(ErrorMessage = "Category name is required.")]
    [MaxLength(200)]
    public string CategoryName { get; set; } = string.Empty;

    [Display(Name = "Sort Order")]
    public int SortOrder { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;
    public int? CreatedBy { get; set; }
    public int? UpdatedBy { get; set; }
}
