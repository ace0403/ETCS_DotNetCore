using ETCS.Shared.Infrastructure.Enums;

namespace ETCS.Shared.Infrastructure.Students;

public interface IGuardianChildEnrollmentService
{
    Task<ChildFormLookupsDto> GetAddChildFormAsync(CancellationToken cancellationToken = default);

    Task<GuardianChildEditFormResponse?> GetEditChildFormAsync(
        int guardianId,
        decimal studentUserId,
        CancellationToken cancellationToken = default);

    Task<GuardianChildOperationResult> CreateAsync(
        int guardianId,
        GuardianChildUpsertRequest request,
        CancellationToken cancellationToken = default);

    Task<GuardianChildOperationResult> UpdateAsync(
        int guardianId,
        GuardianChildUpsertRequest request,
        CancellationToken cancellationToken = default);
}
