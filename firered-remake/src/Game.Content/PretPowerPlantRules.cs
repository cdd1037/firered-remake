using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.World;
namespace Game.Content;

public sealed record PowerPlantMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record PowerPlantPokemonDocument(string SourceCommit, string SourceTree, string Profile,
    int OwnedSpeciesCount, int OpponentSpeciesCount, int ItemCount, int MachineCount,
    EarlyLandEncounterTable Land, ImmutableArray<PowerPlantHeldItemSource> HeldItems,
    ImmutableArray<PowerPlantMachineRows> Machines, string MachinesSemanticSha256,
    ImmutableArray<PewterAdditionalMachine> NewMachines, string NewMachinesSemanticSha256,
    ImmutableArray<EarlyShopItemDefinition> Items, string ItemsSemanticSha256,
    ImmutableArray<FieldPickupDefinition> Pickups, string PickupsSemanticSha256,
    ImmutableArray<CanonicalPokemonInput> Inputs, ImmutableArray<CanonicalPokemonArtifact> DependencyInputs);

/// <summary>Bounded source-locked Power Plant and Thunder Stone catalogs; field access remains Application-owned.</summary>
public static partial class PretPowerPlantRules
{
    public const string Profile = "firered-power-plant-pokemon-v1";
    public const string CatalogSha256 = "c62e537a2468299e54ead67ff42cd19293bbafd404688c858fa49c455a1b0232";
    public const string LockSha256 = "80b06dc419e5067fe32c5e6440be68c7d6c41caa32a9481b18353665ad58bad8";
    public const string PickupsSha256 = "587bbddd669d1937e3e72f8447e30cb3e18e082f17c42a3d52fabaa4bcfdcaf2";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    private static byte[] Read(string root, string path, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, path));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("power_plant_source_bytes:" + path);
        return bytes;
    }
    public static PowerPlantPokemonDocument LoadDocument(string root)
    {
        var d = JsonSerializer.Deserialize<PowerPlantPokemonDocument>(Read(root, "content/generated/power-plant-pokemon/catalog.json", CatalogSha256), Options)
            ?? throw new InvalidDataException("power_plant_source_missing");
        if (d.SourceCommit != PokemonFactory.SourceCommit || d.SourceTree != PretCanonicalPokemonCatalog.SourceTree || d.Profile != Profile ||
            d.OwnedSpeciesCount != 152 || d.OpponentSpeciesCount != 186 || d.ItemCount != 123 || d.MachineCount != 41 ||
            d.Machines.IsDefault || d.Machines.Length != 39 || d.MachinesSemanticSha256 != PowerPlantMachineCatalog.InheritedAdditionsSourceSha256 ||
            d.NewMachines.IsDefault || d.NewMachines.Length != 2 || d.NewMachinesSemanticSha256 != PowerPlantMachineCatalog.NewMachinesSourceSha256 ||
            d.Items.IsDefault || d.Items.Length != 3 || d.ItemsSemanticSha256 != PowerPlantShopCatalog.AdditionalItemsSourceSha256 ||
            d.Pickups.IsDefault || d.Pickups.Length != 7 || d.Pickups.Count(p => p.Hidden) != 2 || d.PickupsSemanticSha256 != PickupsSha256 ||
            d.HeldItems.IsDefault || !d.HeldItems.SequenceEqual(PowerPlantEncounterTables.HeldItems) ||
            d.Land is null || d.Land.Slots.IsDefault || !d.Land.Slots.SequenceEqual(PowerPlantEncounterTables.Land.Slots) ||
            d.Land with { Slots = PowerPlantEncounterTables.Land.Slots } != PowerPlantEncounterTables.Land ||
            d.Inputs.IsDefaultOrEmpty || d.DependencyInputs.IsDefault || d.DependencyInputs.Length != 9)
            throw new InvalidDataException("power_plant_source_scope");
        return d;
    }
    public static PowerPlantOwnedPokemonCatalog LoadOwned(string root, EventIslandOwnedPokemonCatalog predecessor)
    {
        _ = LoadDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in PowerPlantOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, PowerPlantOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return PowerPlantOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }
    public static PowerPlantOpponentPokemonCatalog LoadOpponents(string root, EventIslandOpponentPokemonCatalog predecessor, PowerPlantOwnedPokemonCatalog owned)
    { _ = LoadDocument(root); return PowerPlantOpponentPokemonCatalog.Authenticate(predecessor, owned); }
    public static PowerPlantMachineCatalog LoadMachines(string root, EventIslandMachineCatalog predecessor, PowerPlantOwnedPokemonCatalog owned)
    {
        var d = LoadDocument(root); var old = predecessor.Catalog; var additions = d.Machines.ToDictionary(m => m.Item);
        return PowerPlantMachineCatalog.Authenticate(predecessor, owned, old with {
            Profile = PowerPlantMachineCatalog.Profile, Compatibility = old.Compatibility.AddRange(additions[old.Tm39.Item.Id].Compatibility),
            AdditionalMachines = old.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).Concat(d.NewMachines).ToImmutableArray() });
    }
    public static PowerPlantShopCatalog LoadShop(string root, EventIslandShopCatalog predecessor)
    {
        var d = LoadDocument(root);
        var result = PowerPlantShopCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Profile = PowerPlantShopCatalog.Profile, Items = predecessor.Catalog.Items.AddRange(d.Items) });
        foreach (var pickup in d.Pickups)
        {
            var item = result.Catalog.Items.SingleOrDefault(i => i.Id == pickup.Item);
            if (item is null || item.SourceItemId != pickup.SourceItemId || item.SourceItemSymbol != pickup.ItemSymbol ||
                item.Name != pickup.ItemName || item.Pocket != pickup.Pocket) throw new InvalidDataException("power_plant_pickup_item_source");
        }
        return result;
    }
    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        _ = Read(root, "scripts/power-plant-pokemon.lock.json", LockSha256); var d = LoadDocument(root);
        ValidatePresentationEvidence(root, dependencyRoot);
        foreach (var i in d.Inputs)
        {
            var bytes = Read(root, "content/raw/power-plant-pokemon/" + i.Path, i.Sha256);
            if (bytes.Length != i.ByteLength || Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != i.GitBlobSha1)
                throw new InvalidDataException("power_plant_git_blob:" + i.Path);
        }
        foreach (var i in d.DependencyInputs)
            if (Read(dependencyRoot ?? root, i.Path, i.Sha256).Length != i.ByteLength) throw new InvalidDataException("power_plant_dependency:" + i.Path);
    }
}
