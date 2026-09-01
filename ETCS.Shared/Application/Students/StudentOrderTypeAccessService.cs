using ETCS.Shared.Enumeration;
using ETCS.Shared.Infrastructure.Admin.Master.Schools;
using ETCS.Shared.Infrastructure.Admin.Master.Students;
using ETCS.Shared.Infrastructure.Students;

namespace ETCS.Shared.Application.Students;

public sealed class StudentOrderTypeAccessService : IStudentOrderTypeAccessService
{
    private readonly IStudentOrderTypeAdminRepository _orderTypeRepository;
    private readonly ISchoolOrderTypeAdminRepository _schoolOrderTypeRepository;
    private readonly ISchoolGradeOrderTypeAdminRepository _gradeOrderTypeRepository;
    private readonly IStudentRepository _studentRepository;

    public StudentOrderTypeAccessService(
        IStudentOrderTypeAdminRepository orderTypeRepository,
        ISchoolOrderTypeAdminRepository schoolOrderTypeRepository,
        ISchoolGradeOrderTypeAdminRepository gradeOrderTypeRepository,
        IStudentRepository studentRepository)
    {
        _orderTypeRepository = orderTypeRepository;
        _schoolOrderTypeRepository = schoolOrderTypeRepository;
        _gradeOrderTypeRepository = gradeOrderTypeRepository;
        _studentRepository = studentRepository;
    }

    public async Task<bool> IsAllowedAsync(
        decimal studentId,
        int orderTypeId,
        CancellationToken cancellationToken = default)
    {
        if (studentId <= 0 || orderTypeId <= 0)
        {
            return false;
        }

        var schoolId = await _studentRepository.GetStudentSchoolIdAsync((int)studentId, cancellationToken);
        if (schoolId is > 0)
        {
            var schoolAllowedIds = await _schoolOrderTypeRepository.GetOrderTypeIdsAsync(schoolId.Value, cancellationToken);
            if (schoolAllowedIds.Count == 0 || !schoolAllowedIds.Contains(orderTypeId))
            {
                return false;
            }

            var gradeId = await _studentRepository.ResolveStudentGradeIdAsync((int)studentId, cancellationToken);
            if (gradeId is > 0)
            {
                var gradeAccess = await _gradeOrderTypeRepository.GetAccessAsync(
                    schoolId.Value,
                    gradeId.Value,
                    cancellationToken);

                if (gradeAccess.IsConfigured)
                {
                    if (gradeAccess.IsNoService
                        || gradeAccess.OrderTypeIds.Count == 0
                        || !gradeAccess.OrderTypeIds.Contains(orderTypeId))
                    {
                        return false;
                    }
                }
            }
        }

        var studentAllowedIds = await _orderTypeRepository.GetOrderTypeIdsAsync(studentId, cancellationToken);
        if (studentAllowedIds.Count > 0 && !studentAllowedIds.Contains(orderTypeId))
        {
            return false;
        }

        return true;
    }

    public async Task<IReadOnlyList<int>> FilterAllowedAsync(
        IEnumerable<int> studentIds,
        int orderTypeId,
        CancellationToken cancellationToken = default)
    {
        var allowed = new List<int>();
        foreach (var studentId in studentIds.Distinct())
        {
            if (await IsAllowedAsync(studentId, orderTypeId, cancellationToken))
            {
                allowed.Add(studentId);
            }
        }

        return allowed;
    }

    public string GetDeniedMessage(int orderTypeId) => orderTypeId switch
    {
        (int)TransactionTypeEnum.A_La_Carte => "This student is not allowed to place A La Carte orders.",
        (int)TransactionTypeEnum.MealOrder => "This student's grade is not allowed to place meal plan orders at this school.",
        (int)TransactionTypeEnum.Topup => "This student's grade is not allowed to top up at this school.",
        _ => "This student's grade is not allowed to use this service at your school."
    };
}
