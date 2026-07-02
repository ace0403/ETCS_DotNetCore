using ETCS.Shared.Infrastructure.Enums;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace ETCS.Web.Models;

public sealed class AlaCartePageViewModel
{
    public int StudentId { get; set; }

    public int Duration { get; set; } = 5;

    public DateTime MealDate { get; set; } = DateTime.Today;

    public IReadOnlyList<AlaCarteChildOption> Children { get; init; } = [];

    public SelectList? DurationList { get; set; }
}

public sealed class AlaCarteChildOption
{
    public int Id { get; init; }

    public string Name { get; init; } = string.Empty;
}
