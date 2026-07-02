using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Master.Staff;

public interface IStaffAdminRepository
{
    Task<DataTableResponse<StaffListItemDto>> GetDataAsync(DataTableRequest request, CancellationToken cancellationToken = default);
    Task<StaffSaveRequest?> GetAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaffRoleLookupDto>> RoleLookupsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaffCountryLookupDto>> CountryLookupsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaffSchoolLookupDto>> SchoolLookupsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<StaffSchoolLookupDto>> SchoolLookupsByCountryAsync(int countryId, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> SaveAsync(StaffSaveRequest request, CancellationToken cancellationToken = default);
    Task<AdminOperationResult> DeleteAsync(int id, CancellationToken cancellationToken = default);
}
