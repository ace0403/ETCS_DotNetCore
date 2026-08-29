using System.Security.Claims;
using ETCS.Shared.Infrastructure.Admin.Security;

namespace ETCS.Admin.Infrastructure.Auth;

public sealed class AdminNavigationService : IAdminNavigationService
{
    private static readonly string[] FallbackModuleOrder =
    [
        "Dashboard",
        "School",
        "Guardian",
        "Student",
        "BlacklistCard",
        "Staff",
        "EmailTemplate",
        "Category",
        "MealType",
        "Ingredient",
        "MealItem",
        "MealCombo",
        "MealServingPeriod",
        "SchoolCalendar",
        "Report.CanteenTransactions",
        "Report.AdminTransaction",
        "Report.TerminalSalesSummary",
        "Report.MealOrdersMealDb",
        "Report.MealOrders",
        "Report.MealOrderPaymentsMealDb",
        "Report.MealOrderPayments",
        "Role"
    ];

    private readonly IAdminPermissionRepository _permissionRepository;

    public AdminNavigationService(IAdminPermissionRepository permissionRepository)
    {
        _permissionRepository = permissionRepository;
    }

    public async Task<(string Controller, string Action)?> GetLandingPageAsync(
        ClaimsPrincipal user,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var modules = await _permissionRepository.ListModulesAsync(cancellationToken);
            if (modules.Count > 0)
            {
                foreach (var module in modules.OrderBy(m => m.SortOrder).ThenBy(m => m.DisplayName))
                {
                    if (user.HasPermission($"{module.ModuleKey}.View"))
                    {
                        return ResolveRoute(module);
                    }
                }

                return null;
            }
        }
        catch
        {
            // MealDB AdminModule not deployed yet — use fallback order below.
        }

        return GetLandingPageFromFallback(user);
    }

    private static (string Controller, string Action)? GetLandingPageFromFallback(ClaimsPrincipal user)
    {
        foreach (var moduleKey in FallbackModuleOrder)
        {
            if (user.HasPermission($"{moduleKey}.View"))
            {
                return ResolveRoute(moduleKey, null, null);
            }
        }

        return null;
    }

    private static (string Controller, string Action) ResolveRoute(AdminModuleDto module) =>
        ResolveRoute(module.ModuleKey, module.ControllerName, module.ActionName);

    private static (string Controller, string Action) ResolveRoute(
        string moduleKey,
        string? controllerName,
        string? actionName)
    {
        if (!string.IsNullOrWhiteSpace(controllerName) && !string.IsNullOrWhiteSpace(actionName))
        {
            return (controllerName, actionName);
        }

        if (moduleKey.StartsWith("Report.", StringComparison.OrdinalIgnoreCase))
        {
            return ("Report", moduleKey["Report.".Length..]);
        }

        return (moduleKey, "Index");
    }
}
