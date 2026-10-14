using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record BreedingMetadataDocument(string SourceCommit, string Profile, int SpeciesCount,
    ImmutableArray<MoveId> AllMachineMoves, ImmutableArray<SourceEggSpeciesRow> Species, string SemanticSha256);
public sealed record BreedingMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record BreedingMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    int MachineCount, ImmutableArray<BreedingMachineRows> Additions, string AdditionsSemanticSha256, ImmutableArray<ItemId> ItemsAdded);

/// <summary>Eight breeding-family owned species, 51 acquired-machine extensions and original egg inheritance metadata.</summary>
public static class PretBreedingSpecies
{
    public const string CatalogSha256 = PokemonSha256;
    public const string PokemonSha256 = "4f7c0b85bda98cd0980205f5ab96875a57852efeca1ac8ea1fbd63e5e472dc58";
    public const string MachinesSha256 = "15a696696cb67b3faf8957617e71f2890b65833145474f13e5946d38ce6dcd11";
    public const string SourceInputsSha256 = "5e59f5cacc0d948cf6e1c77e5ce94ed0bfcce039774ca4c9fcbddc7b887590ab";
    public const string LockSha256 = "a15368485c91aca31da81451b30abe3d24b4d0a7a3f78436f451d46ff2722404";

    public const string MetadataSha256 = "9b079771b481f49f7cb1d9030fe9bb450a02b19d9b5d24d7ad0e47228ed8f200";
    public static ImmutableArray<string> AdditionalSourceHashes { get; } = [PokemonSha256, MachinesSha256, MetadataSha256, SourceInputsSha256, LockSha256];
    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/breeding-species", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("breeding-species_source_bytes:" + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }) ?? throw new InvalidDataException("breeding-species_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree ||
            document.GetProperty("profile").GetString() != BreedingOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256 ||
            document.GetProperty("predecessorCatalogSha256").GetString() != PretGameCornerSpecies.PokemonSha256 ||
            document.GetProperty("previousOwnedSpeciesCount").GetInt32() != 174 ||
            document.GetProperty("ownedSpeciesCount").GetInt32() != 182 ||
            document.GetProperty("previousOpponentSpeciesCount").GetInt32() != 196 ||
            document.GetProperty("opponentSpeciesCount").GetInt32() != 203 ||
            !document.GetProperty("opponentSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(BreedingOpponentPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) ||
            document.GetProperty("ownedSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(BreedingOwnedPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) is false ||
            document.GetProperty("evolutions").GetArrayLength() != 9 || document.GetProperty("movesAdded").GetArrayLength() != 0)
            throw new InvalidDataException("breeding-species_pokemon_scope");
        return document;
    }

    public static BreedingOwnedPokemonCatalog LoadOwned(string root, GameCornerOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in BreedingOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, BreedingOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return BreedingOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Species = species
        });
    }

    public static BreedingOpponentPokemonCatalog LoadOpponents(string root, GameCornerOpponentPokemonCatalog predecessor,
        BreedingOwnedPokemonCatalog owned)
    { _ = LoadPokemonDocument(root); return BreedingOpponentPokemonCatalog.Authenticate(predecessor, owned); }

    public static BreedingMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<BreedingMachinesDocument>(root, "machines.json", MachinesSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != BreedingMachineCatalog.Profile ||
            document.PreviousSpeciesCount != 174 || document.SpeciesCount != 182 || document.MachineCount != 51 ||
            document.Additions.IsDefault || document.Additions.Length != 51 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty ||
            document.AdditionsSemanticSha256 != BreedingMachineCatalog.InheritedAdditionsSourceSha256)
            throw new InvalidDataException("breeding-species_machines_scope");
        return document;
    }

    public static BreedingMachineCatalog LoadMachines(string root, PewterMachineCatalog predecessor, BreedingOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor; var additions = document.Additions.ToDictionary(m => m.Item);
        return BreedingMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = BreedingMachineCatalog.Profile,
            Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),
            AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }

    public static BreedingSpeciesMetadataCatalog LoadMetadata(string root, BreedingOwnedPokemonCatalog owned)
    {
        var document = Read<BreedingMetadataDocument>(root, "metadata.json", MetadataSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != "firered-breeding-metadata-v1" ||
            document.SpeciesCount != 182 || document.SemanticSha256 != BreedingSpeciesMetadataCatalog.SemanticSha256)
            throw new InvalidDataException("breeding_metadata_scope");
        return BreedingSpeciesMetadataCatalog.Authenticate(owned, document.Species, document.AllMachineMoves);
    }

    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        var lockBytes = File.ReadAllBytes(Path.Combine(root, "scripts/breeding-species.lock.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(lockBytes)) != LockSha256) throw new InvalidDataException("breeding-species_lock_bytes");
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("breeding-species_source_identity");
        foreach (var input in inputs.GetProperty("inputs").EnumerateArray())
        {
            var path = input.GetProperty("localPath").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(input.GetProperty("shared").GetBoolean() ? dependencyRoot ?? root : root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString() ||
                Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != input.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("breeding-species_source_bytes:" + path);
        }
        foreach (var input in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString())
                throw new InvalidDataException("breeding-species_dependency_bytes:" + path);
        }
        using var locked = JsonDocument.Parse(lockBytes);
        var importer = File.ReadAllBytes(Path.Combine(root, "scripts/import-breeding-species.py"));
        if (Convert.ToHexStringLower(SHA256.HashData(importer)) != locked.RootElement.GetProperty("importerSha256").GetString())
            throw new InvalidDataException("breeding-species_importer_bytes");
        _ = LoadPokemonDocument(root); _ = LoadMachinesDocument(root);
    }
}
