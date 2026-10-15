using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record CelioMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record CelioMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount, int MachineCount,
    ImmutableArray<CelioMachineRows> Additions, ImmutableArray<ItemId> ItemsAdded);
public sealed record CelioPokemonContent(CelioOwnedPokemonCatalog Owned, CelioOpponentPokemonCatalog Opponents,
    CelioMachineCatalog Machines);

/// <summary>Source-authenticated Celio catalogs; lifecycle, battle and campaign admission are separate.</summary>
public static class PretCelioPokemon
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/celio-pokemon", file));
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }) ?? throw new InvalidDataException("celio_source_missing");
    }
    public static JsonElement LoadDocument(string root)
    {
        var document = Read<JsonElement>(root,"pokemon.json");
        return document;
    }
    public static CelioOwnedPokemonCatalog LoadOwned(string root, FiveIslandOwnedPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in CelioOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, CelioOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return CelioOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }
    public static CelioOpponentPokemonCatalog LoadOpponents(FiveIslandOpponentPokemonCatalog predecessor, CelioOwnedPokemonCatalog owned) =>
        CelioOpponentPokemonCatalog.Authenticate(predecessor, owned);
    public static CelioMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<CelioMachinesDocument>(root,"machines.json");
        if (            document.Additions.IsDefault || document.Additions.Length != 39 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty )
            throw new InvalidDataException("celio_machines_scope");
        return document;
    }
    public static CelioMachineCatalog LoadMachines(string root, FiveIslandMachineCatalog predecessor, CelioOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root);
        ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor.Catalog;
        var additions = document.Additions.ToDictionary(m => m.Item);
        return CelioMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = CelioMachineCatalog.Profile,Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray() });
    }
    public static CelioPokemonContent Load(string root, FiveIslandOwnedPokemonCatalog previousOwned,
        FiveIslandOpponentPokemonCatalog previousOpponents, FiveIslandMachineCatalog previousMachines)
    {
        var owned = LoadOwned(root, previousOwned);
        return new(owned, LoadOpponents(previousOpponents, owned), LoadMachines(root, previousMachines, owned));
    }
}
