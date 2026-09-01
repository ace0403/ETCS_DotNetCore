using System.Text.Json;

namespace ETCS.Shared.Infrastructure.Meals.Menu;

public static class MealStudentAllergenParser
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static HashSet<string> ParseNames(string? studentAllergiesJson)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(studentAllergiesJson))
        {
            return result;
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<AllergyJsonRow>>(studentAllergiesJson, JsonOptions);
            if (items is null)
            {
                return result;
            }

            foreach (var name in items
                .Select(x => x.AllergyItemName)
                .Where(x => !string.IsNullOrWhiteSpace(x)))
            {
                result.Add(name!.Trim());
            }
        }
        catch (JsonException)
        {
        }

        return result;
    }

    private sealed class AllergyJsonRow
    {
        public string? AllergyItemName { get; set; }
    }
}
