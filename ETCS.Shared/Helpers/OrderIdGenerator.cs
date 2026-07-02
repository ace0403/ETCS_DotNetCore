using System.Globalization;
using System.Security.Cryptography;

namespace ETCS.Shared.Helpers;

public static class OrderIdGenerator
{
    public static string GenerateForStudent(int studentId) =>
        GenerateForStudent(studentId.ToString(CultureInfo.InvariantCulture));

    public static string GenerateForStudent(string studentId)
    {
        var studentSuffix = studentId.Trim();
        studentSuffix = studentSuffix.Length >= 5 ? studentSuffix[^5..] : studentSuffix.PadLeft(5, '0');
        var randomPart = RandomNumberGenerator.GetInt32(1_000_000, 10_000_000);
        return $"{studentSuffix}{randomPart}";
    }
}
