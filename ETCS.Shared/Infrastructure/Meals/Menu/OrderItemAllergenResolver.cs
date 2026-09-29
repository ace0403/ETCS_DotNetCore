using System.Data.Common;
using Dapper;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Meals.Menu;

public interface IOrderItemAllergenResolver
{
    Task<IReadOnlyDictionary<(int? ItemId, int? PackageId), IReadOnlyList<string>>> ResolveAsync(
        int studentId,
        IReadOnlyList<(int? ItemId, int? PackageId)> lines,
        CancellationToken cancellationToken = default);
}

public sealed class OrderItemAllergenResolver : IOrderItemAllergenResolver
{
    private readonly IMealDbConnectionFactory _connectionFactory;

    public OrderItemAllergenResolver(IMealDbConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyDictionary<(int? ItemId, int? PackageId), IReadOnlyList<string>>> ResolveAsync(
        int studentId,
        IReadOnlyList<(int? ItemId, int? PackageId)> lines,
        CancellationToken cancellationToken = default)
    {
        if (studentId <= 0 || lines.Count == 0)
        {
            return new Dictionary<(int? ItemId, int? PackageId), IReadOnlyList<string>>();
        }

        var distinctLines = lines
            .Where(l => (l.ItemId is > 0) || (l.PackageId is > 0))
            .Select(l => (
                ItemId: l.ItemId is > 0 ? l.ItemId : null,
                PackageId: l.ItemId is > 0 ? null : (l.PackageId is > 0 ? l.PackageId : null)))
            .Where(l => l.ItemId is > 0 || l.PackageId is > 0)
            .Distinct()
            .ToList();

        if (distinctLines.Count == 0)
        {
            return new Dictionary<(int? ItemId, int? PackageId), IReadOnlyList<string>>();
        }

        var itemIds = distinctLines
            .Where(l => l.ItemId is > 0)
            .Select(l => l.ItemId!.Value)
            .Distinct()
            .ToArray();
        var packageIds = distinctLines
            .Where(l => l.PackageId is > 0)
            .Select(l => l.PackageId!.Value)
            .Distinct()
            .ToArray();

        const string sql = """
            SELECT
                oi.ItemId,
                oi.PackageId,
                LTRIM(RTRIM(e.EnumValue)) AS AllergenName
            FROM (
                SELECT ItemId = TRY_CAST(value AS INT), PackageId = CAST(NULL AS INT)
                FROM STRING_SPLIT(@ItemIdsCsv, ',')
                WHERE LTRIM(RTRIM(value)) <> '' AND TRY_CAST(value AS INT) IS NOT NULL
                UNION ALL
                SELECT ItemId = CAST(NULL AS INT), PackageId = TRY_CAST(value AS INT)
                FROM STRING_SPLIT(@PackageIdsCsv, ',')
                WHERE LTRIM(RTRIM(value)) <> '' AND TRY_CAST(value AS INT) IS NOT NULL
            ) oi
            INNER JOIN StudentAllergies sa ON sa.StudentId = @StudentId
            INNER JOIN Enums e ON e.Id = sa.AllergyItemId
            WHERE ISNULL(e.IsActive, 1) = 1
              AND LTRIM(RTRIM(ISNULL(e.EnumValue, ''))) <> ''
              AND (
                    (oi.ItemId IS NOT NULL AND EXISTS (
                        SELECT 1
                        FROM MealItemIngredients mii
                        WHERE mii.MealItemId = oi.ItemId
                          AND mii.IngredientId = sa.AllergyItemId
                    ))
                    OR (oi.PackageId IS NOT NULL AND (
                        EXISTS (
                            SELECT 1
                            FROM MealPackageIngredients mpi
                            WHERE mpi.MealPackageId = oi.PackageId
                              AND mpi.IngredientId = sa.AllergyItemId
                        )
                        OR EXISTS (
                            SELECT 1
                            FROM MealPackageItems mpitem
                            INNER JOIN MealItemIngredients mii2 ON mii2.MealItemId = mpitem.MealItemId
                            WHERE mpitem.MealPackageId = oi.PackageId
                              AND mii2.IngredientId = sa.AllergyItemId
                        )
                    ))
              );
            """;

        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var rows = await dbConnection.QueryAsync<AllergenResolveRow>(
            new CommandDefinition(
                sql,
                new
                {
                    StudentId = studentId,
                    ItemIdsCsv = itemIds.Length == 0 ? string.Empty : string.Join(",", itemIds),
                    PackageIdsCsv = packageIds.Length == 0 ? string.Empty : string.Join(",", packageIds)
                },
                cancellationToken: cancellationToken));

        return rows
            .Where(r => !string.IsNullOrWhiteSpace(r.AllergenName))
            .GroupBy(r => (
                ItemId: r.ItemId is > 0 ? r.ItemId : null,
                PackageId: r.ItemId is > 0 ? null : (r.PackageId is > 0 ? r.PackageId : null)))
            .ToDictionary(
                g => g.Key,
                g => (IReadOnlyList<string>)g
                    .Select(x => x.AllergenName!.Trim())
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                    .ToList());
    }

    private sealed class AllergenResolveRow
    {
        public int? ItemId { get; init; }
        public int? PackageId { get; init; }
        public string? AllergenName { get; init; }
    }
}
