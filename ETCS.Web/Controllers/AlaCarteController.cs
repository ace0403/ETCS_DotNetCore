using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Meals;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Schools.Calendar;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Application.Orders;
using ETCS.Shared.Application.Students;
using ETCS.Web.Infrastructure.AlaCarte;
using ETCS.Web.Infrastructure.Auth;
using ETCS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Globalization;

namespace ETCS.Web.Controllers;

[Authorize]
public class AlaCarteController : Controller
{
    private readonly IStudentRepository _studentRepository;
    private readonly IMealEnumAdminRepository _mealEnumRepository;
    private readonly IMealRepository _mealRepository;
    private readonly IMealOrderRepository _mealOrderRepository;
    private readonly IOrderInitiateService _orderInitiateService;
    private readonly IOrderPaymentCompleteService _orderPaymentCompleteService;
    private readonly IStudentOrderTypeAccessService _orderTypeAccess;
    private readonly ETCS.Web.Infrastructure.Orders.MealOrderBookingWindow _bookingWindow;
    private readonly ISchoolCalendarService _schoolCalendar;

    public AlaCarteController(
        IStudentRepository studentRepository,
        IMealEnumAdminRepository mealEnumRepository,
        IMealRepository mealRepository,
        IMealOrderRepository mealOrderRepository,
        IOrderInitiateService orderInitiateService,
        IOrderPaymentCompleteService orderPaymentCompleteService,
        IStudentOrderTypeAccessService orderTypeAccess,
        ETCS.Web.Infrastructure.Orders.MealOrderBookingWindow bookingWindow,
        ISchoolCalendarService schoolCalendar)
    {
        _studentRepository = studentRepository;
        _mealEnumRepository = mealEnumRepository;
        _mealRepository = mealRepository;
        _mealOrderRepository = mealOrderRepository;
        _orderInitiateService = orderInitiateService;
        _orderPaymentCompleteService = orderPaymentCompleteService;
        _orderTypeAccess = orderTypeAccess;
        _bookingWindow = bookingWindow;
        _schoolCalendar = schoolCalendar;
    }

    public async Task<IActionResult> Index(int? studentId, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        var model = await BuildPageModelAsync(guardianId, studentId, cancellationToken);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> SearchMeal(AlaCarteSearchRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid || !await StudentBelongsToGuardianAsync(guardianId, request.StudentId, cancellationToken))
        {
            return PartialView("_MealList", Array.Empty<AlaCarteMealTypeGroup>());
        }

        if (!await _orderTypeAccess.IsAllowedAsync(request.StudentId, (int)TransactionTypeEnum.A_La_Carte, cancellationToken))
        {
            return PartialView("_MealList", Array.Empty<AlaCarteMealTypeGroup>());
        }

        if (!_bookingWindow.IsBookable(request.MealDate))
        {
            ViewData["SelectedMealDate"] = request.MealDate.ToString("yyyy-MM-dd");
            ViewData["SelectedMealDateDisplay"] = request.MealDate.ToString("dddd, dd MMM yyyy", CultureInfo.InvariantCulture);
            return PartialView("_MealList", Array.Empty<AlaCarteMealTypeGroup>());
        }

        var schoolId = await ResolveStudentSchoolIdAsync(request.StudentId, cancellationToken);
        if (schoolId is > 0
            && !await _schoolCalendar.IsOrderableAsync(schoolId.Value, request.MealDate.Date, cancellationToken))
        {
            var day = await _schoolCalendar.GetDayInfoAsync(schoolId.Value, request.MealDate.Date, cancellationToken);
            return MenuClosedDayPartial(request.MealDate.Date, day);
        }

        var groups = await LoadMealGroupsAsync(request.StudentId, request.MealDate, cancellationToken);
        ViewData["SelectedMealDate"] = request.MealDate.ToString("yyyy-MM-dd");
        ViewData["SelectedMealDateDisplay"] = request.MealDate.ToString("dddd, dd MMM yyyy", CultureInfo.InvariantCulture);
        return PartialView("_MealList", groups);
    }

