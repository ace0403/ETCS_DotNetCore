using ETCS.Shared.Enumeration;

namespace ETCS.Shared.Infrastructure.Schools.Calendar;

public sealed class SchoolCalendarService : ISchoolCalendarService
{
    private readonly ISchoolCalendarRepository _repository;

    public SchoolCalendarService(ISchoolCalendarRepository repository)
    {
        _repository = repository;
    }

    public async Task<SchoolDayInfo> GetDayInfoAsync(
        int schoolId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        var day = date.Date;
        var range = await _repository.ResolveRangeAsync(schoolId, day, day.AddDays(1), cancellationToken);
        return range.FirstOrDefault()
            ?? new SchoolDayInfo(day, SchoolDayStatus.FullDay, Title: null, IsException: false);
    }

    public Task<IReadOnlyList<SchoolDayInfo>> GetRangeAsync(
        int schoolId,
        DateTime fromDateInclusive,
        DateTime toDateExclusive,
        CancellationToken cancellationToken = default) =>
        _repository.ResolveRangeAsync(schoolId, fromDateInclusive, toDateExclusive, cancellationToken);

    public async Task<IReadOnlyList<SchoolDayInfo>> GetMergedRangeAsync(
        IReadOnlyList<int> schoolIds,
        DateTime fromDateInclusive,
        DateTime toDateExclusive,
        CancellationToken cancellationToken = default)
    {
        var uniqueSchoolIds = schoolIds
            .Where(id => id > 0)
            .Distinct()
            .ToList();

        if (uniqueSchoolIds.Count == 0)
        {
            return [];
        }

        if (uniqueSchoolIds.Count == 1)
        {
            return await GetRangeAsync(uniqueSchoolIds[0], fromDateInclusive, toDateExclusive, cancellationToken);
        }

        var perSchool = new List<IReadOnlyList<SchoolDayInfo>>();
        foreach (var schoolId in uniqueSchoolIds)
        {
            perSchool.Add(await GetRangeAsync(schoolId, fromDateInclusive, toDateExclusive, cancellationToken));
        }

        var byDate = new Dictionary<DateTime, SchoolDayInfo>();
        foreach (var days in perSchool)
        {
            foreach (var day in days)
            {
                if (!byDate.TryGetValue(day.Date, out var existing))
                {
                    byDate[day.Date] = day;
                    continue;
                }

                byDate[day.Date] = MergeDay(existing, day);
            }
        }

        return byDate.Values.OrderBy(x => x.Date).ToList();
    }

    public async Task<bool> IsOrderableAsync(
        int schoolId,
        DateTime date,
        CancellationToken cancellationToken = default)
    {
        if (schoolId <= 0)
        {
            // Unknown school: do not block ordering on calendar rules.
            return true;
        }

        var info = await GetDayInfoAsync(schoolId, date, cancellationToken);
        return info.Status == SchoolDayStatus.FullDay;
    }

    private static SchoolDayInfo MergeDay(SchoolDayInfo left, SchoolDayInfo right)
    {
        var status = Rank(left.Status) >= Rank(right.Status) ? left.Status : right.Status;
        var preferLeft = Rank(left.Status) >= Rank(right.Status);
        var title = preferLeft ? left.Title : right.Title;
        if (string.IsNullOrWhiteSpace(title))
        {
            title = preferLeft ? right.Title : left.Title;
        }

        return new SchoolDayInfo(
            left.Date,
            status,
            title,
            left.IsException || right.IsException);
    }

    private static int Rank(SchoolDayStatus status) =>
        status switch
        {
            SchoolDayStatus.Holiday => 2,
            SchoolDayStatus.HalfDay => 1,
            _ => 0
        };
}
