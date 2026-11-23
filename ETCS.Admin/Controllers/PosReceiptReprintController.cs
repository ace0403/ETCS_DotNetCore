using ETCS.Admin.Infrastructure.Auth;
using ETCS.Admin.Options;
using ETCS.Admin.Services;
using ETCS.Shared.Infrastructure.Admin.Master.Students;
using ETCS.Shared.Infrastructure.Admin.Reports.PosReceiptReprint;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace ETCS.Admin.Controllers;

[Authorize]
[AdminPermission]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PosReceiptReprintController(
    IPosReceiptReprintRepository repository,
    IStudentAdminRepository studentAdminRepository,
    IAdminSchoolScopeService schools,
    IReceiptLogoLoader logoLoader,
    IOptions<ReceiptBrandingOptions> brandingOptions,
    IOptions<PosReceiptReprintOptions> reprintOptions) : Controller
{
    [Route("pos-receipt-reprint")]
    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var schoolLookups = await studentAdminRepository.SchoolLookupsAsync(cancellationToken);
        ViewBag.Schools = schools.FilterSchools(schoolLookups, s => s.Id);
        BindReceiptConfig();
        return View();
    }

    [HttpPost]
    public async Task<JsonResult> GetList(
        [FromForm] PosReceiptReprintListRequest request,
        CancellationToken cancellationToken)
    {
        if (request.From is null || request.To is null)
        {
            return Json(Fail(request.Draw, "From and To dates are required."));
        }

        var from = request.From.Value.Date;
        var to = request.To.Value.Date;
        if (from.Year < 2000 || to.Year > 2100 || to < from || (to - from).Days > 31)
        {
            return Json(Fail(request.Draw, "Choose a valid date range of up to 31 days."));
        }

        if (request.SchoolId <= 0)
        {
            return Json(Fail(request.Draw, "School is required."));
        }

        string schoolCode;
        IReadOnlyList<string> allowedCodes;
        try
        {
            var schoolParams = await schools.ResolveAdminSchoolParamsFromIdQueryAsync(
                request.SchoolId.ToString(),
                cancellationToken);
            schoolCode = schoolParams.SchoolCode;
            allowedCodes = string.IsNullOrWhiteSpace(schoolParams.SchoolCodesCsv)
                ? []
                : schoolParams.SchoolCodesCsv
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .ToList();
        }
        catch (UnauthorizedAccessException ex)
        {
            return Json(Fail(request.Draw, ex.Message));
        }

        if (allowedCodes.Count == 0)
        {
            return Json(Fail(request.Draw, "School code could not be resolved."));
        }

        var result = await repository.SearchPagedAsync(
            new PosReceiptReprintSearchFilter
            {
                From = from,
                To = to,
                TransactionId = request.TransactionId,
                CustomerId = request.CustomerId,
                TerminalCode = request.TerminalCode,
                SchoolCode = schoolCode,
                AllowedSchoolCodes = allowedCodes,
                Start = request.Start,
                Length = request.PageSize
            },
            request.Draw,
            cancellationToken);

        return Json(result);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(
        [FromQuery] string transactionId,
        [FromQuery] string customerId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(transactionId) || string.IsNullOrWhiteSpace(customerId))
        {
            return BadRequest(new { success = false, message = "Transaction ID and customer ID are required." });
        }

        var allowedCodes = schools.IsUnrestricted
            ? Array.Empty<string>()
            : await schools.GetAllowedSchoolCodesAsync(cancellationToken);

        var detail = await repository.GetDetailAsync(
            transactionId.Trim(),
            customerId.Trim(),
            allowedCodes,
            cancellationToken);

        if (detail is null)
        {
            return NotFound(new { success = false, message = "POS sale not found." });
        }

        var branding = brandingOptions.Value;
        var reprint = reprintOptions.Value;
        var terminalLabel = string.IsNullOrWhiteSpace(detail.TerminalName)
            ? detail.TerminalCode
            : detail.TerminalName;

        return Json(new
        {
            success = true,
            detail,
            receipt = new
            {
                items = detail.Lines.Select(line => new
                {
                    name = line.ItemName,
                    price = line.Amount,
                    quantity = line.Quantity
                }),
                total = detail.Amount,
                vatPercent = reprint.VatPercent,
                discountPercent = 0m,
                discountApplied = false,
                companyLine = branding.CompanyLine,
                vatRegNoLine = branding.VatRegNoLine,
                locationLine = detail.SchoolName,
                terminalLine = terminalLabel,
                paymentMode = detail.PaymentMode,
                transactionId = detail.TransactionId,
                logoBase64 = logoLoader.LoadLogoBase64() ?? string.Empty,
                printedAt = detail.SaleDateTime,
                reprintedAt = DateTime.Now,
                isUndo = detail.IsUndo,
                isReprint = true
            },
            notice = detail.LinesMissingNotice
        });
    }

    private void BindReceiptConfig()
    {
        ViewBag.BridgeBaseUrl = reprintOptions.Value.BridgeBaseUrl;
        ViewBag.VatPercent = reprintOptions.Value.VatPercent;
        ViewBag.CompanyLine = brandingOptions.Value.CompanyLine;
        ViewBag.VatRegNoLine = brandingOptions.Value.VatRegNoLine;
        ViewBag.LogoBase64 = logoLoader.LoadLogoBase64() ?? string.Empty;
    }

    private static PosReceiptReprintPagedResult Fail(int draw, string message) =>
        new()
        {
            Draw = draw,
            Success = false,
            Message = message,
            RecordsTotal = 0,
            RecordsFiltered = 0,
            Data = []
        };
}
