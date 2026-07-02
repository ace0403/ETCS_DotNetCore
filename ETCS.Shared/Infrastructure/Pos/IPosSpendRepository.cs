namespace ETCS.Shared.Infrastructure.Pos;

public interface IPosSpendRepository
{
    Task<PosSpendInfoDto?> GetSpendInfoByCustomerIdAsync(
        string customerId,
        int orderTypeId,
        CancellationToken cancellationToken);
}
