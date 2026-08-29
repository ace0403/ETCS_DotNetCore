using ETCS.Shared.Infrastructure.Auth;
using ETCS.Web.Infrastructure.Auth;
using ETCS.Web.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Web.Controllers;

[Authorize]
[Route("account-settings")]
public sealed class AccountSettingsController : Controller
{
    private readonly IParentLoginRepository _parentLoginRepository;
    private readonly IDeleteAccountOtpService _deleteAccountOtpService;

    public AccountSettingsController(
        IParentLoginRepository parentLoginRepository,
        IDeleteAccountOtpService deleteAccountOtpService)
    {
        _parentLoginRepository = parentLoginRepository;
        _deleteAccountOtpService = deleteAccountOtpService;
    }

    [HttpGet("")]
    [HttpGet("index")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return RedirectToAction("Index", "Home", new { msg = "unauthorize" });
        }

        var account = await _parentLoginRepository.GetActiveAccountEmailAsync(guardianId, cancellationToken);
        if (account is null || account.IsDelete)
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home", new { msg = "account-deleted" });
        }

        var model = new AccountSettingsPageViewModel
        {
            DisplayName = account.DisplayName,
            Email = account.Email,
            MaskedEmail = MaskEmail(account.Email)
        };

        return View(model);
    }

    [HttpPost("send-delete-otp")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SendDeleteAccountOtp(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Json(new { success = false, message = "Unauthorized." });
        }

        var result = await _deleteAccountOtpService.SendOtpAsync(guardianId, cancellationToken);
        return Json(new
        {
            success = result.IsSuccess,
            message = result.Message,
            expiresInSeconds = result.ExpiresInSeconds,
            maskedEmail = result.MaskedEmail
        });
    }

    [HttpPost("delete-account")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAccount(string otp, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Json(new { success = false, message = "Unauthorized." });
        }

        if (string.IsNullOrWhiteSpace(otp))
        {
            return Json(new { success = false, message = "Verification code is required." });
        }

        var otpResult = await _deleteAccountOtpService.VerifyOtpAsync(guardianId, otp, cancellationToken);
        if (!otpResult.IsSuccess)
        {
            return Json(new { success = false, message = otpResult.Message });
        }

        var deleteResult = await _parentLoginRepository.SoftDeleteAccountAsync(guardianId, cancellationToken);
        if (!deleteResult.Success)
        {
            return Json(new { success = false, message = deleteResult.Message });
        }

        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return Json(new
        {
            success = true,
            message = deleteResult.Message,
            redirectUrl = Url.Action("Index", "Home", new { msg = "account-deleted" })
        });
    }

    private static string MaskEmail(string email)
    {
        var normalized = (email ?? string.Empty).Trim();
        var at = normalized.IndexOf('@');
        if (at <= 1)
        {
            return normalized;
        }

        var local = normalized[..at];
        var domain = normalized[(at + 1)..];
        var visible = local.Length <= 2 ? local[..1] : local[..2];
        return $"{visible}***@{domain}";
    }
}
