using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;
namespace Game.Content;

public sealed record PowerPlantMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record PowerPlantPokemonDocument(string SourceCommit, string Profile, int OwnedSpeciesCount, int OpponentSpeciesCount, int ItemCount,
    int MachineCount, EarlyLandEncounterTable Land, ImmutableArray<PowerPlantHeldItemSource> HeldItems,
    ImmutableArray<PowerPlantMachineRows> Machines, ImmutableArray<PewterAdditionalMachine> NewMachines,
    ImmutableArray<EarlyShopItemDefinition> Items, ImmutableArray<FieldPickupDefinition> Pickups,
    ImmutableArray<CanonicalPokemonInput> Inputs, ImmutableArray<CanonicalPokemonArtifact> DependencyInputs);

/// <summary>Power Plant and Thunder Stone catalogs; field access remains Application-owned.</summary>
public static partial class PretPowerPlantRules
{
    public const string Profile = "firered-power-plant-pokemon-v1";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    private static byte[] Read(string root,string path)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, path));
        return bytes;
    }
    public static PowerPlantPokemonDocument LoadDocument(string root)
    {
        var d = JsonSerializer.Deserialize<PowerPlantPokemonDocument>(Read(root,"content/generated/power-plant-pokemon/catalog.json"), SharedMachineCatalog.Options(root, Options))
            ?? throw new InvalidDataException("power_plant_source_missing");
        if (d.Machines.IsDefaultOrEmpty || d.NewMachines.IsDefaultOrEmpty || d.Items.IsDefaultOrEmpty ||
            d.Pickups.IsDefaultOrEmpty || d.HeldItems.IsDefault || d.Land is null || d.Land.Slots.IsDefaultOrEmpty)
            throw new InvalidDataException("power_plant_source_scope");
        return d;
    }
    public static PowerPlantOwnedPokemonCatalog LoadOwned(EventIslandOwnedPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in PowerPlantOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, PowerPlantOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return PowerPlantOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }
    public static PowerPlantOpponentPokemonCatalog LoadOpponents(EventIslandOpponentPokemonCatalog predecessor, PowerPlantOwnedPokemonCatalog owned)
    => PowerPlantOpponentPokemonCatalog.Authenticate(predecessor, owned);
    public static PowerPlantMachineCatalog LoadMachines(string root, EventIslandMachineCatalog predecessor, PowerPlantOwnedPokemonCatalog owned)
    {
        var d = LoadDocument(root); var old = predecessor.Catalog; var additions = d.Machines.ToDictionary(m => m.Item);
        return PowerPlantMachineCatalog.Authenticate(predecessor, owned, old with {
            Profile = PowerPlantMachineCatalog.Profile,Compatibility = old.Compatibility.AddRange(additions[old.Tm39.Item.Id].Compatibility),AdditionalMachines = old.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).Concat(d.NewMachines).ToImmutableArray() });
    }
    public static PowerPlantShopCatalog LoadShop(string root, EventIslandShopCatalog predecessor)
    {
        var d = LoadDocument(root);
        var result = PowerPlantShopCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Profile = PowerPlantShopCatalog.Profile,Items = predecessor.Catalog.Items.AddRange(d.Items) });
        foreach (var pickup in d.Pickups)
        {
            var item = result.Catalog.Items.SingleOrDefault(i => i.Id == pickup.Item);
            if (item is null || item.SourceItemId != pickup.SourceItemId || item.SourceItemSymbol != pickup.ItemSymbol ||
                item.Name != pickup.ItemName || item.Pocket != pickup.Pocket) throw new InvalidDataException("power_plant_pickup_item_source");
        }
        return result;
    }
}
