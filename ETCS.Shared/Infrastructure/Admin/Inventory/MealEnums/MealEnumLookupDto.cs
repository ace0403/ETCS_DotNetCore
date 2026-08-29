namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;

public sealed class MealEnumLookupDto
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public int SortOrder { get; init; }

    public int? ParentId { get; init; }
}
