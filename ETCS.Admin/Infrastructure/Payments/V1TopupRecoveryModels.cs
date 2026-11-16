namespace ETCS.Admin.Infrastructure.Payments;

public sealed class V1TopupRecoveryOptions
{
    public const string SectionName = "V1TopupRecovery";
    public bool Enabled { get; set; }
}

public sealed class V1TopupRecoveryFilter
{
    public DateTime From { get; set; } = DateTime.Today.AddDays(-7);
    public DateTime To { get; set; } = DateTime.Today;
    public string? Reference { get; set; }
    public int BeforeId { get; set; }
    public bool IncludeCompleted { get; set; }
}

public sealed class V1TopupRecoveryRow
{
    public int Id { get; init; }
    public int StudentId { get; init; }
    public string StudentName { get; init; } = "";
    public string OrderId { get; init; } = "";
    public string TransactionId { get; init; } = "";
    public decimal Amount { get; init; }
    public int StatusId { get; init; }
    public bool IsCompleted { get; init; }
    public long? AccessLogId { get; init; }
    public string SourceChannel { get; init; } = "";
    public int PaymentMethod { get; init; }
    public DateTime CreatedOn { get; init; }
    public DateTime? LastReconcileOn { get; init; }
    public int ReconcileAttemptCount { get; init; }
    public int StudentMatches { get; init; }
    public string CustomerId { get; init; } = "";
    public int ReceiptCount { get; init; }
    public decimal? ReceiptAmount { get; init; }
    public long? ReceiptId { get; init; }
    public string? ReviewReason => V1TopupRecoveryRules.GetReviewReason(this);
    public bool CanRetry => ReviewReason is null;
}

public sealed record V1TopupRecoveryResult(bool Success, bool StopBatch, string Status, string Message);

public static class V1TopupRecoveryRules
{
    public static string? GetReviewReason(V1TopupRecoveryRow row)
    {
        if (row.IsCompleted) return "Already completed; no retry needed.";
        if ((!string.IsNullOrWhiteSpace(row.SourceChannel) && row.SourceChannel != "Api")
            || row.PaymentMethod is not (0 or 4))
            return "Separate payment flow; manual review required.";
        if (row.StatusId is not (19 or 20 or 21))
            return "Closed or failed transaction; manual review required.";
        if (row.StudentId <= 0 || row.StudentMatches != 1 || string.IsNullOrWhiteSpace(row.CustomerId))
            return "Student or wallet could not be identified uniquely.";
        if (row.Amount <= 0 || string.IsNullOrWhiteSpace(row.OrderId) || string.IsNullOrWhiteSpace(row.TransactionId))
            return "Original payment details are incomplete.";
        if (row.ReceiptCount > 1 || (row.ReceiptCount == 1 &&
            (row.StatusId != 21 || row.ReceiptAmount != row.Amount)))
            return "Wallet entry already exists; review before retrying.";
        if (row.AccessLogId is > 0 && row.AccessLogId != row.ReceiptId)
            return "Saved wallet entry does not match this payment.";
        return null;
    }

    public static bool MatchesOriginal(V1TopupRecoveryRow row,
        ETCS.Shared.Infrastructure.Transaction.TopupPendingTransactionState state) =>
        state.TransactionPkId == row.Id && state.IsV1AppTopup &&
        !state.IsTransactionCompleted && state.StatusId is 19 or 20 or 21 &&
        state.StudentId == row.StudentId && state.Amount == row.Amount &&
        string.Equals(state.OrderId, row.OrderId, StringComparison.Ordinal) &&
        string.Equals(state.GatewayTransactionId, row.TransactionId, StringComparison.Ordinal);
}
