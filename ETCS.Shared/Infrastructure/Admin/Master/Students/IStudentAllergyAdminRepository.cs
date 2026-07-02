namespace ETCS.Shared.Infrastructure.Admin.Master.Students;

public interface IStudentAllergyAdminRepository
{
    Task<IReadOnlyList<int>> GetAllergyIdsAsync(decimal studentId, CancellationToken cancellationToken = default);
    Task SaveAllergiesAsync(decimal studentId, IReadOnlyList<int> allergyItemIds, CancellationToken cancellationToken = default);
    Task DeleteAllergiesAsync(decimal studentId, CancellationToken cancellationToken = default);
}
