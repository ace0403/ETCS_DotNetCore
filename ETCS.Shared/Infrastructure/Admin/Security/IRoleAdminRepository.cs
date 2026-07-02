using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Security;

public interface IRoleAdminRepository
{
    Task<DataTableResponse<AdminRoleListItemDto>> GetDataAsync(
        DataTableRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminRoleDetailDto> GetTemplateAsync(CancellationToken cancellationToken = default);

    Task<AdminRoleDetailDto?> GetAsync(int roleId, CancellationToken cancellationToken = default);

    Task<AdminOperationResult> SaveAsync(AdminRoleSaveRequest request, CancellationToken cancellationToken = default);

    Task<AdminOperationResult> DeleteAsync(int roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminRoleLookupDto>> RoleLookupsAsync(CancellationToken cancellationToken = default);
}
