using Dapper;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Data;
using ETCS.Shared.Media;
using Microsoft.Extensions.Caching.Memory;
using System.Data;
using System.Data.Common;
using System.Text.Json;

namespace ETCS.Shared.Infrastructure.Meals;

public sealed class MealRepository : IMealRepository
{
    private const string GetMealItemsForStudentSp = "GetMealItemsForStudent";
    private const string GetMealPackagesForStudentSp = "GetMealPackagesForStudent";

    /// <summary>How many top sellers to mark Popular per school/catalog.</summary>
    private const int PopularTopN = 3;

    /// <summary>Sales lookback window for Popular ranking.</summary>
    private const int PopularLookbackDays = 30;

    private static readonly TimeSpan PopularIdsCacheTtl = TimeSpan.FromMinutes(30);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMealDbConnectionFactory _connectionFactory;
    private readonly MealImageUrlBuilder _imageUrlBuilder;
    private readonly IMemoryCache _cache;

    public MealRepository(
        IMealDbConnectionFactory connectionFactory,
        MealImageUrlBuilder imageUrlBuilder,
        IMemoryCache cache)
    {
        _connectionFactory = connectionFactory;
        _imageUrlBuilder = imageUrlBuilder;
        _cache = cache;
    }

    public async Task<IReadOnlyList<MealItemDto>> GetMealItemsForStudentAsync(
        int studentId,
        int schoolId,
        DateTime mealDate,
        int? mealSessionId = null,
        int? mealTypeId = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var (effectiveMealDate, weekNo, dayId) = BuildDateParams(mealDate);

        var rows = (await dbConnection.QueryAsync<MealItemDbRow>(
            new CommandDefinition(
                GetMealItemsForStudentSp,
                new
                {
                    StudentId = studentId,
                    SchoolId = schoolId,
                    WeekNo = weekNo,
                    DayId = dayId,
                    MealDate = effectiveMealDate,
                    MealSessionId = mealSessionId,
                    MealTypeId = mealTypeId
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken))).AsList();

        var popularIds = await GetPopularMealItemIdsAsync(dbConnection, schoolId, cancellationToken);

        var items = new List<MealItemDto>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(MapToDto(row, popularIds.Contains(row.Id)));
        }

        return items;
    }

