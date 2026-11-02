using System.Text.Json;
using System.Text.Json.Nodes;

namespace ETCS.Shared.Infrastructure.Audit;

public static class AuditJson
{
    public const int MaxLength = 8000;

    private static readonly HashSet<string> SensitiveSegments = new(StringComparer.OrdinalIgnoreCase)
    {
        "password",
        "passwd",
        "token",
        "secret",
        "apikey",
        "cvv",
        "cvc",
        "otp",
        "pin",
        "credential",
        "authorization",
        "cardnumber",
        "cardno"
    };

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNamingPolicy = null,
        WriteIndented = false
    };

    public static string? Serialize(object? value)
    {
        if (value is null)
        {
            return null;
        }

        try
        {
            var node = value is JsonNode existing
                ? existing
                : JsonSerializer.SerializeToNode(value, value.GetType(), SerializerOptions);
            if (node is null)
            {
                return null;
            }

            Redact(node);
            var json = node.ToJsonString(SerializerOptions);
            if (json.Length <= MaxLength)
            {
                return json;
            }

            var previewLength = Math.Max(0, MaxLength - 80);
            return JsonSerializer.Serialize(new
            {
                truncated = true,
                preview = json[..previewLength]
            });
        }
        catch (Exception ex) when (ex is NotSupportedException or JsonException)
        {
            return JsonSerializer.Serialize(new { serializationSkipped = true });
        }
    }

    private static void Redact(JsonNode? node)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var key in obj.Select(property => property.Key).ToList())
                {
                    if (IsSensitive(key))
                    {
                        obj[key] = "[redacted]";
                    }
                    else
                    {
                        Redact(obj[key]);
                    }
                }

                break;
            case JsonArray array:
                foreach (var item in array)
                {
                    Redact(item);
                }

                break;
        }
    }

    internal static bool IsSensitive(string propertyName)
    {
        if (string.IsNullOrWhiteSpace(propertyName))
        {
            return false;
        }

        foreach (var segment in SplitPascal(propertyName))
        {
            if (SensitiveSegments.Contains(segment))
            {
                return true;
            }
        }

        var compact = new string(propertyName.Where(char.IsLetterOrDigit).ToArray());
        return compact.Contains("password", StringComparison.OrdinalIgnoreCase)
            || compact.Contains("token", StringComparison.OrdinalIgnoreCase)
            || compact.Contains("secret", StringComparison.OrdinalIgnoreCase)
            || compact.Contains("apikey", StringComparison.OrdinalIgnoreCase)
            || compact.Contains("cardnumber", StringComparison.OrdinalIgnoreCase)
            || compact.Contains("otp", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> SplitPascal(string name)
    {
        var current = new List<char>();
        foreach (var ch in name)
        {
            if (ch is '_' or '-' or ' ')
            {
                if (current.Count > 0)
                {
                    yield return new string(current.ToArray());
                    current.Clear();
                }

                continue;
            }

            if (char.IsUpper(ch) && current.Count > 0)
            {
                yield return new string(current.ToArray());
                current.Clear();
            }

            if (char.IsLetterOrDigit(ch))
            {
                current.Add(ch);
            }
        }

        if (current.Count > 0)
        {
            yield return new string(current.ToArray());
        }
    }
}
