using System.Globalization;
using ETCS.Shared.Infrastructure.Students;

namespace ETCS.API.Infrastructure.Students;

internal static class ChildBalanceItemFactory
{
    public static async Task<IReadOnlyList<ChildBalanceItemDto>> CreateAsync(
        IReadOnlyList<StudentListingDto> students,
        IStudentRepository studentRepository,
        CancellationToken cancellationToken)
    {
        var tasks = students.Select(student => CreateOneAsync(student, studentRepository, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    private static async Task<ChildBalanceItemDto> CreateOneAsync(
        StudentListingDto student,
        IStudentRepository studentRepository,
        CancellationToken cancellationToken)
    {
        var studentId = Convert.ToInt32(student.UserId, CultureInfo.InvariantCulture);
        var minimumTopup = await studentRepository.GetStudentMinimumTopupAsync(studentId, cancellationToken) ?? 0m;

        return new ChildBalanceItemDto
        {
            StudentId = student.UserId.ToString(CultureInfo.InvariantCulture),
            Name = student.Name ?? string.Empty,
            Balance = student.Balprepaid ?? 0m,
            CardId = student.Cardid?.Trim() ?? string.Empty,
            MinimumTopupAmount = minimumTopup
        };
    }
}