    public async Task<IReadOnlyList<MealPackageDto>> GetMealPackagesForStudentAsync(
        int studentId,
        int schoolId,
        DateTime mealDate,
        int? mealSessionId = null,
        int? mealTypeId = null,
        CancellationToken cancellationToken = default)
    {
        using var connection = _connectionFactory.CreateConnection();
        var dbConnection = (DbConnection)connection;
        await dbConnection.OpenAsync(cancellationToken);

        var (effectiveMealDate, weekNo, dayId) = BuildDateParams(mealDate);

        var rows = (await dbConnection.QueryAsync<MealPackageDbRow>(
            new CommandDefinition(
                GetMealPackagesForStudentSp,
                new
                {
                    StudentId = studentId,
                    SchoolId = schoolId,
                    WeekNo = weekNo,
                    DayId = dayId,
                    MealDate = effectiveMealDate,
                    MealSessionId = mealSessionId,
                    MealTypeId = mealTypeId
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken))).AsList();

        var popularIds = await GetPopularPackageIdsAsync(dbConnection, schoolId, cancellationToken);

        var packages = new List<MealPackageDto>(rows.Count);
        foreach (var row in rows)
        {
            packages.Add(MapToPackageDto(row, popularIds.Contains(row.Id)));
        }

        return packages;
    }

    private async Task<HashSet<int>> GetPopularMealItemIdsAsync(
        DbConnection connection,
        int schoolId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"popular-meal-items:{schoolId}:{PopularLookbackDays}:{PopularTopN}";
        if (_cache.TryGetValue(cacheKey, out HashSet<int>? cached) && cached is not null)
        {
            return cached;
        }

        const string sql = """
            SELECT TOP (@TopN) oi.ItemId AS Id
            FROM [Order] o
            INNER JOIN [OrderItem] oi ON oi.OrderId = o.Id
            INNER JOIN [MealItem] mi ON mi.Id = oi.ItemId
            WHERE ISNULL(o.IsPaid, 0) = 1
              AND o.OrderTypeId = @OrderTypeId
              AND oi.ItemId IS NOT NULL
              AND oi.ItemId IS NOT NULL
              AND (
                  EXISTS (
                      SELECT 1
                      FROM MealItemSchools mis
                      WHERE mis.MealItemId = mi.Id
                        AND mis.SchoolId = @SchoolId
                  )
                  OR (
                      NOT EXISTS (SELECT 1 FROM MealItemSchools mis2 WHERE mis2.MealItemId = mi.Id)
                      AND mi.SchoolId = @SchoolId
                  )
              )
              AND o.OrderDate >= DATEADD(DAY, -@LookbackDays, GETDATE())
            GROUP BY oi.ItemId
            ORDER BY SUM(oi.Quantity) DESC, oi.ItemId ASC;
            """;

        var ids = (await connection.QueryAsync<int>(
            new CommandDefinition(
                sql,
                new
                {
                    TopN = PopularTopN,
                    SchoolId = schoolId,
                    LookbackDays = PopularLookbackDays,
                    OrderTypeId = (int)TransactionTypeEnum.A_La_Carte
                },
                cancellationToken: cancellationToken))).ToHashSet();

        _cache.Set(cacheKey, ids, PopularIdsCacheTtl);
        return ids;
    }

    private async Task<HashSet<int>> GetPopularPackageIdsAsync(
        DbConnection connection,
        int schoolId,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"popular-meal-packages:{schoolId}:{PopularLookbackDays}:{PopularTopN}";
        if (_cache.TryGetValue(cacheKey, out HashSet<int>? cached) && cached is not null)
        {
            return cached;
        }

        const string sql = """
            SELECT TOP (@TopN) oi.PackageId AS Id
            FROM [Order] o
            INNER JOIN [OrderItem] oi ON oi.OrderId = o.Id
            INNER JOIN [MealPackages] mp ON mp.Id = oi.PackageId
            WHERE ISNULL(o.IsPaid, 0) = 1
              AND o.OrderTypeId = @OrderTypeId
              AND oi.PackageId IS NOT NULL
              AND mp.SchoolId = @SchoolId
              AND o.OrderDate >= DATEADD(DAY, -@LookbackDays, GETDATE())
            GROUP BY oi.PackageId
            ORDER BY SUM(oi.Quantity) DESC, oi.PackageId ASC;
            """;

        var ids = (await connection.QueryAsync<int>(
            new CommandDefinition(
                sql,
                new
                {
                    TopN = PopularTopN,
                    SchoolId = schoolId,
                    LookbackDays = PopularLookbackDays,
                    OrderTypeId = (int)TransactionTypeEnum.MealOrder
                },
                cancellationToken: cancellationToken))).ToHashSet();

        _cache.Set(cacheKey, ids, PopularIdsCacheTtl);
        return ids;
    }

    private MealItemDto MapToDto(MealItemDbRow row, bool isPopular)
    {
        var ingredients = ParseIngredients(row.Ingredients);
        return new MealItemDto
        {
            Id = row.Id,
            ItemName = row.ItemName,
            MealSessionId = row.MealSessionId,
            MealSessionName = row.MealSessionName,
            MealSessionCssClass = row.MealSessionCssClass,
            MealTypeId = row.MealTypeId,
            MealTypeName = row.MealTypeName,
            MealCssClass = row.MealCssClass,
            MealTypeSortOrder = row.MealTypeSortOrder,
            MealCategoryId = row.MealCategoryId,
            MealCategoryName = row.MealCategoryName,
            SchoolId = row.SchoolId,
            ImageName = MealImageUrlBuilder.NormalizeFileName(row.ImageName),
            ImageUrl = _imageUrlBuilder.GetFullImageUrl(MealImageKind.MealItem, row.ImageName, absolute: true),
            ThumbnailUrl = _imageUrlBuilder.GetThumbnailUrl(MealImageKind.MealItem, row.ImageName, absolute: true),
            Detail = row.Detail,
            Price = row.Price,
            CreatedOn = row.CreatedOn,
            IngredientIds = ParseIngredientIds(row.IngredientIds),
            Ingredients = ingredients,
            IngredientNames = ingredients.Select(x => x.Name).ToList(),
            NutritionList = ParseJsonList(row.NutritionList),
            StudentAllergies = row.StudentAllergies ?? string.Empty,
            IsPopular = isPopular
        };
    }

    private MealPackageDto MapToPackageDto(MealPackageDbRow row, bool isPopular)
    {
        var ingredients = ParseIngredients(row.Ingredients);
        return new MealPackageDto
        {
            Id = row.Id,
            PackageName = row.PackageName,
            MealSessionId = row.MealSessionId,
            MealSessionName = row.MealSessionName,
            MealSessionCssClass = row.MealSessionCssClass,
            MealTypeId = row.MealTypeId,
            MealTypeName = row.MealTypeName,
            MealCssClass = row.MealCssClass,
            MealTypeSortOrder = row.MealTypeSortOrder,
            MealCategoryId = row.MealCategoryId,
            MealCategoryName = row.MealCategoryName,
            SchoolId = row.SchoolId,
            ImageName = MealImageUrlBuilder.NormalizeFileName(row.ImageName),
            ImageUrl = _imageUrlBuilder.GetFullImageUrl(MealImageKind.MealCombo, row.ImageName, absolute: true),
            ThumbnailUrl = _imageUrlBuilder.GetThumbnailUrl(MealImageKind.MealCombo, row.ImageName, absolute: true),
            Detail = row.Detail,
            Price = row.Price,
            ProcessingFee = row.ProcessingFee,
            CreatedOn = row.CreatedOn,
            ItemsName = row.ItemsName,
            WeekNo = ParseWeekNumbers(row.WeekNo),
            IngredientIds = ParseIngredientIds(row.IngredientIds),
            Ingredients = ingredients,
            IngredientNames = ingredients.Select(x => x.Name).ToList(),
            NutritionList = ParseJsonList(row.NutritionList),
            StudentAllergies = row.StudentAllergies ?? string.Empty,
            IsPopular = isPopular
        };
    }

    private static int[]? ParseIngredientIds(string? rawIds)
    {
        if (string.IsNullOrWhiteSpace(rawIds))
        {
            return null;
        }

        return rawIds
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .Distinct()
            .ToArray();
    }

    private static IReadOnlyList<MealIngredientDto> ParseIngredients(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return [];
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<MealIngredientDto>>(rawJson, JsonOptions);
            if (list is null || list.Count == 0)
            {
                return [];
            }

            return list
                .Where(x => !string.IsNullOrWhiteSpace(x.Name))
                .GroupBy(x => x.Name.Trim(), StringComparer.OrdinalIgnoreCase)
                .Select(g => new MealIngredientDto
                {
                    Name = g.First().Name.Trim(),
                    Icon = string.IsNullOrWhiteSpace(g.First().Icon) ? null : g.First().Icon.Trim()
                })
                .ToList();
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static int[]? ParseWeekNumbers(string? rawWeekNumbers)
    {
        if (string.IsNullOrWhiteSpace(rawWeekNumbers))
        {
            return null;
        }

        return rawWeekNumbers
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(int.Parse)
            .ToArray();
    }

    private static IReadOnlyList<NutritionItemDto> ParseJsonList(string? rawJson)
    {
        if (string.IsNullOrWhiteSpace(rawJson))
        {
            return [];
        }

        try
        {
            var list = JsonSerializer.Deserialize<List<NutritionItemDto>>(rawJson, JsonOptions);
            return list ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static (DateTime EffectiveMealDate, int WeekNo, int DayId) BuildDateParams(DateTime mealDate)
    {
        var effectiveMealDate = mealDate.Date;
        var dayId = CommonFunctions.GetDayId(mealDate.DayOfWeek);
        var weekNo = CommonFunctions.GetWeekNumberOfMonth(mealDate);
        return (effectiveMealDate, weekNo, dayId);
    }

    private sealed class MealItemDbRow
    {
        public int Id { get; init; }
        public string ItemName { get; init; } = string.Empty;
        public string MealSessionId { get; init; } = string.Empty;
        public string MealSessionName { get; init; } = string.Empty;
        public string MealSessionCssClass { get; init; } = string.Empty;
        public string MealTypeId { get; init; } = string.Empty;
        public string MealTypeName { get; init; } = string.Empty;
        public string MealCssClass { get; init; } = string.Empty;
        public int MealTypeSortOrder { get; init; }
        public int? MealCategoryId { get; init; }
        public string MealCategoryName { get; init; } = string.Empty;
        public int SchoolId { get; init; }
        public string? ImageName { get; init; }
        public string? Detail { get; init; }
        public decimal Price { get; init; }
        public DateTime CreatedOn { get; init; }
        public string? IngredientIds { get; init; }
        public string? Ingredients { get; init; }
        public string? NutritionList { get; init; }
        public string? StudentAllergies { get; init; }
    }

    private sealed class MealPackageDbRow
    {
        public int Id { get; init; }
        public string PackageName { get; init; } = string.Empty;
        public string MealSessionId { get; init; } = string.Empty;
        public string MealSessionName { get; init; } = string.Empty;
        public string MealSessionCssClass { get; init; } = string.Empty;
        public string MealTypeId { get; init; } = string.Empty;
        public string MealTypeName { get; init; } = string.Empty;
        public string MealCssClass { get; init; } = string.Empty;
        public int MealTypeSortOrder { get; init; }
        public int? MealCategoryId { get; init; }
        public string MealCategoryName { get; init; } = string.Empty;
        public int SchoolId { get; init; }
        public string? ImageName { get; init; }
        public string? Detail { get; init; }
        public decimal Price { get; init; }
        public decimal ProcessingFee { get; init; }
        public DateTime CreatedOn { get; init; }
        public string ItemsName { get; init; } = string.Empty;
        public string? WeekNo { get; init; }
        public string? IngredientIds { get; init; }
        public string? Ingredients { get; init; }
        public string? NutritionList { get; init; }
        public string? StudentAllergies { get; init; }
    }
}
