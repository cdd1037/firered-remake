using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;
namespace Game.Content;

public sealed record SevenIslandTrainerDocument(ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles) : RegionalTrainerDocument(Trainers, Battles);
public sealed record SevenIslandItemsDocument(ImmutableArray<MachineItem> Items,
    ImmutableArray<EarlyShopItemDefinition> Additions, EarlyShopStock Shop, ImmutableArray<FieldPickupDefinition> Pickups,
    ImmutableArray<string> RenewableFlags);

/// <summary>Only additive typed Seven Island content over authenticated current Cave and League Rematch predecessors.</summary>
public static class PretSevenIslandRules
{
    private static T Read<T>(string root,string file, SharedMachineCatalog? shared = null)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/data/regions/seven-island", file));
        return JsonSerializer.Deserialize<T>(bytes, SharedTrainerCatalog.Options(root, Options(root, shared))) ?? throw new InvalidDataException("seven_island_source_missing");
    }
    private static readonly RegionalTrainerLoader<SevenIslandTrainerDocument, SevenIslandTrainerCatalog> Loader = new(
        "content/generated/data/regions/seven-island/trainers.json", "seven_island_source_missing",
        document => document.ByScript((trainers, battles) => new SevenIslandTrainerCatalog(trainers, battles),
            "seven_island_trainers_missing"), SevenIslandTrainerPartyFactory.ValidateCatalog);

    private static JsonSerializerOptions Options(string root, SharedMachineCatalog? shared) =>
        (shared ?? SharedMachineCatalog.LoadProject(root)).CreateJsonOptions(new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        });

    public static SevenIslandTrainerDocument LoadDocument(string root, SharedMachineCatalog? shared = null) =>
        Loader.LoadDocument(root, Options(root, shared));
    public static SevenIslandTrainerCatalog LoadTrainers(string root, SharedMachineCatalog? shared = null) =>
        Loader.LoadCatalog(root, Options(root, shared));
    public static SevenIslandItemsDocument LoadItemsDocument(string root, SharedMachineCatalog? shared = null)
    {
        var document = Read<SevenIslandItemsDocument>(root,"items.json", shared);
        if (            document.Additions.IsDefault || document.Additions.Length != 3 || document.Pickups.IsDefault || document.Pickups.Length != 13 ||
            document.Pickups.Count(p => p.Hidden) != 9 || document.RenewableFlags.IsDefault || document.RenewableFlags.Length != 6)
            throw new InvalidDataException("seven_island_items_scope");
        return document;
    }

    public static JsonElement LoadEncounterDeclarations(string root, SharedMachineCatalog? shared = null) => Read<JsonElement>(root,"encounters.json", shared);
}
