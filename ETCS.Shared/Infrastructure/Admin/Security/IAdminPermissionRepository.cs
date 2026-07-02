namespace ETCS.Shared.Infrastructure.Admin.Security;

public interface IAdminPermissionRepository
{
    Task<AdminRolePermissionLoadResult> LoadForRoleAsync(int roleId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AdminModuleDto>> ListModulesAsync(CancellationToken cancellationToken = default);
}

public sealed class AdminRolePermissionLoadResult
{
    public bool IsSuperAdmin { get; init; }
    public IReadOnlyList<string> PermissionKeys { get; init; } = [];
}
