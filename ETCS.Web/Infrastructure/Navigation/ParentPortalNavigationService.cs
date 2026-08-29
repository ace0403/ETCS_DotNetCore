using System.Globalization;
using ETCS.Shared.Application.Students;
using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Students;

namespace ETCS.Web.Infrastructure.Navigation;

public sealed class ParentPortalNavigationService : IParentPortalNavigationService
{
    private readonly IStudentRepository _studentRepository;
    private readonly IStudentOrderTypeAccessService _orderTypeAccess;

    public ParentPortalNavigationService(
        IStudentRepository studentRepository,
        IStudentOrderTypeAccessService orderTypeAccess)
    {
        _studentRepository = studentRepository;
        _orderTypeAccess = orderTypeAccess;
    }

    public async Task<ParentPortalNavigationAccess> GetAccessAsync(int guardianId, CancellationToken cancellationToken = default)
    {
        if (guardianId <= 0)
        {
            return ParentPortalNavigationAccess.None;
        }

        var students = await _studentRepository.GetStudentsByGuardianAsync(guardianId, null, cancellationToken);
        var studentIds = students
            .Where(s => s.UserId > 0 && IsActiveStudent(s.Status))
            .Select(s => Convert.ToInt32(s.UserId, CultureInfo.InvariantCulture))
            .Distinct()
            .ToList();

        if (studentIds.Count == 0)
        {
            return ParentPortalNavigationAccess.None;
        }

        var topupAllowed = await _orderTypeAccess.FilterAllowedAsync(
            studentIds,
            (int)TransactionTypeEnum.Topup,
            cancellationToken);
        var mealAllowed = await _orderTypeAccess.FilterAllowedAsync(
            studentIds,
            (int)TransactionTypeEnum.MealOrder,
            cancellationToken);

        return new ParentPortalNavigationAccess
        {
            ShowWallet = topupAllowed.Count > 0,
            ShowPreOrderMeal = mealAllowed.Count > 0
        };
    }

    private static bool IsActiveStudent(string? status) =>
        string.IsNullOrWhiteSpace(status)
        || string.Equals(status.Trim(), "Active", StringComparison.OrdinalIgnoreCase);
}
