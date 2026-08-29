namespace ETCS.Shared.Options;

public sealed class AdminOptions
{
    public const string SectionName = "Admin";

    public string StorePath { get; set; } = string.Empty;

    /// <summary>
    /// Boundary date (yyyy-MM-dd) for meal order payment reports.
    /// Legacy payment reports use this as the maximum selectable date; new payment reports use it as the minimum.
    /// </summary>
    public string? MealOrderReportCutoverDate { get; set; }
}
