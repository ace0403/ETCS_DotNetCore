using System.Globalization;
using ETCS.Shared.Infrastructure.Students;
using ETCS.Shared.Media;

namespace ETCS.API.Infrastructure.Students;

internal static class ChildBalanceItemFactory
{
    public static async Task<IReadOnlyList<ChildBalanceItemDto>> CreateAsync(
        IReadOnlyList<StudentListingDto> students,
        IStudentRepository studentRepository,
        MealImageUrlBuilder imageUrlBuilder,
        CancellationToken cancellationToken)
    {
        var tasks = students.Select(student =>
            CreateOneAsync(student, studentRepository, imageUrlBuilder, cancellationToken));
        return await Task.WhenAll(tasks);
    }

    private static async Task<ChildBalanceItemDto> CreateOneAsync(
        StudentListingDto student,
        IStudentRepository studentRepository,
        MealImageUrlBuilder imageUrlBuilder,
        CancellationToken cancellationToken)
    {
        var studentId = Convert.ToInt32(student.UserId, CultureInfo.InvariantCulture);
        var meta = await studentRepository.GetStudentCardBalanceMetaAsync(studentId, cancellationToken);

        var customerId = FirstNonEmpty(
            meta?.CustomerId,
            student.StudCode,
            student.UserId.ToString(CultureInfo.InvariantCulture));

        var schoolName = student.SchoolName?.Trim() ?? string.Empty;
        var logoFileName = meta?.SchoolLogoFileName;
        if (string.IsNullOrWhiteSpace(logoFileName) && !string.IsNullOrWhiteSpace(schoolName))
        {
            logoFileName = await studentRepository.GetSchoolLogoFileNameByNameAsync(
                schoolName,
                cancellationToken);
        }

        return new ChildBalanceItemDto
        {
            StudentId = student.UserId.ToString(CultureInfo.InvariantCulture),
            Name = student.Name ?? string.Empty,
            Balance = student.Balprepaid ?? 0m,
            CardId = student.Cardid?.Trim() ?? string.Empty,
            MinimumTopupAmount = meta?.MinimumTopupAmount ?? 0m,
            CustomerId = customerId,
            Grade = FirstNonEmpty(student.Std),
            Section = FirstNonEmpty(student.GroupName, student.ClassName),
            SchoolName = schoolName,
            SchoolLogoUrl = imageUrlBuilder.BuildSchoolLogoUrl(logoFileName)
        };
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value.Trim();
            }
        }

        return string.Empty;
    }
}