    [HttpPost]
    public async Task<IActionResult> GetOrderSummary(
        [FromForm] int studentId,
        [FromForm] List<AlaCarteSelectedItemRequest>? items,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        if (studentId <= 0)
        {
            return PartialView("_OrderSummary", new AlaCarteSummaryViewModel());
        }

        if (!await StudentBelongsToGuardianAsync(guardianId, studentId, cancellationToken))
        {
            return PartialView("_OrderSummary", new AlaCarteSummaryViewModel());
        }

        if (!await _orderTypeAccess.IsAllowedAsync(studentId, (int)TransactionTypeEnum.A_La_Carte, cancellationToken))
        {
            return PartialView("_OrderSummary", new AlaCarteSummaryViewModel());
        }

        var summary = await BuildSummaryAsync(studentId, items ?? [], cancellationToken);
        var children = await LoadChildrenAsync(guardianId, cancellationToken);
        summary.StudentName = children.FirstOrDefault(c => c.Id == studentId)?.Name ?? string.Empty;
        return PartialView("_OrderSummary", summary);
    }

    [HttpPost]
    public async Task<JsonResult> PlaceOrder(
        [FromForm] int studentId,
        [FromForm] List<AlaCarteSelectedItemRequest>? mealList,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Json(new { Success = false, Message = "Unauthorized." });
        }

        if (!await StudentBelongsToGuardianAsync(guardianId, studentId, cancellationToken))
        {
            return Json(new { Success = false, Message = "Invalid student selection." });
        }

        if (mealList is null || mealList.Count == 0)
        {
            return Json(new { Success = false, Message = "No meals selected." });
        }

        if (await TryGetClosedDateMessageAsync(studentId, mealList.Select(x => x.MealDate), cancellationToken) is { } closedMessage)
        {
            return Json(new { Success = false, Message = closedMessage });
        }

        var summary = await BuildSummaryAsync(studentId, mealList, cancellationToken);
        if (summary.SelectedMeals.Count == 0)
        {
            return Json(new { Success = false, Message = "Selected meals are no longer available." });
        }

        var mealLines = summary.SelectedMeals.Select(item => new OrderMealLineItemRequest
        {
            ItemId = item.Id,
            MealDate = item.MealDate,
            Price = item.Price,
            Total = item.Price,
            Quantity = 1,
            Id = item.SelectionId
        }).ToList();

        var result = await _orderInitiateService.InitiateAsync(
            new OrderInitiateRequest
            {
                StudentId = studentId,
                GuardianId = guardianId,
                OrderTypeId = (int)TransactionTypeEnum.A_La_Carte,
                Total = summary.OrderAmount,
                Notes = "A La Carte Order",
                MealList = mealLines
            },
            cancellationToken);

        if (!result.IsSuccess)
        {
            return Json(new { Success = false, Message = result.Message });
        }

