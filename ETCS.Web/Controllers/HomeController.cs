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
    private readonly IRegistrationOtpService _registrationOtpService;
    private readonly IGuardianEmailNotificationService _emailNotificationService;
    private readonly WebOptions _webOptions;
    private readonly ILogger<HomeController> _logger;

    public HomeController(
        IParentLoginRepository parentLoginRepository,
        IRegistrationOtpService registrationOtpService,
        IGuardianEmailNotificationService emailNotificationService,
        IOptions<WebOptions> webOptions,
        ILogger<HomeController> logger)
    {
        _parentLoginRepository = parentLoginRepository;
        _registrationOtpService = registrationOtpService;
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

        if (await _parentLoginRepository.IsAccountDeletedAsync(loginRow.id, cancellationToken))
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
    [Route("register")]
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
    public async Task<IActionResult> SendRegistrationOtp(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Json(new { success = false, message = "Email is required." });
        }

        var result = await _registrationOtpService.SendOtpAsync(email, cancellationToken);
        if (!result.IsSuccess)
        {
            return Json(new { success = false, message = result.Message });
        }

        return Json(new
        {
            success = true,
            message = result.Message,
            expiresInSeconds = result.ExpiresInSeconds
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyRegistrationOtp(
        string email,
        string otp,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return Json(new { success = false, message = "Email is required." });
        }

        if (string.IsNullOrWhiteSpace(otp))
        {
            return Json(new { success = false, message = "Verification code is required." });
        }

        var result = await _registrationOtpService.VerifyOtpAsync(email, otp, cancellationToken);
        if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.VerificationToken))
        {
            return Json(new { success = false, message = result.Message });
        }

        return Json(new
        {
            success = true,
            message = result.Message,
            verificationToken = result.VerificationToken,
            expiresInSeconds = result.ExpiresInSeconds
        });
    }

    [AllowAnonymous]
    [HttpPost]
    [Route("register")]
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

        if (string.IsNullOrWhiteSpace(model.VerificationToken))
        {
            ModelState.AddModelError(string.Empty, "Email verification is required to complete registration.");
            return View(model);
        }

        var verification = await _registrationOtpService.ValidateVerificationTokenAsync(
            model.Email,
            model.VerificationToken,
            cancellationToken);
        if (!verification.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, verification.Message);
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
                Password = model.Password,
                VerificationToken = model.VerificationToken
            },
            cancellationToken);

        if (!result.IsSuccess || result.User is null)
        {
            ModelState.AddModelError(string.Empty, result.Message);
            return View(model);
        }

        await _registrationOtpService.MarkVerificationTokenUsedAsync(model.VerificationToken, cancellationToken);

        var guardianName = $"{model.FirstName.Trim()} {model.LastName.Trim()}".Trim();
        await _emailNotificationService.QueueRegistrationSuccessAsync(
            model.Email,
            guardianName,
            BuildAddChildLink(),
            cancellationToken);

        var loginName = model.Email.Trim();
        var principal = ParentClaimsFactory.CreatePrincipal(result.User, loginName);
        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)
            });

        return RedirectToAction("Index", "Dashboard", new { msg = "register-success" });
    }

    [AllowAnonymous]
    public IActionResult Privacy() => RedirectToAction(nameof(LegalController.Privacy), "Legal");

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult PageNotFound(int? code)
    {
        if (code.HasValue && code.Value != StatusCodes.Status404NotFound)
        {
            return StatusCode(code.Value);
        }

        Response.StatusCode = StatusCodes.Status404NotFound;
        var isAuthenticated = User.Identity?.IsAuthenticated == true;

        return View(new ErrorViewModel
        {
            StatusCode = StatusCodes.Status404NotFound,
            Title = "Page not found",
            Message = "The page you're looking for doesn't exist or may have been moved.",
            IconClass = "ti-map-pin-off",
            HeroTagline = "Let's get you back on track.",
            PrimaryActionText = isAuthenticated ? "Go to dashboard" : "Back to sign in",
            PrimaryActionUrl = isAuthenticated
                ? Url.Action("Index", "Dashboard")
                : Url.Action("Index", "Home"),
            SecondaryLinkText = "Contact support",
            SecondaryLinkUrl = isAuthenticated
                ? Url.Action("Index", "Support")
                : Url.Action("Index", "Home")
        });
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult ServerError()
    {
        Response.StatusCode = StatusCodes.Status500InternalServerError;
        var isAuthenticated = User.Identity?.IsAuthenticated == true;
        var supportEmail = string.IsNullOrWhiteSpace(_webOptions.SupportEmail)
            ? "info@etasteuae.com"
            : _webOptions.SupportEmail.Trim();

        return View(new ErrorViewModel
        {
            StatusCode = StatusCodes.Status500InternalServerError,
            Title = "Something went wrong",
            Message = "We're having trouble completing your request. Please try again in a moment.",
            IconClass = "ti-server-bolt",
            HeroTagline = "We're working to fix this.",
            UsePrimaryAsTryAgain = true,
            PrimaryActionText = "Try again",
            SecondaryLinkText = isAuthenticated ? "Go to dashboard" : "Back to sign in",
            SecondaryLinkUrl = isAuthenticated
                ? Url.Action("Index", "Dashboard")
                : Url.Action("Index", "Home"),
            TertiaryLinkText = "Contact support",
            TertiaryLinkUrl = isAuthenticated
                ? Url.Action("Index", "Support")
                : $"mailto:{supportEmail}",
            RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier
        });
    }

    [AllowAnonymous]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() => RedirectToAction(nameof(ServerError));

    private string BuildAddChildLink()
    {
        var baseUrl = ResolvePublicBaseUrl();
        return $"{baseUrl}/MyKids";
    }

    private string BuildPasswordResetLink(string token)
    {
        var baseUrl = ResolvePublicBaseUrl();
        return $"{baseUrl}/Home/ResetPassword?token={Uri.EscapeDataString(token)}";
    }

    private string ResolvePublicBaseUrl()
    {
        var baseUrl = (_webOptions.PublicBaseUrl ?? string.Empty).Trim().TrimEnd('/');
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            baseUrl = $"{Request.Scheme}://{Request.Host}".TrimEnd('/');
        }

        return baseUrl;
    }
}
