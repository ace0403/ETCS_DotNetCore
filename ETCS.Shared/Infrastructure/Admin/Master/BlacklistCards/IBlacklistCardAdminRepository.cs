using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Master.BlacklistCards;

public interface IBlacklistCardAdminRepository
{
    Task<int?> GetStudentSchoolIdAsync(string customerId, CancellationToken cancellationToken = default);

    Task<BlacklistCardLookupResult> GetLinkedCardsAsync(string customerId, CancellationToken cancellationToken = default);

    Task<AdminOperationResult> BlacklistAsync(BlacklistCardRequest request, CancellationToken cancellationToken = default);

    Task<AdminOperationResult> TransferBalanceAsync(
        BlacklistCardTransferRequest request,
        CancellationToken cancellationToken = default);
}
