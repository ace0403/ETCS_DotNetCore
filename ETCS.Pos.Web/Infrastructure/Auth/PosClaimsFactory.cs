using System.Security.Claims;
using ETCS.Shared.Infrastructure.Admin.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ETCS.Pos.Web.Infrastructure.Auth;

public static class PosClaimsFactory
{
    public static ClaimsPrincipal CreatePrincipal(LoginAccountDto account)
    {
        var displayName = string.Join(
            " ",
            new[] { account.FirstName, account.LastName }.Where(p => !string.IsNullOrWhiteSpace(p)));
        if (string.IsNullOrWhiteSpace(displayName))
        {
            displayName = account.Username;
        }

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.Username),
            new("sub", account.Username),
            new(ClaimTypes.Email, account.Email ?? string.Empty),
            new(ClaimTypes.GivenName, displayName),
            new(ClaimTypes.Role, string.IsNullOrWhiteSpace(account.RoleName) ? PosClaimTypes.RequiredRoleName : account.RoleName),
            new(PosClaimTypes.AuthType, PosClaimTypes.PosAuthTypeValue)
        };

        if (account.SchoolId > 0)
        {
            claims.Add(new Claim(PosClaimTypes.SchoolId, account.SchoolId.ToString()));
        }

        if (account.AssignedSchoolIds.Count > 0)
        {
            claims.Add(new Claim(PosClaimTypes.SchoolIds, string.Join(",", account.AssignedSchoolIds)));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}
