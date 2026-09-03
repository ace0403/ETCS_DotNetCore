using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Master.Students;

public interface IStudentAdminRepository
{
    Task<DataTableResponse<StudentAdminListItemDto>> GetDataAsync(DataTableRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StudentAdminListItemDto>> ListForExportAsync(DataTableRequest request, CancellationToken cancellationToken = default);
    Task<StudentAdminSaveRequest?> GetAsync(decimal userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GuardianLookupDto>> GuardianLookupsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SchoolLookupDto>> SchoolLookupsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GradeLookupDto>> GradeLookupsAsync(CancellationToken cancellationToken = default);
    Task<AdminOperationResult> SaveAsync(StudentAdminSaveRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> DeleteAsync(decimal userId, CancellationToken cancellationToken = default);
}
