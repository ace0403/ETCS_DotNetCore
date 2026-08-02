namespace ETCS.Shared.Options;

public sealed class PendingPaymentReconcileOptions
{
    public const string SectionName = "PendingPaymentReconcile";

    public bool Enabled { get; set; } = true;

    public int IntervalMinutes { get; set; } = 5;

    public int LookbackHours { get; set; } = 24;

    public int MaxAttempts { get; set; } = 3;

    public int BatchSize { get; set; } = 50;
}
