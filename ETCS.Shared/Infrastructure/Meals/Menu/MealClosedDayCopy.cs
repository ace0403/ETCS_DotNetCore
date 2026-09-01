using System.Globalization;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Schools.Calendar;

namespace ETCS.Shared.Infrastructure.Meals.Menu;

public static class MealClosedDayCopy
{
    public const string PleaseSelectAnotherDay = "Please select another day.";

    public static bool IsWeekend(DateTime date) =>
        date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;

    public static string StatusName(SchoolDayStatus status) => status switch
    {
        SchoolDayStatus.HalfDay => "HalfDay",
        SchoolDayStatus.Holiday => "Holiday",
        _ => "FullDay"
    };

    public static string? NormalizeTitle(string? title)
    {
        var value = title?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (string.Equals(value, "holiday", StringComparison.OrdinalIgnoreCase)
            || string.Equals(value, "half day", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return value;
    }

    public static MealClosedDayDto CreateCutoff(DateTime mealDate, string cutoffMessage)
    {
        return new MealClosedDayDto
        {
            ClosedType = "cutoff",
            DayName = mealDate.ToString("dddd", CultureInfo.InvariantCulture),
            BadgeText = "No meal service today",
            MessageLine1 = cutoffMessage,
            MessageLine2 = PleaseSelectAnotherDay,
            Title = null
        };
    }

    public static MealClosedDayDto Create(DateTime mealDate, SchoolDayInfo day)
    {
        var title = NormalizeTitle(day.Title);
        var isWeekend = IsWeekend(mealDate);
        var dayName = mealDate.ToString("dddd", CultureInfo.InvariantCulture);

        if (day.Status == SchoolDayStatus.HalfDay)
        {
            return new MealClosedDayDto
            {
                ClosedType = "halfday",
                DayName = dayName,
                BadgeText = "Half day",
                MessageLine1 = "Meal ordering is not available on half day.",
                MessageLine2 = PleaseSelectAnotherDay,
                Title = title
            };
        }

        if (isWeekend && title is null)
        {
            return new MealClosedDayDto
            {
                ClosedType = "weekend",
                DayName = dayName,
                BadgeText = "No meal service today",
                MessageLine1 = "Meal ordering is not available on weekend.",
                MessageLine2 = PleaseSelectAnotherDay,
                Title = null
            };
        }

        if (title is not null)
        {
            return new MealClosedDayDto
            {
                ClosedType = "holiday",
                DayName = dayName,
                BadgeText = "No meal service today",
                MessageLine1 = "Meal ordering is not available on holiday.",
                MessageLine2 = PleaseSelectAnotherDay,
                Title = title
            };
        }

        return new MealClosedDayDto
        {
            ClosedType = "holiday",
            DayName = dayName,
            BadgeText = "No meal service today",
            MessageLine1 = "Meal ordering is not available on school holidays.",
            MessageLine2 = PleaseSelectAnotherDay,
            Title = null
        };
    }

    public static string? ResolveBadge(DateTime date, SchoolDayStatus status, string? title)
    {
        if (status == SchoolDayStatus.HalfDay)
        {
            return "HALF DAY";
        }

        if (status == SchoolDayStatus.Holiday)
        {
            if (IsWeekend(date))
            {
                return "WEEKEND";
            }

            var named = NormalizeTitle(title);
            return string.IsNullOrWhiteSpace(named) ? "HOLIDAY" : named.ToUpperInvariant();
        }

        return null;
    }

    public static string? ResolveClosedType(DateTime date, SchoolDayStatus status, string? title)
    {
        if (status == SchoolDayStatus.FullDay)
        {
            return null;
        }

        if (status == SchoolDayStatus.HalfDay)
        {
            return "halfday";
        }

        if (IsWeekend(date) && NormalizeTitle(title) is null)
        {
            return "weekend";
        }

        return "holiday";
    }
}
