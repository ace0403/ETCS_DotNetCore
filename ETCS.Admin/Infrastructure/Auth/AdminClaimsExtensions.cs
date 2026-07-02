using System.Security.Claims;

namespace ETCS.Admin.Infrastructure.Auth;

public static class AdminClaimsExtensions
{
    public static bool TryGetLoginAccountId(this ClaimsPrincipal user, out int accountId)
    {
        accountId = 0;
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("nameid");
        return int.TryParse(raw, out accountId) && accountId > 0;
    }

    public static string? GetDisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.GivenName)
        ?? user.FindFirstValue(ClaimTypes.Name);

    public static bool IsAdminRole(this ClaimsPrincipal user) =>
        user.IsInRole("Admin")
        || string.Equals(user.FindFirstValue(AdminClaimTypes.IsSuperAdmin), "true", StringComparison.OrdinalIgnoreCase);

    public static bool HasPermission(this ClaimsPrincipal user, string permissionKey)
    {
        if (user.IsAdminRole())
        {
            return true;
        }

        return user.Claims.Any(c =>
            c.Type == AdminClaimTypes.Permission
            && string.Equals(c.Value, permissionKey, StringComparison.OrdinalIgnoreCase));
    }

    public static bool IsSchoolScoped(this ClaimsPrincipal user) =>
        string.Equals(user.FindFirstValue(AdminClaimTypes.SchoolScoped), "true", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<int> GetAssignedSchoolIds(this ClaimsPrincipal user)
    {
        var raw = user.FindFirstValue(AdminClaimTypes.SchoolIds);
        if (string.IsNullOrWhiteSpace(raw))
        {
            if (int.TryParse(user.FindFirstValue(AdminClaimTypes.SchoolId), out var single) && single > 0)
            {
                return [single];
            }

            return [];
        }

        return raw
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(id => int.TryParse(id, out var parsed) ? parsed : 0)
            .Where(id => id > 0)
            .Distinct()
            .ToList();
    }
}
