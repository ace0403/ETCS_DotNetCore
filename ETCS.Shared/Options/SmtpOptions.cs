namespace ETCS.Shared.Options;

/// <summary>
/// Email delivery worker tuning. SMTP connection details are loaded from MealDB dbo.SmtpSettings.
/// </summary>
public sealed class SmtpOptions
{
    public const string SectionName = "Smtp";

    public string DefaultFromName { get; set; } = "ETCS";

    public int PollIntervalSeconds { get; set; } = 30;

    public int BatchSize { get; set; } = 20;
}
