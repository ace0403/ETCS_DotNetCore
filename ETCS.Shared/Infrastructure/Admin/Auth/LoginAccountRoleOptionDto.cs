namespace ETCS.Shared.Infrastructure.Admin.Auth;

public sealed class LoginAccountRoleOptionDto
{
    public int RoleId { get; init; }
    public string RoleName { get; init; } = string.Empty;
    public bool IsDefault { get; init; }
}
