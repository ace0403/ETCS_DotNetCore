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
}
