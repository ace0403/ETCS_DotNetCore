namespace ETCS.Shared.Infrastructure.Admin.Master.Schools;

public interface ISchoolGradeOrderTypeAdminRepository
{
    Task<IReadOnlyList<SchoolGradeOrderTypeConfigDto>> GetConfigsAsync(
        int schoolId,
        CancellationToken cancellationToken = default);

    Task<SchoolGradeOrderTypeAccessDto> GetAccessAsync(
        int schoolId,
        int gradeId,
        CancellationToken cancellationToken = default);

    Task SaveConfigsAsync(
        int schoolId,
        IReadOnlyList<SchoolGradeOrderTypeConfigDto> configs,
        CancellationToken cancellationToken = default);

    Task DeleteConfigsAsync(int schoolId, CancellationToken cancellationToken = default);
}
