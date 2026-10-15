using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record CeruleanCaveMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record CeruleanCaveMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount, int MachineCount,
    ImmutableArray<CeruleanCaveMachineRows> Additions, ImmutableArray<ItemId> ItemsAdded);
public sealed record CeruleanCavePokemonContent(CeruleanCaveOwnedPokemonCatalog Owned, CeruleanCaveOpponentPokemonCatalog Opponents,
    CeruleanCaveMachineCatalog Machines);

/// <summary>Source-authenticated current content extension; one shared execution owner, with encounter admission kept separate.</summary>
public static class PretCeruleanCavePokemon
{
    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/cerulean-cave-pokemon", file));
        return JsonSerializer.Deserialize<T>(bytes, SharedMachineCatalog.Options(root, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } })) ?? throw new InvalidDataException("cerulean_cave_source_missing");
    }
    public static JsonElement LoadDocument(string root)
    {
        var document = Read<JsonElement>(root,"pokemon.json");
        return document;
    }
    public static CeruleanCaveOwnedPokemonCatalog LoadOwned(string root, CelioOwnedPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in CeruleanCaveOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, CeruleanCaveOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return CeruleanCaveOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }
    public static CeruleanCaveOpponentPokemonCatalog LoadOpponents(CelioOpponentPokemonCatalog predecessor, CeruleanCaveOwnedPokemonCatalog owned) =>
        CeruleanCaveOpponentPokemonCatalog.Authenticate(predecessor, owned);
    public static CeruleanCaveMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<CeruleanCaveMachinesDocument>(root,"machines.json");
        if (            document.Additions.IsDefault || document.Additions.Length != 39 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty )
            throw new InvalidDataException("cerulean_cave_machines_scope");
        return document;
    }
    public static CeruleanCaveMachineCatalog LoadMachines(string root, CelioMachineCatalog predecessor, CeruleanCaveOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root);
        ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor.Catalog;
        var additions = document.Additions.ToDictionary(m => m.Item);
        return CeruleanCaveMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = CeruleanCaveMachineCatalog.Profile,Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray() });
    }
    public static CeruleanCavePokemonContent Load(string root, CelioOwnedPokemonCatalog previousOwned,
        CelioOpponentPokemonCatalog previousOpponents, CelioMachineCatalog previousMachines)
    {
        var owned = LoadOwned(root, previousOwned);
        return new(owned, LoadOpponents(previousOpponents, owned), LoadMachines(root, previousMachines, owned));
    }
}
