using ETCS.Admin.Infrastructure.Auth;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Admin.Auth;
using ETCS.Shared.Infrastructure.Admin.Models.Requests;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Admin.Controllers;

public class HomeController : Controller
{
    private readonly IAdminLoginRepository _adminLoginRepository;
    private readonly IAdminNavigationService _navigationService;

    public HomeController(
        IAdminLoginRepository adminLoginRepository,
        IAdminNavigationService navigationService)
    {
        _adminLoginRepository = adminLoginRepository;
        _navigationService = navigationService;
    }

    [AllowAnonymous]
    public async Task<IActionResult> Index(string? msg, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            if (string.Equals(msg, "unauthorize", StringComparison.OrdinalIgnoreCase))
            {
                return RedirectToAction(nameof(AccessDenied));
            }

            var landing = await _navigationService.GetLandingPageAsync(User, cancellationToken);
            if (landing is not null)
            {
                return RedirectToAction(landing.Value.Action, landing.Value.Controller);
            }

            return RedirectToAction(nameof(AccessDenied));
        }

        return View(new AdminLoginRequest());
    }

    [Authorize]
    [AllowWithoutAdminPermission]
    public IActionResult AccessDenied() => View();

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(AdminLoginRequest model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var account = await _adminLoginRepository.GetByLoginNameAsync(model.Username, cancellationToken);
        if (account is null)
        {
            return RedirectToAction(nameof(Index), new { msg = "login-failed" });
        }

        var hashed = SecurityHelper.GetMd5Hash(model.Password);
        if (!PasswordMatches(account.StoredPasswordHash, hashed, model.Password))
        {
            return RedirectToAction(nameof(Index), new { msg = "login-failed" });
        }

        var principal = AdminClaimsFactory.CreatePrincipal(account);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        var landing = await _navigationService.GetLandingPageAsync(principal, cancellationToken);
        if (landing is not null)
        {
            return RedirectToAction(landing.Value.Action, landing.Value.Controller, new { msg = "login-success" });
        }

        return RedirectToAction(nameof(AccessDenied));
    }

    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Index), new { msg = "logout-success" });
    }

    [Authorize]
    public IActionResult ChangePasswordModal()
    {
        return PartialView("_ChangePassword", new ChangePasswordRequest());
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordRequest model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return Json(new { success = false, message = "Please correct the highlighted fields." });
        }

        if (!User.TryGetLoginAccountId(out var accountId))
        {
            return Json(new { success = false, message = "Unauthorized." });
        }

        var result = await _adminLoginRepository.ChangePasswordAsync(
            accountId,
            model.CurrentPassword,
            model.NewPassword,
            cancellationToken);

        return Json(new { success = result.Success, message = result.Message });
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new Models.ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
    }

    private static bool PasswordMatches(string stored, string md5Hash, string plainPassword) =>
        string.Equals(stored, md5Hash, StringComparison.OrdinalIgnoreCase)
        || string.Equals(stored, plainPassword, StringComparison.Ordinal);
}