        return Json(new
        {
            Success = true,
            Message = result.Message,
            RedirectUrl = result.PaymentUrl,
            OrderId = result.OrderId,
            TransactionId = result.GatewayTransactionId
        });
    }

    [HttpPost]
    public async Task<JsonResult> CompleteOrder(string orderId, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Json(new { Success = false, Message = "Unauthorized." });
        }

        if (string.IsNullOrWhiteSpace(orderId))
        {
            return Json(new { Success = false, Message = "Order reference is required." });
        }

        var paymentState = await _mealOrderRepository.GetPaymentStateAsync(orderId.Trim(), cancellationToken);
        if (paymentState is null || paymentState.GuardianId != guardianId)
        {
            return Json(new { Success = false, Message = "Order was not found." });
        }

        var gatewayTransactionId = await _mealOrderRepository.GetGatewayTransactionIdByOrderIdAsync(orderId.Trim(), cancellationToken);
        if (string.IsNullOrWhiteSpace(gatewayTransactionId))
        {
            return Json(new { Success = false, Message = "Payment session was not found." });
        }

        var result = await _orderPaymentCompleteService.CompleteAsync(
            new OrderCompleteRequest
            {
                StudentId = paymentState.StudentId,
                GuardianId = guardianId,
                OrderId = orderId.Trim(),
                TransactionId = gatewayTransactionId
            },
            cancellationToken);

        return Json(new
        {
            Success = result.IsSuccess,
            Pending = result.IsPending,
            Message = result.Message
        });
    }

    private async Task<AlaCartePageViewModel> BuildPageModelAsync(
        int guardianId,
        int? preferredStudentId,
        CancellationToken cancellationToken)
    {
        var children = await LoadEligibleChildrenAsync(guardianId, cancellationToken);

        var selectedStudentId = children.FirstOrDefault()?.Id ?? 0;
        if (preferredStudentId is > 0 && children.Any(c => c.Id == preferredStudentId.Value))
        {
            selectedStudentId = preferredStudentId.Value;
        }

        const int defaultDurationDays = 30;

        return new AlaCartePageViewModel
        {
            StudentId = selectedStudentId,
            Duration = defaultDurationDays,
            MealDate = _bookingWindow.GetEarliestBookableDate(),
            DurationList = new SelectList(
                new[] { new { Value = defaultDurationDays, Text = "30 Days" } },
                "Value",
                "Text",
                defaultDurationDays),
            Children = children
        };
    }

    private async Task<IReadOnlyList<AlaCarteChildOption>> LoadEligibleChildrenAsync(
        int guardianId,
        CancellationToken cancellationToken)
    {
        var children = await LoadChildrenAsync(guardianId, cancellationToken);
        var allowedIds = (await _orderTypeAccess.FilterAllowedAsync(
            children.Select(c => c.Id),
            (int)TransactionTypeEnum.A_La_Carte,
            cancellationToken)).ToHashSet();

        return children.Where(c => allowedIds.Contains(c.Id)).ToList();
    }

    private async Task<IReadOnlyList<AlaCarteChildOption>> LoadChildrenAsync(int guardianId, CancellationToken cancellationToken)
    {
        var students = await _studentRepository.GetStudentsByGuardianAsync(guardianId, customerId: null, cancellationToken);
        return students
            .Select(s => new AlaCarteChildOption
            {
                Id = Convert.ToInt32(s.UserId),
                Name = string.IsNullOrWhiteSpace(s.Name) ? (s.UserName ?? "Student") : s.Name.Trim()
            })
            .OrderBy(s => s.Name)
            .ToList();
    }

    private async Task<bool> StudentBelongsToGuardianAsync(int guardianId, int studentId, CancellationToken cancellationToken)
    {
        var children = await LoadChildrenAsync(guardianId, cancellationToken);
        return children.Any(c => c.Id == studentId);
    }

    private async Task<int?> ResolveStudentSchoolIdAsync(int studentId, CancellationToken cancellationToken)
    {
        return await _studentRepository.GetStudentSchoolIdAsync(studentId, cancellationToken);
    }

    private async Task<IReadOnlyList<AlaCarteMealTypeGroup>> LoadMealGroupsAsync(
        int studentId,
        DateTime mealDate,
        CancellationToken cancellationToken)
    {
        var schoolId = await ResolveStudentSchoolIdAsync(studentId, cancellationToken);
        if (schoolId is null or <= 0)
        {
            return [];
        }

        var items = await _mealRepository.GetMealItemsForStudentAsync(
            studentId,
            schoolId.Value,
            mealDate,
            cancellationToken: cancellationToken);
        return items
            .GroupBy(x => new { x.MealSessionId, x.MealSessionName, x.MealSessionCssClass })
            .OrderBy(g => g.Key.MealSessionId)
            .Select(g => new AlaCarteMealTypeGroup
            {
                MealSessionId = g.Key.MealSessionId,
                MealSessionName = g.Key.MealSessionName,
                MealSessionCssClass = g.Key.MealSessionCssClass,
                MealItems = g.OrderBy(i => i.MealTypeId).ThenBy(i => i.ItemName).ToList()
            })
            .ToList();
    }

    private async Task<AlaCarteSummaryViewModel> BuildSummaryAsync(
        int studentId,
        IReadOnlyList<AlaCarteSelectedItemRequest> selections,
        CancellationToken cancellationToken)
    {
        if (selections.Count == 0)
        {
            return new AlaCarteSummaryViewModel();
        }

        var schoolId = await ResolveStudentSchoolIdAsync(studentId, cancellationToken);
        if (schoolId is null or <= 0)
        {
            return new AlaCarteSummaryViewModel();
        }

        var menuLookup = new Dictionary<int, MealItemDto>();
        var mealDates = new List<DateTime>();
        foreach (var raw in selections.Select(s => ParseMealDate(s.MealDate)).Where(d => d != default).Distinct())
        {
            if (await IsMealDateBookableAsync(schoolId.Value, raw, cancellationToken))
            {
                mealDates.Add(raw);
            }
        }

        foreach (var mealDate in mealDates)
        {
            var menuItems = await _mealRepository.GetMealItemsForStudentAsync(
                studentId,
                schoolId.Value,
                mealDate,
                cancellationToken: cancellationToken);
            foreach (var menuItem in menuItems)
            {
                menuLookup[menuItem.Id] = menuItem;
            }
        }

        var summaryItems = new List<AlaCarteSummaryItem>();
        foreach (var selection in selections)
        {
            var mealDate = ParseMealDate(selection.MealDate);
            if (mealDate == default
                || selection.ItemId <= 0
                || !await IsMealDateBookableAsync(schoolId.Value, mealDate, cancellationToken))
            {
                continue;
            }

            if (!menuLookup.TryGetValue(selection.ItemId, out var menuItem))
            {
                continue;
            }

            summaryItems.Add(new AlaCarteSummaryItem
            {
                Id = menuItem.Id,
                SelectionId = selection.Id == Guid.Empty ? Guid.NewGuid() : selection.Id,
                ItemName = menuItem.ItemName,
                MealTypeName = menuItem.MealTypeName,
                Price = menuItem.Price,
                MealDate = mealDate,
                ImageName = menuItem.ImageName
            });
        }

        return new AlaCarteSummaryViewModel
        {
            OrderAmount = summaryItems.Sum(x => x.Price),
            SelectedMeals = summaryItems
        };
    }

    private static int ParseDurationDays(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) ? days : 0;
    }

    private async Task<bool> IsMealDateBookableAsync(
        int schoolId,
        DateTime mealDate,
        CancellationToken cancellationToken)
    {
        if (!_bookingWindow.IsBookable(mealDate))
        {
            return false;
        }

        return await _schoolCalendar.IsOrderableAsync(schoolId, mealDate.Date, cancellationToken);
    }

    private async Task<string?> TryGetClosedDateMessageAsync(
        int studentId,
        IEnumerable<string?> mealDates,
        CancellationToken cancellationToken)
    {
        var schoolId = await ResolveStudentSchoolIdAsync(studentId, cancellationToken);
        var dates = mealDates
            .Select(ParseMealDate)
            .Where(d => d != default)
            .OrderBy(d => d)
            .Distinct()
            .ToList();

        foreach (var date in dates)
        {
            if (!_bookingWindow.IsBookable(date))
            {
                return _bookingWindow.FormatClosedDateMessage(date);
            }

            if (schoolId is > 0
                && !await _schoolCalendar.IsOrderableAsync(schoolId.Value, date, cancellationToken))
            {
                var day = await _schoolCalendar.GetDayInfoAsync(schoolId.Value, date, cancellationToken);
                return day.GetClosedOrderMessage(date);
            }
        }

        return null;
    }

    private static DateTime ParseMealDate(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return default;
        }

        var formats = new[] { "yyyy/MM/dd", "yyyy-MM-dd", "dd/MM/yyyy" };
        if (DateTime.TryParseExact(raw, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return parsed.Date;
        }

        return DateTime.TryParse(raw, CultureInfo.InvariantCulture, DateTimeStyles.None, out parsed)
            ? parsed.Date
            : default;
    }

    private IActionResult MenuClosedDayPartial(DateTime mealDate, SchoolDayInfo day)
    {
        var title = string.IsNullOrWhiteSpace(day.Title) ? null : day.Title.Trim();
        var isGenericHolidayTitle = string.Equals(title, "Holiday", StringComparison.OrdinalIgnoreCase);
        var isWeekend = mealDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday
            && (string.IsNullOrWhiteSpace(title) || isGenericHolidayTitle);

        ViewData["ClosedDate"] = mealDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ViewData["ClosedDayName"] = mealDate.ToString("dddd", CultureInfo.InvariantCulture);
        ViewData["ClosedDayType"] = day.Status == SchoolDayStatus.HalfDay
            ? "halfday"
            : isWeekend
                ? "weekend"
                : "holiday";
        ViewData["ClosedDayTitle"] = isGenericHolidayTitle ? null : title;
        return PartialView("_MenuClosedDay");
    }
}

