using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.World;
namespace Game.Content;

public sealed record FightingDojoTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement ResolvedMoves);
public sealed record FightingDojoMachineRows(ItemId Item, ImmutableArray<PewterMachineCompatibility> Compatibility);
public sealed record FightingDojoMachinesDocument(string SourceCommit, string Profile, int PreviousSpeciesCount, int SpeciesCount,
    int MachineCount, ImmutableArray<FightingDojoMachineRows> Additions, string AdditionsSemanticSha256, ImmutableArray<ItemId> ItemsAdded);
/// <summary>Bounded Dojo ownership, opponent, trainer and machine catalogs over authenticated Power Plant resources.</summary>
public static class PretFightingDojoRules
{
    public const string CatalogSha256 = PokemonSha256;
    public const string LockSha256 = "eb484c310d3b9f6588ac0650ab1945fa3318c05f3a76285a6fca39d4b06c034c";
    // Hash constants are pinned to independently regenerable source imports.
    public const string TrainersSha256 = "1b91967d81795a37437309205c3834c473a66597fa88a8de8ac384091dfb3762";
    public const string PokemonSha256 = "4679a195369fe66442a0d4b8f75000efc9714083e52232c40e717e3d4c89cdd1";
    public const string RulesSha256 = "fec84f03b6da2aff0d2d4243b1f81b4557b8e787522f7e9a9ca44ac668787ce4";
    public const string MachinesSha256 = "c47f335e8091e1f8b2e0cda39819af278a4a4f47140ae762942d95d7b99143d6";
    public const string SourceInputsSha256 = "f6f53b2cc4ed81a4c8dbfb0bb6e9c24a8014a100ff72b95b489cefc6a418b340";
    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/fighting-dojo-rules", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("fighting_dojo_source_bytes:" + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }) ?? throw new InvalidDataException("fighting_dojo_source_missing");
    }
    public static FightingDojoTrainerDocument LoadDocument(string root)
    {
        var document = Read<FightingDojoTrainerDocument>(root, "trainers.json", TrainersSha256);
        _ = LoadTrainers(document); return document;
    }
    public static FightingDojoTrainerCatalog LoadTrainers(string root) => LoadTrainers(LoadDocument(root));
    private static FightingDojoTrainerCatalog LoadTrainers(FightingDojoTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty) throw new InvalidDataException("fighting_dojo_trainers_missing");
        var result = new FightingDojoTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        FightingDojoTrainerPartyFactory.ValidateCatalog(result); return result;
    }
    public static JsonElement LoadPokemonDocument(string root)
    {
        var document = Read<JsonElement>(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("profile").GetString() != FightingDojoOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256 ||
            document.GetProperty("ownedSpeciesCount").GetInt32() != 154 || document.GetProperty("opponentSpeciesCount").GetInt32() != 186)
            throw new InvalidDataException("fighting_dojo_pokemon_scope");
        return document;
    }
    public static FightingDojoOwnedPokemonCatalog LoadOwned(string root, PowerPlantOwnedPokemonCatalog predecessor)
    {
        _ = LoadPokemonDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in FightingDojoOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, FightingDojoOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return FightingDojoOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }
    public static FightingDojoOpponentPokemonCatalog LoadOpponents(string root, PowerPlantOpponentPokemonCatalog predecessor,
        FightingDojoOwnedPokemonCatalog owned)
    { _ = LoadPokemonDocument(root); return FightingDojoOpponentPokemonCatalog.Authenticate(predecessor, owned); }
    public static FightingDojoMachinesDocument LoadMachinesDocument(string root)
    {
        var document = Read<FightingDojoMachinesDocument>(root, "machines.json", MachinesSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != FightingDojoMachineCatalog.Profile ||
            document.PreviousSpeciesCount != 152 || document.SpeciesCount != 154 || document.MachineCount != 41 ||
            document.Additions.IsDefault || document.Additions.Length != 41 || document.ItemsAdded.IsDefault || !document.ItemsAdded.IsEmpty ||
            document.AdditionsSemanticSha256 != FightingDojoMachineCatalog.InheritedAdditionsSourceSha256)
            throw new InvalidDataException("fighting_dojo_machines_scope");
        return document;
    }
    public static FightingDojoMachineCatalog LoadMachines(string root, PowerPlantMachineCatalog predecessor, FightingDojoOwnedPokemonCatalog owned)
    {
        var document = LoadMachinesDocument(root); ArgumentNullException.ThrowIfNull(predecessor);
        var previous = predecessor.Catalog; var additions = document.Additions.ToDictionary(m => m.Item);
        return FightingDojoMachineCatalog.Authenticate(predecessor, owned, previous with {
            Profile = FightingDojoMachineCatalog.Profile,
            Compatibility = previous.Compatibility.AddRange(additions[previous.Tm39.Item.Id].Compatibility),
            AdditionalMachines = previous.AdditionalMachines!.Value.Select(m => m with {
                Compatibility = m.Compatibility.AddRange(additions[m.Machine.Item.Id].Compatibility)
            }).ToImmutableArray() });
    }
    public static JsonElement LoadRules(string root) => Read<JsonElement>(root, "rules.json", RulesSha256);
    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        var lockBytes = File.ReadAllBytes(Path.Combine(root, "scripts/fighting-dojo-rules.lock.json"));
        if (Convert.ToHexStringLower(SHA256.HashData(lockBytes)) != LockSha256) throw new InvalidDataException("fighting_dojo_lock_bytes");
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree) throw new InvalidDataException("fighting_dojo_source_identity");
        foreach (var input in inputs.GetProperty("inputs").EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(root, "content/raw/fighting-dojo-rules", path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString() ||
                Convert.ToHexStringLower(SHA1.HashData([.. Encoding.ASCII.GetBytes($"blob {bytes.Length}\0"), .. bytes])) != input.GetProperty("gitBlobSha1").GetString())
                throw new InvalidDataException("fighting_dojo_source_bytes:" + path);
        }
        foreach (var input in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = input.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != input.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != input.GetProperty("sha256").GetString())
                throw new InvalidDataException("fighting_dojo_dependency_bytes:" + path);
        }
        _ = LoadDocument(root); _ = LoadPokemonDocument(root); _ = LoadMachinesDocument(root); _ = LoadRules(root);
    }
}
