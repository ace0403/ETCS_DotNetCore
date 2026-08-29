using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ETCS.Shared.Infrastructure.Admin.Inventory.Categories;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Enumeration;

namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;

public sealed class MealItemExcelImportService : IMealItemExcelImportService
{
    private static readonly Regex WeekSheetRegex = new(@"Week\s*(\d+)\s*Detailed", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly string[] RequiredColumns =
    [
        "Day",
        "Menu Category",
        "Component / Item",
        "Calories (kcal)",
        "Carbs (g)",
        "Protein (g)",
        "Fat (g)",
        "Suggested Allergens",
        "Verification Notes",
        "Row Type"
    ];

    private readonly IMealEnumAdminRepository _mealEnumAdminRepository;
    private readonly ICategoryAdminRepository _categoryAdminRepository;

    public MealItemExcelImportService(
        IMealEnumAdminRepository mealEnumAdminRepository,
        ICategoryAdminRepository categoryAdminRepository)
    {
        _mealEnumAdminRepository = mealEnumAdminRepository;
        _categoryAdminRepository = categoryAdminRepository;
    }

    public async Task<MealItemImportParseResult> ParseAsync(
        Stream fileStream,
        int schoolId,
        int mealSessionId,
        int mealTypeId,
        bool createMissingCategories = false,
        int? createdBy = null,
        CancellationToken cancellationToken = default)
    {
        if (schoolId <= 0 || mealSessionId <= 0 || mealTypeId <= 0)
        {
            return new MealItemImportParseResult
            {
                Success = false,
                Message = "School, meal session, and meal type are required."
            };
        }

        if (!await _mealEnumAdminRepository.IsMealTypeInSessionAsync(mealTypeId, mealSessionId, cancellationToken))
        {
            return new MealItemImportParseResult
            {
                Success = false,
                Message = "Selected meal type does not belong to the chosen meal session."
            };
        }

        var warnings = new List<string>();
        var lookup = await BuildLookupContextAsync(cancellationToken);

        using var workbook = new XLWorkbook(fileStream);
        var rawRows = new List<RawImportRow>();

        foreach (var worksheet in workbook.Worksheets)
        {
            var weekMatch = WeekSheetRegex.Match(worksheet.Name.Trim());
            if (!weekMatch.Success)
            {
                continue;
            }

            if (!int.TryParse(weekMatch.Groups[1].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var weekNo))
            {
                warnings.Add($"Could not parse week number from sheet '{worksheet.Name}'.");
                continue;
            }

            var columnMap = ReadHeaderRow(worksheet);
            if (columnMap is null)
            {
                return new MealItemImportParseResult
                {
                    Success = false,
                    Message = $"Sheet '{worksheet.Name}' is missing required columns."
                };
            }

            var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 4;
            for (var rowNo = 5; rowNo <= lastRow; rowNo++)
            {
                var rowType = GetCellString(worksheet, rowNo, columnMap["Row Type"]);
                if (!string.Equals(rowType, "Item", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var itemName = GetCellString(worksheet, rowNo, columnMap["Component / Item"]);
                var categoryName = GetCellString(worksheet, rowNo, columnMap["Menu Category"]);
                var dayName = GetCellString(worksheet, rowNo, columnMap["Day"]);

                if (string.IsNullOrWhiteSpace(itemName) || string.IsNullOrWhiteSpace(categoryName))
                {
                    continue;
                }

                rawRows.Add(new RawImportRow
                {
                    WeekNo = weekNo,
                    DayName = dayName,
                    ItemName = itemName.Trim(),
                    CategoryName = categoryName.Trim(),
                    Calories = GetCellDecimal(worksheet, rowNo, columnMap["Calories (kcal)"]),
                    Carbs = GetCellDecimal(worksheet, rowNo, columnMap["Carbs (g)"]),
                    Protein = GetCellDecimal(worksheet, rowNo, columnMap["Protein (g)"]),
                    Fat = GetCellDecimal(worksheet, rowNo, columnMap["Fat (g)"]),
                    Allergens = GetCellString(worksheet, rowNo, columnMap["Suggested Allergens"]),
                    Detail = GetCellString(worksheet, rowNo, columnMap["Verification Notes"])
                });
            }
        }

        if (rawRows.Count == 0)
        {
            return new MealItemImportParseResult
            {
                Success = false,
                Message = "No item rows were found in Week 1-4 Detailed sheets."
            };
        }

        var categoriesCreated = new List<string>();
        if (createMissingCategories)
        {
            var distinctCategoryNames = rawRows
                .Select(r => r.CategoryName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
                .ToList();

            categoriesCreated = await CreateMissingCategoriesAsync(distinctCategoryNames, lookup, createdBy, cancellationToken);
            if (categoriesCreated.Count > 0)
            {
                lookup = await BuildLookupContextAsync(cancellationToken);
            }
        }

        var grouped = rawRows
            .GroupBy(r => (r.ItemName, r.CategoryName), StringTupleComparer.Instance)
            .ToList();

        var parsedItems = new List<MealItemImportParsedItem>();
        foreach (var group in grouped)
        {
            var first = group.First();
            var weekNos = group.Select(r => r.WeekNo).Distinct().OrderBy(w => w).ToList();
            var dayNames = group
                .Select(r => r.DayName)
                .Where(d => !string.IsNullOrWhiteSpace(d))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(d => d, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var dayIds = new List<int>();
            foreach (var dayName in dayNames)
            {
                if (lookup.DayIdsByName.TryGetValue(NormalizeKey(dayName), out var dayId))
                {
                    dayIds.Add(dayId);
                }
            }

            if (dayIds.Count == 0)
            {
                parsedItems.Add(new MealItemImportParsedItem
                {
                    ItemName = group.Key.ItemName,
                    CategoryName = group.Key.CategoryName,
                    WeekNos = weekNos,
                    DayNames = dayNames,
                    IsValid = false,
                    Message = "No valid days were found for this item."
                });
                continue;
            }

            if (!lookup.CategoryIdsByName.TryGetValue(NormalizeKey(group.Key.CategoryName), out var categoryId))
            {
                warnings.Add($"Unmapped menu category '{group.Key.CategoryName}' for item '{group.Key.ItemName}'.");
                parsedItems.Add(new MealItemImportParsedItem
                {
                    ItemName = group.Key.ItemName,
                    CategoryName = group.Key.CategoryName,
                    WeekNos = weekNos,
                    DayNames = dayNames,
                    IsValid = false,
                    Message = $"Menu category '{group.Key.CategoryName}' was not found."
                });
                continue;
            }

            var nutritionLines = BuildNutritionLines(first, lookup, warnings, group.Key.ItemName);
            if (nutritionLines.Count == 0)
            {
                parsedItems.Add(new MealItemImportParsedItem
                {
                    ItemName = group.Key.ItemName,
                    CategoryName = group.Key.CategoryName,
                    WeekNos = weekNos,
                    DayNames = dayNames,
                    IsValid = false,
                    Message = "Nutrition values could not be mapped."
                });
                continue;
            }

            var ingredientIds = MapAllergens(first.Allergens, lookup, warnings, group.Key.ItemName);
            var detail = group
                .Select(r => r.Detail)
                .FirstOrDefault(d => !string.IsNullOrWhiteSpace(d));

            var request = new MealItemSaveRequest
            {
                SchoolId = schoolId,
                SchoolIds = [schoolId],
                MealSessionId = mealSessionId,
                MealTypeId = mealTypeId,
                MealCategoryId = categoryId,
                ItemName = group.Key.ItemName,
                Detail = detail,
                Price = 0m,
                IsActive = true,
                OrderTypeIds = [MealItemChannelOptionIds.DefaultWhenMissing],
                IngredientIds = ingredientIds,
                WeekNos = weekNos,
                DayIds = dayIds.Distinct().OrderBy(d => d).ToList(),
                NutritionLines = nutritionLines
            };

            parsedItems.Add(new MealItemImportParsedItem
            {
                Request = request,
                ItemName = group.Key.ItemName,
                CategoryName = group.Key.CategoryName,
                WeekNos = weekNos,
                DayNames = dayNames,
                IsValid = true,
                Message = string.Empty
            });
        }

        return new MealItemImportParseResult
        {
            Success = true,
            Message = $"Parsed {parsedItems.Count} unique item(s).",
            Items = parsedItems,
            Warnings = warnings.Distinct(StringComparer.Ordinal).ToList(),
            CategoriesCreated = categoriesCreated
        };
    }

    private async Task<List<string>> CreateMissingCategoriesAsync(
        IReadOnlyList<string> categoryNames,
        LookupContext lookup,
        int? createdBy,
        CancellationToken cancellationToken)
    {
        var created = new List<string>();
        if (categoryNames.Count == 0)
        {
            return created;
        }

        var existingCategories = await _categoryAdminRepository.ListAsync(cancellationToken);
        var maxSortOrder = existingCategories.Count > 0 ? existingCategories.Max(c => c.SortOrder) : 0;

        foreach (var categoryName in categoryNames)
        {
            if (lookup.CategoryIdsByName.ContainsKey(NormalizeKey(categoryName)))
            {
                continue;
            }

            maxSortOrder++;
            var result = await _categoryAdminRepository.SaveAsync(
                new CategorySaveRequest
                {
                    CategoryName = categoryName,
                    SortOrder = maxSortOrder,
                    IsActive = true,
                    CreatedBy = createdBy
                },
                cancellationToken);

            if (result.Success)
            {
                created.Add(categoryName);
            }
        }

        return created;
    }

    private async Task<LookupContext> BuildLookupContextAsync(CancellationToken cancellationToken)
    {
        var categories = await _categoryAdminRepository.ListAsync(cancellationToken);
        var ingredients = await _mealEnumAdminRepository.GetByTypeIdAsync(MealEnumTypeIds.FoodAllergy, cancellationToken);
        var weekDays = await _mealEnumAdminRepository.GetByTypeIdAsync(MealEnumTypeIds.WeekDays, cancellationToken);
        var nutritionTypes = await _mealEnumAdminRepository.GetByTypeIdAsync(MealEnumTypeIds.Nutrition, cancellationToken);
        var measureTypes = await _mealEnumAdminRepository.GetByTypeIdAsync(MealEnumTypeIds.MeasureType, cancellationToken);

        var categoryIdsByName = categories
            .Where(c => !string.IsNullOrWhiteSpace(c.CategoryName))
            .GroupBy(c => NormalizeKey(c.CategoryName))
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

        var ingredientIdsByName = ingredients
            .Where(i => !string.IsNullOrWhiteSpace(i.Name))
            .GroupBy(i => NormalizeKey(i.Name))
            .ToDictionary(g => g.Key, g => g.First().Id, StringComparer.Ordinal);

        var dayIdsByName = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var day in weekDays)
        {
            if (!string.IsNullOrWhiteSpace(day.Name))
            {
                dayIdsByName[NormalizeKey(day.Name)] = day.Id;
            }
        }

        foreach (DaysEnum day in Enum.GetValues<DaysEnum>())
        {
            var key = NormalizeKey(day.ToString());
            if (!dayIdsByName.ContainsKey(key))
            {
                dayIdsByName[key] = (int)day;
            }
        }

        return new LookupContext
        {
            CategoryIdsByName = categoryIdsByName,
            IngredientIdsByName = ingredientIdsByName,
            DayIdsByName = dayIdsByName,
            NutritionTypes = nutritionTypes,
            MeasureTypes = measureTypes,
            CaloriesNutritionId = ResolveNutritionId(nutritionTypes, "calor", "energy"),
            CarbsNutritionId = ResolveNutritionId(nutritionTypes, "carb"),
            ProteinNutritionId = ResolveNutritionId(nutritionTypes, "protein"),
            FatNutritionId = ResolveNutritionId(nutritionTypes, "fat"),
            KcalMeasureTypeId = ResolveMeasureTypeId(measureTypes, "kcal", "cal"),
            GramMeasureTypeId = ResolveMeasureTypeId(measureTypes, "g", "gram")
        };
    }

    private static List<MealItemNutritionLineDto> BuildNutritionLines(
        RawImportRow row,
        LookupContext lookup,
        List<string> warnings,
        string itemName)
    {
        var lines = new List<MealItemNutritionLineDto>();

        if (row.Calories.HasValue && lookup.CaloriesNutritionId > 0 && lookup.KcalMeasureTypeId > 0)
        {
            lines.Add(new MealItemNutritionLineDto
            {
                NutritionId = lookup.CaloriesNutritionId,
                MeasureValue = row.Calories.Value,
                MeasureTypeId = lookup.KcalMeasureTypeId
            });
        }

        if (row.Carbs.HasValue && lookup.CarbsNutritionId > 0 && lookup.GramMeasureTypeId > 0)
        {
            lines.Add(new MealItemNutritionLineDto
            {
                NutritionId = lookup.CarbsNutritionId,
                MeasureValue = row.Carbs.Value,
                MeasureTypeId = lookup.GramMeasureTypeId
            });
        }

        if (row.Protein.HasValue && lookup.ProteinNutritionId > 0 && lookup.GramMeasureTypeId > 0)
        {
            lines.Add(new MealItemNutritionLineDto
            {
                NutritionId = lookup.ProteinNutritionId,
                MeasureValue = row.Protein.Value,
                MeasureTypeId = lookup.GramMeasureTypeId
            });
        }

        if (row.Fat.HasValue && lookup.FatNutritionId > 0 && lookup.GramMeasureTypeId > 0)
        {
            lines.Add(new MealItemNutritionLineDto
            {
                NutritionId = lookup.FatNutritionId,
                MeasureValue = row.Fat.Value,
                MeasureTypeId = lookup.GramMeasureTypeId
            });
        }

        if (lines.Count == 0)
        {
            if (lookup.CaloriesNutritionId <= 0 || lookup.KcalMeasureTypeId <= 0)
            {
                warnings.Add($"Calories nutrition/measure enums are not configured for item '{itemName}'.");
            }
            if (lookup.GramMeasureTypeId <= 0)
            {
                warnings.Add($"Gram measure enum is not configured for item '{itemName}'.");
            }
        }

        return lines;
    }

    private static List<int> MapAllergens(
        string? allergensRaw,
        LookupContext lookup,
        List<string> warnings,
        string itemName)
    {
        if (string.IsNullOrWhiteSpace(allergensRaw))
        {
            return [];
        }

        var ingredientIds = new List<int>();
        foreach (var part in allergensRaw.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (part.Equals("None identified", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (lookup.IngredientIdsByName.TryGetValue(NormalizeKey(part), out var ingredientId))
            {
                ingredientIds.Add(ingredientId);
                continue;
            }

            warnings.Add($"Unmapped allergen '{part}' for item '{itemName}'.");
        }

        return ingredientIds.Distinct().ToList();
    }

    private static Dictionary<string, int>? ReadHeaderRow(IXLWorksheet worksheet)
    {
        var columnMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var headerRow = worksheet.Row(4);
        var lastColumn = worksheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        for (var col = 1; col <= lastColumn; col++)
        {
            var header = headerRow.Cell(col).GetString().Trim();
            if (!string.IsNullOrWhiteSpace(header))
            {
                columnMap[header] = col;
            }
        }

        foreach (var required in RequiredColumns)
        {
            if (!columnMap.ContainsKey(required))
            {
                return null;
            }
        }

        return columnMap;
    }

    private static string GetCellString(IXLWorksheet worksheet, int row, int column)
    {
        return worksheet.Cell(row, column).GetString().Trim();
    }

    private static decimal? GetCellDecimal(IXLWorksheet worksheet, int row, int column)
    {
        var cell = worksheet.Cell(row, column);
        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.TryGetValue(out double number))
        {
            return Convert.ToDecimal(number);
        }

        return decimal.TryParse(cell.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : null;
    }

    private static int ResolveNutritionId(IReadOnlyList<MealEnumLookupDto> nutritionTypes, params string[] tokens)
    {
        foreach (var nutrition in nutritionTypes)
        {
            var name = nutrition.Name ?? string.Empty;
            if (tokens.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                return nutrition.Id;
            }
        }

        return 0;
    }

    private static int ResolveMeasureTypeId(IReadOnlyList<MealEnumLookupDto> measureTypes, params string[] tokens)
    {
        foreach (var measure in measureTypes)
        {
            var name = NormalizeKey(measure.Name ?? string.Empty);
            if (tokens.Any(token => name.Equals(NormalizeKey(token), StringComparison.Ordinal)
                                    || name.Contains(NormalizeKey(token), StringComparison.Ordinal)))
            {
                return measure.Id;
            }
        }

        return 0;
    }

    private static string NormalizeKey(string value) =>
        value.Trim().ToLowerInvariant();

    private sealed class RawImportRow
    {
        public int WeekNo { get; init; }
        public string DayName { get; init; } = string.Empty;
        public string ItemName { get; init; } = string.Empty;
        public string CategoryName { get; init; } = string.Empty;
        public decimal? Calories { get; init; }
        public decimal? Carbs { get; init; }
        public decimal? Protein { get; init; }
        public decimal? Fat { get; init; }
        public string? Allergens { get; init; }
        public string? Detail { get; init; }
    }

    private sealed class LookupContext
    {
        public required Dictionary<string, int> CategoryIdsByName { get; init; }
        public required Dictionary<string, int> IngredientIdsByName { get; init; }
        public required Dictionary<string, int> DayIdsByName { get; init; }
        public required IReadOnlyList<MealEnumLookupDto> NutritionTypes { get; init; }
        public required IReadOnlyList<MealEnumLookupDto> MeasureTypes { get; init; }
        public int CaloriesNutritionId { get; init; }
        public int CarbsNutritionId { get; init; }
        public int ProteinNutritionId { get; init; }
        public int FatNutritionId { get; init; }
        public int KcalMeasureTypeId { get; init; }
        public int GramMeasureTypeId { get; init; }
    }

    private sealed class StringTupleComparer : IEqualityComparer<(string ItemName, string CategoryName)>
    {
        public static StringTupleComparer Instance { get; } = new();

        public bool Equals((string ItemName, string CategoryName) x, (string ItemName, string CategoryName) y) =>
            string.Equals(x.ItemName, y.ItemName, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.CategoryName, y.CategoryName, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string ItemName, string CategoryName) obj) =>
            HashCode.Combine(
                obj.ItemName.ToLowerInvariant(),
                obj.CategoryName.ToLowerInvariant());
    }
}
