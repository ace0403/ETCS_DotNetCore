using ETCS.Shared.Enumeration;

namespace ETCS.Web.Models;

public sealed class HistoryPageViewModel
{
    public IReadOnlyList<HistoryChildOption> Children { get; init; } = [];

    public int? SelectedStudentId { get; init; }

    public string SelectedType { get; init; } = "all";

    public DateTime? FromDate { get; init; }

    public DateTime? ToDate { get; init; }

    public int Page { get; init; } = 1;

    public int PageSize { get; init; } = 20;

    public int TotalCount { get; init; }

    public IReadOnlyList<HistoryListItemViewModel> Items { get; init; } = [];

    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed class HistoryChildOption
{
    public int StudentId { get; init; }

    public string Name { get; init; } = string.Empty;
}

public sealed class HistoryListItemViewModel
{
    public int Id { get; init; }

    public string TransactionType { get; init; } = "topup";

    public int? OrderTypeId { get; init; }

    public string TypeLabel { get; init; } = string.Empty;

    public string StudentName { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public bool IsCredit { get; init; }

    public string StatusLabel { get; init; } = string.Empty;

    public string StatusCss { get; init; } = string.Empty;

    public bool IsCompleted { get; init; }

    public bool IsPending { get; init; }

    public DateTime CreatedOn { get; init; }

    public string DetailUrl { get; init; } = string.Empty;
}

public sealed class HistoryTopupDetailViewModel
{
    public int Id { get; init; }

    public string StudentName { get; init; } = string.Empty;

    public decimal Amount { get; init; }

    public string StatusLabel { get; init; } = string.Empty;

    public string StatusCss { get; init; } = string.Empty;

    public bool IsCompleted { get; init; }

    public bool IsPending { get; init; }

    public string Reference { get; init; } = string.Empty;

    public string GatewayTransactionId { get; init; } = string.Empty;

    public string Remarks { get; init; } = string.Empty;

    public DateTime CreatedOn { get; init; }
}

public sealed class HistoryOrderDetailViewModel
{
    public bool IsSuccess { get; init; }

    public bool IsPending { get; init; }

    public string StatusLabel { get; init; } = string.Empty;

    public string StatusCss { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;

    public string OrderId { get; init; } = string.Empty;

    public DateTime CreatedOn { get; init; }

    public int OrderTypeId { get; init; }

    public AlaCarteSummaryViewModel? AlaCarteSummary { get; init; }

    public MealComboSummaryViewModel? ComboSummary { get; init; }
}

public static class HistoryStatusHelper
{
    public static (string Label, string Css, bool IsPending, bool IsCompleted, bool IsFailed) Resolve(
        int? statusId,
        bool isTransactionCompleted)
    {
        return statusId switch
        {
            (int)TransactionStatusEnum.Success =>
                ("Completed", "is-success", false, true, false),
            (int)TransactionStatusEnum.Pending or
            (int)TransactionStatusEnum.Initiated =>
                ("Pending", "is-pending", true, false, false),
            (int)TransactionStatusEnum.Failed =>
                ("Failed", "is-failed", false, false, true),
            (int)TransactionStatusEnum.Cancelled =>
                ("Cancelled", "is-failed", false, false, true),
            _ when isTransactionCompleted =>
                ("Completed", "is-success", false, true, false),
            _ =>
                ("Pending", "is-pending", true, false, false)
        };
    }

    /// <summary>
    /// Resolves display status when Order and Transaction may disagree (e.g. payment session failed).
    /// Failed/Cancelled on either side wins; Completed if either side succeeded or paid flags are set.
    /// </summary>
    public static (string Label, string Css, bool IsPending, bool IsCompleted, bool IsFailed) ResolveCanonical(
        int? orderStatusId,
        int? transactionStatusId,
        bool isPaid,
        bool isTransactionCompleted)
    {
        if (IsFailedOrCancelled(transactionStatusId))
        {
            return Resolve(transactionStatusId, false);
        }

        if (IsFailedOrCancelled(orderStatusId))
        {
            return Resolve(orderStatusId, false);
        }

        if (transactionStatusId == (int)TransactionStatusEnum.Success
            || orderStatusId == (int)TransactionStatusEnum.Success
            || isPaid
            || isTransactionCompleted)
        {
            return Resolve((int)TransactionStatusEnum.Success, true);
        }

        return Resolve(transactionStatusId ?? orderStatusId, isTransactionCompleted || isPaid);
    }

    public static bool CountsTowardSpend(int? statusId, bool isTransactionCompleted)
    {
        var status = Resolve(statusId, isTransactionCompleted);
        return status.IsCompleted && !status.IsFailed;
    }

    private static bool IsFailedOrCancelled(int? statusId) =>
        statusId is (int)TransactionStatusEnum.Failed or (int)TransactionStatusEnum.Cancelled;
}
