using ETCS.Pos.Web.Infrastructure.Auth;
using ETCS.Pos.Web.Models;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Admin.Auth;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Pos.Web.Controllers;

public sealed class HomeController : Controller
{
    private readonly IAdminLoginRepository _loginRepository;

    public HomeController(IAdminLoginRepository loginRepository)
    {
        _loginRepository = loginRepository;
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Index(string? msg)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Pos");
        }

        return View(new PosLoginRequest());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(PosLoginRequest model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var account = await _loginRepository.GetByLoginNameAsync(model.Username, cancellationToken);
        if (account is null)
        {
            return RedirectToAction(nameof(Index), new { msg = "login-failed" });
        }

        var hashed = SecurityHelper.GetMd5Hash(model.Password);
        if (!PasswordMatches(account.StoredPasswordHash, hashed, model.Password))
        {
            return RedirectToAction(nameof(Index), new { msg = "login-failed" });
        }

        if (!string.Equals(account.RoleName, PosClaimTypes.RequiredRoleName, StringComparison.OrdinalIgnoreCase))
        {
            return RedirectToAction(nameof(Index), new { msg = "unauthorized-role" });
        }

        var principal = PosClaimsFactory.CreatePrincipal(account);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        return RedirectToAction("Index", "Pos", new { msg = "login-success" });
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Index), new { msg = "logout-success" });
    }

    private static bool PasswordMatches(string stored, string md5Hash, string plainPassword) =>
        string.Equals(stored, md5Hash, StringComparison.OrdinalIgnoreCase)
        || string.Equals(stored, plainPassword, StringComparison.Ordinal);
}
