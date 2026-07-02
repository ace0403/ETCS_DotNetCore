namespace ETCS.Shared.Infrastructure.Admin.Auth;

public sealed class LoginAccountDto
{
    public int Id { get; init; }
    public string Username { get; init; } = string.Empty;
    public string FirstName { get; init; } = string.Empty;
    public string LastName { get; init; } = string.Empty;
    public string Email { get; init; } = string.Empty;
    public string StoredPasswordHash { get; init; } = string.Empty;
    public int RoleId { get; init; }
    public string RoleName { get; init; } = "Admin";
    public int SchoolId { get; init; }
    public bool IsActive { get; init; }
    public bool IsSuperAdmin { get; init; }
    public bool IsSchoolScoped { get; init; }
    public IReadOnlyList<int> AssignedSchoolIds { get; init; } = [];
    public IReadOnlyList<string> Permissions { get; init; } = [];
}
