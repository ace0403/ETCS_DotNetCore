using ETCS.PaymentGateway.Models;
using ETCS.Shared.Application.Topup;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Infrastructure.Transaction;
using ETCS.Web.Infrastructure.Auth;
using ETCS.Web.Infrastructure.Orders;
using ETCS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace ETCS.Web.Controllers;

[Authorize]
public sealed class TopupController : Controller
{
    private const int RecentTransactionCount = 5;
    private const int HistoryWindowPageSize = 200;

    private readonly IStudentRepository _studentRepository;
    private readonly ITransactionRepository _transactionRepository;
    private readonly ITopupInitiateService _topupInitiateService;
    private readonly ITopupPaymentCompleteService _topupPaymentCompleteService;

    public TopupController(
        IStudentRepository studentRepository,
        ITransactionRepository transactionRepository,
        ITopupInitiateService topupInitiateService,
        ITopupPaymentCompleteService topupPaymentCompleteService)
    {
        _studentRepository = studentRepository;
        _transactionRepository = transactionRepository;
        _topupInitiateService = topupInitiateService;
        _topupPaymentCompleteService = topupPaymentCompleteService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        var model = await BuildPageModelAsync(guardianId, cancellationToken);
        return View(model);
    }

    [HttpGet]
    public async Task<JsonResult> Minimum(int studentId, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Json(new { Success = false, Message = "Unauthorized." });
        }

        if (studentId <= 0 || !await StudentBelongsToGuardianAsync(guardianId, studentId, cancellationToken))
        {
            return Json(new { Success = false, Message = "Invalid student selection." });
        }

