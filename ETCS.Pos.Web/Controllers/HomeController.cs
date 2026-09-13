using ETCS.Pos.Web.Infrastructure.Auth;

using ETCS.Pos.Web.Models;

using ETCS.Pos.Web.Services;

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

    private readonly IBridgeSetupFileResolver _bridgeSetupResolver;



    public HomeController(

        IAdminLoginRepository loginRepository,

        IBridgeSetupFileResolver bridgeSetupResolver)

    {

        _loginRepository = loginRepository;

        _bridgeSetupResolver = bridgeSetupResolver;

    }



    [AllowAnonymous]

    [HttpGet]

    public IActionResult Index(string? msg)

    {

        if (User.Identity?.IsAuthenticated == true)

        {

            return RedirectToAction("Index", "Pos");

        }



        return View(CreateLoginPageModel());

    }



    [AllowAnonymous]

    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> Index(PosLoginPageViewModel model, CancellationToken cancellationToken)

    {

        if (!ModelState.IsValid)

        {

            return View(CreateLoginPageModel(model.Username, model.Password));

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



        var posRole = account.AvailableRoles.FirstOrDefault(role =>

            string.Equals(role.RoleName, PosClaimTypes.RequiredRoleName, StringComparison.OrdinalIgnoreCase));

        if (posRole is null)

        {

            return RedirectToAction(nameof(Index), new { msg = "unauthorized-role" });

        }



        if (account.ActiveRoleId != posRole.RoleId)

        {

            account = await _loginRepository.GetByLoginNameForRoleAsync(

                model.Username,

                posRole.RoleId,

                cancellationToken);

            if (account is null)

            {

                return RedirectToAction(nameof(Index), new { msg = "unauthorized-role" });

            }

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



    [AllowAnonymous]

    [HttpGet]

    public IActionResult DownloadBridgeSetup()

    {

        var path = _bridgeSetupResolver.Resolve();

        if (path is null)

        {

            return NotFound();

        }



        return PhysicalFile(path, "application/octet-stream", BridgeSetupFileResolver.SetupFileName);

    }



    [Authorize]

    [HttpPost]

    [ValidateAntiForgeryToken]

    public async Task<IActionResult> Logout()

    {

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

        return RedirectToAction(nameof(Index), new { msg = "logout-success" });

    }



    private PosLoginPageViewModel CreateLoginPageModel(string username = "", string password = "")

    {

        var bridgeSetupAvailable = _bridgeSetupResolver.IsAvailable;

        return new PosLoginPageViewModel

        {

            Username = username,

            Password = password,

            BridgeSetupAvailable = bridgeSetupAvailable,

            BridgeSetupDownloadUrl = bridgeSetupAvailable

                ? Url.Action(nameof(DownloadBridgeSetup), "Home") ?? string.Empty

                : string.Empty

        };

    }



    private static bool PasswordMatches(string stored, string md5Hash, string plainPassword) =>

        string.Equals(stored, md5Hash, StringComparison.OrdinalIgnoreCase)

        || string.Equals(stored, plainPassword, StringComparison.Ordinal);

}


