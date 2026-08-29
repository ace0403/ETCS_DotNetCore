using ETCS.Shared.Infrastructure.Admin.Models;

namespace ETCS.Shared.Infrastructure.Schools.Calendar;

public interface ISchoolCalendarRepository
{
    Task EnsureWeeklyDefaultsAsync(int schoolId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolWeeklyDayDto>> GetWeeklyAsync(int schoolId, CancellationToken cancellationToken = default);

    Task<AdminOperationResult> SaveWeeklyAsync(
        int schoolId,
        IReadOnlyList<SchoolWeeklyDaySaveRequest> days,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolCalendarExceptionDto>> GetExceptionsAsync(
        int schoolId,
        DateTime? fromDateInclusive,
        DateTime? toDateExclusive,
        CancellationToken cancellationToken = default);

    Task<DataTableResponse<SchoolCalendarExceptionDto>> GetExceptionsPagedAsync(
        int? schoolId,
        DataTableRequest request,
        CancellationToken cancellationToken = default);

    Task<SchoolCalendarExceptionDto?> GetExceptionByIdAsync(int id, CancellationToken cancellationToken = default);

    Task<AdminOperationResult> SaveExceptionAsync(
        SchoolCalendarExceptionSaveRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminOperationResult> DeleteExceptionAsync(int id, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SchoolDayInfo>> ResolveRangeAsync(
        int schoolId,
        DateTime fromDateInclusive,
        DateTime toDateExclusive,
        CancellationToken cancellationToken = default);
}
