namespace ETCS.Shared.Options;

public sealed class MealDatabaseOptions
{
    public const string SectionName = "MealDatabase";

    public string ConnectionString { get; set; } = string.Empty;
}
