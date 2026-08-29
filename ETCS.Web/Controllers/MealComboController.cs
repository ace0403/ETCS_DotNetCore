using ETCS.Shared.Enumeration;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Meals;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Schools.Calendar;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Application.Orders;
using ETCS.Shared.Application.Students;
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
    private readonly IStudentOrderTypeAccessService _orderTypeAccess;
    private readonly MealOrderBookingWindow _bookingWindow;
    private readonly ISchoolCalendarService _schoolCalendar;

    public MealComboController(
        IStudentRepository studentRepository,
        IMealEnumAdminRepository mealEnumRepository,
        IMealRepository mealRepository,
        IOrderInitiateService orderInitiateService,
        IStudentOrderTypeAccessService orderTypeAccess,
        MealOrderBookingWindow bookingWindow,
        ISchoolCalendarService schoolCalendar)
    {
        _studentRepository = studentRepository;
        _mealEnumRepository = mealEnumRepository;
        _mealRepository = mealRepository;
        _orderInitiateService = orderInitiateService;
        _orderTypeAccess = orderTypeAccess;
        _bookingWindow = bookingWindow;
        _schoolCalendar = schoolCalendar;
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
            return PartialView("_PackageList", Array.Empty<MealComboSessionSection>());
        }

        if (!await _orderTypeAccess.IsAllowedAsync(request.StudentId, (int)TransactionTypeEnum.MealOrder, cancellationToken))
        {
            return PartialView("_PackageList", Array.Empty<MealComboSessionSection>());
        }

        if (!_bookingWindow.IsBookable(request.MealDate))
        {
            ViewData["SelectedMealDate"] = request.MealDate.ToString("yyyy-MM-dd");
            ViewData["SelectedMealDateDisplay"] = request.MealDate.ToString("dddd, dd MMM yyyy", CultureInfo.InvariantCulture);
            return PartialView("_PackageList", Array.Empty<MealComboSessionSection>());
        }

        var schoolId = await ResolveStudentSchoolIdAsync(request.StudentId, cancellationToken);
        if (schoolId is > 0
            && !await _schoolCalendar.IsOrderableAsync(schoolId.Value, request.MealDate.Date, cancellationToken))
        {
            var day = await _schoolCalendar.GetDayInfoAsync(schoolId.Value, request.MealDate.Date, cancellationToken);
            return MenuClosedDayPartial(request.MealDate.Date, day);
        }

        var sections = await LoadSessionSectionsAsync(request.StudentId, request.MealDate, cancellationToken);
        ViewData["SelectedMealDate"] = request.MealDate.ToString("yyyy-MM-dd");
        ViewData["SelectedMealDateDisplay"] = request.MealDate.ToString("dddd, dd MMM yyyy", CultureInfo.InvariantCulture);
        return PartialView("_PackageList", sections);
    }

    [HttpPost]
    public async Task<IActionResult> GetOrderSummary(
        [FromForm] int studentId,
        [FromForm] List<MealComboSelectedLineRequest>? items,
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

        if (!await _orderTypeAccess.IsAllowedAsync(studentId, (int)TransactionTypeEnum.MealOrder, cancellationToken))
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
        [FromForm] List<MealComboSelectedLineRequest>? mealList,
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
            return Json(new { Success = false, Message = "No items selected." });
        }

        if (await TryGetClosedDateMessageAsync(studentId, mealList.Select(x => x.MealDate), cancellationToken) is { } closedMessage)
        {
            return Json(new { Success = false, Message = closedMessage });
        }

        var summary = await BuildSummaryAsync(studentId, mealList, cancellationToken);
        if (summary.SelectedLines.Count == 0)
        {
            return Json(new { Success = false, Message = "Selected items are no longer available." });
        }

        var mealLines = summary.SelectedLines.Select(item => new OrderMealLineItemRequest
        {
            PackageId = item.IsAddon ? null : item.Id,
            ItemId = item.IsAddon ? item.Id : null,
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
        var children = await LoadEligibleChildrenAsync(guardianId, cancellationToken);
        const int defaultDurationDays = 30;

        return new MealComboPageViewModel
        {
            StudentId = children.FirstOrDefault()?.Id ?? 0,
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
            (int)TransactionTypeEnum.MealOrder,
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

    private async Task<IReadOnlyList<MealComboSessionSection>> LoadSessionSectionsAsync(
        int studentId,
        DateTime mealDate,
        CancellationToken cancellationToken)
    {
        var schoolId = await ResolveStudentSchoolIdAsync(studentId, cancellationToken);
        if (schoolId is null or <= 0)
        {
            return [];
        }

        var packagesTask = _mealRepository.GetMealPackagesForStudentAsync(
            studentId,
            schoolId.Value,
            mealDate,
            cancellationToken: cancellationToken);
        var itemsTask = _mealRepository.GetMealItemsForStudentAsync(
            studentId,
            schoolId.Value,
            mealDate,
            cancellationToken: cancellationToken);

        await Task.WhenAll(packagesTask, itemsTask);

        var activeSessions = await _mealEnumRepository.GetMealSessionsAsync(cancellationToken);
        var activeSessionIds = activeSessions
            .Select(s => s.Id)
            .ToHashSet();

        var packages = (await packagesTask)
            .Where(p => int.TryParse(p.MealSessionId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sessionId)
                && activeSessionIds.Contains(sessionId))
            .ToList();
        var addonItems = (await itemsTask)
            .Where(i => int.TryParse(i.MealSessionId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sessionId)
                && activeSessionIds.Contains(sessionId))
            .ToList();

        var packageGroups = packages
            .GroupBy(x => new { x.MealSessionId, x.MealSessionName, x.MealSessionCssClass })
            .ToDictionary(
                g => g.Key.MealSessionId,
                g => g.OrderBy(p => p.MealTypeSortOrder)
                    .ThenBy(p => p.MealTypeName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.PackageName, StringComparer.OrdinalIgnoreCase)
                    .ToList());

        var addonGroups = addonItems
            .GroupBy(x => new { x.MealSessionId, x.MealSessionName, x.MealSessionCssClass })
            .ToDictionary(
                g => g.Key.MealSessionId,
                g => g.OrderBy(i => i.MealTypeSortOrder)
                    .ThenBy(i => i.MealTypeName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(i => i.ItemName, StringComparer.OrdinalIgnoreCase)
                    .ToList());

        var sessionMetaById = packages
            .Select(x => new { x.MealSessionId, x.MealSessionName, x.MealSessionCssClass })
            .Concat(addonItems.Select(x => new { x.MealSessionId, x.MealSessionName, x.MealSessionCssClass }))
            .GroupBy(x => x.MealSessionId)
            .ToDictionary(g => g.Key, g => g.First());

        var sessionMeta = activeSessions
            .Select(session => session.Id.ToString(CultureInfo.InvariantCulture))
            .Where(sessionMetaById.ContainsKey)
            .Select(sessionId => sessionMetaById[sessionId])
            .ToList();

        var mealTypeResults = await Task.WhenAll(
            activeSessions.Select(async session =>
                (session.Id, Types: await _mealEnumRepository.GetMealTypesBySessionAsync(session.Id, cancellationToken))));
        var mealTypesBySession = mealTypeResults.ToDictionary(x => x.Id, x => x.Types);

        var sections = new List<MealComboSessionSection>();
        foreach (var meta in sessionMeta)
        {
            packageGroups.TryGetValue(meta.MealSessionId, out var sessionPackages);
            addonGroups.TryGetValue(meta.MealSessionId, out var sessionAddons);
            sessionPackages ??= [];
            sessionAddons ??= [];

            var mealTypeSources = sessionPackages
                .Select(p => (p.MealTypeId, p.MealTypeName))
                .Concat(sessionAddons.Select(i => (i.MealTypeId, i.MealTypeName)));
            IReadOnlyList<MealEnumLookupDto> sessionMealTypes = [];
            if (int.TryParse(meta.MealSessionId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedSessionId)
                && mealTypesBySession.TryGetValue(parsedSessionId, out var resolvedMealTypes)
                && resolvedMealTypes is not null)
            {
                sessionMealTypes = resolvedMealTypes;
            }

            var section = new MealComboSessionSection
            {
                MealSessionId = meta.MealSessionId,
                MealSessionName = meta.MealSessionName,
                MealSessionCssClass = meta.MealSessionCssClass,
                Packages = sessionPackages,
                AddonItems = sessionAddons,
                DisplayItems = AlaCarteMealTypeHelper.BuildMergedMenuCards(sessionPackages, sessionAddons),
                MealTypeFilters = AlaCarteMealTypeHelper.BuildSortedMealTypeFilters(mealTypeSources, sessionMealTypes)
            };

            if (IsDisplayableMealSession(section))
            {
                sections.Add(section);
            }
        }

        return sections;
    }

    private static bool IsDisplayableMealSession(MealComboSessionSection section)
    {
        if (section.Packages.Count == 0 && section.AddonItems.Count == 0)
        {
            return false;
        }

        if (!int.TryParse(section.MealSessionId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var sessionId)
            || sessionId <= 0)
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(section.MealSessionName);
    }

    private async Task<MealComboSummaryViewModel> BuildSummaryAsync(
        int studentId,
        IReadOnlyList<MealComboSelectedLineRequest> selections,
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
        var itemLookup = new Dictionary<int, MealItemDto>();
        var mealDates = new List<DateTime>();
        foreach (var raw in selections
            .Select(s => OrderPaymentSummaryBuilder.ParseMealDate(s.MealDate))
            .Where(d => d != default)
            .Distinct())
        {
            if (await IsMealDateBookableAsync(schoolId.Value, raw, cancellationToken))
            {
                mealDates.Add(raw);
            }
        }

        foreach (var mealDate in mealDates)
        {
            var packages = await _mealRepository.GetMealPackagesForStudentAsync(
                studentId,
                schoolId.Value,
                mealDate,
                cancellationToken: cancellationToken);
            foreach (var package in packages)
            {
                packageLookup[package.Id] = package;
            }

            var items = await _mealRepository.GetMealItemsForStudentAsync(
                studentId,
                schoolId.Value,
                mealDate,
                cancellationToken: cancellationToken);
            foreach (var item in items)
            {
                itemLookup[item.Id] = item;
            }
        }

        var summaryItems = new List<MealComboSummaryItem>();
        foreach (var selection in selections)
        {
            var mealDate = OrderPaymentSummaryBuilder.ParseMealDate(selection.MealDate);
            if (mealDate == default || !await IsMealDateBookableAsync(schoolId.Value, mealDate, cancellationToken))
            {
                continue;
            }

            if (selection.PackageId > 0)
            {
                if (!packageLookup.TryGetValue(selection.PackageId, out var package))
                {
                    continue;
                }

                var totalPrice = MealPackagePricing.GetTotalPrice(package.Price, package.ProcessingFee);
                summaryItems.Add(new MealComboSummaryItem
                {
                    Id = package.Id,
                    SelectionId = selection.Id == Guid.Empty ? Guid.NewGuid() : selection.Id,
                    IsAddon = false,
                    PackageName = package.PackageName,
                    ItemsName = package.ItemsName,
                    MealTypeName = package.MealTypeName,
                    MealSessionName = package.MealSessionName,
                    Detail = package.Detail,
                    Price = totalPrice,
                    MealDate = mealDate,
                    ImageName = package.ImageName
                });
                continue;
            }

            if (selection.ItemId > 0 && itemLookup.TryGetValue(selection.ItemId, out var menuItem))
            {
                summaryItems.Add(new MealComboSummaryItem
                {
                    Id = menuItem.Id,
                    SelectionId = selection.Id == Guid.Empty ? Guid.NewGuid() : selection.Id,
                    IsAddon = true,
                    ItemName = menuItem.ItemName,
                    MealTypeName = menuItem.MealTypeName,
                    MealSessionName = menuItem.MealSessionName,
                    Detail = menuItem.Detail,
                    Price = menuItem.Price,
                    MealDate = mealDate,
                    ImageName = menuItem.ImageName
                });
            }
        }

        return new MealComboSummaryViewModel
        {
            OrderAmount = summaryItems.Sum(x => x.Price),
            SelectedLines = summaryItems
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
            .Select(OrderPaymentSummaryBuilder.ParseMealDate)
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
