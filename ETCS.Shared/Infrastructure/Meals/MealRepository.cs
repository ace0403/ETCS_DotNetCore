using Dapper;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Data;
using ETCS.Shared.Media;
using System.Data;
using System.Data.Common;
using System.Text.Json;

namespace ETCS.Shared.Infrastructure.Meals;

public sealed class MealRepository : IMealRepository
{
    private const string GetMealItemsForStudentSp = "GetMealItemsForStudent";
    private const string GetMealPackagesForStudentSp = "GetMealPackagesForStudent";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IMealDbConnectionFactory _connectionFactory;
    private readonly MealImageUrlBuilder _imageUrlBuilder;

    public MealRepository(IMealDbConnectionFactory connectionFactory, MealImageUrlBuilder imageUrlBuilder)
    {
        _connectionFactory = connectionFactory;
        _imageUrlBuilder = imageUrlBuilder;
    }

    public async Task<IReadOnlyList<MealItemDto>> GetMealItemsForStudentAsync(
        int studentId,
        int schoolId,
        DateTime mealDate,
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
                    MealTypeId = mealTypeId
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken))).AsList();

        var items = new List<MealItemDto>(rows.Count);
        foreach (var row in rows)
        {
            items.Add(MapToDto(row));
        }

        return items;
    }

    public async Task<IReadOnlyList<MealPackageDto>> GetMealPackagesForStudentAsync(
        int studentId,
        int schoolId,
        DateTime mealDate,
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
                    MealTypeId = mealTypeId
                },
                commandType: CommandType.StoredProcedure,
                cancellationToken: cancellationToken))).AsList();

        var packages = new List<MealPackageDto>(rows.Count);
        foreach (var row in rows)
        {
            packages.Add(MapToPackageDto(row));
        }

        return packages;
    }

    private MealItemDto MapToDto(MealItemDbRow row)
    {
        return new MealItemDto
        {
            Id = row.Id,
            ItemName = row.ItemName,
            MealTypeId = row.MealTypeId,
            MealTypeName = row.MealTypeName,
            MealCssClass = row.MealCssClass,
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
            NutritionList = ParseJsonList(row.NutritionList),
            StudentAllergies = row.StudentAllergies ?? string.Empty
        };
    }

    private MealPackageDto MapToPackageDto(MealPackageDbRow row)
    {
        return new MealPackageDto
        {
            Id = row.Id,
            PackageName = row.PackageName,
            MealTypeId = row.MealTypeId,
            MealTypeName = row.MealTypeName,
            MealCssClass = row.MealCssClass,
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
            NutritionList = ParseJsonList(row.NutritionList),
            StudentAllergies = row.StudentAllergies ?? string.Empty
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
        public string MealTypeId { get; init; } = string.Empty;
        public string MealTypeName { get; init; } = string.Empty;
        public string MealCssClass { get; init; } = string.Empty;
        public int? MealCategoryId { get; init; }
        public string MealCategoryName { get; init; } = string.Empty;
        public int SchoolId { get; init; }
        public string? ImageName { get; init; }
        public string? Detail { get; init; }
        public decimal Price { get; init; }
        public DateTime CreatedOn { get; init; }
        public string? IngredientIds { get; init; }
        public string? NutritionList { get; init; }
        public string? StudentAllergies { get; init; }
    }

    private sealed class MealPackageDbRow
    {
        public int Id { get; init; }
        public string PackageName { get; init; } = string.Empty;
        public string MealTypeId { get; init; } = string.Empty;
        public string MealTypeName { get; init; } = string.Empty;
        public string MealCssClass { get; init; } = string.Empty;
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
        public string? NutritionList { get; init; }
        public string? StudentAllergies { get; init; }
    }
}
