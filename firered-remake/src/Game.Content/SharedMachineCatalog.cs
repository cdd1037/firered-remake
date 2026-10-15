using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record SharedMachineDefinition(PewterMachineItem Machine, MoveId Move, int SourceMoveId, int MovePp);
public sealed record SharedMachineSpecies(SpeciesId Species, int SourceSpeciesId, OpeningItemSourceRef Source,
    ImmutableArray<ItemId> CompatibleItems);
public sealed record SharedMachineDocument(int SchemaVersion, string SourceCommit,
    ImmutableArray<SharedMachineDefinition> Machines, ImmutableArray<SharedMachineSpecies> Compatibility);

/// <summary>One immutable definition and compatibility table for original TM/HM teaching.</summary>
public sealed class SharedMachineCatalog
{
    private static readonly ConcurrentDictionary<string, Lazy<SharedMachineCatalog>> Projects = new(StringComparer.Ordinal);
    private readonly ImmutableDictionary<ItemId, SharedMachineDefinition> _machines;
    private readonly ImmutableDictionary<SpeciesId, SharedMachineSpecies> _species;
    private readonly ConcurrentDictionary<(ItemId, SpeciesId), PewterMachineCompatibility> _cells = new();
    private SharedMachineCatalog(SharedMachineDocument document)
    {
        if (document.SchemaVersion != 1 || document.Machines.Length != 57 || document.Compatibility.Length != 386)
            throw new InvalidDataException("machine_catalog_shape");
        _machines = document.Machines.ToImmutableDictionary(m => m.Machine.Item.Id);
        _species = document.Compatibility.ToImmutableDictionary(s => s.Species);
        if (_species.Values.Any(s => s.SourceSpeciesId < 1 || s.CompatibleItems.Distinct().Count() != s.CompatibleItems.Length))
            throw new InvalidDataException("machine_catalog_species");
    }
    public static SharedMachineCatalog LoadProject(string root) => Projects.GetOrAdd(Path.GetFullPath(root), path =>
        new Lazy<SharedMachineCatalog>(() => new(ContentJson.ParseStrict<SharedMachineDocument>(
            File.ReadAllText(Path.Combine(path, "content/generated/machine-catalog/catalog.json")), ContentJson.CreateOptions())))).Value;
    public SharedMachineDefinition Definition(ItemId item) => _machines.TryGetValue(item, out var value)
        ? value : throw new InvalidDataException("machine_definition_missing: " + item.Value);
    public PewterMachineCompatibility Compatibility(ItemId item, SpeciesId species)
    {
        var symbol = item.Value;
        if (!(symbol.StartsWith("pret:ITEM_TM", StringComparison.Ordinal) && int.TryParse(symbol[12..], out var tm) && tm is >= 1 and <= 50) &&
            !(symbol.StartsWith("pret:ITEM_HM", StringComparison.Ordinal) && int.TryParse(symbol[12..], out var hm) && hm is >= 1 and <= 8))
            throw new InvalidDataException("machine_compatibility_item");
        return _cells.GetOrAdd((item, species), key =>
        {
            if (!_species.TryGetValue(key.Item2, out var row)) throw new InvalidDataException("machine_compatibility_species");
            return new(row.Species, row.SourceSpeciesId, row.CompatibleItems.Contains(key.Item1), row.Source);
        });
    }
    public PewterAdditionalMachine Project(ItemId item, ImmutableArray<SpeciesId> species)
    {
        var definition = Definition(item);
        if (species.IsDefault || species.Distinct().Count() != species.Length) throw new InvalidDataException("machine_membership");
        return new(definition.Machine, definition.Move, definition.SourceMoveId, definition.MovePp,
            species.Select(s => Compatibility(item, s)).ToImmutableArray());
    }
    public static JsonSerializerOptions Options(string root, JsonSerializerOptions? original = null)
    {
        var options = original is null ? ContentJson.CreateOptions() : new JsonSerializerOptions(original);
        AddConverters(options, root);
        return options;
    }
    public JsonSerializerOptions CreateJsonOptions()
    {
        var options = ContentJson.CreateOptions();
        AddConverters(options);
        return options;
    }
    public void AddConverters(JsonSerializerOptions options) => AddConverters(options, () => this);
    public static void AddConverters(JsonSerializerOptions options, string root) => AddConverters(options, () => LoadProject(root));
    private static void AddConverters(JsonSerializerOptions options, Func<SharedMachineCatalog> catalog)
    {
        options.Converters.Add(new MachineItemConverter(catalog));
        options.Converters.Add(new MachineConverter(catalog));
        options.Converters.Add(new CompatibilityConverter(catalog));
    }
    private sealed record MachineReference(string Item, ImmutableArray<string> Species);
    private sealed record CompatibilityReference(string Item, string Species);
    private sealed class MachineItemConverter(Func<SharedMachineCatalog> catalog) : JsonConverter<PewterMachineItem>
    {
        public override PewterMachineItem Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var data = document.RootElement;
            if (data.TryGetProperty("item", out var item) && item.ValueKind == JsonValueKind.String)
            {
                if (data.EnumerateObject().Count() != 1) throw new JsonException("Unexpected machine item reference property.");
                return catalog().Definition(new(item.GetString()!)).Machine;
            }
            // Ordinary items (including the TM Case) retain their own item metadata.
            var ordinary = new JsonSerializerOptions(options);
            for (var index = ordinary.Converters.Count - 1; index >= 0; index--)
                if (ordinary.Converters[index] is MachineItemConverter) ordinary.Converters.RemoveAt(index);
            return data.Deserialize<PewterMachineItem>(ordinary) ?? throw new JsonException("Missing item metadata.");
        }
        public override void Write(Utf8JsonWriter writer, PewterMachineItem value, JsonSerializerOptions options) => throw new NotSupportedException();
    }
    private sealed class MachineConverter(Func<SharedMachineCatalog> catalog) : JsonConverter<PewterAdditionalMachine>
    {
        public override PewterAdditionalMachine Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var reference = JsonSerializer.Deserialize<MachineReference>(ref reader, ContentJson.CreateOptions()) ?? throw new JsonException("Missing machine reference.");
            return catalog().Project(new(reference.Item), reference.Species.Select(s => new SpeciesId(s)).ToImmutableArray());
        }
        public override void Write(Utf8JsonWriter writer, PewterAdditionalMachine value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, new MachineReference(value.Machine.Item.Id.Value, value.Compatibility.Select(c => c.Species.Value).ToImmutableArray()), options);
    }
    private sealed class CompatibilityConverter(Func<SharedMachineCatalog> catalog) : JsonConverter<PewterMachineCompatibility>
    {
        public override PewterMachineCompatibility Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var reference = JsonSerializer.Deserialize<CompatibilityReference>(ref reader, ContentJson.CreateOptions()) ?? throw new JsonException("Missing machine compatibility reference.");
            return catalog().Compatibility(new(reference.Item), new(reference.Species));
        }
        public override void Write(Utf8JsonWriter writer, PewterMachineCompatibility value, JsonSerializerOptions options) => throw new NotSupportedException("Compatibility references require their machine identity.");
    }
}
