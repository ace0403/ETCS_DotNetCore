using ETCS.Shared.Enumeration;

namespace ETCS.Web.Models;

public sealed class OrderCalendarPageViewModel
{
    public IReadOnlyList<HistoryChildOption> Children { get; init; } = [];

    public int? SelectedStudentId { get; init; }
}

public sealed class OrderCalendarEventItemDto
{
    public string ItemName { get; init; } = string.Empty;

    public decimal ItemPrice { get; init; }

    public int Quantity { get; init; }

    public string OrderId { get; init; } = string.Empty;

    public int OrderTypeId { get; init; }
}

public sealed class OrderCalendarEventDto
{
    public string Id { get; init; } = string.Empty;

    public string Title { get; init; } = string.Empty;

    public string Start { get; init; } = string.Empty;

    public string? Color { get; init; }

    public string? BorderColor { get; init; }

    public string? TextColor { get; init; }

    public OrderCalendarEventExtendedProps ExtendedProps { get; init; } = new();
}

public sealed class OrderCalendarEventExtendedProps
{
    public string MealDate { get; init; } = string.Empty;

    public int StudentId { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public int OrderTypeId { get; init; }

    public string OrderTypeLabel { get; init; } = string.Empty;

    public IReadOnlyList<OrderCalendarEventItemDto> Items { get; init; } = [];
}

public sealed class OrderCalendarSchoolDayDto
{
    public string Date { get; init; } = string.Empty;

    public byte Status { get; init; }

    public string StatusLabel { get; init; } = string.Empty;

    public string? Title { get; init; }

    public bool IsException { get; init; }
}
