using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record FossilLabMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record FossilLabMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    int MachineCount, ImmutableArray<FossilLabMachineRows> Additions, string AdditionsSemanticSha256, ImmutableArray<ItemId> ItemsAdded);

/// <summary>Authenticated Helix/Dome catalogs and level-five creation evidence over the exact Dojo resources.</summary>
public static class PretFossilLabRules
{
    public const string CatalogSha256 = PokemonSha256;
    public const string LockSha256 = "c1f57f5d56266b1dd9c09b4c0c4c3ba133a10cbe587338e8ba683237aa702f1a";
    public const string PokemonSha256 = "c73920819f51fd82518103ae1bc38b2d8bd6c8a2aa34eb2409335f9be1ded12c";
    public const string MachinesSha256 = "e4d88c47202263f022904bda3d8f5144391a7e6d9ccbc80dd507fa7195170c82";
    public const string RulesSha256 = "ff539780bec46809aea06499c14bfde16611d48f973a3947072705109df1d762";
    public const string SourceInputsSha256 = "7d64b4b10a874629c5ab8106aa099e061f9c5501fcdc88156fcc9b5946b9a13a";

    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/fossil-lab-rules", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("fossil_lab_source_bytes:" + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }) ?? throw new InvalidDataException("fossil_lab_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree ||
            document.GetProperty("profile").GetString() != FossilLabOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256 ||
            document.GetProperty("previousOwnedSpeciesCount").GetInt32() != 154 ||
            document.GetProperty("ownedSpeciesCount").GetInt32() != 158 ||
            document.GetProperty("previousOpponentSpeciesCount").GetInt32() != 186 ||
            document.GetProperty("opponentSpeciesCount").GetInt32() != 190 ||
            document.GetProperty("evolutions").GetArrayLength() != 2)
            throw new InvalidDataException("fossil_lab_pokemon_scope");
        return document;
    }

    public static FossilLabOwnedPokemonCatalog LoadOwned(string root, FightingDojoOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in FossilLabOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, FossilLabOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return FossilLabOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }

    public static FossilLabOpponentPokemonCatalog LoadOpponents(string root, FightingDojoOpponentPokemonCatalog predecessor,
        FossilLabOwnedPokemonCatalog owned)
    { _ = LoadPokemonDocument(root); return FossilLabOpponentPokemonCatalog.Authenticate(predecessor, owned); }

    public static FossilLabMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<FossilLabMachinesDocument>(root, "machines.json", MachinesSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != FossilLabMachineCatalog.Profile ||
            document.PreviousSpeciesCount != 154 || document.SpeciesCount != 158 || document.MachineCount != 41 ||
            document.Additions.IsDefault || document.Additions.Length != 41 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty ||
            document.AdditionsSemanticSha256 != FossilLabMachineCatalog.InheritedAdditionsSourceSha256)
            throw new InvalidDataException("fossil_lab_machines_scope");
        return document;
    }

    public static FossilLabMachineCatalog LoadMachines(string root, FightingDojoMachineCatalog predecessor, FossilLabOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor.Catalog; var additions = document.Additions.ToDictionary(m => m.Item);
        return FossilLabMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = FossilLabMachineCatalog.Profile,
            Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),
            AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }

    public static JsonElement LoadRules(string root) => Read<JsonElement>(root, "rules.json", RulesSha256);

    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        var lockBytes = File.ReadAllBytes(Path.Combine(root, "scripts/fossil-lab-rules.lock.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(lockBytes)) != LockSha256) throw new InvalidDataException("fossil_lab_lock_bytes");
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("fossil_lab_source_identity");
        foreach (var input in inputs.GetProperty("inputs").EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(root, "content/raw/fossil-lab-rules", path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString() ||
                Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != input.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("fossil_lab_source_bytes:" + path);
        }
        foreach (var input in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString())
                throw new InvalidDataException("fossil_lab_dependency_bytes:" + path);
        }
        using var locked = JsonDocument.Parse(lockBytes);
        var importer = File.ReadAllBytes(Path.Combine(root, "scripts/import-fossil-lab-rules.py"));
        if (Convert.ToHexStringLower(SHA256.HashData(importer)) != locked.RootElement.GetProperty("importerSha256").GetString())
            throw new InvalidDataException("fossil_lab_importer_bytes");
        _ = LoadPokemonDocument(root); _ = LoadMachinesDocument(root); _ = LoadRules(root);
    }
}
