using System.Text.Json;

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

    public static string ResolveIcon(string allergenName)
    {
        var name = allergenName ?? string.Empty;
        if (Contains(name, "milk")) return "ti ti-bottle";
        if (Contains(name, "butter")) return "ti ti-droplet";
        if (Contains(name, "cheese") || Contains(name, "dairy")) return "ti ti-cheese";
        if (Contains(name, "nut") || Contains(name, "peanut") || Contains(name, "almond")) return "ti ti-nut";
        if (Contains(name, "egg")) return "ti ti-egg";
        if (Contains(name, "wheat") || Contains(name, "gluten") || Contains(name, "bread")) return "ti ti-bread";
        if (Contains(name, "fish") || Contains(name, "seafood") || Contains(name, "shellfish")) return "ti ti-fish";
        if (Contains(name, "soy") || Contains(name, "soya")) return "ti ti-leaf";
        if (Contains(name, "sesame")) return "ti ti-grain";
        return "ti ti-alert-circle";
    }

    public static string ResolveTone(string allergenName)
    {
        var name = allergenName ?? string.Empty;
        if (Contains(name, "milk")) return "is-milk";
        if (Contains(name, "butter")) return "is-butter";
        if (Contains(name, "cheese") || Contains(name, "dairy")) return "is-dairy";
        if (Contains(name, "nut") || Contains(name, "peanut") || Contains(name, "almond")) return "is-nut";
        if (Contains(name, "egg")) return "is-egg";
        if (Contains(name, "wheat") || Contains(name, "gluten") || Contains(name, "bread")) return "is-gluten";
        if (Contains(name, "fish") || Contains(name, "seafood") || Contains(name, "shellfish")) return "is-fish";
        if (Contains(name, "soy") || Contains(name, "soya")) return "is-soy";
        return "is-default";
    }

    public static string? NormalizeIconFileName(string? icon)
    {
        if (string.IsNullOrWhiteSpace(icon))
        {
            return null;
        }

        var fileName = Path.GetFileName(icon.Trim());
        if (string.IsNullOrWhiteSpace(fileName) || fileName.Contains("..", StringComparison.Ordinal))
        {
            return null;
        }

        return fileName;
    }

    private static bool Contains(string haystack, string needle) =>
        haystack.Contains(needle, StringComparison.OrdinalIgnoreCase);

    private sealed class AllergyJsonRow
    {
        public string? AllergyItemName { get; set; }
    }
}
