using ETCS.Web.Infrastructure.Auth;
using ETCS.Web.Infrastructure.Navigation;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Web.ViewComponents;

public sealed class ParentPortalNavigationViewComponent : ViewComponent
{
    private readonly IParentPortalNavigationService _navigationService;

    public ParentPortalNavigationViewComponent(IParentPortalNavigationService navigationService)
    {
        _navigationService = navigationService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        if (!HttpContext.User.TryGetGuardianId(out var guardianId))
        {
            return View("~/Views/Shared/_Sidebar.cshtml", ParentPortalNavigationAccess.None);
        }

        var access = await _navigationService.GetAccessAsync(guardianId, HttpContext.RequestAborted);
        return View("~/Views/Shared/_Sidebar.cshtml", access);
    }
}
