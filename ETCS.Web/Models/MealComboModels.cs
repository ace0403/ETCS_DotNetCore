using ETCS.Shared.Infrastructure.Meals;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace ETCS.Web.Models;

public sealed class MealComboPageViewModel
{
    public int StudentId { get; set; }

    public int Duration { get; set; } = 5;

    public DateTime MealDate { get; set; } = DateTime.Today;

    public IReadOnlyList<AlaCarteChildOption> Children { get; init; } = [];

    public SelectList? DurationList { get; set; }
}

public sealed class MealComboSearchRequest
{
    [Required]
    [Range(1, int.MaxValue)]
    public int StudentId { get; set; }

    [Required]
    public DateTime MealDate { get; set; }
}

public sealed class MealComboSessionSection
{
    public string MealSessionId { get; init; } = string.Empty;

    public string MealSessionName { get; init; } = string.Empty;

    public string MealSessionCssClass { get; init; } = string.Empty;

    public IReadOnlyList<MealPackageDto> Packages { get; init; } = [];

    public IReadOnlyList<MealItemDto> AddonItems { get; init; } = [];

    public IReadOnlyList<MealComboMenuCard> DisplayItems { get; init; } = [];

    public IReadOnlyList<MealComboMealTypeFilterOption> MealTypeFilters { get; init; } = [];
}

public sealed class MealComboMenuCard
{
    public bool IsAddon { get; init; }

    public MealPackageDto? Package { get; init; }

    public MealItemDto? Addon { get; init; }
}

public sealed class MealComboMealTypeFilterOption
{
    public string FilterKey { get; init; } = string.Empty;

    public string TypeName { get; init; } = string.Empty;

    public int SortOrder { get; init; }
}

public sealed class MealComboSelectedLineRequest
{
    public int PackageId { get; set; }

    public int ItemId { get; set; }

    public string MealDate { get; set; } = string.Empty;

    public Guid Id { get; set; }
}

public sealed class MealComboSummaryViewModel
{
    public decimal OrderAmount { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public IReadOnlyList<MealComboSummaryItem> SelectedLines { get; init; } = [];

    public int ItemCount => SelectedLines.Count;

    public int DayCount => SelectedLines.Select(x => x.MealDate.Date).Distinct().Count();
}

public sealed class MealComboSummaryItem
{
    public int Id { get; init; }

    public Guid SelectionId { get; init; }

    public bool IsAddon { get; init; }

    public string PackageName { get; init; } = string.Empty;

    public string ItemName { get; init; } = string.Empty;

    public string DisplayName => IsAddon ? ItemName : PackageName;

    public string ItemsName { get; init; } = string.Empty;

    public string MealTypeName { get; init; } = string.Empty;

    public string MealSessionName { get; init; } = string.Empty;

    public string? Detail { get; init; }

    public decimal Price { get; init; }

    public DateTime MealDate { get; init; }

    public string? ImageName { get; init; }
}
