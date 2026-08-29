using System.Data.Common;
using Dapper;
using Microsoft.Data.SqlClient;

namespace ETCS.Shared.Infrastructure.Students;

public static class StudentCardNumber
{
    public const string DuplicateMessage = "A student with this card number already exists.";
    public const string DigitsOnlyMessage = "Student card number must contain digits only.";
    public const string SchoolCodeRequiredMessage = "Selected school does not have a numeric school code.";
    public const string RequiredMessage = "Student card number is required.";
    public const string TooLongMessage = "Student card number is too long.";
    public const int MaxLength = 50;

    public static bool IsDigitsOnly(string? value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return false;
        }

        foreach (var ch in value)
        {
            if (!char.IsDigit(ch))
            {
                return false;
            }
        }

        return true;
    }

    public static string? ResolveForCreate(string? enteredCard, string? schoolCode, out string cardNo)
    {
        cardNo = string.Empty;
        var prefix = schoolCode?.Trim() ?? string.Empty;
        if (!IsDigitsOnly(prefix))
        {
            return SchoolCodeRequiredMessage;
        }

        var entered = enteredCard?.Trim() ?? string.Empty;
        if (entered.Length == 0)
        {
            return RequiredMessage;
        }

        if (!IsDigitsOnly(entered))
        {
            return DigitsOnlyMessage;
        }

        cardNo = entered.StartsWith(prefix, StringComparison.Ordinal)
            ? entered
            : prefix + entered;

        if (cardNo.Length > MaxLength)
        {
            return TooLongMessage;
        }

        if (cardNo.Length <= prefix.Length)
        {
            return RequiredMessage;
        }

        return null;
    }

    public static string? ValidateForEdit(string? enteredCard, out string cardNo)
    {
        cardNo = enteredCard?.Trim() ?? string.Empty;
        if (cardNo.Length == 0)
        {
            return RequiredMessage;
        }

        if (!IsDigitsOnly(cardNo))
        {
            return DigitsOnlyMessage;
        }

        if (cardNo.Length > MaxLength)
        {
            return TooLongMessage;
        }

        return null;
    }

    public static async Task<bool> IsTakenAsync(
        DbConnection connection,
        string? cardNo,
        decimal? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        var normalized = cardNo?.Trim();
        if (string.IsNullOrEmpty(normalized))
        {
            return false;
        }

        const string sql = """
            SELECT TOP (1) 1
            FROM StudentLogin
            WHERE (
                    UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), ISNULL(CustomerId, N'')))))
                        = UPPER(LTRIM(RTRIM(@CardNo)))
                 OR UPPER(LTRIM(RTRIM(CONVERT(nvarchar(50), ISNULL(StudCode, N'')))))
                        = UPPER(LTRIM(RTRIM(@CardNo)))
            )
            AND (@ExcludeUserId IS NULL OR UserId <> @ExcludeUserId);
            """;

        var exists = await connection.ExecuteScalarAsync<int?>(
            new CommandDefinition(
                sql,
                new
                {
                    CardNo = normalized,
                    ExcludeUserId = excludeUserId
                },
                cancellationToken: cancellationToken));

        return exists.HasValue;
    }

    public static bool IsDuplicateConflict(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && sql.Number is 2627 or 2601)
            {
                return true;
            }

            var message = current.Message;
            if (message.Contains("PK_IDMember", StringComparison.OrdinalIgnoreCase)
                || message.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public static string MessageOrDuplicate(Exception ex) =>
        IsDuplicateConflict(ex) ? DuplicateMessage : ex.Message;
}
