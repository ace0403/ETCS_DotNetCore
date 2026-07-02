using ETCS.Shared.Infrastructure.Auth.Models;

namespace ETCS.Shared.Infrastructure.Auth;

public interface IParentLoginRepository
{
    Task<ParentLoginResult> GetLoginAsync(string loginName, CancellationToken cancellationToken);
    Task<ParentRegistrationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
}

public sealed record ParentLoginResult(bool SpIndicatesSuccess, int id, string? StoredPasswordOrHash, UserResponse? User);
public sealed record ParentRegistrationResult(bool IsSuccess, int GuardianId, string Message, UserResponse? User);
