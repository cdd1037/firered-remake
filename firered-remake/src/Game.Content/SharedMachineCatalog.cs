using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record SharedMachineDefinition(MachineItem Machine, MoveId Move, int SourceMoveId, int MovePp);
public sealed record SharedMachineSpecies(SpeciesId Species, int SourceSpeciesId,
    ImmutableArray<ItemId> CompatibleItems);
public sealed record CurrentMachineAdmission(ImmutableArray<ItemId> Order, MachineItem TmCase,
    int MachineSlots, int KeyItemSlots, int MaximumStack);
public sealed record SharedMachineDocument(int SchemaVersion,
    ImmutableArray<SharedMachineDefinition> Machines, ImmutableArray<SharedMachineSpecies> Compatibility,
    CurrentMachineAdmission Current);

/// <summary>One immutable definition and compatibility table for original TM/HM teaching.</summary>
public sealed class SharedMachineCatalog
{
    private static readonly ConcurrentDictionary<string, Lazy<SharedMachineCatalog>> Projects = new(StringComparer.Ordinal);
    private readonly ImmutableDictionary<ItemId, SharedMachineDefinition> _machines;
    private readonly ImmutableDictionary<SpeciesId, SharedMachineSpecies> _species;
    private readonly ConcurrentDictionary<(ItemId, SpeciesId), MachineCompatibility> _cells = new();
    public SharedItemCatalog Items { get; }
    private readonly CurrentMachineAdmission _current;
    private SharedMachineCatalog(SharedMachineDocument document, SharedItemCatalog items)
    {
        Items = items;
        _current = document.Current;
        if (document.SchemaVersion != 1 || document.Machines.Length != 57 || document.Compatibility.Length != 386)
            throw new InvalidDataException("machine_catalog_shape");
        _machines = document.Machines.ToImmutableDictionary(m => m.Machine.Item.Id);
        _species = document.Compatibility.ToImmutableDictionary(s => s.Species);
        if (_species.Values.Any(s => s.SourceSpeciesId < 1 || s.CompatibleItems.Distinct().Count() != s.CompatibleItems.Length))
            throw new InvalidDataException("machine_catalog_species");
        if (_current.Order.IsDefault || _current.Order.Length != 57 || _current.Order.Distinct().Count() != 57 ||
            _current.Order.Any(item => !_machines.ContainsKey(item)) ||
            _current.TmCase.Item.Id.Value != "pret:ITEM_TM_CASE" || string.IsNullOrWhiteSpace(_current.TmCase.Description) ||
            _current.MachineSlots != 58 || _current.KeyItemSlots != 30 || _current.MaximumStack != 999)
            throw new InvalidDataException("machine_current_admission");
    }
    public static SharedMachineCatalog LoadProject(string root) => Projects.GetOrAdd(Path.GetFullPath(root), path =>
        new Lazy<SharedMachineCatalog>(() => LoadProject(path, SharedItemCatalog.LoadProject(path)))).Value;
    public static SharedMachineCatalog LoadProject(string root, SharedItemCatalog items) => new(
        ContentJson.ParseStrict<SharedMachineDocument>(File.ReadAllText(Path.Combine(root,
            "content/generated/data/machines.json")), items.CreateJsonOptions()), items);
    public MachineCatalog LoadCurrent(GamePokemonCatalog pokemon)
    {
        ArgumentNullException.ThrowIfNull(pokemon);
        // Compatibility is a lookup, so use one stable species order rather than regional construction order.
        var species = pokemon.Catalog.Species.Values.OrderBy(value => value.SourceSpeciesId).Select(value => value.Id).ToImmutableArray();
        if (species.IsEmpty || species.Any(id => !_species.TryGetValue(id, out var row) ||
                row.SourceSpeciesId != pokemon.Catalog.Species[id].SourceSpeciesId))
            throw new InvalidDataException("machine_current_species");
        if (_machines.Values.Any(definition => !pokemon.Catalog.Moves.TryGetValue(definition.Move, out var move) ||
                move.SourceMoveId != definition.SourceMoveId || move.Pp != definition.MovePp))
            throw new InvalidDataException("machine_current_move");
        var result = new MachineCatalog(_current.TmCase, _current.Order.Select(item => Project(item, species)).ToImmutableArray(),
            _current.MachineSlots, _current.KeyItemSlots, _current.MaximumStack);
        MachineRules.ValidateCatalog(result);
        return result;
    }
    public SharedMachineDefinition Definition(ItemId item) => _machines.TryGetValue(item, out var value)
        ? value : throw new InvalidDataException("machine_definition_missing: " + item.Value);
    public MachineCompatibility Compatibility(ItemId item, SpeciesId species)
    {
        var symbol = item.Value;
        if (!(symbol.StartsWith("pret:ITEM_TM", StringComparison.Ordinal) && int.TryParse(symbol[12..], out var tm) && tm is >= 1 and <= 50) &&
            !(symbol.StartsWith("pret:ITEM_HM", StringComparison.Ordinal) && int.TryParse(symbol[12..], out var hm) && hm is >= 1 and <= 8))
            throw new InvalidDataException("machine_compatibility_item");
        return _cells.GetOrAdd((item, species), key =>
        {
            if (!_species.TryGetValue(key.Item2, out var row)) throw new InvalidDataException("machine_compatibility_species");
            return new(row.Species, row.SourceSpeciesId, row.CompatibleItems.Contains(key.Item1));
        });
    }
    public MachineDefinition Project(ItemId item, ImmutableArray<SpeciesId> species)
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
    public JsonSerializerOptions CreateJsonOptions(JsonSerializerOptions? original = null)
    {
        var options = original is null ? ContentJson.CreateOptions() : new JsonSerializerOptions(original);
        AddConverters(options);
        return options;
    }
    public void AddConverters(JsonSerializerOptions options) => AddConverters(options, () => this);
    public static void AddConverters(JsonSerializerOptions options, string root) => AddConverters(options, () => LoadProject(root));
    private static void AddConverters(JsonSerializerOptions options, Func<SharedMachineCatalog> catalog)
    {
        var references = ContentJson.CreateOptions();
        references.MakeReadOnly(populateMissingResolver: true);
        catalog().Items.AddConverters(options);
        options.Converters.Add(new MachineItemConverter(catalog));
        options.Converters.Add(new MachineConverter(catalog, references));
        options.Converters.Add(new CompatibilityConverter(catalog, references));
    }
    private sealed record MachineReference(string Item, ImmutableArray<string> Species);
    private sealed record CompatibilityReference(string Item, string Species);
    private sealed class MachineItemConverter(Func<SharedMachineCatalog> catalog) : JsonConverter<MachineItem>
    {
        private readonly System.Runtime.CompilerServices.ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> _ordinaryOptions = new();
        public override MachineItem Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            using var document = JsonDocument.ParseValue(ref reader);
            var data = document.RootElement;
            if (data.TryGetProperty("item", out var item) && item.ValueKind == JsonValueKind.String && data.EnumerateObject().Count() == 1)
            {
                if (data.EnumerateObject().Count() != 1) throw new JsonException("Unexpected machine item reference property.");
                return catalog().Definition(new(item.GetString()!)).Machine;
            }
            // Regional presentation pairs keep their description and resolve the same business item.
            var ordinary = _ordinaryOptions.GetValue(options, source =>
            {
                var copy = new JsonSerializerOptions(source);
                for (var index = copy.Converters.Count - 1; index >= 0; index--)
                    if (copy.Converters[index] is MachineItemConverter) copy.Converters.RemoveAt(index);
                copy.MakeReadOnly(populateMissingResolver: true);
                return copy;
            });
            return data.Deserialize<MachineItem>(ordinary) ?? throw new JsonException("Missing item metadata.");
        }
        public override void Write(Utf8JsonWriter writer, MachineItem value, JsonSerializerOptions options) => throw new NotSupportedException();
    }
    private sealed class MachineConverter(Func<SharedMachineCatalog> catalog, JsonSerializerOptions references) : JsonConverter<MachineDefinition>
    {
        public override MachineDefinition Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var reference = JsonSerializer.Deserialize<MachineReference>(ref reader, references) ?? throw new JsonException("Missing machine reference.");
            return catalog().Project(new(reference.Item), reference.Species.Select(s => new SpeciesId(s)).ToImmutableArray());
        }
        public override void Write(Utf8JsonWriter writer, MachineDefinition value, JsonSerializerOptions options) =>
            JsonSerializer.Serialize(writer, new MachineReference(value.Machine.Item.Id.Value, value.Compatibility.Select(c => c.Species.Value).ToImmutableArray()), options);
    }
    private sealed class CompatibilityConverter(Func<SharedMachineCatalog> catalog, JsonSerializerOptions references) : JsonConverter<MachineCompatibility>
    {
        public override MachineCompatibility Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            var reference = JsonSerializer.Deserialize<CompatibilityReference>(ref reader, references) ?? throw new JsonException("Missing machine compatibility reference.");
            return catalog().Compatibility(new(reference.Item), new(reference.Species));
        }
        public override void Write(Utf8JsonWriter writer, MachineCompatibility value, JsonSerializerOptions options) => throw new NotSupportedException("Compatibility references require their machine identity.");
    }
}
