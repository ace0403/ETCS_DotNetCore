using ETCS.Web.Infrastructure.Auth;
using ETCS.Web.Models;
using ETCS.Web.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text;

namespace ETCS.Web.Controllers;

[Authorize]
public sealed class SupportController : Controller
{
    private readonly WebOptions _webOptions;

    public SupportController(IOptions<WebOptions> webOptions)
    {
        _webOptions = webOptions.Value;
    }

    [HttpGet]
    [Route("support")]
    public IActionResult Index()
    {
        var supportEmail = string.IsNullOrWhiteSpace(_webOptions.SupportEmail)
            ? "info@etasteuae.com"
            : _webOptions.SupportEmail.Trim();

        User.TryGetGuardianId(out var guardianId);
        var guardianName = (User.GetDisplayName() ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(guardianName))
        {
            guardianName = "Parent";
        }

        var guardianEmail = (User.GetEmail() ?? string.Empty).Trim();

        var subject = Uri.EscapeDataString($"MealHub Support – {guardianName}");
        var bodyBuilder = new StringBuilder();
        bodyBuilder.AppendLine($"Guardian: {guardianName}");
        if (!string.IsNullOrWhiteSpace(guardianEmail))
        {
            bodyBuilder.AppendLine($"Email: {guardianEmail}");
        }

        if (guardianId > 0)
        {
            bodyBuilder.AppendLine($"GuardianId: {guardianId}");
        }

        bodyBuilder.AppendLine();
        bodyBuilder.AppendLine("Message:");
        bodyBuilder.AppendLine();

        var body = Uri.EscapeDataString(bodyBuilder.ToString());
        var mailtoHref = $"mailto:{supportEmail}?subject={subject}&body={body}";

        var model = new SupportPageViewModel
        {
            SupportEmail = supportEmail,
            SupportPhone = string.IsNullOrWhiteSpace(_webOptions.SupportPhone) ? null : _webOptions.SupportPhone.Trim(),
            SupportHours = string.IsNullOrWhiteSpace(_webOptions.SupportHours) ? null : _webOptions.SupportHours.Trim(),
            GuardianName = guardianName,
            GuardianEmail = guardianEmail,
            GuardianId = guardianId > 0 ? guardianId : null,
            MailtoHref = mailtoHref
        };

        return View(model);
    }
}
