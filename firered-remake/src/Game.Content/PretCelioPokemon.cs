using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record CelioMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record CelioMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    int MachineCount, ImmutableArray<CelioMachineRows> Additions, string AdditionsSemanticSha256, ImmutableArray<ItemId> ItemsAdded);
public sealed record CelioPokemonContent(CelioOwnedPokemonCatalog Owned, CelioOpponentPokemonCatalog Opponents,
    CelioMachineCatalog Machines);

/// <summary>Source-authenticated Celio catalogs; lifecycle, battle and campaign admission are separate.</summary>
public static class PretCelioPokemon
{
    public const string PokemonSha256 = "3f5b6aeeb5454af27c38ff837757fe9f50647a2ed95dc19ea94e83dc05c54e9f";
    public const string MachinesSha256 = "98b8fcfa9fda49e6a9b74081386c1a2d52781394af2dd62c802f22b34b0578f0";
    public const string SourceInputsSha256 = "ff5618b3f99225ebeb5ef07a8d385e99125be5e349ab27a77673575c4cdc0182";
    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/celio-pokemon", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("celio_source_bytes:" + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }) ?? throw new InvalidDataException("celio_source_missing");
    }
    public static JsonElement LoadDocument(string root)
    {
        var document = Read<JsonElement>(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("profile").GetString() != CelioOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256 ||
            document.GetProperty("ownedSpeciesCount").GetInt32() != 138 || document.GetProperty("opponentSpeciesCount").GetInt32() != 162)
            throw new InvalidDataException("celio_pokemon_scope");
        return document;
    }
    public static CelioOwnedPokemonCatalog LoadOwned(string root, FiveIslandOwnedPokemonCatalog predecessor)
    {
        _ = LoadDocument(root);
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
        var document = Read<CelioMachinesDocument>(root, "machines.json", MachinesSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != CelioMachineCatalog.Profile ||
            document.PreviousSpeciesCount != 135 || document.SpeciesCount != 138 || document.MachineCount != 39 ||
            document.Additions.IsDefault || document.Additions.Length != 39 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty ||
            document.AdditionsSemanticSha256 != CelioMachineCatalog.InheritedAdditionsSourceSha256)
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
            Profile = CelioMachineCatalog.Profile,
            Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),
            AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray() });
    }
    public static CelioPokemonContent Load(string root, FiveIslandOwnedPokemonCatalog previousOwned,
        FiveIslandOpponentPokemonCatalog previousOpponents, FiveIslandMachineCatalog previousMachines)
    {
        var owned = LoadOwned(root, previousOwned);
        return new(owned, LoadOpponents(previousOpponents, owned), LoadMachines(root, previousMachines, owned));
    }
    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("celio_source_identity");
        foreach (var row in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = row.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != row.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != row.GetProperty("sha256").GetString())
                throw new InvalidDataException("celio_dependency_source:" + path);
        }
        _ = LoadDocument(root); _ = LoadMachinesDocument(root);
    }
}
