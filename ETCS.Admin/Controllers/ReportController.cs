using ETCS.Admin.Infrastructure.Auth;
using ETCS.Admin.Infrastructure.Reports;
using ETCS.Shared.Infrastructure.Admin.Reports.AdminTransactions;
using ETCS.Shared.Infrastructure.Admin.Reports.CanteenTransactions;
using ETCS.Shared.Infrastructure.Admin.Reports.MealOrders;
using ETCS.Shared.Infrastructure.Admin.Reports.TerminalSalesSummary;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ETCS.Admin.Controllers;

[Authorize]
[AdminPermission]
public class ReportController : Controller
{
    private readonly ICanteenTransactionReportRepository _canteenTransactionReportRepository;
    private readonly IAdminTransactionReportRepository _adminTransactionReportRepository;
    private readonly ITerminalSalesSummaryReportRepository _terminalSalesSummaryReportRepository;
    private readonly IMealOrderReportRepository _mealOrderReportRepository;
    private readonly IMealOrderMealDbReportRepository _mealOrderMealDbReportRepository;
    private readonly IAdminSchoolScopeService _schoolScope;

    public ReportController(
        ICanteenTransactionReportRepository canteenTransactionReportRepository,
        IAdminTransactionReportRepository adminTransactionReportRepository,
        ITerminalSalesSummaryReportRepository terminalSalesSummaryReportRepository,
        IMealOrderReportRepository mealOrderReportRepository,
        IMealOrderMealDbReportRepository mealOrderMealDbReportRepository,
        IAdminSchoolScopeService schoolScope)
    {
        _canteenTransactionReportRepository = canteenTransactionReportRepository;
        _adminTransactionReportRepository = adminTransactionReportRepository;
        _terminalSalesSummaryReportRepository = terminalSalesSummaryReportRepository;
        _mealOrderReportRepository = mealOrderReportRepository;
        _mealOrderMealDbReportRepository = mealOrderMealDbReportRepository;
        _schoolScope = schoolScope;
    }

    public async Task<IActionResult> CanteenTransactions(CancellationToken cancellationToken)
    {
        ViewBag.Schools = await GetScopedCodeSchoolsAsync(cancellationToken);
        return View();
    }

    public async Task<IActionResult> AdminTransaction(CancellationToken cancellationToken)
    {
        ViewBag.Schools = await GetScopedCodeSchoolsAsync(cancellationToken);
        return View();
    }

    public async Task<IActionResult> TerminalSalesSummary(CancellationToken cancellationToken)
    {
        ViewBag.Schools = await GetScopedCodeSchoolsAsync(cancellationToken);
        return View();
    }

    public async Task<IActionResult> MealOrders(CancellationToken cancellationToken)
    {
        var schools = await _mealOrderReportRepository.GetSchoolsAsync(cancellationToken);
        ViewBag.Schools = _schoolScope.FilterMealOrderSchools(schools, s => s.Id);
        return View();
    }

    public async Task<IActionResult> MealOrdersMealDb(CancellationToken cancellationToken)
    {
        var schools = await _mealOrderMealDbReportRepository.GetSchoolsAsync(cancellationToken);
        ViewBag.Schools = _schoolScope.FilterMealOrderSchools(schools, s => s.Id);
        return View();
    }

    [HttpPost]
    public async Task<JsonResult> GetCanteenTransactionsList(
        [FromForm] CanteenTransactionReportListRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartDate is null || request.EndDate is null)
        {
            return Json(new
            {
                request.Draw,
                RecordsTotal = 0,
                RecordsFiltered = 0,
                Data = Array.Empty<CanteenTransactionReportRowDto>(),
                Success = false,
                Message = "Start date and end date are required."
            });
        }

        if (request.StartDate.Value.Date > request.EndDate.Value.Date)
        {
            return Json(new
            {
                request.Draw,
                RecordsTotal = 0,
                RecordsFiltered = 0,
                Data = Array.Empty<CanteenTransactionReportRowDto>(),
                Success = false,
                Message = "Start date should be less than End date."
            });
        }

        if (!await ValidateCodeSchoolScopeAsync(request.SchoolCode, cancellationToken))
        {
            return Json(new
            {
                request.Draw,
                RecordsTotal = 0,
                RecordsFiltered = 0,
                Data = Array.Empty<CanteenTransactionReportRowDto>(),
                Success = false,
                Message = "You do not have access to this school."
            });
        }

