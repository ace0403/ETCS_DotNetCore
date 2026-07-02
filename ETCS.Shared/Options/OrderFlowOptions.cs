namespace ETCS.Shared.Options;

public sealed class OrderFlowOptions
{
    public const string SectionName = "OrderFlow";

    public decimal DailySpendingLimit { get; set; } = 100m;

    public decimal WeeklySpendingLimit { get; set; } = 500m;

    public short AccessLogTransactionType { get; set; } = 1;

    public string AccessLogDescription { get; set; } = "MEAL ORDER";
}
