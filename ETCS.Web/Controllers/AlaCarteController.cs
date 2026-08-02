using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Meals;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Application.Orders;
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

    public AlaCarteController(
        IStudentRepository studentRepository,
        IMealEnumAdminRepository mealEnumRepository,
        IMealRepository mealRepository,
        IMealOrderRepository mealOrderRepository,
        IOrderInitiateService orderInitiateService,
        IOrderPaymentCompleteService orderPaymentCompleteService)
    {
        _studentRepository = studentRepository;
        _mealEnumRepository = mealEnumRepository;
        _mealRepository = mealRepository;
        _mealOrderRepository = mealOrderRepository;
        _orderInitiateService = orderInitiateService;
        _orderPaymentCompleteService = orderPaymentCompleteService;
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
        var children = await LoadChildrenAsync(guardianId, cancellationToken);

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
            DurationList = new SelectList(
                new[] { new { Value = defaultDurationDays, Text = "30 Days" } },
                "Value",
                "Text",
                defaultDurationDays),
            Children = children
        };
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

        var items = await _mealRepository.GetMealItemsForStudentAsync(studentId, schoolId.Value, mealDate, mealTypeId: null, cancellationToken);
        return items
            .GroupBy(x => new { x.MealTypeId, x.MealTypeName, x.MealCssClass })
            .OrderBy(g => g.Key.MealTypeId)
            .Select(g => new AlaCarteMealTypeGroup
            {
                MealTypeId = g.Key.MealTypeId,
                MealTypeName = g.Key.MealTypeName,
                MealCssClass = g.Key.MealCssClass,
                MealItems = g.ToList()
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
        var mealDates = selections
            .Select(s => ParseMealDate(s.MealDate))
            .Where(d => d != default)
            .Distinct()
            .ToList();

        foreach (var mealDate in mealDates)
        {
            var menuItems = await _mealRepository.GetMealItemsForStudentAsync(studentId, schoolId.Value, mealDate, null, cancellationToken);
            foreach (var menuItem in menuItems)
            {
                menuLookup[menuItem.Id] = menuItem;
            }
        }

        var summaryItems = new List<AlaCarteSummaryItem>();
        foreach (var selection in selections)
        {
            var mealDate = ParseMealDate(selection.MealDate);
            if (mealDate == default || selection.ItemId <= 0)
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
}

