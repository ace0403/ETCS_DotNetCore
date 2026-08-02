namespace ETCS.Web.Models;

public sealed class TopupPageViewModel
{
    public IReadOnlyList<TopupChildItem> Children { get; init; } = [];

    public int SelectedStudentId { get; init; }

    public decimal TotalBalance { get; init; }

    public IReadOnlyList<DashboardChartPoint> WeeklySpendSeries { get; init; } = [];

    public IReadOnlyList<WalletRecentTransactionItem> RecentTransactions { get; init; } = [];
}

public sealed class TopupChildItem
{
    public int StudentId { get; init; }

    public string Name { get; init; } = string.Empty;

    public decimal Balance { get; init; }

    public decimal MinimumTopupAmount { get; init; }

    public string DisplayName => $"{Name} (AED {Balance:0.00})";
}

public sealed class TopupRequestModel
{
    public int StudentId { get; set; }

    public decimal Amount { get; set; }
}

public sealed class TopupPaymentReturnViewModel
{
    public bool IsSuccess { get; init; }

    public bool IsPending { get; init; }

    public string Message { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public string TransactionId { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string StudentName { get; init; } = string.Empty;
}

public sealed class WalletRecentTransactionItem
{
    public string Title { get; init; } = string.Empty;

    public string Reference { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public bool IsCredit { get; init; }

    /// <summary>Tabler icon class without the shared "ti" prefix, e.g. "ti-wallet".</summary>
    public string IconClass { get; init; } = "ti-receipt";

    /// <summary>Icon tone class: is-topup | is-debit.</summary>
    public string IconToneCss { get; init; } = "is-debit";

    public string StatusLabel { get; init; } = string.Empty;

    public string StatusCss { get; init; } = string.Empty;

    public string DetailUrl { get; init; } = string.Empty;
}
