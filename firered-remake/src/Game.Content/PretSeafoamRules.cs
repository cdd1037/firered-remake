using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.Encounters;

namespace Game.Content;

public sealed record SeafoamTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement ResolvedMoves);
public sealed record SeafoamMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record SeafoamMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    int MachineCount, ImmutableArray<SeafoamMachineRows> Additions, string AdditionsSemanticSha256, ImmutableArray<ItemId> ItemsAdded);

/// <summary>Thin source-authenticated Articuno ownership, machines and original Route19/20 trainer teams.</summary>
public static class PretSeafoamRules
{
    public const string CatalogSha256 = PokemonSha256;
    public const string PokemonSha256 = "7713707128f4c5d19883db77ef995938b798a848c1cee7ebf43750aaeb04af4d";
    public const string MachinesSha256 = "b4d08d47e03b25c5eda92f0ab53192f2dec08c3c47554be7a09f96eb23e752c0";
    public const string SourceInputsSha256 = "61b81604b2335162e9123f36ea596379bd5d22e9e10fe8bc78c4eb5cb8c7eff0";
    public const string LockSha256 = "da402a0689af6bef398b8c25eb9bbf03bb214d098d355bdcbbec1d335cc92ea5";

    public const string TrainersSha256 = "2643b67c8484d3bbf43b53b5b4756701debeda24076b6ad294a3f7b280d8ea8d";
    public const string RulesSha256 = "f4fc11dcaf72393b1928162340f90a4840124ca4921a6c77a02b4226ada1ab31";
    public static ImmutableArray<string> AdditionalSourceHashes { get; } = [PokemonSha256, MachinesSha256, TrainersSha256, RulesSha256, SourceInputsSha256, LockSha256];
    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/seafoam-rules", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("seafoam_source_bytes:" + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
        }) ?? throw new InvalidDataException("seafoam_source_missing");
    }

    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree ||
            document.GetProperty("profile").GetString() != SeafoamOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256 ||
            document.GetProperty("predecessorCatalogSha256").GetString() != PretNpcTradeOwnedRules.PokemonSha256 ||
            document.GetProperty("previousOwnedSpeciesCount").GetInt32() != 161 ||
            document.GetProperty("ownedSpeciesCount").GetInt32() != 162 ||
            document.GetProperty("previousOpponentSpeciesCount").GetInt32() != 190 ||
            document.GetProperty("opponentSpeciesCount").GetInt32() != 191 ||
            document.GetProperty("ownedSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(["SPECIES_ARTICUNO"]) is false ||
            !document.GetProperty("opponentSpeciesAdded").EnumerateArray().Select(p => p.GetString()).SequenceEqual(["SPECIES_ARTICUNO"]) || document.GetProperty("evolutions").GetArrayLength() != 0)
            throw new InvalidDataException("seafoam_pokemon_scope");
        return document;
    }

    public static SeafoamOwnedPokemonCatalog LoadOwned(string root, NpcTradeOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in SeafoamOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, SeafoamOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return SeafoamOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with {
            Species = species
        });
    }

    public static SeafoamOpponentPokemonCatalog LoadOpponents(string root, NpcTradeOpponentPokemonCatalog predecessor,
        SeafoamOwnedPokemonCatalog owned)
    { _ = LoadPokemonDocument(root); return SeafoamOpponentPokemonCatalog.Authenticate(predecessor, owned); }

    public static SeafoamMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<SeafoamMachinesDocument>(root, "machines.json", MachinesSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != SeafoamMachineCatalog.Profile ||
            document.PreviousSpeciesCount != 161 || document.SpeciesCount != 162 || document.MachineCount != 41 ||
            document.Additions.IsDefault || document.Additions.Length != 41 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty ||
            document.AdditionsSemanticSha256 != SeafoamMachineCatalog.InheritedAdditionsSourceSha256)
            throw new InvalidDataException("seafoam_machines_scope");
        return document;
    }

    public static SeafoamMachineCatalog LoadMachines(string root, NpcTradeMachineCatalog predecessor, SeafoamOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor.Catalog; var additions = document.Additions.ToDictionary(m => m.Item);
        return SeafoamMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = SeafoamMachineCatalog.Profile,
            Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),
            AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray()
        });
    }

    public static SeafoamTrainerDocument LoadDocument(string root)
    {
        var document = Read<SeafoamTrainerDocument>(root, "trainers.json", TrainersSha256);
        _ = LoadTrainers(document); return document;
    }
    public static SeafoamTrainerCatalog LoadTrainers(string root) => LoadTrainers(LoadDocument(root));
    private static SeafoamTrainerCatalog LoadTrainers(SeafoamTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty || document.Trainers.Sum(t => t.Party.Length) != 57)
            throw new InvalidDataException("seafoam_trainers_missing");
        var result = new SeafoamTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        SeafoamTrainerPartyFactory.ValidateCatalog(result); return result;
    }
    public static JsonElement LoadRules(string root) => Read<JsonElement>(root, "rules.json", RulesSha256);
    public static SeafoamWaterEncounterCatalog LoadWater(SevenIslandWaterEncounterCatalog predecessor) =>
        SeafoamWaterEncounterCatalog.Authenticate(predecessor, SeafoamEncounterTables.Water);

    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        var lockBytes = File.ReadAllBytes(Path.Combine(root, "scripts/seafoam-rules.lock.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(lockBytes)) != LockSha256) throw new InvalidDataException("seafoam_lock_bytes");
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("seafoam_source_identity");
        foreach (var input in inputs.GetProperty("inputs").EnumerateArray())
        {
            var path = input.GetProperty("localPath").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(input.GetProperty("shared").GetBoolean() ? dependencyRoot ?? root : root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString() ||
                Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != input.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("seafoam_source_bytes:" + path);
        }
        foreach (var input in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString())
                throw new InvalidDataException("seafoam_dependency_bytes:" + path);
        }
        using var locked = JsonDocument.Parse(lockBytes);
        var importer = File.ReadAllBytes(Path.Combine(root, "scripts/import-seafoam-rules.py"));
        if (Convert.ToHexStringLower(SHA256.HashData(importer)) != locked.RootElement.GetProperty("importerSha256").GetString())
            throw new InvalidDataException("seafoam_importer_bytes");
        _ = LoadPokemonDocument(root); _ = LoadMachinesDocument(root); _ = LoadTrainers(root); _ = LoadRules(root);
    }
}
