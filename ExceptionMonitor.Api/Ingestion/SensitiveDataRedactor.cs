using System.Text.Json;

namespace ExceptionMonitor.Api.Ingestion;

// Strips common secret-bearing headers/fields from captured request context before it is
// persisted, since exception payloads are forwarded verbatim by client SDKs and may carry
// auth material the sender never intended to have stored or shown in the UI.
public static class SensitiveDataRedactor
{
    private const string RedactedValue = "***REDACTED***";

    private static readonly HashSet<string> DefaultHeaderNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "authorization", "proxy-authorization", "cookie", "set-cookie",
        "x-api-key", "x-auth-token", "x-access-token", "x-csrf-token"
    };

    private static readonly HashSet<string> DefaultFieldNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "password", "passwd", "pwd", "secret", "token",
        "accesstoken", "access_token", "refreshtoken", "refresh_token",
        "apikey", "api_key", "authorization", "cookie",
        "creditcard", "credit_card", "cardnumber", "card_number", "cvv", "cvv2",
        "ssn", "sessionid", "session_id"
    };

    public static JsonElement? RedactHeaders(JsonElement? element, RedactionOptions options) =>
        Redact(element, Merge(DefaultHeaderNames, options.HeaderNames));

    public static JsonElement? RedactFields(JsonElement? element, RedactionOptions options) =>
        Redact(element, Merge(DefaultFieldNames, options.FieldNames));

    private static HashSet<string> Merge(HashSet<string> defaults, string[] extra)
    {
        if (extra.Length == 0) return defaults;
        var merged = new HashSet<string>(defaults, StringComparer.OrdinalIgnoreCase);
        foreach (var name in extra) merged.Add(name);
        return merged;
    }

    private static JsonElement? Redact(JsonElement? element, HashSet<string> denylist)
    {
        if (element is not { } value) return null;

        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            Write(value, writer, denylist);
        }

        return JsonDocument.Parse(stream.ToArray()).RootElement;
    }

    private static void Write(JsonElement element, Utf8JsonWriter writer, HashSet<string> denylist)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in element.EnumerateObject())
                {
                    writer.WritePropertyName(property.Name);
                    if (denylist.Contains(property.Name))
                    {
                        writer.WriteStringValue(RedactedValue);
                    }
                    else
                    {
                        Write(property.Value, writer, denylist);
                    }
                }
                writer.WriteEndObject();
                break;
            case JsonValueKind.Array:
                writer.WriteStartArray();
                foreach (var item in element.EnumerateArray())
                {
                    Write(item, writer, denylist);
                }
                writer.WriteEndArray();
                break;
            default:
                element.WriteTo(writer);
                break;
        }
    }
}
