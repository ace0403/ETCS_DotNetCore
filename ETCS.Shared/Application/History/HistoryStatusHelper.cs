using ETCS.Shared.Enumeration;

namespace ETCS.Shared.Application.History;

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
