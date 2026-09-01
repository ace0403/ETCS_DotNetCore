using System.Globalization;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Helpers;
using ETCS.Shared.Infrastructure.Admin.Inventory.MealEnums;
using ETCS.Shared.Infrastructure.Orders;
using ETCS.Shared.Infrastructure.Schools.Calendar;

namespace ETCS.Shared.Infrastructure.Meals.Menu;

public sealed class MealMenuComposer : IMealMenuComposer
{
    private readonly IMealRepository _mealRepository;
    private readonly IMealEnumAdminRepository _mealEnumRepository;
    private readonly ISchoolCalendarService _schoolCalendar;
    private readonly MealOrderBookingWindow _bookingWindow;

    public MealMenuComposer(
        IMealRepository mealRepository,
        IMealEnumAdminRepository mealEnumRepository,
        ISchoolCalendarService schoolCalendar,
        MealOrderBookingWindow bookingWindow)
    {
        _mealRepository = mealRepository;
        _mealEnumRepository = mealEnumRepository;
        _schoolCalendar = schoolCalendar;
        _bookingWindow = bookingWindow;
    }

    public async Task<MealMenuResponse> ComposeMenuAsync(
        int studentId,
        int schoolId,
        DateTime mealDate,
        CancellationToken cancellationToken = default)
    {
        var date = mealDate.Date;
        var dayInfo = await _schoolCalendar.GetDayInfoAsync(schoolId, date, cancellationToken);
        var isBookable = _bookingWindow.IsBookable(date);
        var isCalendarOrderable = dayInfo.Status == SchoolDayStatus.FullDay;
        var isOrderable = isBookable && isCalendarOrderable;

        MealClosedDayDto? closedDay = null;
        if (!isBookable)
        {
            closedDay = MealClosedDayCopy.CreateCutoff(date, _bookingWindow.FormatClosedDateMessage(date));
        }
        else if (!isCalendarOrderable)
        {
            closedDay = MealClosedDayCopy.Create(date, dayInfo);
        }

        IReadOnlyList<MealMenuSessionDto> sessions = [];
        if (isOrderable)
        {
            sessions = await LoadSessionsAsync(studentId, schoolId, date, cancellationToken);
        }

        return new MealMenuResponse
        {
            StudentId = studentId,
            MealDate = DateOnly.FromDateTime(date),
            IsOrderable = isOrderable,
            DayStatus = MealClosedDayCopy.StatusName(dayInfo.Status),
            ClosedDay = closedDay,
            Sessions = sessions
        };
    }

    public async Task<IReadOnlyList<MealSchoolDayDto>> ComposeSchoolDaysAsync(
        int schoolId,
        DateTime fromDateInclusive,
        DateTime toDateInclusive,
        CancellationToken cancellationToken = default)
    {
        var from = fromDateInclusive.Date;
        var toExclusive = toDateInclusive.Date.AddDays(1);
        if (toExclusive <= from)
        {
            toExclusive = from.AddDays(1);
        }

        var calendarDays = await _schoolCalendar.GetRangeAsync(schoolId, from, toExclusive, cancellationToken);
        var byDate = calendarDays.ToDictionary(d => d.Date.Date, d => d);

        var result = new List<MealSchoolDayDto>();
        for (var cursor = from; cursor < toExclusive; cursor = cursor.AddDays(1))
        {
            if (!byDate.TryGetValue(cursor, out var info))
            {
                info = new SchoolDayInfo(cursor, SchoolDayStatus.FullDay, Title: null, IsException: false);
            }

            var isWeekend = MealClosedDayCopy.IsWeekend(cursor);
            var isBookable = _bookingWindow.IsBookable(cursor);
            var isOrderable = isBookable && info.Status == SchoolDayStatus.FullDay;
            var closedType = isOrderable
                ? null
                : !isBookable
                    ? "cutoff"
                    : MealClosedDayCopy.ResolveClosedType(cursor, info.Status, info.Title);

            result.Add(new MealSchoolDayDto
            {
                Date = DateOnly.FromDateTime(cursor),
                Status = MealClosedDayCopy.StatusName(info.Status),
                IsWeekend = isWeekend,
                IsOrderable = isOrderable,
                Badge = isOrderable ? null : MealClosedDayCopy.ResolveBadge(cursor, info.Status, info.Title),
                ClosedType = closedType,
                Title = MealClosedDayCopy.NormalizeTitle(info.Title)
            });
        }

        return result;
    }

