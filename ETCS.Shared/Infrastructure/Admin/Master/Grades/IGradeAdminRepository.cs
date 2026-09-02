using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Master.Grades;

public interface IGradeAdminRepository
{
    Task<DataTableResponse<GradeListItemDto>> GetDataAsync(DataTableRequest request, CancellationToken cancellationToken = default);
    Task<GradeSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> SaveAsync(GradeSaveRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
