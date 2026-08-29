namespace ETCS.Shared.Application.Students;

public interface IStudentOrderTypeAccessService
{
    /// <summary>
    /// Returns true when the student has no order-type rows (allow all) or the requested type is selected.
    /// </summary>
    Task<bool> IsAllowedAsync(
        decimal studentId,
        int orderTypeId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the subset of student IDs that are allowed to use the given order type.
    /// </summary>
    Task<IReadOnlyList<int>> FilterAllowedAsync(
        IEnumerable<int> studentIds,
        int orderTypeId,
        CancellationToken cancellationToken = default);

    string GetDeniedMessage(int orderTypeId);
}
