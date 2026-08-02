using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Orders;
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
public sealed class HistoryController : Controller
{
    private const int DefaultPageSize = 20;

    private readonly ITransactionRepository _transactionRepository;
    private readonly IMealOrderRepository _mealOrderRepository;
    private readonly IStudentRepository _studentRepository;
    private readonly OrderPaymentSummaryBuilder _summaryBuilder;

    public HistoryController(
        ITransactionRepository transactionRepository,
        IMealOrderRepository mealOrderRepository,
        IStudentRepository studentRepository,
        OrderPaymentSummaryBuilder summaryBuilder)
    {
        _transactionRepository = transactionRepository;
        _mealOrderRepository = mealOrderRepository;
        _studentRepository = studentRepository;
        _summaryBuilder = summaryBuilder;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        int? studentId,
        string? type,
        DateTime? fromDate,
        DateTime? toDate,
        int page = 1,
        CancellationToken cancellationToken = default)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        var normalizedType = string.IsNullOrWhiteSpace(type) ? "all" : type.Trim().ToLowerInvariant();
        if (normalizedType is not ("all" or "topup" or "order"))
        {
            normalizedType = "all";
        }

        if (page <= 0)
        {
            page = 1;
        }

        if (studentId is <= 0)
        {
            studentId = null;
        }

        var students = await _studentRepository.GetStudentBasicListByGuardianAsync(guardianId, cancellationToken);
        if (studentId is int selectedStudentId
            && !students.Any(s => Convert.ToInt32(s.UserId, CultureInfo.InvariantCulture) == selectedStudentId))
        {
            studentId = null;
        }

        var normalizedFromDate = fromDate?.Date;
        var normalizedToDate = toDate?.Date;
        if (normalizedFromDate is DateTime from
            && normalizedToDate is DateTime to
            && from > to)
        {
            (normalizedFromDate, normalizedToDate) = (to, from);
        }

        var history = await _transactionRepository.GetTransactionHistoryAsync(
            studentId,
            guardianId,
            normalizedType,
            normalizedFromDate,
            normalizedToDate,
            page,
            DefaultPageSize,
            cancellationToken);

        var items = history.Items
            .Select(MapListItem)
            .ToList();

        var model = new HistoryPageViewModel
        {
            Children = students
                .Select(s => new HistoryChildOption
                {
                    StudentId = Convert.ToInt32(s.UserId, CultureInfo.InvariantCulture),
                    Name = s.Name?.Trim() ?? string.Empty
                })
                .ToList(),
            SelectedStudentId = studentId,
            SelectedType = normalizedType,
            FromDate = normalizedFromDate,
            ToDate = normalizedToDate,
            Page = history.Page,
            PageSize = history.PageSize,
            TotalCount = history.TotalCount,
            Items = items
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> Detail(string? orderId, CancellationToken cancellationToken = default)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(orderId))
        {
            return NotFound();
        }

        var trimmedOrderId = orderId.Trim();
        var order = await _mealOrderRepository.GetOrderDetailByOrderIdAsync(guardianId, trimmedOrderId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        var status = HistoryStatusHelper.ResolveCanonical(
            order.OrderStatusId,
            order.TransactionStatusId,
            order.IsPaid,
            order.IsTransactionCompleted);
        AlaCarteSummaryViewModel? alaCarteSummary = null;
        MealComboSummaryViewModel? comboSummary = null;

        if (order.OrderTypeId == (int)TransactionTypeEnum.A_La_Carte)
        {
            alaCarteSummary = await _summaryBuilder.BuildAlaCarteSummaryFromOrderAsync(
                guardianId,
                trimmedOrderId,
                cancellationToken);
        }
        else if (order.OrderTypeId == (int)TransactionTypeEnum.MealOrder)
        {
            comboSummary = await _summaryBuilder.BuildComboSummaryFromOrderAsync(
                guardianId,
                trimmedOrderId,
                cancellationToken);
        }

        var model = new HistoryOrderDetailViewModel
        {
            IsSuccess = status.IsCompleted || order.IsPaid,
            IsPending = status.IsPending,
            StatusLabel = status.Label,
            StatusCss = status.Css,
            Message = BuildOrderStatusMessage(status.Label, order.IsPaid),
            OrderId = order.OrderId,
            CreatedOn = order.CreatedOn,
            OrderTypeId = order.OrderTypeId,
            AlaCarteSummary = alaCarteSummary,
            ComboSummary = comboSummary
        };

        return View(model);
    }

    [HttpGet]
    public async Task<IActionResult> TopupDetail(int id, CancellationToken cancellationToken = default)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        if (id <= 0)
        {
            return NotFound();
        }

        var transaction = await _transactionRepository.GetGuardianTransactionByIdAsync(guardianId, id, cancellationToken);
        if (transaction is null
            || !string.Equals(transaction.TransactionType, "topup", StringComparison.OrdinalIgnoreCase))
        {
            return NotFound();
        }

        var status = HistoryStatusHelper.Resolve(transaction.StatusId, transaction.IsTransactionCompleted);
        var model = new HistoryTopupDetailViewModel
        {
            Id = transaction.Id,
            StudentName = transaction.StudentName?.Trim() ?? string.Empty,
            Amount = transaction.Amount,
            StatusLabel = status.Label,
            StatusCss = status.Css,
            IsCompleted = status.IsCompleted,
            IsPending = status.IsPending,
            Reference = string.IsNullOrWhiteSpace(transaction.OrderId)
                ? transaction.Id.ToString(CultureInfo.InvariantCulture)
                : transaction.OrderId,
            GatewayTransactionId = transaction.GatewayTransactionId?.Trim() ?? string.Empty,
            Remarks = transaction.Remarks?.Trim() ?? string.Empty,
            CreatedOn = transaction.CreatedOn
        };

        return View(model);
    }

    private HistoryListItemViewModel MapListItem(TransactionHistoryItemDto item)
    {
        var status = HistoryStatusHelper.Resolve(item.StatusId, item.IsTransactionCompleted);
        var isTopup = string.Equals(item.TransactionType, "topup", StringComparison.OrdinalIgnoreCase);
        var detailUrl = isTopup
            ? Url.Action("TopupDetail", "History", new { id = item.Id }) ?? "#"
            : Url.Action("Detail", "History", new { orderId = item.OrderId }) ?? "#";

        return new HistoryListItemViewModel
        {
            Id = item.Id,
            TransactionType = item.TransactionType,
            OrderTypeId = item.OrderTypeId,
            TypeLabel = ResolveTypeLabel(item),
            StudentName = string.IsNullOrWhiteSpace(item.StudentName) ? "—" : item.StudentName.Trim(),
            OrderId = item.OrderId,
            Amount = item.Amount,
            IsCredit = isTopup,
            StatusLabel = status.Label,
            StatusCss = status.Css,
            IsCompleted = status.IsCompleted,
            IsPending = status.IsPending,
            CreatedOn = item.CreatedOn,
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

    private static string BuildOrderStatusMessage(string statusLabel, bool isPaid)
    {
        if (isPaid || string.Equals(statusLabel, "Completed", StringComparison.OrdinalIgnoreCase))
        {
            return "Order details are shown below.";
        }

        if (string.Equals(statusLabel, "Pending", StringComparison.OrdinalIgnoreCase))
        {
            return "This order payment is still processing.";
        }

        return "This order was not completed successfully.";
    }
}
