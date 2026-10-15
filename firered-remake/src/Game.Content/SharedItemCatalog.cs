using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;

namespace Game.Content;

public sealed record SharedItemDocument(int SchemaVersion, string SourceCommit,
    ImmutableArray<QuestItemDefinition> Items);

/// <summary>Immutable item business definitions. Resolving an item never admits it to an inventory or field service.</summary>
public sealed class SharedItemCatalog
{
    private readonly ImmutableDictionary<ItemId, QuestItemDefinition> _items;
    private SharedItemCatalog(SharedItemDocument document)
    {
        if (document.SchemaVersion != 1 || document.SourceCommit != QuestItemRules.SourceCommit ||
            document.Items.IsDefault || document.Items.Length != 307)
            throw new InvalidDataException("item_catalog_shape");
        _items = document.Items.ToImmutableDictionary(item => item.Id);
        if (_items.Values.Select(item => item.SourceItemId).Distinct().Count() != _items.Count ||
            _items.Values.Any(item => item.Id.Value != "pret:" + item.SourceItemSymbol ||
                item.SourceItemId < 0 || item.Price < 0 || item.Registrability is < 0 or > 1 ||
                item.CanToss != (item.Importance == 0) || item.CanGive != (item.Importance == 0) ||
                item.CanSell != (item.Price != 0)))
            throw new InvalidDataException("item_catalog_definition");
    }
    public static SharedItemCatalog LoadProject(string root) => new(ContentJson.ParseStrict<SharedItemDocument>(
        File.ReadAllText(Path.Combine(root, "content/generated/item-catalog/catalog.json")), ContentJson.CreateOptions()));
    public QuestItemDefinition Definition(ItemId id) => _items.TryGetValue(id, out var item)
        ? item : throw new InvalidDataException("item_definition_missing: " + id.Value);
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
