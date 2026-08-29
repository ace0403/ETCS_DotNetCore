namespace ETCS.Web.Models;

public sealed class DashboardPageViewModel
{
    public string GuardianDisplayName { get; init; } = string.Empty;

    public decimal TotalBalance { get; init; }

    public decimal? WalletBalanceChangePercent { get; init; }

    public decimal MonthlySpending { get; init; }

    public decimal? MonthlySpendingChangePercent { get; init; }

    public int TodayOrdersCount { get; init; }

    public int ActivePlansCount { get; init; }

    public int PendingOrdersCount { get; init; }

    public IReadOnlyList<DashboardChildItem> Children { get; init; } = [];

    public IReadOnlyList<DashboardActivityItem> RecentActivity { get; init; } = [];

    public IReadOnlyList<DashboardTodayMealItem> TodaysMeals { get; init; } = [];

    public IReadOnlyList<NotificationListItemViewModel> Notifications { get; init; } = [];

    public IReadOnlyList<DashboardChartPoint> MonthlySpendSeries { get; init; } = [];

    public IReadOnlyList<DashboardChartPoint> CategoryBreakdown { get; init; } = [];

    public bool ShowWallet { get; init; }

    public bool ShowPreOrderMeal { get; init; }
}

public sealed class DashboardChildItem
{
    public int StudentId { get; init; }

    public string Name { get; init; } = string.Empty;

    public decimal Balance { get; init; }

    public string CardId { get; init; } = string.Empty;
}

public sealed class DashboardChartPoint
{
    public string Label { get; init; } = string.Empty;

    public decimal Value { get; init; }
}

public sealed class DashboardActivityItem
{
    public string Title { get; init; } = string.Empty;

    public string RelativeTime { get; init; } = string.Empty;

    public string Icon { get; init; } = "shopping.svg";

    public string Tone { get; init; } = "tone-order";

    public string StatusLabel { get; init; } = string.Empty;

    public string StatusCss { get; init; } = string.Empty;

    public bool HasDetail { get; init; }

    public string DetailUrl { get; init; } = string.Empty;
}

public sealed class DashboardTodayMealItem
{
    public string StudentName { get; init; } = string.Empty;

    public string MealLabel { get; init; } = string.Empty;

    public string StatusLabel { get; init; } = string.Empty;

    public string StatusCss { get; init; } = "is-confirmed";

    public string DetailUrl { get; init; } = "#";
}
