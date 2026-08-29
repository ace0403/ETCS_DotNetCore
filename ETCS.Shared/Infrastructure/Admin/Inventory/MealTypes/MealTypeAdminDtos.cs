using System.ComponentModel.DataAnnotations;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealTypes;

public static class MealTypeKinds
{
    public const string Session = "session";
    public const string Type = "type";

    public static bool IsSession(string? kind) =>
        string.Equals(kind, Session, StringComparison.OrdinalIgnoreCase);

    public static bool IsType(string? kind) =>
        string.Equals(kind, Type, StringComparison.OrdinalIgnoreCase);
}

public sealed class MealSessionListItemDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public bool IsActive { get; init; }
}

public sealed class MealTypeListItemDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public int SessionId { get; init; }

    public string SessionName { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public bool IsActive { get; init; }
}

public sealed class MealTypeSaveRequest
{
    public int Id { get; set; }

    public string Kind { get; set; } = MealTypeKinds.Session;

    [Display(Name = "Name")]
    [Required(ErrorMessage = "Name is required.")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Meal Session")]
    public int? ParentId { get; set; }

    [Display(Name = "Sort Order")]
    public int SortOrder { get; set; }

    [Display(Name = "Active")]
    public bool IsActive { get; set; } = true;

    public int? CreatedBy { get; set; }

    public int? UpdatedBy { get; set; }
}
