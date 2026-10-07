using System.Data.Common;
using System.Text.Json.Serialization;
using Dapper;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealCombos;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;
using ETCS.Shared.Infrastructure.Data;

namespace ETCS.Shared.Infrastructure.Admin.Inventory;

internal static class MealCatalogAuditSnapshot
{
    public static async Task<MealItemAuditSnapshot?> ForItemAsync(
        IMealDbConnectionFactory mealConnectionFactory,
        IDbConnectionFactory schoolConnectionFactory,
        MealItemSaveRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return null;
        }

        var names = await LoadNamesAsync(
            mealConnectionFactory,
            schoolConnectionFactory,
            (request.SchoolIds ?? []).Append(request.SchoolId),
            EnumIds(request.MealSessionId, request.MealTypeId, request.MealCategoryId, request.IngredientIds, request.DayIds, request.OrderTypeIds, request.NutritionLines),
            cancellationToken);

        return new MealItemAuditSnapshot
        {
            Id = request.Id,
            ItemName = request.ItemName,
            Detail = request.Detail,
            Price = request.Price,
            ImageName = request.ImageName,
            IsActive = request.IsActive,
            Schools = Labels(names.Schools, request.SchoolIds),
            MealSession = Label(names.Enums, request.MealSessionId),
            MealType = Label(names.Enums, request.MealTypeId),
            MealCategory = request.MealCategoryId is > 0 ? Label(names.Enums, request.MealCategoryId.Value) : null,
            Ingredients = Labels(names.Enums, request.IngredientIds),
            Weeks = request.WeekNos?.Where(week => week > 0).Distinct().OrderBy(week => week).ToArray() ?? [],
            Days = Labels(names.Enums, request.DayIds),
            Nutrition = (request.NutritionLines ?? [])
                .Where(line => line.NutritionId > 0)
                .Select(line => new MealNutritionAuditLine
                {
                    Nutrition = Label(names.Enums, line.NutritionId),
                    MeasureValue = line.MeasureValue,
                    MeasureType = Label(names.Enums, line.MeasureTypeId)
                })
                .ToArray(),
            OrderTypes = Labels(names.Enums, request.OrderTypeIds),
            CreatedBy = request.CreatedBy,
            UpdatedBy = request.UpdatedBy
        };
    }

    public static async Task<MealComboAuditSnapshot?> ForComboAsync(
        IMealDbConnectionFactory mealConnectionFactory,
        IDbConnectionFactory schoolConnectionFactory,
        MealComboSaveRequest? request,
        CancellationToken cancellationToken)
    {
        if (request is null)
        {
            return null;
        }

        var names = await LoadNamesAsync(
            mealConnectionFactory,
            schoolConnectionFactory,
            [request.SchoolId],
            EnumIds(request.MealSessionId, request.MealTypeId, request.MealCategoryId, request.IngredientIds, request.DayIds, [], request.NutritionLines),
            cancellationToken);

        return new MealComboAuditSnapshot
        {
            Id = request.Id,
            PackageName = request.PackageName,
            Detail = request.Detail,
            Price = request.Price,
            ProcessingFee = request.ProcessingFee,
            ImageName = request.ImageName,
            IsActive = request.IsActive,
            School = Label(names.Schools, request.SchoolId),
            MealSession = Label(names.Enums, request.MealSessionId),
            MealType = Label(names.Enums, request.MealTypeId),
            MealCategory = request.MealCategoryId is > 0 ? Label(names.Enums, request.MealCategoryId.Value) : null,
            Ingredients = Labels(names.Enums, request.IngredientIds),
            Weeks = request.WeekNos?.Where(week => week > 0).Distinct().OrderBy(week => week).ToArray() ?? [],
            Days = Labels(names.Enums, request.DayIds),
            Nutrition = (request.NutritionLines ?? [])
                .Where(line => line.NutritionId > 0)
                .Select(line => new MealNutritionAuditLine
                {
                    Nutrition = Label(names.Enums, line.NutritionId),
                    MeasureValue = line.MeasureValue,
                    MeasureType = Label(names.Enums, line.MeasureTypeId)
                })
                .ToArray(),
            CreatedBy = request.CreatedBy,
            UpdatedBy = request.UpdatedBy
        };
    }

    private static IEnumerable<int> EnumIds(
        int mealSessionId,
        int mealTypeId,
        int? mealCategoryId,
        IEnumerable<int>? ingredientIds,
        IEnumerable<int>? dayIds,
        IEnumerable<int>? orderTypeIds,
        IEnumerable<MealItemNutritionLineDto>? nutritionLines)
    {
        yield return mealSessionId;
        yield return mealTypeId;
        if (mealCategoryId is > 0)
        {
            yield return mealCategoryId.Value;
        }

        if (ingredientIds is not null)
        {
            foreach (var id in ingredientIds)
            {
                yield return id;
            }
        }

        if (dayIds is not null)
        {
            foreach (var id in dayIds)
            {
                yield return id;
            }
        }

        if (orderTypeIds is not null)
        {
            foreach (var id in orderTypeIds)
            {
                yield return id;
            }
        }

        if (nutritionLines is null)
        {
            yield break;
        }

        foreach (var line in nutritionLines)
        {
            yield return line.NutritionId;
            yield return line.MeasureTypeId;
        }
    }

    private static async Task<(Dictionary<int, string> Schools, Dictionary<int, string> Enums)> LoadNamesAsync(
        IMealDbConnectionFactory mealConnectionFactory,
        IDbConnectionFactory schoolConnectionFactory,
        IEnumerable<int> schoolIds,
        IEnumerable<int> enumIds,
        CancellationToken cancellationToken)
    {
        var schools = await LoadMapAsync(
            schoolConnectionFactory.CreateConnection,
            """
            SELECT SchoolId AS Id, LTRIM(RTRIM(ISNULL(SchoolName, ''))) AS Name
            FROM SchoolInfo
            WHERE SchoolId IN @Ids;
            """,
            schoolIds,
            cancellationToken);

        var enums = await LoadMapAsync(
            mealConnectionFactory.CreateConnection,
            """
            SELECT Id, LTRIM(RTRIM(ISNULL(EnumValue, ''))) AS Name
            FROM Enums
            WHERE Id IN @Ids;
            """,
            enumIds,
            cancellationToken);

        return (schools, enums);
    }

    private static async Task<Dictionary<int, string>> LoadMapAsync(
        Func<System.Data.IDbConnection> createConnection,
        string sql,
        IEnumerable<int> ids,
        CancellationToken cancellationToken)
    {
        var distinct = ids.Where(id => id > 0).Distinct().ToArray();
        if (distinct.Length == 0)
        {
            return [];
        }

        try
        {
            using var connection = createConnection();
            var dbConnection = (DbConnection)connection;
            await dbConnection.OpenAsync(cancellationToken);
            var rows = await dbConnection.QueryAsync<IdNameRow>(
                new CommandDefinition(sql, new { Ids = distinct }, cancellationToken: cancellationToken));
            return rows
                .GroupBy(row => row.Id)
                .ToDictionary(group => group.Key, group => group.First().Name);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return [];
        }
    }

    private static string[] Labels(IReadOnlyDictionary<int, string> names, IEnumerable<int>? ids)
    {
        if (ids is null)
        {
            return [];
        }

        return ids
            .Where(id => id > 0)
            .Distinct()
            .Select(id => Label(names, id))
            .ToArray();
    }

    private static string Label(IReadOnlyDictionary<int, string> names, int id)
    {
        if (id <= 0)
        {
            return string.Empty;
        }

        return names.TryGetValue(id, out var name) && !string.IsNullOrWhiteSpace(name)
            ? name
            : id.ToString();
    }

    private sealed class IdNameRow
    {
        public int Id { get; set; }

        public string Name { get; init; } = string.Empty;
    }
}

