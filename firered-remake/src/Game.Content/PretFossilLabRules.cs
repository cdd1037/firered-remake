using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record FossilLabMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record FossilLabMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount, int MachineCount,
    ImmutableArray<FossilLabMachineRows> Additions, ImmutableArray<ItemId> ItemsAdded);
public sealed record FossilLabShopDocument(string SourceCommit, string Profile, int PreviousItemCount, int ItemCount,
    ImmutableArray<EarlyShopItemDefinition> Additions, ImmutableArray<EarlyShopStock> ShopsAdded);

/// <summary>Authenticated three-fossil catalogs and level-five creation evidence over the exact Dojo resources.</summary>
public static class PretFossilLabRules
{

    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/fossil-lab-rules", file));
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }) ?? throw new InvalidDataException("fossil_lab_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root,"pokemon.json");
        if (                        document.GetProperty("evolutions").GetArrayLength() != 2)
            throw new InvalidDataException("fossil_lab_pokemon_scope");
        return document;
    }

    public static FossilLabOwnedPokemonCatalog LoadOwned(string root, FightingDojoOwnedPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in FossilLabOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, FossilLabOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return FossilLabOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }

    public static FossilLabOpponentPokemonCatalog LoadOpponents(string root, FightingDojoOpponentPokemonCatalog predecessor,
        FossilLabOwnedPokemonCatalog owned)
    { return FossilLabOpponentPokemonCatalog.Authenticate(predecessor, owned); }

    public static FossilLabMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<FossilLabMachinesDocument>(root,"machines.json");
        if (            document.Additions.IsDefault || document.Additions.Length != 41 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty )
            throw new InvalidDataException("fossil_lab_machines_scope");
        return document;
    }

    public static FossilLabMachineCatalog LoadMachines(string root, FightingDojoMachineCatalog predecessor, FossilLabOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor.Catalog; var additions = document.Additions.ToDictionary(m => m.Item);
        return FossilLabMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = FossilLabMachineCatalog.Profile,Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }

    public static JsonElement LoadRules(string root) => Read<JsonElement>(root,"rules.json");

    public static FossilLabShopDocument LoadShopDocument(string root)
    {
        var document = Read<FossilLabShopDocument>(root,"shop.json");
        if (            document.Additions.IsDefault || document.Additions.Length != 1 || document.Additions[0].Id != FossilLabShopCatalog.OldAmberId ||
            document.ShopsAdded.IsDefault || !document.ShopsAdded.IsEmpty)
            throw new InvalidDataException("fossil_lab_shop_scope");
        return document;
    }

    public static FossilLabShopCatalog LoadShop(string root, TrainerTowerShopCatalog predecessor)
    {
        var document = LoadShopDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        return FossilLabShopCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Profile = FossilLabShopCatalog.Profile,Items = predecessor.Catalog.Items.AddRange(document.Additions)
        });
    }
}
