namespace ETCS.Shared.Infrastructure.Admin.Security;

public sealed class AdminModuleDto
{
    public int ModuleId { get; init; }
    public string ModuleKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string GroupName { get; init; } = string.Empty;
    public string? ControllerName { get; init; }
    public string? ActionName { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; }
}

public sealed class AdminRolePermissionRowDto
{
    public int ModuleId { get; init; }
    public string ModuleKey { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public string GroupName { get; init; } = string.Empty;
    public bool CanView { get; init; }
    public bool CanAdd { get; init; }
    public bool CanEdit { get; init; }
    public bool CanDelete { get; init; }
}
