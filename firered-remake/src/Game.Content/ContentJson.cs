using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Game.Content;

public static class ContentJson
{
    public const int MaximumJsonBytes = 8 * 1024 * 1024;
    internal static JsonSerializerOptions CreateOptions(JsonNamingPolicy? naming = null) => new()
    {
        PropertyNamingPolicy = naming ?? JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        AllowOutOfOrderMetadataProperties = true,
        MaxDepth = 64,
        Converters = { new ExactEnumConverter<SourceKind>(), new ExactEnumConverter<ContentTileKind>() }
    };

    public static ContentBundle ParseAndValidate(string json)
    {
        var bundle = ParseStrict<ContentBundle>(json, CreateOptions());
        ContentValidator.ThrowIfInvalid(bundle);
        return bundle;
    }

    internal static T ParseStrict<T>(string json, JsonSerializerOptions options)
    {
        if (json is null || Encoding.UTF8.GetByteCount(json) > MaximumJsonBytes)
            throw Error("json_size", "$", "JSON is null or exceeds the 8 MiB engineering limit.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 64 });
            CheckDuplicateProperties(document.RootElement, "$");
            return JsonSerializer.Deserialize<T>(json, options)
                ?? throw Error("json_null", "$", "The document must not be null.");
        }
        catch (JsonException exception)
        {
            throw Error("json_schema", exception.Path ?? "$", exception.Message);
        }
        catch (NotSupportedException exception)
        {
            throw Error("json_unsupported", "$", exception.Message);
        }
    }

    private static void CheckDuplicateProperties(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var property in element.EnumerateObject())
            {
                if (!seen.Add(property.Name)) throw Error("duplicate_property", path + "." + property.Name, "Duplicate JSON property.");
                CheckDuplicateProperties(property.Value, path + "." + property.Name);
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            var index = 0;
            foreach (var value in element.EnumerateArray()) CheckDuplicateProperties(value, $"{path}[{index++}]");
        }
    }

    // Catalog ordering is not semantic; grid cells and instruction ordering are semantic.
    public static string ToCanonicalJson(ContentBundle bundle)
    {
        ContentValidator.ThrowIfInvalid(bundle);
        static ImmutableArray<T> Sort<T>(ImmutableArray<T> values) where T : IContentDefinition
            => values.OrderBy(v => v.StableId, StringComparer.Ordinal).ToImmutableArray();
        var normalized = bundle with
        {
            Tiles = Sort(bundle.Tiles), Texts = Sort(bundle.Texts), Flags = Sort(bundle.Flags),
            Items = Sort(bundle.Items), Species = Sort(bundle.Species), Scripts = Sort(bundle.Scripts),
            Maps = Sort(bundle.Maps.Select(m => m with { Warps = Sort(m.Warps), Npcs = Sort(m.Npcs) }).ToImmutableArray()),
            Rewards = Sort(bundle.Rewards.Select(r => r with
                { Items = r.Items.OrderBy(i => i.ItemId, StringComparer.Ordinal).ToImmutableArray() }).ToImmutableArray())
        };
        return Canonicalize(JsonSerializer.SerializeToElement(normalized, CreateOptions()));
    }

    public static string Sha256(ContentBundle bundle)
        => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(ToCanonicalJson(bundle))));

    internal static string Canonicalize(JsonElement value)
    {
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = false }))
            WriteCanonical(writer, value);
        return Encoding.UTF8.GetString(stream.ToArray()) + "\n";
    }

    private static void WriteCanonical(Utf8JsonWriter writer, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();
                foreach (var property in value.EnumerateObject().OrderBy(p => p.Name, StringComparer.Ordinal))
                { writer.WritePropertyName(property.Name); WriteCanonical(writer, property.Value); }
                writer.WriteEndObject(); break;
            case JsonValueKind.Array:
                writer.WriteStartArray(); foreach (var item in value.EnumerateArray()) WriteCanonical(writer, item);
                writer.WriteEndArray(); break;
            default: value.WriteTo(writer); break;
        }
    }

    internal static ContentValidationException Error(string code, string path, string message)
        => new([new ContentDiagnostic(code, path, message)]);
}

// JsonStringEnumConverter also accepts case variants/comma combinations. The content grammar does not.
internal sealed class ExactEnumConverter<T> : JsonConverter<T> where T : struct, Enum
{
    private static readonly IReadOnlyDictionary<string, T> Names = Enum.GetValues<T>()
        .ToDictionary(value => JsonNamingPolicy.CamelCase.ConvertName(value.ToString()), StringComparer.Ordinal);
    public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.String && reader.GetString() is { } name && Names.TryGetValue(name, out var value))
            return value;
        throw new JsonException($"Expected an exact named {typeof(T).Name} value.");
    }
    public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
    {
        if (!Enum.IsDefined(value)) throw new JsonException($"Unknown {typeof(T).Name} value.");
        writer.WriteStringValue(JsonNamingPolicy.CamelCase.ConvertName(value.ToString()));
    }
}