    private async Task<IReadOnlyList<MealMenuSessionDto>> LoadSessionsAsync(
        int studentId,
        int schoolId,
        DateTime mealDate,
        CancellationToken cancellationToken)
    {
        var packagesTask = _mealRepository.GetMealPackagesForStudentAsync(
            studentId,
            schoolId,
            mealDate,
            cancellationToken: cancellationToken);
        var itemsTask = _mealRepository.GetMealItemsForStudentAsync(
            studentId,
            schoolId,
            mealDate,
            cancellationToken: cancellationToken);

        await Task.WhenAll(packagesTask, itemsTask);

        var activeSessions = await _mealEnumRepository.GetMealSessionsAsync(cancellationToken);
        var activeSessionIds = activeSessions
            .Select(s => s.Id)
            .ToHashSet();

        var packages = (await packagesTask)
            .Where(p => TryParsePositiveId(p.MealSessionId, out var sessionId) && activeSessionIds.Contains(sessionId))
            .ToList();
        var addonItems = (await itemsTask)
            .Where(i => TryParsePositiveId(i.MealSessionId, out var sessionId) && activeSessionIds.Contains(sessionId))
            .ToList();

        var packageGroups = packages
            .GroupBy(x => x.MealSessionId)
            .ToDictionary(
                g => g.Key,
                g => g.OrderBy(p => p.MealTypeSortOrder)
                    .ThenBy(p => p.MealTypeName, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(p => p.PackageName, StringComparer.OrdinalIgnoreCase)
                    .ToList());

        var addonGroups = addonItems
            .GroupBy(x => x.MealSessionId)
            .ToDictionary(
                g => g.Key,
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

        var sections = new List<MealMenuSessionDto>();
        foreach (var meta in sessionMeta)
        {
            if (!TryParsePositiveId(meta.MealSessionId, out var sessionId)
                || string.IsNullOrWhiteSpace(meta.MealSessionName))
            {
                continue;
            }

            packageGroups.TryGetValue(meta.MealSessionId, out var sessionPackages);
            addonGroups.TryGetValue(meta.MealSessionId, out var sessionAddons);
            sessionPackages ??= [];
            sessionAddons ??= [];
            if (sessionPackages.Count == 0 && sessionAddons.Count == 0)
            {
                continue;
            }

            IReadOnlyList<MealEnumLookupDto> sessionMealTypes = [];
            if (mealTypesBySession.TryGetValue(sessionId, out var resolvedMealTypes) && resolvedMealTypes is not null)
            {
                sessionMealTypes = resolvedMealTypes;
            }

            var mealTypeSources = sessionPackages
                .Select(p => (p.MealTypeId, p.MealTypeName))
                .Concat(sessionAddons.Select(i => (i.MealTypeId, i.MealTypeName)));

            var items = BuildMergedItems(sessionPackages, sessionAddons);
            sections.Add(new MealMenuSessionDto
            {
                MealSessionId = sessionId,
                MealSessionName = meta.MealSessionName.Trim(),
                Subtitle = MealMenuTypeHelper.ResolveSubtitle(meta.MealSessionName),
                CssClass = meta.MealSessionCssClass?.Trim() ?? string.Empty,
                MealTypeFilters = MealMenuTypeHelper.BuildSortedMealTypeFilters(mealTypeSources, sessionMealTypes),
                Items = items
            });
        }

        return sections;
    }

    private static IReadOnlyList<MealMenuItemDto> BuildMergedItems(
        IReadOnlyList<MealPackageDto> packages,
        IReadOnlyList<MealItemDto> addonItems)
    {
        var ranked = new List<(int SortOrder, MealMenuItemDto Item)>(packages.Count + addonItems.Count);

        foreach (var package in packages)
        {
            ranked.Add((package.MealTypeSortOrder, new MealMenuItemDto
            {
                Id = package.Id,
                IsAddon = false,
                Name = package.PackageName,
                Description = string.IsNullOrWhiteSpace(package.Detail) ? package.ItemsName : package.Detail,
                ItemsName = package.ItemsName,
                Price = MealPackagePricing.GetTotalPrice(package.Price, package.ProcessingFee),
                MealTypeId = MealMenuTypeHelper.ResolveFilterKey(package.MealTypeId, package.MealTypeName),
                MealTypeName = package.MealTypeName,
                MealCategoryName = package.MealCategoryName,
                IngredientNames = OrderIngredientNames(package.Ingredients, package.IngredientNames, package.StudentAllergies),
                Ingredients = package.Ingredients,
                ImageName = package.ImageName,
                ImageUrl = package.ImageUrl,
                ThumbnailUrl = package.ThumbnailUrl,
                NutritionList = package.NutritionList,
                StudentAllergies = package.StudentAllergies,
                IsPopular = package.IsPopular
            }));
        }

        foreach (var addon in addonItems)
        {
            ranked.Add((addon.MealTypeSortOrder, new MealMenuItemDto
            {
                Id = addon.Id,
                IsAddon = true,
                Name = addon.ItemName,
                Description = addon.Detail,
                ItemsName = string.Empty,
                Price = addon.Price,
                MealTypeId = MealMenuTypeHelper.ResolveFilterKey(addon.MealTypeId, addon.MealTypeName),
                MealTypeName = addon.MealTypeName,
                MealCategoryName = addon.MealCategoryName,
                IngredientNames = OrderIngredientNames(addon.Ingredients, addon.IngredientNames, addon.StudentAllergies),
                Ingredients = addon.Ingredients,
                ImageName = addon.ImageName,
                ImageUrl = addon.ImageUrl,
                ThumbnailUrl = addon.ThumbnailUrl,
                NutritionList = addon.NutritionList,
                StudentAllergies = addon.StudentAllergies,
                IsPopular = addon.IsPopular
            }));
        }

        return ranked
            .OrderBy(x => x.SortOrder)
            .ThenBy(x => x.Item.MealTypeName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(x => x.Item.IsAddon)
            .ThenBy(x => x.Item.Name, StringComparer.OrdinalIgnoreCase)
            .Select(x => x.Item)
            .ToList();
    }

    private static IReadOnlyList<string> OrderIngredientNames(
        IReadOnlyList<MealIngredientDto> ingredients,
        IReadOnlyList<string> ingredientNames,
        string studentAllergies)
    {
        var names = ingredients.Count > 0
            ? ingredients.Select(i => i.Name).Where(n => !string.IsNullOrWhiteSpace(n)).ToList()
            : ingredientNames.Where(n => !string.IsNullOrWhiteSpace(n)).ToList();
        if (names.Count == 0)
        {
            return [];
        }

        var studentAllergens = MealStudentAllergenParser.ParseNames(studentAllergies);
        if (studentAllergens.Count == 0)
        {
            return names;
        }

        return names
            .OrderByDescending(name => studentAllergens.Contains(name))
            .ThenBy(name => name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool TryParsePositiveId(string? value, out int id)
    {
        id = 0;
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out id) && id > 0;
    }
}
