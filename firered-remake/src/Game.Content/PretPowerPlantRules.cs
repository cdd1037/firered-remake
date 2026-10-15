using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;
namespace Game.Content;

public sealed record PowerPlantMachineRows(ItemId Item, ImmutableArray<MachineCompatibility> Compatibility);
public sealed record PowerPlantPokemonDocument(int OwnedSpeciesCount, int OpponentSpeciesCount, int ItemCount,
    int MachineCount, EarlyLandEncounterTable Land, ImmutableArray<PowerPlantHeldItemSource> HeldItems,
    ImmutableArray<PowerPlantMachineRows> Machines, ImmutableArray<MachineDefinition> NewMachines,
    ImmutableArray<EarlyShopItemDefinition> Items, ImmutableArray<FieldPickupDefinition> Pickups);

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
    public static PowerPlantPokemonDocument LoadDocument(string root, SharedMachineCatalog? shared = null)
    {
        var d = JsonSerializer.Deserialize<PowerPlantPokemonDocument>(Read(root,"content/generated/data/pokemon/power-plant.json"), (shared ?? SharedMachineCatalog.LoadProject(root)).CreateJsonOptions(Options))
            ?? throw new InvalidDataException("power_plant_source_missing");
        if (d.Machines.IsDefaultOrEmpty || d.NewMachines.IsDefaultOrEmpty || d.Items.IsDefaultOrEmpty ||
            d.Pickups.IsDefaultOrEmpty || d.HeldItems.IsDefault || d.Land is null || d.Land.Slots.IsDefaultOrEmpty)
            throw new InvalidDataException("power_plant_source_scope");
        return d;
    }

}
