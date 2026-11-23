namespace ETCS.Shared.Infrastructure.Admin.Reports.PosReceiptReprint;

public interface IPosReceiptReprintRepository
{
    Task<PosReceiptReprintPagedResult> SearchPagedAsync(
        PosReceiptReprintSearchFilter filter,
        int draw,
        CancellationToken cancellationToken = default);

    Task<PosReceiptReprintDetailDto?> GetDetailAsync(
        string transactionId,
        string customerId,
        IReadOnlyList<string> allowedSchoolCodes,
        CancellationToken cancellationToken = default);
}
