using System.Security.Claims;
using ETCS.Shared.Infrastructure.Admin.Auth;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ETCS.Admin.Infrastructure.Auth;

public static class AdminClaimsFactory
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
            new(ClaimTypes.Email, account.Email),
            new(ClaimTypes.GivenName, displayName),
            new(ClaimTypes.Role, string.IsNullOrWhiteSpace(account.RoleName) ? "Admin" : account.RoleName),
            new(AdminClaimTypes.AuthType, AdminClaimTypes.AdminAuthTypeValue)
        };

        if (account.IsSuperAdmin)
        {
            claims.Add(new Claim(AdminClaimTypes.IsSuperAdmin, "true"));
        }

        if (account.IsSchoolScoped)
        {
            claims.Add(new Claim(AdminClaimTypes.SchoolScoped, "true"));
        }

        if (account.SchoolId > 0)
        {
            claims.Add(new Claim(AdminClaimTypes.SchoolId, account.SchoolId.ToString()));
        }

        if (account.AssignedSchoolIds.Count > 0)
        {
            claims.Add(new Claim(AdminClaimTypes.SchoolIds, string.Join(",", account.AssignedSchoolIds)));
        }

        foreach (var permission in account.Permissions)
        {
            claims.Add(new Claim(AdminClaimTypes.Permission, permission));
        }

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}
