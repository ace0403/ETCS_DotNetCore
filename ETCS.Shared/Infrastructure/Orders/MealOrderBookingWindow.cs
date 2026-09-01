using System.Globalization;

namespace ETCS.Shared.Infrastructure.Orders;

public sealed class MealOrderBookingWindow
{
    public const int DefaultCutoffHour = 15;

    private static readonly TimeZoneInfo SchoolTimeZone = ResolveSchoolTimeZone();

    private readonly int _cutoffHour;

    public MealOrderBookingWindow(int cutoffHour = DefaultCutoffHour)
    {
        _cutoffHour = cutoffHour is >= 0 and <= 23 ? cutoffHour : DefaultCutoffHour;
    }

    public int CutoffHour => _cutoffHour;

    public DateTime GetSchoolLocalNow() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, SchoolTimeZone);

    public DateTime GetEarliestBookableDate()
    {
        var now = GetSchoolLocalNow();
        var today = now.Date;
        return now.TimeOfDay < TimeSpan.FromHours(_cutoffHour)
            ? today.AddDays(1)
            : today.AddDays(2);
    }

    public bool IsBookable(DateTime mealDate) =>
        mealDate.Date >= GetEarliestBookableDate();

    public string FormatClosedDateMessage(DateTime mealDate)
    {
        var display = mealDate.ToString("dd MMM", CultureInfo.InvariantCulture);
        return $"Orders for {display} closed at {FormatCutoffTime(_cutoffHour)}. Choose a later date.";
    }

    private static string FormatCutoffTime(int hour)
    {
        var h = ((hour % 24) + 24) % 24;
        if (h == 0)
        {
            return "12:00 AM";
        }

        if (h == 12)
        {
            return "12:00 PM";
        }

        return h < 12 ? $"{h}:00 AM" : $"{h - 12}:00 PM";
    }

    private static TimeZoneInfo ResolveSchoolTimeZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Asia/Dubai");
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById("Arabian Standard Time");
        }
        catch (TimeZoneNotFoundException)
        {
        }
        catch (InvalidTimeZoneException)
        {
        }

        return TimeZoneInfo.CreateCustomTimeZone(
            "Gulf Standard Time",
            TimeSpan.FromHours(4),
            "Gulf Standard Time",
            "Gulf Standard Time");
    }
}
