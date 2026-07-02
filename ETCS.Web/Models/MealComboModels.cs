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

public sealed class MealComboPackageTypeGroup
{
    public string MealTypeId { get; init; } = string.Empty;

    public string MealTypeName { get; init; } = string.Empty;

    public string MealCssClass { get; init; } = string.Empty;

    public IReadOnlyList<MealPackageDto> Packages { get; init; } = [];
}

public sealed class MealComboSelectedPackageRequest
{
    public int PackageId { get; set; }

    public string MealDate { get; set; } = string.Empty;

    public Guid Id { get; set; }
}

public sealed class MealComboSummaryViewModel
{
    public decimal OrderAmount { get; set; }

    public string StudentName { get; set; } = string.Empty;

    public IReadOnlyList<MealComboSummaryItem> SelectedPackages { get; init; } = [];

    public int ItemCount => SelectedPackages.Count;

    public int DayCount => SelectedPackages.Select(x => x.MealDate.Date).Distinct().Count();
}

public sealed class MealComboSummaryItem
{
    public int Id { get; init; }

    public Guid SelectionId { get; init; }

    public string PackageName { get; init; } = string.Empty;

    public string ItemsName { get; init; } = string.Empty;

    public string MealTypeName { get; init; } = string.Empty;

    public string? Detail { get; init; }

    public decimal Price { get; init; }

    public DateTime MealDate { get; init; }

    public string? ImageName { get; init; }
}
