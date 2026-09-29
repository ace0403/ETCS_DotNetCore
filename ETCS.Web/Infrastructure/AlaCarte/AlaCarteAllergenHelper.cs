using System.Text.Json;
using ETCS.Shared.Infrastructure.Meals.Menu;

namespace ETCS.Web.Infrastructure.AlaCarte;

public static class AlaCarteAllergenHelper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string ParseAllergenNames(string? studentAllergiesJson)
    {
        if (string.IsNullOrWhiteSpace(studentAllergiesJson))
        {
            return string.Empty;
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<AllergyJsonRow>>(studentAllergiesJson, JsonOptions);
            if (items is null || items.Count == 0)
            {
                return string.Empty;
            }

            return string.Join(", ", items
                .Select(x => x.AllergyItemName)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase));
        }
        catch (JsonException)
        {
            return string.Empty;
        }
    }

    public static bool HasAllergens(string? studentAllergiesJson) =>
        ParseAllergenList(studentAllergiesJson).Count > 0;

    public static IReadOnlyList<string> ParseAllergenList(string? studentAllergiesJson)
    {
        var names = ParseAllergenNames(studentAllergiesJson);
        if (string.IsNullOrWhiteSpace(names))
        {
            return [];
        }

        return names
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static string FormatWarning(IReadOnlyList<string> allergenNames)
    {
        if (allergenNames.Count == 0)
        {
            return string.Empty;
        }

        var joined = string.Join(", ", allergenNames.Select(name => name.Trim().ToLowerInvariant()));
        return $"Contains {joined}.";
    }

    public static string ResolveIcon(string allergenName) =>
        DeclaredAllergenDisplayHelper.ResolveTablerIconClass(allergenName);

    public static string ResolveTone(string allergenName) =>
        DeclaredAllergenDisplayHelper.ResolveToneClass(allergenName);

    public static string? NormalizeIconFileName(string? icon) =>
        DeclaredAllergenDisplayHelper.NormalizeIconFileName(icon);

    private sealed class AllergyJsonRow
    {
        public string? AllergyItemName { get; set; }
    }
}
