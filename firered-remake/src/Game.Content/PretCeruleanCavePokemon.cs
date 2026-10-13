using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record CeruleanCaveMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record CeruleanCaveMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    int MachineCount, ImmutableArray<CeruleanCaveMachineRows> Additions, string AdditionsSemanticSha256, ImmutableArray<ItemId> ItemsAdded);
public sealed record CeruleanCavePokemonContent(CeruleanCaveOwnedPokemonCatalog Owned, CeruleanCaveOpponentPokemonCatalog Opponents,
    CeruleanCaveMachineCatalog Machines);

/// <summary>Source-authenticated current content extension; one shared execution owner, with encounter admission kept separate.</summary>
public static class PretCeruleanCavePokemon
{
    public const string PokemonSha256 = "55b3b1b8f593c72d71f92a43e7d599cf0bd278caad2e636b969b07b125330a95";
    public const string MachinesSha256 = "a0b1fd8623e3a07d32b95746a2c2a2bb3340c0c8d70e62e6925e8cd0c071e172";
    public const string SourceInputsSha256 = "ea9b6ea83e56d3cb8568bc743a650ac853844748efeddba58e55f087fcce62a9";
    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/cerulean-cave-pokemon", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("cerulean_cave_source_bytes:" + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }) ?? throw new InvalidDataException("cerulean_cave_source_missing");
    }
    public static JsonElement LoadDocument(string root)
    {
        var document = Read<JsonElement>(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("profile").GetString() != CeruleanCaveOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256 ||
            document.GetProperty("ownedSpeciesCount").GetInt32() != 139 || document.GetProperty("opponentSpeciesCount").GetInt32() != 163)
            throw new InvalidDataException("cerulean_cave_pokemon_scope");
        return document;
    }
    public static CeruleanCaveOwnedPokemonCatalog LoadOwned(string root, CelioOwnedPokemonCatalog predecessor)
    {
        _ = LoadDocument(root);
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
        var document = Read<CeruleanCaveMachinesDocument>(root, "machines.json", MachinesSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != CeruleanCaveMachineCatalog.Profile ||
            document.PreviousSpeciesCount != 138 || document.SpeciesCount != 139 || document.MachineCount != 39 ||
            document.Additions.IsDefault || document.Additions.Length != 39 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty ||
            document.AdditionsSemanticSha256 != CeruleanCaveMachineCatalog.InheritedAdditionsSourceSha256)
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
            Profile = CeruleanCaveMachineCatalog.Profile,
            Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),
            AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray() });
    }
    public static CeruleanCavePokemonContent Load(string root, CelioOwnedPokemonCatalog previousOwned,
        CelioOpponentPokemonCatalog previousOpponents, CelioMachineCatalog previousMachines)
    {
        var owned = LoadOwned(root, previousOwned);
        return new(owned, LoadOpponents(previousOpponents, owned), LoadMachines(root, previousMachines, owned));
    }
    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("cerulean_cave_source_identity");
        foreach (var row in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = row.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != row.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != row.GetProperty("sha256").GetString())
                throw new InvalidDataException("cerulean_cave_dependency_source:" + path);
        }
        _ = LoadDocument(root); _ = LoadMachinesDocument(root);
    }
}
