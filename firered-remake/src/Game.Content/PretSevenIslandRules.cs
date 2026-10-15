using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;
namespace Game.Content;

public sealed record SevenIslandTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement ResolvedMoves);
public sealed record SevenIslandMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record SevenIslandMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount, int MachineCount,
    ImmutableArray<SevenIslandMachineRows> Additions, ImmutableArray<ItemId> ItemsAdded);
public sealed record SevenIslandItemsDocument(string SourceCommit, string Profile, ImmutableArray<PewterMachineItem> Items,
    ImmutableArray<EarlyShopItemDefinition> Additions, EarlyShopStock Shop, ImmutableArray<FieldPickupDefinition> Pickups,
    ImmutableArray<string> RenewableFlags);

/// <summary>Only additive typed Seven Island content over authenticated current Cave and League Rematch predecessors.</summary>
public static class PretSevenIslandRules
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/seven-island-rules", file));
        return JsonSerializer.Deserialize<T>(bytes, SharedMachineCatalog.Options(root, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } })) ?? throw new InvalidDataException("seven_island_source_missing");
    }
    public static SevenIslandTrainerDocument LoadDocument(string root)
    {
        var document = Read<SevenIslandTrainerDocument>(root,"trainers.json");
        _ = LoadTrainers(document); return document;
    }
    public static SevenIslandTrainerCatalog LoadTrainers(string root) => LoadTrainers(LoadDocument(root));
    private static SevenIslandTrainerCatalog LoadTrainers(SevenIslandTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty) throw new InvalidDataException("seven_island_trainers_missing");
        var result = new SevenIslandTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        SevenIslandTrainerPartyFactory.ValidateCatalog(result); return result;
    }
    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root,"pokemon.json");
        if (            document.GetProperty("ownedSpeciesCount").GetInt32() != 146 || document.GetProperty("opponentSpeciesCount").GetInt32() != 180)
            throw new InvalidDataException("seven_island_pokemon_scope");
        return document;
    }
    public static SevenIslandOwnedPokemonCatalog LoadOwned(string root, CeruleanCaveOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in SevenIslandOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, SevenIslandOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return SevenIslandOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }
    public static SevenIslandOpponentPokemonCatalog LoadOpponents(string root, LeagueRematchOpponentPokemonCatalog predecessor,
        SevenIslandOwnedPokemonCatalog owned)
    { _ = LoadPokemonDocument(root); return SevenIslandOpponentPokemonCatalog.Authenticate(predecessor, owned); }
    public static SevenIslandMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<SevenIslandMachinesDocument>(root,"machines.json");
        if (            document.PreviousSpeciesCount != 139 || document.SpeciesCount != 146 || document.MachineCount != 39 ||
            document.Additions.IsDefault || document.Additions.Length != 39 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty )
            throw new InvalidDataException("seven_island_machines_scope");
        return document;
    }
    public static SevenIslandMachineCatalog LoadMachines(string root, CeruleanCaveMachineCatalog predecessor, SevenIslandOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor.Catalog; var additions = document.Additions.ToDictionary(m => m.Item);
        return SevenIslandMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = SevenIslandMachineCatalog.Profile,Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray() });
    }
    public static SevenIslandItemsDocument LoadItemsDocument(string root)
    {
        var document = Read<SevenIslandItemsDocument>(root,"items.json");
        if (            document.Additions.IsDefault || document.Additions.Length != 3 || document.Pickups.IsDefault || document.Pickups.Length != 13 ||
            document.Pickups.Count(p => p.Hidden) != 9 || document.RenewableFlags.IsDefault || document.RenewableFlags.Length != 6)
            throw new InvalidDataException("seven_island_items_scope");
        return document;
    }
    public static SevenIslandShopCatalog LoadShop(string root, FiveIslandShopCatalog predecessor)
    {
        var document = LoadItemsDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var shop = SevenIslandShopCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Profile = SevenIslandShopCatalog.Profile,Items = predecessor.Catalog.Items.AddRange(document.Additions),Shops = predecessor.Catalog.Shops.Add(document.Shop) });
        foreach (var pickup in document.Pickups)
        {
            var item = shop.Catalog.Items.SingleOrDefault(i => i.Id == pickup.Item);
            if (item is null || item.SourceItemId != pickup.SourceItemId || item.SourceItemSymbol != pickup.ItemSymbol ||
                item.Name != pickup.ItemName || item.Pocket != pickup.Pocket) throw new InvalidDataException("seven_island_pickup_item_source");
        }
        return shop;
    }
    public static JsonElement LoadRules(string root) => Read<JsonElement>(root,"rules.json");
    public static JsonElement LoadEncounterDeclarations(string root) => Read<JsonElement>(root,"encounters.json");
}
