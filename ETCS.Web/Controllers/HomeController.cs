using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Auth;
using ETCS.Web.Infrastructure.Auth;
using ETCS.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ETCS.Web.Controllers;

public class HomeController : Controller
{
    private readonly IParentLoginRepository _parentLoginRepository;

    public HomeController(IParentLoginRepository parentLoginRepository)
    {
        _parentLoginRepository = parentLoginRepository;
    }

    [AllowAnonymous]
    public IActionResult Index()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new ParentLoginRequest());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(ParentLoginRequest model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var loginName = model.Username.Trim();
        var loginRow = await _parentLoginRepository.GetLoginAsync(loginName, cancellationToken);
        if (!loginRow.SpIndicatesSuccess || loginRow.User is null)
        {
            return RedirectToAction(nameof(Index), new { msg = "login-failed" });
        }

        if (string.IsNullOrEmpty(loginRow.StoredPasswordOrHash))
        {
            return RedirectToAction(nameof(Index), new { msg = "login-failed" });
        }

        var hashedInput = SecurityHelper.GetMd5Hash(model.Password);
        if (!string.Equals(hashedInput, loginRow.StoredPasswordOrHash, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Index), new { msg = "login-failed" });
        }

        var principal = ParentClaimsFactory.CreatePrincipal(loginRow.User, loginName);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        return RedirectToAction("Index", "Dashboard", new { msg = "login-success" });
    }

    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Index), new { msg = "logout-success" });
    }

    [Authorize]
    public IActionResult Privacy()
    {
        return View();
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
