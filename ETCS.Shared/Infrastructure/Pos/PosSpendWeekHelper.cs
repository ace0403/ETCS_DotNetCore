namespace ETCS.Shared.Infrastructure.Pos;

public static class PosSpendWeekHelper
{
    /// <summary>
    /// Matches legacy Form2.cs spendinform week start (Sunday-based rolling window).
    /// </summary>
    public static DateTime GetWeekStartDate(DateTime reference)
    {
        return reference.DayOfWeek switch
        {
            DayOfWeek.Sunday => reference.Date,
            DayOfWeek.Monday => reference.Date.AddDays(-1),
            DayOfWeek.Tuesday => reference.Date.AddDays(-2),
            DayOfWeek.Wednesday => reference.Date.AddDays(-3),
            DayOfWeek.Thursday => reference.Date.AddDays(-4),
            DayOfWeek.Friday => reference.Date.AddDays(-5),
            DayOfWeek.Saturday => reference.Date.AddDays(-6),
            _ => reference.Date
        };
    }
}
