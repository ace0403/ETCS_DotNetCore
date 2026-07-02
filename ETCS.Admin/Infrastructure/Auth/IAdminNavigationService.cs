using System.Security.Claims;

namespace ETCS.Admin.Infrastructure.Auth;

public interface IAdminNavigationService
{
    Task<(string Controller, string Action)?> GetLandingPageAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default);
}
