using System.ComponentModel.DataAnnotations;

namespace ETCS.Shared.Infrastructure.Admin.Security;

public sealed class AdminRoleListItemDto
{
    public int RoleId { get; init; }
    public string RoleName { get; init; } = string.Empty;
    public bool IsSuperAdmin { get; init; }
    public bool IsSystem { get; init; }
    public bool IsActive { get; init; }
    public int UserCount { get; init; }
}

public sealed class AdminRoleSaveRequest
{
    public int RoleId { get; set; }

    [Required]
    [StringLength(100)]
    public string RoleName { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public IReadOnlyList<AdminRolePermissionSaveItem> Permissions { get; set; } = [];
}

public sealed class AdminRolePermissionSaveItem
{
    public int ModuleId { get; set; }
    public bool CanView { get; set; }
    public bool CanAdd { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
}

public sealed class AdminRoleDetailDto
{
    public int RoleId { get; init; }
    public string RoleName { get; init; } = string.Empty;
    public bool IsSuperAdmin { get; init; }
    public bool IsSystem { get; init; }
    public bool IsActive { get; init; }
    public IReadOnlyList<AdminRolePermissionRowDto> Permissions { get; init; } = [];
}

public sealed class AdminRoleLookupDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
}
