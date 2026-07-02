namespace ETCS.Shared.Infrastructure.Pos;

public interface IPosLegacyTransactionRepository
{
    Task<bool> InsertCashPurchaseAsync(
        decimal amount,
        string branchCode,
        int terminalCode,
        string transactionId,
        CancellationToken cancellationToken);

    Task<bool> UndoCashPurchaseAsync(
        decimal amount,
        string branchCode,
        int terminalCode,
        string transactionId,
        CancellationToken cancellationToken);

    Task<bool> InsertCardPurchaseAsync(
        decimal amount,
        string branchCode,
        int terminalCode,
        string transactionId,
        string creditCardNumber,
        CancellationToken cancellationToken);

    Task<bool> InsertPosPurchaseLineAsync(
        string customerId,
        string skuCode,
        decimal amount,
        DateTime purchaseDate,
        string transactionId,
        string ipAddress,
        CancellationToken cancellationToken);

    Task<bool> RollbackSpendLimitAsync(
        string customerId,
        decimal amount,
        CancellationToken cancellationToken);

    Task<PosLegacySpendLimitRow?> GetSpendLimitInfoAsync(
        string customerId,
        DateTime currentDate,
        DateTime weekStartDate,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<int, string>> GetItemCodesByMealItemIdsAsync(
        IReadOnlyList<int> mealItemIds,
        CancellationToken cancellationToken);
}
