namespace ETCS.Shared.Infrastructure.Admin.Auth;

public interface IStaffLoginAssignmentRepository
{
    Task<IReadOnlyList<int>> GetSchoolIdsAsync(int loginAccountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetRoleIdsAsync(int loginAccountId, CancellationToken cancellationToken = default);

    Task<int?> GetDefaultRoleIdAsync(int loginAccountId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetLoginAccountIdsBySchoolAsync(int schoolId, CancellationToken cancellationToken = default);

    Task SaveSchoolIdsAsync(
        int loginAccountId,
        IReadOnlyList<int> schoolIds,
        CancellationToken cancellationToken = default);

    Task SaveRoleIdsAsync(
        int loginAccountId,
        IReadOnlyList<int> roleIds,
        int? defaultRoleId,
        CancellationToken cancellationToken = default);

    Task DeleteAssignmentsAsync(int loginAccountId, CancellationToken cancellationToken = default);
}
