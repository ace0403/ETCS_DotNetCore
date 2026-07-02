using System.Security.Claims;

namespace ETCS.Admin.Infrastructure.Auth;

public static class AdminPermissionViewHelper
{
    public static bool CanView(ClaimsPrincipal user, string moduleKey) =>
        user.HasPermission($"{moduleKey}.View");

    public static bool CanAdd(ClaimsPrincipal user, string moduleKey) =>
        user.HasPermission($"{moduleKey}.Add");

    public static bool CanEdit(ClaimsPrincipal user, string moduleKey) =>
        user.HasPermission($"{moduleKey}.Edit");

    public static bool CanDelete(ClaimsPrincipal user, string moduleKey) =>
        user.HasPermission($"{moduleKey}.Delete");
}
