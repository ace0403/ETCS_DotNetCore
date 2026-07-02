namespace ETCS.Shared.Infrastructure.Enums;

public sealed class EnumDetailDto
{
    public int Id { get; init; }

    public int TypeId { get; init; }

    public string Value { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public int SortOrder { get; init; }
}
