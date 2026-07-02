namespace ETCS.Shared.Infrastructure.Pos;

public sealed class PosLegacySpendLimitRow
{
    public decimal DailyPurchaseAmount { get; init; }
    public decimal DailyUndoPurchaseAmount { get; init; }
    public decimal WeeklyPurchaseAmount { get; init; }
    public decimal WeeklyUndoPurchaseAmount { get; init; }
    public decimal DailySpendLimit { get; init; }
    public decimal WeeklySpendLimit { get; init; }

    public decimal DailyNetSpent => DailyPurchaseAmount - DailyUndoPurchaseAmount;
    public decimal WeeklyNetSpent => WeeklyPurchaseAmount - WeeklyUndoPurchaseAmount;

    public bool IsWeeklyLimitExceeded => WeeklySpendLimit > 0 && WeeklyNetSpent > WeeklySpendLimit;
    public bool IsDailyLimitExceeded => DailySpendLimit > 0 && DailyNetSpent > DailySpendLimit;
}
