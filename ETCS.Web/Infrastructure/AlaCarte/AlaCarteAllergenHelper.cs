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
        !string.IsNullOrWhiteSpace(ParseAllergenNames(studentAllergiesJson));

    private sealed class AllergyJsonRow
    {
        public string? AllergyItemName { get; set; }
    }
}
