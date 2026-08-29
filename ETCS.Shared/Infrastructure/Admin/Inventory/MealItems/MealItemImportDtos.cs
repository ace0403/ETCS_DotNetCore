namespace ETCS.Shared.Infrastructure.Admin.Inventory.MealItems;

public enum MealItemImportRowStatus
{
    Ready,
    Exists,
    Invalid
}

public sealed class MealItemImportPreviewRow
{
    public string ItemName { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public IReadOnlyList<int> WeekNos { get; init; } = [];
    public IReadOnlyList<string> DayNames { get; init; } = [];
    public MealItemImportRowStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class MealItemImportPreviewResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int ParsedCount { get; init; }
    public int ToInsert { get; init; }
    public int SkippedExisting { get; init; }
    public int SkippedInvalid { get; init; }
    public int CategoriesCreated { get; init; }
    public IReadOnlyList<string> CreatedCategoryNames { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<MealItemImportPreviewRow> Rows { get; init; } = [];
    public string? ImportToken { get; init; }
}

public sealed class MealItemImportConfirmResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int Inserted { get; init; }
    public int SkippedExisting { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public sealed class MealItemBulkImportResult
{
    public int Inserted { get; init; }
    public int SkippedExisting { get; init; }
    public int Failed { get; init; }
    public IReadOnlyList<string> Errors { get; init; } = [];
}

public sealed class MealItemImportParseResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<MealItemImportParsedItem> Items { get; init; } = [];
    public IReadOnlyList<string> Warnings { get; init; } = [];
    public IReadOnlyList<string> CategoriesCreated { get; init; } = [];
}

public sealed class MealItemImportParsedItem
{
    public MealItemSaveRequest? Request { get; init; }
    public string ItemName { get; init; } = string.Empty;
    public string CategoryName { get; init; } = string.Empty;
    public IReadOnlyList<int> WeekNos { get; init; } = [];
    public IReadOnlyList<string> DayNames { get; init; } = [];
    public bool IsValid { get; init; }
    public string Message { get; init; } = string.Empty;
}

public sealed class MealItemImportCacheEntry
{
    public int SchoolId { get; init; }
    public int MealSessionId { get; init; }
    public int MealTypeId { get; init; }
    public int? CreatedBy { get; init; }
    public IReadOnlyList<MealItemSaveRequest> Items { get; init; } = [];
}
