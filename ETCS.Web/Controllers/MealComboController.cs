using ETCS.Shared.Enumeration;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Meals;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Application.Orders;
using ETCS.Web.Infrastructure.AlaCarte;
using ETCS.Web.Infrastructure.Auth;
using ETCS.Web.Infrastructure.Orders;
using ETCS.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Globalization;

namespace ETCS.Web.Controllers;

[Authorize]
public class MealComboController : Controller
{
    private readonly IStudentRepository _studentRepository;
    private readonly IMealEnumAdminRepository _mealEnumRepository;
    private readonly IMealRepository _mealRepository;
    private readonly IOrderInitiateService _orderInitiateService;

    public MealComboController(
        IStudentRepository studentRepository,
        IMealEnumAdminRepository mealEnumRepository,
        IMealRepository mealRepository,
        IOrderInitiateService orderInitiateService)
    {
        _studentRepository = studentRepository;
        _mealEnumRepository = mealEnumRepository;
        _mealRepository = mealRepository;
        _orderInitiateService = orderInitiateService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        var model = await BuildPageModelAsync(guardianId, cancellationToken);
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> SearchPackages(MealComboSearchRequest request, CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        if (!ModelState.IsValid || !await StudentBelongsToGuardianAsync(guardianId, request.StudentId, cancellationToken))
        {
            return PartialView("_PackageList", Array.Empty<MealComboPackageTypeGroup>());
        }

        var groups = await LoadPackageGroupsAsync(request.StudentId, request.MealDate, cancellationToken);
        ViewData["SelectedMealDate"] = request.MealDate.ToString("yyyy-MM-dd");
        ViewData["SelectedMealDateDisplay"] = request.MealDate.ToString("dddd, dd MMM yyyy", CultureInfo.InvariantCulture);
        return PartialView("_PackageList", groups);
    }

    [HttpPost]
    public async Task<IActionResult> GetOrderSummary(
        [FromForm] int studentId,
        [FromForm] List<MealComboSelectedPackageRequest>? items,
        CancellationToken cancellationToken)
    {
        if (!User.TryGetGuardianId(out var guardianId))
        {
            return Forbid();
        }

        if (studentId <= 0)
        {
            return PartialView("_OrderSummary", new MealComboSummaryViewModel());
        }

        if (!await StudentBelongsToGuardianAsync(guardianId, studentId, cancellationToken))
        {
            return PartialView("_OrderSummary", new MealComboSummaryViewModel());
        }

        var summary = await BuildSummaryAsync(studentId, items ?? [], cancellationToken);
        var children = await LoadChildrenAsync(guardianId, cancellationToken);
        summary.StudentName = children.FirstOrDefault(c => c.Id == studentId)?.Name ?? string.Empty;
        return PartialView("_OrderSummary", summary);
    }

    [HttpPost]
    public async Task<JsonResult> PlaceOrder(
        [FromForm] int studentId,
        [FromForm] List<MealComboSelectedPackageRequest>? mealList,
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
            return Json(new { Success = false, Message = "No combos selected." });
        }

        var summary = await BuildSummaryAsync(studentId, mealList, cancellationToken);
        if (summary.SelectedPackages.Count == 0)
        {
            return Json(new { Success = false, Message = "Selected combos are no longer available." });
        }

        var mealLines = summary.SelectedPackages.Select(item => new OrderMealLineItemRequest
        {
            PackageId = item.Id,
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
                OrderTypeId = (int)TransactionTypeEnum.MealOrder,
                Total = summary.OrderAmount,
                Notes = "Meal Combo Order",
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

    private async Task<MealComboPageViewModel> BuildPageModelAsync(int guardianId, CancellationToken cancellationToken)
    {
        var children = await LoadChildrenAsync(guardianId, cancellationToken);
        const int defaultDurationDays = 30;

        return new MealComboPageViewModel
        {
            StudentId = children.FirstOrDefault()?.Id ?? 0,
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

    private async Task<IReadOnlyList<MealComboPackageTypeGroup>> LoadPackageGroupsAsync(
        int studentId,
        DateTime mealDate,
        CancellationToken cancellationToken)
    {
        var schoolId = await ResolveStudentSchoolIdAsync(studentId, cancellationToken);
        if (schoolId is null or <= 0)
        {
            return [];
        }

        var packages = await _mealRepository.GetMealPackagesForStudentAsync(studentId, schoolId.Value, mealDate, mealTypeId: null, cancellationToken);
        return packages
            .GroupBy(x => new { x.MealTypeId, x.MealTypeName, x.MealCssClass })
            .OrderBy(g => g.Key.MealTypeId)
            .Select(g => new MealComboPackageTypeGroup
            {
                MealTypeId = g.Key.MealTypeId,
                MealTypeName = g.Key.MealTypeName,
                MealCssClass = g.Key.MealCssClass,
                Packages = g.ToList()
            })
            .ToList();
    }

    private async Task<MealComboSummaryViewModel> BuildSummaryAsync(
        int studentId,
        IReadOnlyList<MealComboSelectedPackageRequest> selections,
        CancellationToken cancellationToken)
    {
        if (selections.Count == 0)
        {
            return new MealComboSummaryViewModel();
        }

        var schoolId = await ResolveStudentSchoolIdAsync(studentId, cancellationToken);
        if (schoolId is null or <= 0)
        {
            return new MealComboSummaryViewModel();
        }

        var packageLookup = new Dictionary<int, MealPackageDto>();
        var mealDates = selections
            .Select(s => OrderPaymentSummaryBuilder.ParseMealDate(s.MealDate))
            .Where(d => d != default)
            .Distinct()
            .ToList();

        foreach (var mealDate in mealDates)
        {
            var packages = await _mealRepository.GetMealPackagesForStudentAsync(studentId, schoolId.Value, mealDate, null, cancellationToken);
            foreach (var package in packages)
            {
                packageLookup[package.Id] = package;
            }
        }

        var summaryItems = new List<MealComboSummaryItem>();
        foreach (var selection in selections)
        {
            var mealDate = OrderPaymentSummaryBuilder.ParseMealDate(selection.MealDate);
            if (mealDate == default || selection.PackageId <= 0)
            {
                continue;
            }

            if (!packageLookup.TryGetValue(selection.PackageId, out var package))
            {
                continue;
            }

            var totalPrice = MealPackagePricing.GetTotalPrice(package.Price, package.ProcessingFee);
            summaryItems.Add(new MealComboSummaryItem
            {
                Id = package.Id,
                SelectionId = selection.Id == Guid.Empty ? Guid.NewGuid() : selection.Id,
                PackageName = package.PackageName,
                ItemsName = package.ItemsName,
                MealTypeName = package.MealTypeName,
                Detail = package.Detail,
                Price = totalPrice,
                MealDate = mealDate,
                ImageName = package.ImageName
            });
        }

        return new MealComboSummaryViewModel
        {
            OrderAmount = summaryItems.Sum(x => x.Price),
            SelectedPackages = summaryItems
        };
    }

    private static int ParseDurationDays(string? value)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var days) ? days : 0;
    }
}
