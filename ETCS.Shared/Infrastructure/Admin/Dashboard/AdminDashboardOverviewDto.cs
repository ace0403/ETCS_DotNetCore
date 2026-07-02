namespace ETCS.Shared.Infrastructure.Admin.Dashboard;

public sealed class AdminDashboardOverviewDto
{
    public AdminDashboardSummaryDto Summary { get; init; } = new();
    public IReadOnlyList<AdminDashboardDailyPointDto> DailySeries { get; init; } = [];
    public IReadOnlyList<AdminDashboardTypeBreakdownDto> TypeBreakdown { get; init; } = [];
    public IReadOnlyList<AdminDashboardTerminalDto> TopTerminals { get; init; } = [];
    public IReadOnlyList<AdminDashboardRecentTransactionDto> RecentTransactions { get; init; } = [];
}

public sealed class AdminDashboardSummaryDto
{
    public decimal TotalSales { get; init; }
    public int TransactionCount { get; init; }
    public decimal StudentCardSales { get; init; }
    public decimal CashCardSales { get; init; }
    public decimal PriorTotalSales { get; init; }
    public int PriorTransactionCount { get; init; }
    public decimal PriorStudentCardSales { get; init; }
    public decimal PriorCashCardSales { get; init; }
    public DateTime PriorStartDate { get; init; }
    public DateTime PriorEndDate { get; init; }

    public decimal? TotalSalesTrendPercent =>
        CalculateTrendPercent(TotalSales, PriorTotalSales);

    public decimal? TransactionCountTrendPercent =>
        CalculateTrendPercent(TransactionCount, PriorTransactionCount);

    public decimal? StudentCardSalesTrendPercent =>
        CalculateTrendPercent(StudentCardSales, PriorStudentCardSales);

    public decimal? CashCardSalesTrendPercent =>
        CalculateTrendPercent(CashCardSales, PriorCashCardSales);

    private static decimal? CalculateTrendPercent(decimal current, decimal prior)
    {
        if (prior == 0m)
        {
            return current == 0m ? 0m : null;
        }

        return Math.Round((current - prior) / prior * 100m, 1);
    }

    private static decimal? CalculateTrendPercent(int current, int prior)
    {
        if (prior == 0)
        {
            return current == 0 ? 0m : null;
        }

        return Math.Round((current - prior) / (decimal)prior * 100m, 1);
    }
}

public sealed class AdminDashboardDailyPointDto
{
    public DateTime Day { get; init; }
    public decimal SalesAmount { get; init; }
    public int TransactionCount { get; init; }
}

public sealed class AdminDashboardTypeBreakdownDto
{
    public string Label { get; init; } = string.Empty;
    public decimal Amount { get; init; }
}

public sealed class AdminDashboardTerminalDto
{
    public string TerminalCode { get; init; } = string.Empty;
    public string TerminalName { get; init; } = string.Empty;
    public decimal SalesAmount { get; init; }
}

public sealed class AdminDashboardRecentTransactionDto
{
    public DateTime Datetime { get; init; }
    public string StudentCardNo { get; init; } = string.Empty;
    public string StudentName { get; init; } = string.Empty;
    public string TransactionType { get; init; } = string.Empty;
    public decimal Amount { get; init; }
    public string TerminalName { get; init; } = string.Empty;
}

public sealed class AdminDashboardFilter
{
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public string? SchoolCode { get; set; }
}
