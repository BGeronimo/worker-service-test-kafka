using System.Text.Json;

namespace NotificacionWorker.Application.Composition;

internal static class NotificationCompositionDataReader
{
    public static string? ReadString(Dictionary<string, object> data, params string[] candidateKeys)
    {
        foreach (var key in candidateKeys)
        {
            if (!data.TryGetValue(key, out var value) || value is null)
            {
                continue;
            }

            var converted = ConvertToString(value);
            if (!string.IsNullOrWhiteSpace(converted))
            {
                return converted;
            }
        }

        return null;
    }

    private static string? ConvertToString(object value)
    {
        return value switch
        {
            string text => text,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.String => jsonElement.GetString(),
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Number => jsonElement.GetRawText(),
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.True => bool.TrueString,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.False => bool.FalseString,
            JsonElement jsonElement when jsonElement.ValueKind == JsonValueKind.Null => null,
            JsonElement jsonElement => jsonElement.GetRawText(),
            _ => value.ToString()
        };
    }
}
