using System.Security.Claims;

namespace ETCS.Web.Infrastructure.Auth;

public static class ParentClaimsExtensions
{
    public static bool TryGetGuardianId(this ClaimsPrincipal user, out int guardianId)
    {
        guardianId = 0;
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("nameid");
        return int.TryParse(raw, out guardianId) && guardianId > 0;
    }

    public static string? GetDisplayName(this ClaimsPrincipal user) =>
        user.FindFirstValue(ClaimTypes.GivenName)
        ?? user.FindFirstValue(ClaimTypes.Name);
}
