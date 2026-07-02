using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Master.Schools;

public interface ISchoolAdminRepository
{
    Task<DataTableResponse<SchoolListItemDto>> GetDataAsync(DataTableRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SchoolCountryLookupDto>> CountryLookupsAsync(CancellationToken cancellationToken = default);
    Task<SchoolSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> SaveAsync(SchoolSaveRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
