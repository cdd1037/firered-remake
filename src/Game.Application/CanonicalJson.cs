using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
namespace Game.Application;
/// <summary>Sort object keys ordinally; array order remains semantic. JSON only, no reflection-based rule state.</summary>
public static class CanonicalJson
{
    public static byte[] Encode<T>(T value)
    {
        var element = JsonSerializer.SerializeToElement(value);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream)) Write(writer, element);
        return stream.ToArray();
    }
    public static string Hash<T>(T value) => Convert.ToHexStringLower(SHA256.HashData(Encode(value)));
    public static string Text<T>(T value) => Encoding.UTF8.GetString(Encode(value));
    private static void Write(Utf8JsonWriter writer, JsonElement element)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var p in element.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal)) { writer.WritePropertyName(p.Name); Write(writer, p.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var e in element.EnumerateArray()) Write(writer, e); writer.WriteEndArray(); break;
            default: element.WriteTo(writer); break;
        }
    }
}
