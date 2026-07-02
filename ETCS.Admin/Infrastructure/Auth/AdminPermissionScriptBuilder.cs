using System.Security.Claims;
using System.Text.Json;

namespace ETCS.Admin.Infrastructure.Auth;

public static class AdminPermissionScriptBuilder
{
    public static string BuildPermissionsJson(ClaimsPrincipal user)
    {
        var map = new Dictionary<string, Dictionary<string, bool>>(StringComparer.OrdinalIgnoreCase);

        if (user.IsAdminRole())
        {
            return JsonSerializer.Serialize(map);
        }

        foreach (var claim in user.FindAll(AdminClaimTypes.Permission))
        {
            var lastDot = claim.Value.LastIndexOf('.');
            if (lastDot <= 0 || lastDot >= claim.Value.Length - 1)
            {
                continue;
            }

            var moduleKey = claim.Value[..lastDot];
            var action = claim.Value[(lastDot + 1)..].ToLowerInvariant();
            if (!map.TryGetValue(moduleKey, out var actions))
            {
                actions = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);
                map[moduleKey] = actions;
            }

            actions[action] = true;
        }

        foreach (var entry in map.Values)
        {
            entry.TryAdd("view", false);
            entry.TryAdd("add", false);
            entry.TryAdd("edit", false);
            entry.TryAdd("delete", false);
        }

        return JsonSerializer.Serialize(map);
    }

    public static string BuildSchoolScopeJson(ClaimsPrincipal user)
    {
        var payload = new
        {
            restricted = user.IsSchoolScoped() && !user.IsAdminRole(),
            schoolIds = user.GetAssignedSchoolIds()
        };

        return JsonSerializer.Serialize(payload);
    }
}
