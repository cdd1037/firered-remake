using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record SuperRodMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record SuperRodMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    int MachineCount, ImmutableArray<SuperRodMachineRows> Additions, string AdditionsSemanticSha256, ImmutableArray<ItemId> ItemsAdded);

/// <summary>Source-authenticated Qwilfish ownership and exactly 41 inherited machine rows.</summary>
public static class PretSuperRodSpecies
{
    public const string CatalogSha256 = PokemonSha256;
    public const string PokemonSha256 = "8294dc3ad4142e91f92f3a9bd67924c335822b10fe77b38310e133ae80e2544a";
    public const string MachinesSha256 = "efec45a3d9ecfe83a8808ae4e6b5761ba5c9466ad0ad0b6ee0665d980cbf6bf5";
    public const string SourceInputsSha256 = "e7a6fdf9141b33d5df2ac8c2f610a2fc6486a17fb6b2a2e5bb8fbb246aee6d25";
    public const string LockSha256 = "7b3ca9be3c6352297dde25eaef1af8accdb8dfbcde2044ff7fa86ad5a957d74e";

    public static ImmutableArray<string> AdditionalSourceHashes { get; } = [PokemonSha256, MachinesSha256, SourceInputsSha256, LockSha256];
    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/super-rod-species", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("super-rod-species_source_bytes:" + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }) ?? throw new InvalidDataException("super-rod-species_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree ||
            document.GetProperty("profile").GetString() != SuperRodOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256 ||
            document.GetProperty("predecessorCatalogSha256").GetString() != PretSeafoamRules.PokemonSha256 ||
            document.GetProperty("previousOwnedSpeciesCount").GetInt32() != 162 ||
            document.GetProperty("ownedSpeciesCount").GetInt32() != 163 ||
            document.GetProperty("previousOpponentSpeciesCount").GetInt32() != 191 ||
            document.GetProperty("opponentSpeciesCount").GetInt32() != 192 ||
            document.GetProperty("ownedSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(["SPECIES_QWILFISH"]) is false ||
            !document.GetProperty("opponentSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(["SPECIES_QWILFISH"]) || document.GetProperty("evolutions").GetArrayLength() != 0)
            throw new InvalidDataException("super-rod-species_pokemon_scope");
        return document;
    }

    public static SuperRodOwnedPokemonCatalog LoadOwned(string root, SeafoamOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in SuperRodOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, SuperRodOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return SuperRodOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Species = species
        });
    }

    public static SuperRodOpponentPokemonCatalog LoadOpponents(string root, SeafoamOpponentPokemonCatalog predecessor,
        SuperRodOwnedPokemonCatalog owned)
    { _ = LoadPokemonDocument(root); return SuperRodOpponentPokemonCatalog.Authenticate(predecessor, owned); }

    public static SuperRodMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<SuperRodMachinesDocument>(root, "machines.json", MachinesSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != SuperRodMachineCatalog.Profile ||
            document.PreviousSpeciesCount != 162 || document.SpeciesCount != 163 || document.MachineCount != 41 ||
            document.Additions.IsDefault || document.Additions.Length != 41 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty ||
            document.AdditionsSemanticSha256 != SuperRodMachineCatalog.InheritedAdditionsSourceSha256)
            throw new InvalidDataException("super-rod-species_machines_scope");
        return document;
    }

    public static SuperRodMachineCatalog LoadMachines(string root, SeafoamMachineCatalog predecessor, SuperRodOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor.Catalog; var additions = document.Additions.ToDictionary(m => m.Item);
        return SuperRodMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = SuperRodMachineCatalog.Profile,
            Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),
            AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }

    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        var lockBytes = File.ReadAllBytes(Path.Combine(root, "scripts/super-rod-species.lock.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(lockBytes)) != LockSha256) throw new InvalidDataException("super-rod-species_lock_bytes");
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("super-rod-species_source_identity");
        foreach (var input in inputs.GetProperty("inputs").EnumerateArray())
        {
            var path = input.GetProperty("localPath").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(input.GetProperty("shared").GetBoolean() ? dependencyRoot ?? root : root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString() ||
                Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != input.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("super-rod-species_source_bytes:" + path);
        }
        foreach (var input in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString())
                throw new InvalidDataException("super-rod-species_dependency_bytes:" + path);
        }
        using var locked = JsonDocument.Parse(lockBytes);
        var importer = File.ReadAllBytes(Path.Combine(root, "scripts/import-super-rod-species.py"));
        if (Convert.ToHexStringLower(SHA256.HashData(importer)) != locked.RootElement.GetProperty("importerSha256").GetString())
            throw new InvalidDataException("super-rod-species_importer_bytes");
        _ = LoadPokemonDocument(root); _ = LoadMachinesDocument(root);
    }
}
