using System.Security.Claims;

namespace ETCS.API.Infrastructure.Auth;

public static class GuardianClaimsExtensions
{
    public static bool TryGetGuardianId(this ClaimsPrincipal user, out int guardianId)
    {
        guardianId = 0;
        var raw = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("nameid")
            ?? user.FindFirstValue("guardianId")
            ?? user.FindFirstValue("GuardianId");

        return int.TryParse(raw, out guardianId) && guardianId > 0;
    }
}
