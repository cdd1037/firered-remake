using System.Collections.Immutable;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
namespace Game.Content;

public sealed record EventIslandMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record EventIslandSpeciesDocument(string SourceCommit, string Profile, int OwnedSpeciesCount, int OpponentSpeciesCount,
    ImmutableArray<EventIslandMachineRows> Machines, ImmutableArray<EarlyShopItemDefinition> Items,
    ImmutableArray<CanonicalPokemonInput> Inputs, ImmutableArray<CanonicalPokemonArtifact> DependencyInputs);

/// <summary>Source-locked additive event content; map access and distribution remain Application concerns.</summary>
public static class PretEventIslandRules
{
    public const string Profile = "firered-event-island-species-v1";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    private static byte[] Read(string root,string path)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, path));
        return bytes;
    }
    public static EventIslandSpeciesDocument LoadDocument(string root)
    {
        var d = JsonSerializer.Deserialize<EventIslandSpeciesDocument>(Read(root,"content/generated/event-island-species/catalog.json"), Options)
            ?? throw new InvalidDataException("event_island_source_missing");
        if (d.Machines.IsDefault || d.Items.IsDefault) throw new InvalidDataException("event_island_catalog_arrays_missing");
        return d;
    }
    public static EventIslandOwnedPokemonCatalog LoadOwned(string root, SevenIslandOwnedPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in EventIslandOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, EventIslandOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return EventIslandOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }
    public static EventIslandOpponentPokemonCatalog LoadOpponents(string root, SevenIslandOpponentPokemonCatalog predecessor, EventIslandOwnedPokemonCatalog owned)
    { return EventIslandOpponentPokemonCatalog.Authenticate(predecessor, owned); }
    public static EventIslandMachineCatalog LoadMachines(string root, SevenIslandMachineCatalog predecessor, EventIslandOwnedPokemonCatalog owned)
    {
        var d = LoadDocument(root); var old = predecessor.Catalog; var additions = d.Machines.ToDictionary(m => m.Item);
        return EventIslandMachineCatalog.Authenticate(predecessor, owned, old with {
            Profile = EventIslandMachineCatalog.Profile,Compatibility = old.Compatibility.AddRange(additions[old.Tm39.Item.Id].Compatibility),AdditionalMachines = old.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray() });
    }
    public static EventIslandShopCatalog LoadShop(string root, SevenIslandShopCatalog predecessor) =>
        EventIslandShopCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Profile = EventIslandShopCatalog.Profile,Items = predecessor.Catalog.Items.AddRange(LoadDocument(root).Items) });
}