        var result = await _canteenTransactionReportRepository.GetTransactionsPagedAsync(request, cancellationToken);
        return Json(new
        {
            result.Draw,
            result.RecordsTotal,
            result.RecordsFiltered,
            result.Data,
            Success = true
        });
    }

    [HttpPost]
    public async Task<JsonResult> GetAdminTransactionsList(
        [FromForm] AdminTransactionReportListRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartDate is null || request.EndDate is null)
        {
            return Json(new
            {
                request.Draw,
                RecordsTotal = 0,
                RecordsFiltered = 0,
                Data = Array.Empty<AdminTransactionReportRowDto>(),
                Success = false,
                Message = "Start date and end date are required."
            });
        }

        if (request.StartDate.Value.Date > request.EndDate.Value.Date)
        {
            return Json(new
            {
                request.Draw,
                RecordsTotal = 0,
                RecordsFiltered = 0,
                Data = Array.Empty<AdminTransactionReportRowDto>(),
                Success = false,
                Message = "Start date should be less than End date."
            });
        }

        var result = await _adminTransactionReportRepository.GetTransactionsPagedAsync(request, cancellationToken);
        return Json(new
        {
            result.Draw,
            result.RecordsTotal,
            result.RecordsFiltered,
            result.Data,
            Success = true
        });
    }

    [HttpPost]
    public async Task<JsonResult> GetTerminalSalesSummaryList(
        [FromForm] TerminalSalesSummaryReportListRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartDate is null || request.EndDate is null)
        {
            return Json(new
            {
                request.Draw,
                RecordsTotal = 0,
                RecordsFiltered = 0,
                Data = Array.Empty<TerminalSalesSummaryReportRowDto>(),
                Success = false,
                Message = "Start date and end date are required."
            });
        }

        if (request.StartDate.Value.Date > request.EndDate.Value.Date)
        {
            return Json(new
            {
                request.Draw,
                RecordsTotal = 0,
                RecordsFiltered = 0,
                Data = Array.Empty<TerminalSalesSummaryReportRowDto>(),
                Success = false,
                Message = "Start date should be less than End date."
            });
        }

        var result = await _terminalSalesSummaryReportRepository.GetSummaryPagedAsync(request, cancellationToken);
        return Json(new
        {
            result.Draw,
            result.RecordsTotal,
            result.RecordsFiltered,
            result.Data,
            Success = true
        });
    }

    [HttpPost]
    public async Task<JsonResult> GetMealOrdersList(
        [FromForm] MealOrderReportListRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartDate is null || request.EndDate is null)
        {
            return Json(new
            {
                request.Draw,
                RecordsTotal = 0,
                RecordsFiltered = 0,
                Data = Array.Empty<MealOrderReportRowDto>(),
                Success = false,
                Message = "Start date and end date are required."
            });
        }

        if (request.StartDate.Value.Date > request.EndDate.Value.Date)
        {
            return Json(new
            {
                request.Draw,
                RecordsTotal = 0,
                RecordsFiltered = 0,
                Data = Array.Empty<MealOrderReportRowDto>(),
                Success = false,
                Message = "Start date should be less than End date."
            });
        }

        var result = await _mealOrderReportRepository.GetOrdersPagedAsync(request, cancellationToken);
        return Json(new
        {
            result.Draw,
            result.RecordsTotal,
            result.RecordsFiltered,
            result.Data,
            Success = true
        });
    }

    [HttpPost]
    public async Task<JsonResult> GetMealOrdersMealDbList(
        [FromForm] MealOrderReportListRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartDate is null || request.EndDate is null)
        {
            return Json(new
            {
                request.Draw,
                RecordsTotal = 0,
                RecordsFiltered = 0,
                Data = Array.Empty<MealOrderReportRowDto>(),
                Success = false,
                Message = "Start date and end date are required."
            });
        }

        if (request.StartDate.Value.Date > request.EndDate.Value.Date)
        {
            return Json(new
            {
                request.Draw,
                RecordsTotal = 0,
                RecordsFiltered = 0,
                Data = Array.Empty<MealOrderReportRowDto>(),
                Success = false,
                Message = "Start date should be less than End date."
            });
        }

        var result = await _mealOrderMealDbReportRepository.GetOrdersPagedAsync(request, cancellationToken);
        return Json(new
        {
            result.Draw,
            result.RecordsTotal,
            result.RecordsFiltered,
            result.Data,
            Success = true
        });
    }

    [HttpGet]
    public async Task<JsonResult> GetCanteenBranches(string? schoolCode, CancellationToken cancellationToken)
    {
        var branches = await _canteenTransactionReportRepository.GetBranchesAsync(schoolCode, cancellationToken);
        return Json(branches);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportCanteenTransactions(
        [FromForm] CanteenTransactionReportFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.StartDate.Date > filter.EndDate.Date)
        {
            TempData["ReportError"] = "Start date should be less than End date.";
            return RedirectToAction(nameof(CanteenTransactions));
        }

        var rows = await _canteenTransactionReportRepository.GetTransactionsAsync(filter, cancellationToken);
        if (rows.Count == 0)
        {
            TempData["ReportError"] = "No data available..";
            return RedirectToAction(nameof(CanteenTransactions));
        }

        var fileBytes = CanteenTransactionExcelExporter.Export(rows);
        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            CanteenTransactionExcelExporter.BuildFileName());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportAdminTransactions(
        [FromForm] AdminTransactionReportFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.StartDate.Date > filter.EndDate.Date)
        {
            TempData["ReportError"] = "Start date should be less than End date.";
            return RedirectToAction(nameof(AdminTransaction));
        }

        var rows = await _adminTransactionReportRepository.GetTransactionsAsync(filter, cancellationToken);
        if (rows.Count == 0)
        {
            TempData["ReportError"] = "No data available..";
            return RedirectToAction(nameof(AdminTransaction));
        }

        var fileBytes = AdminTransactionExcelExporter.Export(rows);
        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            AdminTransactionExcelExporter.BuildFileName());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportTerminalSalesSummary(
        [FromForm] TerminalSalesSummaryReportFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.StartDate.Date > filter.EndDate.Date)
        {
            TempData["ReportError"] = "Start date should be less than End date.";
            return RedirectToAction(nameof(TerminalSalesSummary));
        }

        var rows = await _terminalSalesSummaryReportRepository.GetSummaryAsync(filter, cancellationToken);
        if (rows.Count == 0)
        {
            TempData["ReportError"] = "No data available..";
            return RedirectToAction(nameof(TerminalSalesSummary));
        }

        var fileBytes = TerminalSalesSummaryExcelExporter.Export(rows);
        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            TerminalSalesSummaryExcelExporter.BuildFileName());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportMealOrders(
        [FromForm] MealOrderReportFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.StartDate.Date > filter.EndDate.Date)
        {
            TempData["ReportError"] = "Start date should be less than End date.";
            return RedirectToAction(nameof(MealOrders));
        }

        var rows = await _mealOrderReportRepository.GetOrdersAsync(filter, cancellationToken);
        if (rows.Count == 0)
        {
            TempData["ReportError"] = "No data available..";
            return RedirectToAction(nameof(MealOrders));
        }

        var fileBytes = MealOrderExcelExporter.Export(rows);
        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            MealOrderExcelExporter.BuildFileName());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportMealOrdersMealDb(
        [FromForm] MealOrderReportFilter filter,
        CancellationToken cancellationToken)
    {
        if (filter.StartDate.Date > filter.EndDate.Date)
        {
            TempData["ReportError"] = "Start date should be less than End date.";
            return RedirectToAction(nameof(MealOrdersMealDb));
        }

        var rows = await _mealOrderMealDbReportRepository.GetOrdersAsync(filter, cancellationToken);
        if (rows.Count == 0)
        {
            TempData["ReportError"] = "No data available..";
            return RedirectToAction(nameof(MealOrdersMealDb));
        }

        var fileBytes = MealOrderExcelExporter.Export(rows);
        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            MealOrderExcelExporter.BuildFileName());
    }

    private async Task<IReadOnlyList<SchoolCodeLookupDto>> GetScopedCodeSchoolsAsync(CancellationToken cancellationToken)
    {
        var schools = await _canteenTransactionReportRepository.GetSchoolsAsync(cancellationToken);
        return await _schoolScope.FilterSchoolCodesAsync(schools, cancellationToken);
    }

    private async Task<bool> ValidateCodeSchoolScopeAsync(string? schoolCode, CancellationToken cancellationToken)
    {
        try
        {
            await _schoolScope.EnsureReportSchoolCodeInScopeAsync(schoolCode, cancellationToken);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    private bool ValidateIdSchoolScope(string? schoolId)
    {
        try
        {
            _schoolScope.EnsureReportSchoolIdInScope(schoolId);
            return true;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }
}