        var minimum = await _studentRepository.GetStudentMinimumTopupAsync(studentId, cancellationToken) ?? 0m;
        return Json(new
        {
            Success = true,
            StudentId = studentId.ToString(CultureInfo.InvariantCulture),
            MinimumTopupAmount = minimum
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<JsonResult> RequestTopup(TopupRequestModel model, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Json(new { Success = false, Message = "Unauthorized." });
        }

        if (model.StudentId <= 0)
        {
            return Json(new { Success = false, Message = "Please select a child." });
        }

        if (!await StudentBelongsToGuardianAsync(guardianId, model.StudentId, cancellationToken))
        {
            return Json(new { Success = false, Message = "Invalid student selection." });
        }

        if (model.Amount <= 0)
        {
            return Json(new { Success = false, Message = "Amount must be greater than zero." });
        }

        var returnUrl = Url.Action(
            "PaymentReturn",
            "Topup",
            values: null,
            protocol: Request.Scheme,
            host: Request.Host.Value) + "?orderId={0}";

        var result = await _topupInitiateService.InitiateAsync(
            new TopupInitiateRequest
            {
                GuardianId = guardianId,
                StudentId = model.StudentId.ToString(CultureInfo.InvariantCulture),
                Amount = model.Amount,
                ReturnUrl = returnUrl
            },
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Json(new
            {
                Success = false,
                Message = result.Message,
                MinimumTopupAmount = result.MinimumTopupAmount
            });
        }

        return Json(new
        {
            Success = true,
            Message = result.Message,
            RedirectUrl = result.RedirectUrl,
            OrderId = result.OrderId,
            TransactionId = result.TransactionId
        });
    }

    [HttpGet]
    [HttpPost]
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> PaymentReturn(
        [FromQuery] string? orderId,
        [FromForm] ComtrustCallbackRequest? callback,
        CancellationToken cancellationToken)
    {
        var resolvedOrderId = OrderPaymentSummaryBuilder.ResolvePaymentReturnOrderId(orderId, callback);
        if (string.IsNullOrWhiteSpace(resolvedOrderId))
        {
            return View(new TopupPaymentReturnViewModel
            {
                IsSuccess = false,
                Message = "Top-up reference is missing."
            });
        }

        var topupState = await _transactionRepository.GetTopupPendingByOrderIdAsync(
            resolvedOrderId,
            callback?.TransactionID,
            cancellationToken);

        if (topupState is null)
        {
            return View(new TopupPaymentReturnViewModel
            {
                IsSuccess = false,
                Message = "Top-up was not found.",
                OrderId = resolvedOrderId
            });
        }

        if (User.TryGetGuardianId(out var sessionGuardianId)
            && topupState.GuardianId > 0
            && sessionGuardianId != topupState.GuardianId)
        {
            return View(new TopupPaymentReturnViewModel
            {
                IsSuccess = false,
                Message = "Top-up was not found.",
                OrderId = resolvedOrderId
            });
        }

        var gatewayTransactionId = topupState.GatewayTransactionId;
        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
        {
            gatewayTransactionId = callback?.TransactionID?.Trim() ?? string.Empty;
        }

        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
        {
            return View(new TopupPaymentReturnViewModel
            {
                IsSuccess = false,
                Message = "Payment session was not found.",
                OrderId = resolvedOrderId,
                Amount = topupState.Amount
            });
        }

        var studentName = await ResolveStudentNameAsync(
            topupState.GuardianId,
            topupState.StudentId,
            cancellationToken);

        var completeResult = await _topupPaymentCompleteService.CompleteAsync(
            new TopupCompleteRequest
            {
                StudentId = topupState.StudentId,
                OrderId = resolvedOrderId,
                TransactionId = gatewayTransactionId
            },
            cancellationToken);

        return View(new TopupPaymentReturnViewModel
        {
            IsSuccess = completeResult.IsSuccess,
            IsPending = completeResult.IsPending,
            Message = completeResult.Message,
            OrderId = resolvedOrderId,
            TransactionId = completeResult.TransactionId,
            Amount = completeResult.Amount > 0 ? completeResult.Amount : topupState.Amount,
            StudentName = studentName
        });
    }

    private async Task<TopupPageViewModel> BuildPageModelAsync(int guardianId, CancellationToken cancellationToken)
    {
        var today = DateTime.Today;
        var weekStart = GetWeekStartMonday(today);
        var historyFrom = weekStart < today.AddDays(-30) ? weekStart : today.AddDays(-30);

        var studentsTask = _studentRepository.GetStudentsByGuardianAsync(guardianId, null, cancellationToken);
        var historyTask = _transactionRepository.GetTransactionHistoryAsync(
            studentId: null,
            guardianId: guardianId,
            type: "all",
            fromDate: historyFrom,
            toDate: today,
            page: 1,
            pageSize: HistoryWindowPageSize,
            cancellationToken);

        await Task.WhenAll(studentsTask, historyTask);

        var students = await studentsTask;
        var historyItems = (await historyTask).Items.ToList();

        var children = new List<TopupChildItem>();
        foreach (var student in students)
        {
            var studentId = Convert.ToInt32(student.UserId, CultureInfo.InvariantCulture);
            var minimum = await _studentRepository.GetStudentMinimumTopupAsync(studentId, cancellationToken) ?? 0m;
            children.Add(new TopupChildItem
            {
                StudentId = studentId,
                Name = student.Name?.Trim() ?? string.Empty,
                Balance = student.Balprepaid ?? 0m,
                MinimumTopupAmount = minimum
            });
        }

        var orderedItems = historyItems
            .OrderByDescending(i => i.CreatedOn)
            .ThenByDescending(i => i.Id)
            .ToList();

        return new TopupPageViewModel
        {
            Children = children,
            SelectedStudentId = children.FirstOrDefault()?.StudentId ?? 0,
            TotalBalance = children.Sum(c => c.Balance),
            WeeklySpendSeries = BuildWeeklySpendSeries(historyItems, weekStart),
            RecentTransactions = orderedItems
                .Take(RecentTransactionCount)
                .Select(MapRecentTransaction)
                .ToList()
        };
    }

    private static DateTime GetWeekStartMonday(DateTime day)
    {
        var offset = ((int)day.DayOfWeek + 6) % 7;
        return day.Date.AddDays(-offset);
    }

    private static IReadOnlyList<DashboardChartPoint> BuildWeeklySpendSeries(
        IReadOnlyList<TransactionHistoryItemDto> items,
        DateTime weekStartMonday)
    {
        var debits = items
            .Where(i => !string.Equals(i.TransactionType, "topup", StringComparison.OrdinalIgnoreCase)
                        && HistoryStatusHelper.CountsTowardSpend(i.StatusId, i.IsTransactionCompleted))
            .ToList();

        var points = new List<DashboardChartPoint>(7);
        for (var i = 0; i < 7; i++)
        {
            var day = weekStartMonday.AddDays(i);
            var total = debits
                .Where(x => x.CreatedOn.Date == day)
                .Sum(x => x.Amount);

            points.Add(new DashboardChartPoint
            {
                Label = day.ToString("ddd", CultureInfo.InvariantCulture),
                Value = total
            });
        }

        return points;
    }

    private WalletRecentTransactionItem MapRecentTransaction(TransactionHistoryItemDto item)
    {
        var status = HistoryStatusHelper.Resolve(item.StatusId, item.IsTransactionCompleted);
        var isTopup = string.Equals(item.TransactionType, "topup", StringComparison.OrdinalIgnoreCase);
        var studentName = string.IsNullOrWhiteSpace(item.StudentName) ? "your child" : item.StudentName.Trim();
        var detailUrl = isTopup
            ? Url.Action("TopupDetail", "History", new { id = item.Id }) ?? "#"
            : Url.Action("Detail", "History", new { orderId = item.OrderId }) ?? "#";

        var title = isTopup
            ? $"Wallet top-up AED {item.Amount:0.00}"
            : $"{ResolveTypeLabel(item)} for {studentName}";

        if (!isTopup && !string.IsNullOrWhiteSpace(item.Remarks))
        {
            title = $"{ResolveTypeLabel(item)} for {studentName} - {item.Remarks.Trim()}";
        }

        var reference = !string.IsNullOrWhiteSpace(item.OrderId)
            ? item.OrderId.Trim()
            : $"TXN-{item.Id}";

        return new WalletRecentTransactionItem
        {
            Title = title,
            Reference = reference,
            Amount = item.Amount,
            IsCredit = isTopup,
            IconClass = isTopup ? "ti-wallet" : ResolveDebitIcon(item),
            IconToneCss = isTopup ? "is-topup" : "is-debit",
            StatusLabel = status.Label.ToLowerInvariant(),
            StatusCss = status.Css,
            DetailUrl = detailUrl
        };
    }

    private static string ResolveTypeLabel(TransactionHistoryItemDto item)
    {
        if (string.Equals(item.TransactionType, "topup", StringComparison.OrdinalIgnoreCase))
        {
            return "Top-up";
        }

        return item.OrderTypeId switch
        {
            (int)TransactionTypeEnum.A_La_Carte => "Ala-Carte",
            (int)TransactionTypeEnum.MealOrder => "Meal Combo",
            (int)TransactionTypeEnum.POS => "POS Order",
            _ => "Order"
        };
    }

    private static string ResolveDebitIcon(TransactionHistoryItemDto item)
    {
        return item.OrderTypeId switch
        {
            (int)TransactionTypeEnum.A_La_Carte => "ti-tools-kitchen-2",
            (int)TransactionTypeEnum.MealOrder => "ti-package",
            _ => "ti-shopping-bag"
        };
    }

    private async Task<bool> StudentBelongsToGuardianAsync(
        int guardianId,
        int studentId,
        CancellationToken cancellationToken)
    {
        var students = await _studentRepository.GetStudentBasicListByGuardianAsync(guardianId, cancellationToken);
        return students.Any(s => s.UserId == studentId);
    }

    private async Task<string> ResolveStudentNameAsync(
        int guardianId,
        int studentId,
        CancellationToken cancellationToken)
    {
        if (guardianId <= 0 || studentId <= 0)
        {
            return string.Empty;
        }

        var students = await _studentRepository.GetStudentBasicListByGuardianAsync(guardianId, cancellationToken);
        var studentIdText = studentId.ToString(CultureInfo.InvariantCulture);
        return students
            .FirstOrDefault(s => string.Equals(s.StudentId, studentIdText, StringComparison.OrdinalIgnoreCase))
            ?.Name
            ?.Trim() ?? string.Empty;
    }
}
