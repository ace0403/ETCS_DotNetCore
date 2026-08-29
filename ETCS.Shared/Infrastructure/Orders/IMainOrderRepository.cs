namespace ETCS.Shared.Infrastructure.Orders;

public interface IMainOrderRepository
{
    Task<MemberFinancialProfile?> GetMemberFinancialProfileAsync(
        string customerId,
        CancellationToken cancellationToken);

    Task<long> ApplySuccessfulOrderAsync(
        string customerId,
        string orderId,
        string gatewayTransactionId,
        decimal total,
        string notes,
        short accessLogTransactionType,
        string accessLogDescription,
        string terminalCode,
        string companyCode,
        CancellationToken cancellationToken);

    Task<long> InsertAccessLogAsync(
        string customerId,
        decimal amount,
        short accessLogTransactionType,
        string description,
        string transactionId,
        string terminalCode,
        string companyCode,
        CancellationToken cancellationToken);

    /// <summary>
    /// Finds an existing AccessLog row for the customer + gateway transaction id (avoids duplicate ledger inserts on resume).
    /// </summary>
    Task<long?> FindAccessLogIdByGatewayTransactionAsync(
        string customerId,
        string gatewayTransactionId,
        CancellationToken cancellationToken);
}
