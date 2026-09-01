using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Admin.Auth;

public interface IAdminLoginRepository
{
    Task<LoginAccountDto?> GetByLoginNameAsync(string loginName, CancellationToken cancellationToken = default);

    Task<LoginAccountDto?> GetByLoginNameForRoleAsync(
        string loginName,
        int roleId,
        CancellationToken cancellationToken = default);

    Task<AdminOperationResult> ChangePasswordAsync(int accountId, string currentPassword, string newPassword, CancellationToken cancellationToken = default);
}
