using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
namespace Game.Content;

public sealed record EventIslandMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record EventIslandSpeciesDocument(string SourceCommit, string SourceTree, string Profile,
    int OwnedSpeciesCount, int OpponentSpeciesCount, ImmutableArray<EventIslandMachineRows> Machines,
    string MachinesSemanticSha256, ImmutableArray<EarlyShopItemDefinition> Items, string ItemsSemanticSha256,
    ImmutableArray<CanonicalPokemonInput> Inputs, ImmutableArray<CanonicalPokemonArtifact> DependencyInputs);

/// <summary>Source-locked additive event content; map access and distribution remain Application concerns.</summary>
public static class PretEventIslandRules
{
    public const string Profile = "firered-event-island-species-v1";
    public const string CatalogSha256 = "ec077629917befe12235c92727590edefe2d752104793e54b923d21f46ddfa2a";
    public const string LockSha256 = "244624fafbe1b0143d8b9d36213197f9cac7695b716f315b16183f4ceb9b2522";
    private static readonly JsonSerializerOptions Options = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } };
    private static byte[] Read(string root, string path, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, path));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("event_island_source_bytes:" + path);
        return bytes;
    }
    public static EventIslandSpeciesDocument LoadDocument(string root)
    {
        var d = JsonSerializer.Deserialize<EventIslandSpeciesDocument>(Read(root, "content/generated/event-island-species/catalog.json", CatalogSha256), Options)
            ?? throw new InvalidDataException("event_island_source_missing");
        if (d.SourceCommit != PokemonFactory.SourceCommit || d.SourceTree != PretCanonicalPokemonCatalog.SourceTree || d.Profile != Profile ||
            d.OwnedSpeciesCount != 149 || d.OpponentSpeciesCount != 183 || d.Machines.IsDefault || d.Machines.Length != 39 ||
            d.MachinesSemanticSha256 != EventIslandMachineCatalog.InheritedAdditionsSourceSha256 || d.Items.IsDefault || d.Items.Length != 6 ||
            d.ItemsSemanticSha256 != EventIslandShopCatalog.AdditionalItemsSourceSha256 || d.Inputs.IsDefault || d.Inputs.Length != 17 ||
            d.DependencyInputs.IsDefault || d.DependencyInputs.Length != 2) throw new InvalidDataException("event_island_source_scope");
        return d;
    }
    public static EventIslandOwnedPokemonCatalog LoadOwned(string root, SevenIslandOwnedPokemonCatalog predecessor)
    {
        _ = LoadDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in EventIslandOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, EventIslandOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return EventIslandOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }
    public static EventIslandOpponentPokemonCatalog LoadOpponents(string root, SevenIslandOpponentPokemonCatalog predecessor, EventIslandOwnedPokemonCatalog owned)
    { _ = LoadDocument(root); return EventIslandOpponentPokemonCatalog.Authenticate(predecessor, owned); }
    public static EventIslandMachineCatalog LoadMachines(string root, SevenIslandMachineCatalog predecessor, EventIslandOwnedPokemonCatalog owned)
    {
        var d = LoadDocument(root); var old = predecessor.Catalog; var additions = d.Machines.ToDictionary(m => m.Item);
        return EventIslandMachineCatalog.Authenticate(predecessor, owned, old with {
            Profile = EventIslandMachineCatalog.Profile, Compatibility = old.Compatibility.AddRange(additions[old.Tm39.Item.Id].Compatibility),
            AdditionalMachines = old.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray() });
    }
    public static EventIslandShopCatalog LoadShop(string root, SevenIslandShopCatalog predecessor) =>
        EventIslandShopCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Profile = EventIslandShopCatalog.Profile, Items = predecessor.Catalog.Items.AddRange(LoadDocument(root).Items) });
    public static void ValidateSourceEvidence(string root, string dependencyRoot)
    {
        _ = Read(root, "scripts/event-island-species.lock.json", LockSha256);
        var d = LoadDocument(root);
        foreach (var i in d.Inputs)
        {
            var bytes = Read(root, "content/raw/event-island-species/" + i.Path, i.Sha256);
            if (bytes.Length != i.ByteLength || Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != i.GitBlobSha1)
                throw new InvalidDataException("event_island_git_blob:" + i.Path);
        }
        foreach (var i in d.DependencyInputs)
            if (Read(dependencyRoot, i.Path, i.Sha256).Length != i.ByteLength) throw new InvalidDataException("event_island_dependency:" + i.Path);
    }
}
