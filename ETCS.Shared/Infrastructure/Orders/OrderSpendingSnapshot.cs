namespace ETCS.Shared.Infrastructure.Orders;

public sealed class OrderSpendingSnapshot
{
    public decimal DailySpent { get; init; }

    public decimal WeeklySpent { get; init; }
}
