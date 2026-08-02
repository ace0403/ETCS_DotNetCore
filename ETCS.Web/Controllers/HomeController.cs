using ETCS.Shared.Application.Email;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Auth;
using ETCS.Shared.Infrastructure.Auth.Models;
using ETCS.Web.Infrastructure.Auth;
using ETCS.Web.Models;
using ETCS.Web.Options;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Diagnostics;

namespace ETCS.Web.Controllers;

public class HomeController : Controller
{
    private const int PasswordResetExpiryMinutes = 60;

    private readonly IParentLoginRepository _parentLoginRepository;
    private readonly IGuardianEmailNotificationService _emailNotificationService;
    private readonly WebOptions _webOptions;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IParentLoginRepository parentLoginRepository,
        IGuardianEmailNotificationService emailNotificationService,
        IOptions<WebOptions> webOptions,
        ILogger<HomeController> logger)
    {
        _parentLoginRepository = parentLoginRepository;
        _emailNotificationService = emailNotificationService;
        _webOptions = webOptions.Value;
        _logger = logger;
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

        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Json(new { success = false, message = "Unauthorized." });
        }

        var result = await _parentLoginRepository.ChangePasswordAsync(
            guardianId,
            model.CurrentPassword,
            model.NewPassword,
            cancellationToken);

        return Json(new { success = result.Success, message = result.Message });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult ForgotPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new ForgotPasswordRequest());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordRequest model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var requestResult = await _parentLoginRepository.RequestPasswordResetAsync(
            model.Email,
            TimeSpan.FromMinutes(PasswordResetExpiryMinutes),
            cancellationToken);

        if (requestResult.AccountFound && !string.IsNullOrWhiteSpace(requestResult.Token))
        {
            var resetLink = BuildPasswordResetLink(requestResult.Token);
            if (string.IsNullOrWhiteSpace(resetLink))
            {
                _logger.LogError("Password reset requested but Web:PublicBaseUrl is not configured.");
            }
            else
            {
                await _emailNotificationService.QueuePasswordResetAsync(
                    requestResult.Email,
                    requestResult.GuardianName,
                    resetLink,
                    PasswordResetExpiryMinutes,
                    cancellationToken);
            }
        }

        return RedirectToAction(nameof(ForgotPassword), new { msg = "reset-sent" });
    }

    [AllowAnonymous]
    [HttpGet]
    public async Task<IActionResult> ResetPassword(string? token, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        var validation = await _parentLoginRepository.ValidatePasswordResetTokenAsync(token ?? string.Empty, cancellationToken);
        if (!validation.IsValid)
        {
            return View("ResetPasswordInvalid", model: validation.Message);
        }

        return View(new ResetPasswordRequest { Token = token!.Trim() });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordRequest model, CancellationToken cancellationToken)
    {
        var validation = await _parentLoginRepository.ValidatePasswordResetTokenAsync(model.Token, cancellationToken);
        if (!validation.IsValid)
        {
            return View("ResetPasswordInvalid", model: validation.Message);
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _parentLoginRepository.CompletePasswordResetAsync(
            model.Token,
            model.NewPassword,
            cancellationToken);

        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        return RedirectToAction(nameof(Index), new { msg = "password-reset-success" });
    }

    [AllowAnonymous]
    [HttpGet]
    public IActionResult Register()
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        return View(new ParentRegisterRequest());
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(ParentRegisterRequest model, CancellationToken cancellationToken)
    {
        if (User.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Dashboard");
        }

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        var result = await _parentLoginRepository.RegisterAsync(
            new RegisterRequest
            {
                FirstName = model.FirstName,
                LastName = model.LastName,
                Username = model.Username,
                Email = model.Email,
                MobileNumber = model.MobileNumber,
                Password = model.Password
            },
            cancellationToken);

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        return RedirectToAction(nameof(Index), new { msg = "register-success" });
    }

    [AllowAnonymous]
    public IActionResult Privacy() => RedirectToAction(nameof(LegalController.Privacy), "Legal");

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private string BuildPasswordResetLink(string token)
    {
        var baseUrl = (_webOptions.PublicBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = $"{Request.Scheme}://{Request.Host}".TrimEnd('/');
        }

        return $"{baseUrl}/Home/ResetPassword?token={Uri.EscapeDataString(token)}";
    }
}
