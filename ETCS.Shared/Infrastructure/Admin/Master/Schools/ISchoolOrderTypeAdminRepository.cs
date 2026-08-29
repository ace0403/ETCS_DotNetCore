namespace ETCS.Shared.Infrastructure.Admin.Master.Schools;

public interface ISchoolOrderTypeAdminRepository
{
    Task<IReadOnlyList<int>> GetOrderTypeIdsAsync(int schoolId, CancellationToken cancellationToken = default);

    Task SaveOrderTypesAsync(
        int schoolId,
        IReadOnlyList<int> orderTypeIds,
        CancellationToken cancellationToken = default);

    Task DeleteOrderTypesAsync(int schoolId, CancellationToken cancellationToken = default);
}
