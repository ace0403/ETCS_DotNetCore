using System.Net;

namespace ETCS.Shared.Infrastructure.Meals.Menu;

public static class AllergenConsentText
{
    public static string Format(
        string? studentName,
        IEnumerable<string>? allergenNames,
        string itemKind = "meal")
    {
        var names = NormalizeAllergenNames(allergenNames);
        if (names.Count == 0)
        {
            return string.Empty;
        }

        return FormatConsent(studentName, itemKind);
    }

    public static string FormatConsent(string? studentName, string itemKind = "meal")
    {
        var safeName = string.IsNullOrWhiteSpace(studentName) ? "your child" : studentName.Trim();
        var kind = string.Equals(itemKind, "combo", StringComparison.OrdinalIgnoreCase)
            ? "combo"
            : "meal";

        return $"I consent to {safeName} receiving this {kind} despite the allergens it contains.";
    }

    /// <summary>Single consent line for order confirmation emails when one or more lines required allergen consent.</summary>
    public static string FormatOrderEmailConsent(string? studentName)
    {
        var safeName = string.IsNullOrWhiteSpace(studentName) ? "your child" : studentName.Trim();
        return $"I consent to {safeName} receiving the ordered item(s) despite the allergens they contain.";
    }

    /// <summary>HTML-safe consent line with emphasized student name (for order summary markup).</summary>
    public static string FormatConsentHtml(string? studentName, string itemKind = "meal")
    {
        var displayName = string.IsNullOrWhiteSpace(studentName) ? "your child" : studentName.Trim();
        var encodedName = WebUtility.HtmlEncode(displayName);
        var kind = string.Equals(itemKind, "combo", StringComparison.OrdinalIgnoreCase)
            ? "combo"
            : "meal";

        return $"I consent to <strong>{encodedName}</strong> receiving this {kind} despite the allergens it contains.";
    }

    public static string FormatStoredConsentDisplay(
        string? studentName,
        string? allergenItemText,
        string itemKind = "meal")
    {
        var names = ParseAllergenNameList(allergenItemText);
        if (names.Count > 0)
        {
            return FormatDisplay(studentName, names, itemKind);
        }

        return FormatConsent(studentName, itemKind);
    }

    public static string FormatDisplay(
        string? studentName,
        IEnumerable<string>? allergenNames,
        string itemKind = "meal")
    {
        var names = NormalizeAllergenNames(allergenNames);
        if (names.Count == 0)
        {
            return string.Empty;
        }

        return $"{string.Join(", ", names)}. {FormatConsent(studentName, itemKind)}";
    }

    public const int MaxAllergenItemTextLength = 500;

    public static string FormatAllergenItemText(IReadOnlyList<string> allergenNames)
    {
        var names = NormalizeAllergenNames(allergenNames);
        if (names.Count == 0)
        {
            return string.Empty;
        }

        var joined = string.Join(", ", names);
        if (joined.Length <= MaxAllergenItemTextLength)
        {
            return joined;
        }

        return joined[..MaxAllergenItemTextLength].TrimEnd();
    }

    public static IReadOnlyList<string> ParseAllergenNameList(string? allergenNamesCsv)
    {
        if (string.IsNullOrWhiteSpace(allergenNamesCsv))
        {
            return [];
        }

        return allergenNamesCsv
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static List<string> NormalizeAllergenNames(IEnumerable<string>? allergenNames)
    {
        if (allergenNames is null)
        {
            return [];
        }

        return allergenNames
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
