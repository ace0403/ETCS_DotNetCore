using ETCS.Shared.Enumeration;
using System.Globalization;

namespace ETCS.Shared.Infrastructure.Schools.Calendar;

public sealed record SchoolDayInfo(
    DateTime Date,
    SchoolDayStatus Status,
    string? Title,
    bool IsException)
{
    public string GetClosedOrderMessage(DateTime? date = null)
    {
        var displayDate = date ?? Date;
        var label = string.IsNullOrWhiteSpace(Title)
            ? displayDate.ToString("dd MMM", CultureInfo.InvariantCulture)
            : Title.Trim();
        var reason = Status == SchoolDayStatus.HalfDay ? "half day" : "school holiday";
        return $"Orders are not available on {label} ({reason}). Choose another date.";
    }
}

public sealed class SchoolWeeklyDayDto
{
    public byte DayOfWeek { get; init; }

    public byte DayStatus { get; init; }
}

public sealed class SchoolWeeklyDaySaveRequest
{
    public byte DayOfWeek { get; set; }

    public byte DayStatus { get; set; }
}

public sealed class SchoolCalendarExceptionDto
{
    public int Id { get; init; }

    public int SchoolId { get; init; }

    public DateTime ExceptionDate { get; init; }

    public byte DayStatus { get; init; }

    public string Title { get; init; } = string.Empty;

    public string? Notes { get; init; }

    public string SchoolName { get; set; } = string.Empty;

    public string DayStatusLabel { get; set; } = string.Empty;
}

public sealed class SchoolCalendarExceptionSaveRequest
{
    public int Id { get; set; }

    public int SchoolId { get; set; }

    public DateTime ExceptionDate { get; set; }

    public byte DayStatus { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Notes { get; set; }
}

public sealed class SchoolWeeklyScheduleSaveRequest
{
    public int SchoolId { get; set; }

    public List<SchoolWeeklyDaySaveRequest> Days { get; set; } = [];
}
