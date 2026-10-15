using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.Items;
using Game.Domain.Ids;

namespace Game.Content;

public sealed record TrainerTowerRulesDocument(TrainerTowerLocalData Local);
public sealed record TrainerTowerPrizeDefinition(TrainerTowerChallengeKind Course, ItemId Item, int SourceItemId, int Quantity);
public sealed record TrainerTowerItemsDocument(ImmutableArray<EarlyShopItemDefinition> Additions, EarlyShopStock Shop,
    ImmutableArray<TrainerTowerPrizeDefinition> Prizes);

/// <summary>Loads the local Trainer Tower courses, opponents and prizes.</summary>
public static class PretTrainerTowerRules
{
    private static readonly JsonSerializerOptions Options = new() {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        RespectNullableAnnotations = true,
        RespectRequiredConstructorParameters = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private static byte[] Read(string root,string path)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, path));
        return bytes;
    }
    private static T Document<T>(string root,string name, JsonSerializerOptions? options = null) =>
        JsonSerializer.Deserialize<T>(Read(root,"content/generated/data/regions/trainer-tower/" + name), options ?? Options)
            ?? throw new InvalidDataException("trainer_tower_source_missing:" + name);
    public static TrainerTowerRulesDocument LoadDocument(string root)
    {
        var document = Document<TrainerTowerRulesDocument>(root,"catalog.json");
        _ = TrainerTowerCatalog.Authenticate(document.Local);
        return document;
    }
    public static TrainerTowerCatalog Load(string root) => TrainerTowerCatalog.Authenticate(LoadDocument(root).Local);
    public static TrainerTowerItemsDocument LoadItemsDocument(string root)
    {
        var options = new JsonSerializerOptions(Options);
        SharedItemCatalog.LoadProject(root).AddConverters(options);
        var d = Document<TrainerTowerItemsDocument>(root,"items.json", options);
        var courses = Load(root).Courses;
        if (d.Additions.IsDefaultOrEmpty || d.Prizes.IsDefault || d.Prizes.Length != courses.Length ||
            d.Prizes.Where((p, i) => (int)p.Course != i || p.Item.Value != "pret:" + courses[i].PrizeItem ||
                p.SourceItemId != courses[i].SourcePrizeItemId || p.Quantity != 1).Any())
            throw new InvalidDataException("trainer_tower_items_scope");
        return d;
    }

}
