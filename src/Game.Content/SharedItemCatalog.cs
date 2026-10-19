using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record SharedItemDocument(int SchemaVersion,
    ImmutableArray<QuestItemDefinition> Items, ImmutableDictionary<string, string> Descriptions);

/// <summary>Immutable item definitions and display descriptions. Resolving an item never admits it to an inventory or field service.</summary>
public sealed class SharedItemCatalog
{
    private readonly ImmutableDictionary<ItemId, QuestItemDefinition> _items;
    private readonly ImmutableDictionary<ItemId, string> _descriptions;
    internal SourceTextPack TextPack { get; }
    private SharedItemCatalog(SharedItemDocument document, SourceTextPack pack)
    {
        if (document.SchemaVersion != 2 ||
            document.Items.IsDefault || document.Items.Length != 307)
            throw new InvalidDataException("item_catalog_shape");
        _items = document.Items.ToImmutableDictionary(item => item.Id);
        if (_items.Values.Select(item => item.SourceItemId).Distinct().Count() != _items.Count ||
            _items.Values.Any(item => item.Id.Value != "pret:" + item.SourceItemSymbol ||
                string.IsNullOrWhiteSpace(item.Name) || item.SourceItemId < 0 || item.Price < 0 ||
                item.Registrability is < 0 or > 1 ||
                item.CanToss != (item.Importance == 0) || item.CanGive != (item.Importance == 0) ||
                item.CanSell != (item.Price != 0)))
            throw new InvalidDataException("item_catalog_definition");
        if (document.Descriptions.Count != _items.Count ||
            _items.Keys.Any(id => !document.Descriptions.ContainsKey(id.Value)) ||
            document.Descriptions.Values.Any(string.IsNullOrWhiteSpace))
            throw new InvalidDataException("item_catalog_descriptions");
        TextPack = pack;
        _descriptions = pack.Apply("item-descriptions", document.Descriptions)
            .ToImmutableDictionary(pair => new ItemId(pair.Key), pair => pair.Value);
        var names = pack.Apply("item-names", _items.ToDictionary(pair => pair.Key.Value, pair => pair.Value.Name));
        _items = _items.ToImmutableDictionary(pair => pair.Key,
            pair => pair.Value with { Name = names[pair.Key.Value] });
    }
    public static SharedItemCatalog LoadProject(string root, SourceTextPack? pack = null) => new(ContentJson.ParseStrict<SharedItemDocument>(
        File.ReadAllText(Path.Combine(root, "content/generated/data/items.json")), ContentJson.CreateOptions()), pack ?? SourceTextPack.Current);
    public QuestItemDefinition Definition(ItemId id) => _items.TryGetValue(id, out var item)
        ? item : throw new InvalidDataException("item_definition_missing: " + id.Value);
    public string Description(ItemId id) => _descriptions.TryGetValue(id, out var description)
        ? description : throw new InvalidDataException("item_description_missing: " + id.Value);
    public ImmutableDictionary<ItemId, QuestItemDefinition> Select(IEnumerable<ItemId> ids) =>
        ids.Distinct().ToImmutableDictionary(id => id, Definition);
    public JsonSerializerOptions CreateJsonOptions()
    {
        var options = ContentJson.CreateOptions();
        AddConverters(options);
        return options;
    }
    public void AddConverters(JsonSerializerOptions options) => options.Converters.Add(new ItemReferenceConverter(this));
    private sealed class ItemReferenceConverter(SharedItemCatalog catalog) : JsonConverter<QuestItemDefinition>
    {
        public override QuestItemDefinition Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.String) throw new JsonException("An item reference must be a stable item ID.");
            return catalog.Definition(new(reader.GetString()!));
        }
        public override void Write(Utf8JsonWriter writer, QuestItemDefinition item, JsonSerializerOptions options) =>
            writer.WriteStringValue(item.Id.Value);
    }
}
