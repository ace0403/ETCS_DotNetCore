namespace ETCS.Shared.Infrastructure.Admin.Master.Students;

public interface IStudentOrderTypeAdminRepository
{
    Task<IReadOnlyList<int>> GetOrderTypeIdsAsync(decimal studentId, CancellationToken cancellationToken = default);

    Task SaveOrderTypesAsync(
        decimal studentId,
        IReadOnlyList<int> orderTypeIds,
        CancellationToken cancellationToken = default);

    Task DeleteOrderTypesAsync(decimal studentId, CancellationToken cancellationToken = default);
}