internal sealed class MealItemAuditSnapshot
{
    public int Id { get; set; }

    public string ItemName { get; init; } = string.Empty;

    public string? Detail { get; init; }

    public decimal? Price { get; init; }

    public string? ImageName { get; init; }

    public bool IsActive { get; init; }

    public IReadOnlyList<string> Schools { get; init; } = [];

    public string? MealSession { get; init; }

    public string? MealType { get; init; }

    public string? MealCategory { get; init; }

    public IReadOnlyList<string> Ingredients { get; init; } = [];

    public IReadOnlyList<int> Weeks { get; init; } = [];

    public IReadOnlyList<string> Days { get; init; } = [];

    public IReadOnlyList<MealNutritionAuditLine> Nutrition { get; init; } = [];

    public IReadOnlyList<string> OrderTypes { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CreatedBy { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? UpdatedBy { get; init; }
}

internal sealed class MealComboAuditSnapshot
{
    public int Id { get; set; }

    public string PackageName { get; init; } = string.Empty;

    public string? Detail { get; init; }

    public decimal Price { get; init; }

    public decimal ProcessingFee { get; init; }

    public string? ImageName { get; init; }

    public bool IsActive { get; init; }

    public string? School { get; init; }

    public string? MealSession { get; init; }

    public string? MealType { get; init; }

    public string? MealCategory { get; init; }

    public IReadOnlyList<string> Ingredients { get; init; } = [];

    public IReadOnlyList<int> Weeks { get; init; } = [];

    public IReadOnlyList<string> Days { get; init; } = [];

    public IReadOnlyList<MealNutritionAuditLine> Nutrition { get; init; } = [];

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? CreatedBy { get; init; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? UpdatedBy { get; init; }
}

internal sealed class MealNutritionAuditLine
{
    public string Nutrition { get; init; } = string.Empty;

    public decimal MeasureValue { get; init; }

    public string MeasureType { get; init; } = string.Empty;
}
