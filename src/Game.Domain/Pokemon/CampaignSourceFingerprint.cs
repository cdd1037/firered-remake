using System.Security.Cryptography;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace Game.Domain.Pokemon;

/// <summary>Stable complete source-record digests; array order remains significant.</summary>
internal static class CampaignSourceFingerprint
{
    internal static string Compute<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value, new JsonSerializerOptions
        { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping });
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
            Write(writer, element);
        return Convert.ToHexStringLower(SHA256.HashData(stream.ToArray()));
    }
    private static void Write(Utf8JsonWriter writer, JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();
            foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
            { writer.WritePropertyName(property.Name); Write(writer, property.Value); }
            writer.WriteEndObject();
        }
        else if (value.ValueKind == JsonValueKind.Array)
        {
            writer.WriteStartArray(); foreach (var child in value.EnumerateArray()) Write(writer, child); writer.WriteEndArray();
        }
        else value.WriteTo(writer);
    }
}
