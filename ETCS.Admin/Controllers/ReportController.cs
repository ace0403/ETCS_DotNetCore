using ETCS.Admin.Infrastructure.Auth;
using ETCS.Admin.Infrastructure.Reports;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Admin.Reports.AdminTransactions;
using ETCS.Shared.Infrastructure.Admin.Reports.CanteenTransactions;
using ETCS.Shared.Infrastructure.Admin.Reports.MealOrders;
using ETCS.Shared.Infrastructure.Admin.Reports.MealOrderPayments;
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
    private readonly IMealOrderPaymentReportRepository _mealOrderPaymentReportRepository;
    private readonly IMealOrderPaymentMealDbReportRepository _mealOrderPaymentMealDbReportRepository;
    private readonly IMealEnumAdminRepository _mealEnumAdminRepository;
    private readonly IAdminSchoolScopeService _schoolScope;
    private readonly MealOrderPaymentReportDateRules _mealOrderPaymentReportDateRules;

    public ReportController(
        ICanteenTransactionReportRepository canteenTransactionReportRepository,
        IAdminTransactionReportRepository adminTransactionReportRepository,
        ITerminalSalesSummaryReportRepository terminalSalesSummaryReportRepository,
        IMealOrderReportRepository mealOrderReportRepository,
        IMealOrderMealDbReportRepository mealOrderMealDbReportRepository,
        IMealOrderPaymentReportRepository mealOrderPaymentReportRepository,
        IMealOrderPaymentMealDbReportRepository mealOrderPaymentMealDbReportRepository,
        IMealEnumAdminRepository mealEnumAdminRepository,
        IAdminSchoolScopeService schoolScope,
        MealOrderPaymentReportDateRules mealOrderPaymentReportDateRules)
    {
        _canteenTransactionReportRepository = canteenTransactionReportRepository;
        _adminTransactionReportRepository = adminTransactionReportRepository;
        _terminalSalesSummaryReportRepository = terminalSalesSummaryReportRepository;
        _mealOrderReportRepository = mealOrderReportRepository;
        _mealOrderMealDbReportRepository = mealOrderMealDbReportRepository;
        _mealOrderPaymentReportRepository = mealOrderPaymentReportRepository;
        _mealOrderPaymentMealDbReportRepository = mealOrderPaymentMealDbReportRepository;
        _mealEnumAdminRepository = mealEnumAdminRepository;
        _schoolScope = schoolScope;
        _mealOrderPaymentReportDateRules = mealOrderPaymentReportDateRules;
    }

    [Route("/report/canteen-transactions")]
    public async Task<IActionResult> CanteenTransactions(CancellationToken cancellationToken)
    {
        ViewBag.Schools = await GetScopedCodeSchoolsAsync(cancellationToken);
        return View();
    }

    [Route("/report/transactions")]
    public async Task<IActionResult> AdminTransaction(CancellationToken cancellationToken)
    {
        ViewBag.Schools = await GetScopedCodeSchoolsAsync(cancellationToken);
        return View();
    }

    [Route("/report/terminal-sales-summary")]
    public async Task<IActionResult> TerminalSalesSummary(CancellationToken cancellationToken)
    {
        ViewBag.Schools = await GetScopedCodeSchoolsAsync(cancellationToken);
        return View();
    }

    [Route("/report/meal-order")]
    public async Task<IActionResult> MealOrders(CancellationToken cancellationToken)
    {
        var schools = await _mealOrderReportRepository.GetSchoolsAsync(cancellationToken);
        ViewBag.Schools = _schoolScope.FilterMealOrderSchools(schools, s => s.Id);
        return View();
    }

    [Route("/report/meal-order/new")]
    public async Task<IActionResult> MealOrdersMealDb(CancellationToken cancellationToken)
    {
        var schools = await _mealOrderMealDbReportRepository.GetSchoolsAsync(cancellationToken);
        ViewBag.Schools = _schoolScope.FilterMealOrderSchools(schools, s => s.Id);
        ViewBag.MealSessions = await _mealEnumAdminRepository.GetMealSessionsAsync(cancellationToken);
        return View();
    }

    [Route("/report/meal-order-payment")]
    public async Task<IActionResult> MealOrderPayments(CancellationToken cancellationToken)
    {
        var schools = await _mealOrderPaymentReportRepository.GetSchoolsAsync(cancellationToken);
        ViewBag.Schools = _schoolScope.FilterMealOrderSchools(schools, s => s.Id);
        SetLegacyMealOrderPaymentReportViewData();
        return View();
    }

    [Route("/report/meal-order-payment/new")]
    public async Task<IActionResult> MealOrderPaymentsMealDb(CancellationToken cancellationToken)
    {
        var schools = await _mealOrderPaymentMealDbReportRepository.GetSchoolsAsync(cancellationToken);
        ViewBag.Schools = _schoolScope.FilterMealOrderSchools(schools, s => s.Id);
        ViewBag.MealSessions = await _mealEnumAdminRepository.GetMealSessionsAsync(cancellationToken);
        SetNewMealOrderPaymentReportViewData();
        return View();
    }

    [HttpGet]
    public async Task<JsonResult> GetMealOrderMealTypes(int sessionId, CancellationToken cancellationToken)
    {
        var data = sessionId > 0
            ? await _mealEnumAdminRepository.GetMealTypesBySessionAsync(sessionId, cancellationToken)
            : [];
        return Json(new { data });
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

    [HttpPost]
    public async Task<JsonResult> GetMealOrderPaymentsList(
        [FromForm] MealOrderPaymentReportListRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartDate is null || request.EndDate is null)
        {
            return Json(new
            {
                request.Draw,
                RecordsTotal = 0,
                RecordsFiltered = 0,
                Data = Array.Empty<MealOrderPaymentReportRowDto>(),
                Success = false,
                Message = "Start date and end date are required."
            });
        }

        var adminRequest = MealOrderPaymentAdminTransactionMapper.ToAdminListRequest(request);
        var result = await _adminTransactionReportRepository.GetTransactionsPagedAsync(
            adminRequest,
            cancellationToken);

        return Json(new
        {
            result.Draw,
            result.RecordsTotal,
            result.RecordsFiltered,
            Data = MealOrderPaymentAdminTransactionMapper.ToPaymentRows(result.Data),
            Success = true
        });
    }

    [HttpPost]
    public async Task<JsonResult> GetMealOrderPaymentsMealDbList(
        [FromForm] MealOrderPaymentReportListRequest request,
        CancellationToken cancellationToken)
    {
        if (request.StartDate is null || request.EndDate is null)
        {
            return Json(new
            {
                request.Draw,
                RecordsTotal = 0,
                RecordsFiltered = 0,
                Data = Array.Empty<MealOrderPaymentReportRowDto>(),
                Success = false,
                Message = "Start date and end date are required."
            });
        }

        var result = await _mealOrderPaymentMealDbReportRepository.GetOrdersPagedAsync(request, cancellationToken);
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportMealOrderPayments(
        [FromForm] MealOrderPaymentReportFilter filter,
        CancellationToken cancellationToken)
    {
        var adminFilter = MealOrderPaymentAdminTransactionMapper.ToAdminFilter(filter);
        var transactionRows = await _adminTransactionReportRepository.GetTransactionsAsync(
            adminFilter,
            cancellationToken);
        var rows = MealOrderPaymentAdminTransactionMapper.ToPaymentRows(transactionRows);

        if (rows.Count == 0)
        {
            TempData["ReportError"] = "No data available..";
            return RedirectToAction(nameof(MealOrderPayments));
        }

        var fileBytes = MealOrderPaymentExcelExporter.ExportOld(rows);
        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            MealOrderPaymentExcelExporter.BuildFileName());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ExportMealOrderPaymentsMealDb(
        [FromForm] MealOrderPaymentReportFilter filter,
        CancellationToken cancellationToken)
    {
        var rows = await _mealOrderPaymentMealDbReportRepository.GetOrdersAsync(filter, cancellationToken);
        if (rows.Count == 0)
        {
            TempData["ReportError"] = "No data available..";
            return RedirectToAction(nameof(MealOrderPaymentsMealDb));
        }

        var fileBytes = MealOrderPaymentExcelExporter.ExportNew(rows);
        return File(
            fileBytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            MealOrderPaymentExcelExporter.BuildFileName());
    }

    private void SetLegacyMealOrderPaymentReportViewData()
    {
        var defaults = _mealOrderPaymentReportDateRules.GetLegacyDefaultRange();
        ViewBag.MealOrderPaymentStartDate = defaults.StartDate;
        ViewBag.MealOrderPaymentEndDate = defaults.EndDate;
        ViewBag.MealOrderPaymentMaxDate = _mealOrderPaymentReportDateRules.CutoverDateIso;
    }

    private void SetNewMealOrderPaymentReportViewData()
    {
        var defaults = _mealOrderPaymentReportDateRules.GetNewDefaultRange();
        ViewBag.MealOrderPaymentStartDate = defaults.StartDate;
        ViewBag.MealOrderPaymentEndDate = defaults.EndDate;
        ViewBag.MealOrderPaymentMinDate = _mealOrderPaymentReportDateRules.CutoverDateIso;
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
