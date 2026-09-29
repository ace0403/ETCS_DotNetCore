using System.Data;
using Dapper;
using ETCS.Shared.Infrastructure.Data;
using ETCS.Shared.Infrastructure.Meals.Menu;

namespace ETCS.Shared.Infrastructure.Pos;

internal static class PosMealItemAllergenCatalog
{
    public static async Task<IReadOnlyDictionary<int, IReadOnlyList<PosDeclaredAllergenDto>>> GetDeclaredAllergensByMealItemIdsAsync(
        IMealDbConnectionFactory connectionFactory,
        IReadOnlyList<int> mealItemIds,
        CancellationToken cancellationToken)
    {
        if (mealItemIds.Count == 0)
        {
            return new Dictionary<int, IReadOnlyList<PosDeclaredAllergenDto>>();
        }

        const string sql = """
            SELECT
                mii.MealItemId,
                LTRIM(RTRIM(e.EnumValue)) AS AllergenName,
                LTRIM(RTRIM(ISNULL(e.Description, ''))) AS Description
            FROM MealItemIngredients mii
            INNER JOIN Enums e ON e.Id = mii.IngredientId
            WHERE mii.MealItemId IN @MealItemIds
              AND ISNULL(e.IsActive, 1) = 1
              AND LTRIM(RTRIM(ISNULL(e.EnumValue, ''))) <> ''
            ORDER BY mii.MealItemId, e.EnumValue;
            """;

        using var connection = connectionFactory.CreateConnection();
        var rows = await connection.QueryAsync<AllergenRow>(new CommandDefinition(
            sql,
            new { MealItemIds = mealItemIds.Distinct().ToArray() },
            commandType: CommandType.Text,
            cancellationToken: cancellationToken));

        return rows
            .Where(r => r.MealItemId > 0 && !string.IsNullOrWhiteSpace(r.AllergenName))
            .GroupBy(r => r.MealItemId)
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<PosDeclaredAllergenDto>)g
                    .GroupBy(x => x.AllergenName!.Trim(), StringComparer.OrdinalIgnoreCase)
                    .Select(grp =>
                    {
                        var first = grp.First();
                        var icon = DeclaredAllergenDisplayHelper.TryGetIconFileFromDescription(first.Description);
                        return new PosDeclaredAllergenDto
                        {
                            Name = grp.Key,
                            Icon = icon
                        };
                    })
                    .OrderBy(x => x.Name, StringComparer.OrdinalIgnoreCase)
                    .ToList());
    }

    private sealed class AllergenRow
    {
        public int MealItemId { get; init; }
        public string? AllergenName { get; init; }
        public string? Description { get; init; }
    }
}
