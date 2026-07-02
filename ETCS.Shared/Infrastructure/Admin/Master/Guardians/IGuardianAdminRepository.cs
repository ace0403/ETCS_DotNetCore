using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Master.Guardians;

public interface IGuardianAdminRepository
{
    Task<DataTableResponse<GuardianListItemDto>> GetDataAsync(DataTableRequest request, CancellationToken cancellationToken = default);
    Task<GuardianSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> SaveAsync(GuardianSaveRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
    Task<GuardianChildrenViewModel?> GetChildrenViewAsync(int guardianId, CancellationToken cancellationToken = default);
    Task<GuardianTransferViewModel?> GetTransferViewAsync(int guardianId, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> TransferBalanceAsync(GuardianBalanceTransferRequest request, CancellationToken cancellationToken = default);
    Task<GuardianAddStudentViewModel?> GetAddStudentViewAsync(int guardianId, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> AddStudentAsync(GuardianAddStudentRequest request, CancellationToken cancellationToken = default);
    Task<GuardianEditStudentViewModel?> GetEditStudentViewAsync(int guardianId, decimal userId, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> EditStudentAsync(GuardianEditStudentRequest request, CancellationToken cancellationToken = default);
}
