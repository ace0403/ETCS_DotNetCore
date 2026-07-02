using System.Security.Claims;
using ETCS.Shared.Infrastructure.Auth.Models;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace ETCS.Web.Infrastructure.Auth;

public static class ParentClaimsFactory
{
    public static ClaimsPrincipal CreatePrincipal(UserResponse user, string loginName)
    {
        var displayName = string.IsNullOrWhiteSpace(user.name) ? loginName : user.name;

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.id.ToString()),
            new(ClaimTypes.Name, loginName),
            new("sub", loginName),
            new(ClaimTypes.Email, user.email ?? string.Empty),
            new(ClaimTypes.GivenName, displayName),
            new(ClaimTypes.Role, "Parent")
        };

        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        return new ClaimsPrincipal(identity);
    }
}
