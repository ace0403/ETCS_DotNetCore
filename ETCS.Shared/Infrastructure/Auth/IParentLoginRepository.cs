using ETCS.Shared.Infrastructure.Auth.Models;

namespace ETCS.Shared.Infrastructure.Auth;

public interface IParentLoginRepository
{
    Task<ParentLoginResult> GetLoginAsync(string loginName, CancellationToken cancellationToken);
    Task<ParentRegistrationResult> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<ParentChangePasswordResult> ChangePasswordAsync(
        int guardianId,
        string currentPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
    Task<ParentPasswordResetRequestResult> RequestPasswordResetAsync(
        string email,
        TimeSpan tokenLifetime,
        CancellationToken cancellationToken = default);
    Task<ParentPasswordResetValidateResult> ValidatePasswordResetTokenAsync(
        string token,
        CancellationToken cancellationToken = default);
    Task<ParentChangePasswordResult> CompletePasswordResetAsync(
        string token,
        string newPassword,
        CancellationToken cancellationToken = default);
}

public sealed record ParentLoginResult(bool SpIndicatesSuccess, int id, string? StoredPasswordOrHash, UserResponse? User);
public sealed record ParentRegistrationResult(bool IsSuccess, int GuardianId, string Message, UserResponse? User);
public sealed record ParentChangePasswordResult(bool Success, string Message);
public sealed record ParentPasswordResetRequestResult(
    bool AccountFound,
    int GuardianId,
    string Email,
    string GuardianName,
    string? Token);
public sealed record ParentPasswordResetValidateResult(bool IsValid, int GuardianId, string Message);
