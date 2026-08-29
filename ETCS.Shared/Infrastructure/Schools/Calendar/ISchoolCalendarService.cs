using ETCS.Shared.Enumeration;

namespace ETCS.Shared.Infrastructure.Schools.Calendar;

public interface ISchoolCalendarService
{
    Task<SchoolDayInfo> GetDayInfoAsync(int schoolId, DateTime date, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolDayInfo>> GetRangeAsync(
        int schoolId,
        DateTime fromDateInclusive,
        DateTime toDateExclusive,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolDayInfo>> GetMergedRangeAsync(
        IReadOnlyList<int> schoolIds,
        DateTime fromDateInclusive,
        DateTime toDateExclusive,
        CancellationToken cancellationToken = default);

    Task<bool> IsOrderableAsync(int schoolId, DateTime date, CancellationToken cancellationToken = default);
}
