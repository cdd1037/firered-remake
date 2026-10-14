using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record StoneEvolutionMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record StoneEvolutionMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    int MachineCount, ImmutableArray<StoneEvolutionMachineRows> Additions, string AdditionsSemanticSha256, ImmutableArray<ItemId> ItemsAdded);

/// <summary>Source-authenticated seven-target ownership and exactly 42 inherited machine rows.</summary>
public static class PretStoneEvolutionSpecies
{
    public const string CatalogSha256 = PokemonSha256;
    public const string PokemonSha256 = "43ce70520ebb5d4629285acaea21f4acec81ea9c59885876dc8400efdc97c490";
    public const string MachinesSha256 = "333dc352f5e9fc491c05a333250142cb5c5e24ed4c525240fc864629621fb2ab";
    public const string SourceInputsSha256 = "3778a3452b34913410ec4acce9f4dfcbb7a2d28de6410b3a3a7f9a27a073013f";
    public const string LockSha256 = "6fc19b347eea76261935ef0dc9e2e3eb50dd10aef6747ea5a1f9695fd2d897fe";

    public static ImmutableArray<string> AdditionalSourceHashes { get; } = [PokemonSha256, MachinesSha256, SourceInputsSha256, LockSha256];
    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/stone-evolution-species", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("stone-evolution-species_source_bytes:" + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }) ?? throw new InvalidDataException("stone-evolution-species_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree ||
            document.GetProperty("profile").GetString() != StoneEvolutionOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256 ||
            document.GetProperty("predecessorCatalogSha256").GetString() != PretSuperRodSpecies.PokemonSha256 ||
            document.GetProperty("previousOwnedSpeciesCount").GetInt32() != 163 ||
            document.GetProperty("ownedSpeciesCount").GetInt32() != 170 ||
            document.GetProperty("previousOpponentSpeciesCount").GetInt32() != 192 ||
            document.GetProperty("opponentSpeciesCount").GetInt32() != 195 ||
            document.GetProperty("ownedSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(StoneEvolutionOwnedPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) is false ||
            !document.GetProperty("opponentSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(StoneEvolutionOpponentPokemonCatalog.AdditionalSpecies.Select(id => id.Value[5..])) || document.GetProperty("evolutions").GetArrayLength() != 0)
            throw new InvalidDataException("stone-evolution-species_pokemon_scope");
        return document;
    }

    public static StoneEvolutionOwnedPokemonCatalog LoadOwned(string root, SuperRodOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in StoneEvolutionOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, StoneEvolutionOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return StoneEvolutionOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Species = species
        });
    }

    public static StoneEvolutionOpponentPokemonCatalog LoadOpponents(string root, SuperRodOpponentPokemonCatalog predecessor,
        StoneEvolutionOwnedPokemonCatalog owned)
    { _ = LoadPokemonDocument(root); return StoneEvolutionOpponentPokemonCatalog.Authenticate(predecessor, owned); }

    public static StoneEvolutionMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<StoneEvolutionMachinesDocument>(root, "machines.json", MachinesSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != StoneEvolutionMachineCatalog.Profile ||
            document.PreviousSpeciesCount != 163 || document.SpeciesCount != 170 || document.MachineCount != 42 ||
            document.Additions.IsDefault || document.Additions.Length != 42 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty ||
            document.AdditionsSemanticSha256 != StoneEvolutionMachineCatalog.InheritedAdditionsSourceSha256)
            throw new InvalidDataException("stone-evolution-species_machines_scope");
        return document;
    }

    public static StoneEvolutionMachineCatalog LoadMachines(string root, DepartmentStoreMachineStock predecessor, StoneEvolutionOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor.Machines; var additions = document.Additions.ToDictionary(m => m.Item);
        return StoneEvolutionMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = StoneEvolutionMachineCatalog.Profile,
            Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),
            AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }

    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        var lockBytes = File.ReadAllBytes(Path.Combine(root, "scripts/stone-evolution-species.lock.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(lockBytes)) != LockSha256) throw new InvalidDataException("stone-evolution-species_lock_bytes");
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("stone-evolution-species_source_identity");
        foreach (var input in inputs.GetProperty("inputs").EnumerateArray())
        {
            var path = input.GetProperty("localPath").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(input.GetProperty("shared").GetBoolean() ? dependencyRoot ?? root : root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString() ||
                Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != input.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("stone-evolution-species_source_bytes:" + path);
        }
        foreach (var input in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString())
                throw new InvalidDataException("stone-evolution-species_dependency_bytes:" + path);
        }
        using var locked = JsonDocument.Parse(lockBytes);
        var importer = File.ReadAllBytes(Path.Combine(root, "scripts/import-stone-evolution-species.py"));
        if (Convert.ToHexStringLower(SHA256.HashData(importer)) != locked.RootElement.GetProperty("importerSha256").GetString())
            throw new InvalidDataException("stone-evolution-species_importer_bytes");
        _ = LoadPokemonDocument(root); _ = LoadMachinesDocument(root);
    }
}
