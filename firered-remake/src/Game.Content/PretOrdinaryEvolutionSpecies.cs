using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record OrdinaryEvolutionMetadataDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    ImmutableArray<SourceEggSpeciesRow> Additions, string SemanticSha256);
public sealed record OrdinaryEvolutionMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record OrdinaryEvolutionMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    int MachineCount, ImmutableArray<OrdinaryEvolutionMachineRows> Additions, string AdditionsSemanticSha256, ImmutableArray<ItemId> ItemsAdded);

/// <summary>Four original ordinary evolution targets, existing-machine compatibility and additive breeding metadata.</summary>
public static class PretOrdinaryEvolutionSpecies
{
    public const string CatalogSha256 = PokemonSha256;
    public const string PokemonSha256 = "1aec214d566a49e84b617a7a393a55d8f622e690a2f7ab20010fdefbb222b2ee";
    public const string MachinesSha256 = "f64f126985c190ccf739fa508b14d0f755bd179beba4426c168ce9f40ce89c88";
    public const string SourceInputsSha256 = "aa747d32453190ad3b2e9392ef92f376f00994a67c2bf4cbb8f9640651a837ae";
    public const string LockSha256 = "aff481fadfc7f61d53a1ea00363563eb143e0882684c4843e65d1e7d1e2632d8";

    public const string MetadataSha256 = "f7de0b3d08df4af841c5919e7c26a950a146dd02a0d3af447b80d17b9d35d70b";
    public static ImmutableArray<string> AdditionalSourceHashes { get; } = [PokemonSha256, MachinesSha256, MetadataSha256, SourceInputsSha256, LockSha256];
    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/ordinary-evolution-species", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("ordinary-evolution-species_source_bytes:" + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }) ?? throw new InvalidDataException("ordinary-evolution-species_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree ||
            document.GetProperty("profile").GetString() != OrdinaryEvolutionOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256 ||
            document.GetProperty("predecessorCatalogSha256").GetString() != PretBreedingSpecies.PokemonSha256 ||
            document.GetProperty("previousOwnedSpeciesCount").GetInt32() != 182 ||
            document.GetProperty("ownedSpeciesCount").GetInt32() != 186 ||
            document.GetProperty("previousOpponentSpeciesCount").GetInt32() != 203 ||
            document.GetProperty("opponentSpeciesCount").GetInt32() != 204 ||
            !document.GetProperty("opponentSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(OrdinaryEvolutionOpponentPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) ||
            document.GetProperty("ownedSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(OrdinaryEvolutionOwnedPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) is false ||
            document.GetProperty("evolutions").GetArrayLength() != 4 || document.GetProperty("movesAdded").GetArrayLength() != 0)
            throw new InvalidDataException("ordinary-evolution-species_pokemon_scope");
        return document;
    }

    public static OrdinaryEvolutionOwnedPokemonCatalog LoadOwned(string root, BreedingOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in OrdinaryEvolutionOwnedPokemonCatalog.AdditionalSpecies.Concat(OrdinaryEvolutionOwnedPokemonCatalog.UpdatedParents))
            species = species.SetItem(id, OrdinaryEvolutionOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return OrdinaryEvolutionOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Species = species
        });
    }

    public static OrdinaryEvolutionOpponentPokemonCatalog LoadOpponents(string root, BreedingOpponentPokemonCatalog predecessor,
        OrdinaryEvolutionOwnedPokemonCatalog owned)
    { _ = LoadPokemonDocument(root); return OrdinaryEvolutionOpponentPokemonCatalog.Authenticate(predecessor, owned); }

    public static OrdinaryEvolutionMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<OrdinaryEvolutionMachinesDocument>(root, "machines.json", MachinesSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != OrdinaryEvolutionMachineCatalog.Profile ||
            document.PreviousSpeciesCount != 182 || document.SpeciesCount != 186 || document.MachineCount != 51 ||
            document.Additions.IsDefault || document.Additions.Length != 51 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty ||
            document.AdditionsSemanticSha256 != OrdinaryEvolutionMachineCatalog.InheritedAdditionsSourceSha256)
            throw new InvalidDataException("ordinary-evolution-species_machines_scope");
        return document;
    }

    public static OrdinaryEvolutionMachineCatalog LoadMachines(string root, PewterMachineCatalog predecessor, OrdinaryEvolutionOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor; var additions = document.Additions.ToDictionary(m => m.Item);
        return OrdinaryEvolutionMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = OrdinaryEvolutionMachineCatalog.Profile,
            Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),
            AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }

    public static BreedingSpeciesMetadataCatalog LoadMetadata(string root, OrdinaryEvolutionOwnedPokemonCatalog owned)
    {
        var document = Read<OrdinaryEvolutionMetadataDocument>(root, "metadata.json", MetadataSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != "firered-ordinary-evolution-metadata-v1" ||
            document.PreviousSpeciesCount != 182 || document.SpeciesCount != 186 || document.SemanticSha256 != OrdinaryEvolutionSpeciesMetadataCatalog.SemanticSha256)
            throw new InvalidDataException("ordinary_evolution_metadata_scope");
        return OrdinaryEvolutionSpeciesMetadataCatalog.Authenticate(owned, document.Additions);
    }

    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        var lockBytes = File.ReadAllBytes(Path.Combine(root, "scripts/ordinary-evolution-species.lock.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(lockBytes)) != LockSha256) throw new InvalidDataException("ordinary-evolution-species_lock_bytes");
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("ordinary-evolution-species_source_identity");
        foreach (var input in inputs.GetProperty("inputs").EnumerateArray())
        {
            var path = input.GetProperty("localPath").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(input.GetProperty("shared").GetBoolean() ? dependencyRoot ?? root : root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString() ||
                Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != input.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("ordinary-evolution-species_source_bytes:" + path);
        }
        foreach (var input in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString())
                throw new InvalidDataException("ordinary-evolution-species_dependency_bytes:" + path);
        }
        using var locked = JsonDocument.Parse(lockBytes);
        var importer = File.ReadAllBytes(Path.Combine(root, "scripts/import-ordinary-evolution-species.py"));
        if (Convert.ToHexStringLower(SHA256.HashData(importer)) != locked.RootElement.GetProperty("importerSha256").GetString())
            throw new InvalidDataException("ordinary-evolution-species_importer_bytes");
        _ = LoadPokemonDocument(root); _ = LoadMachinesDocument(root);
    }
}
