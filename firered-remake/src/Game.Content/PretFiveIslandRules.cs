using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record FiveIslandTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles, JsonElement ResolvedMoves);

/// <summary>Source-authenticated initial trainer catalog and additive owned/opponent proofs; no runtime route admission.</summary>
public static class PretFiveIslandRules
{
    public const string TrainersSha256 = "34ba81e7cdfbbe48a8d03087739cb85a443f7aaaba70a0ff8bb788ed434eb948";
    public const string PokemonSha256 = "9788f567d061f21e66d877718a542a3fb2068629f34b65da9dd604e387a0a909";
    public const string RulesSha256 = "60ebc4bb8e5f40cfd94b4f6c6b3cf3e43d8d1a9d24748e6ce7009c8cbfb31e62";
    public const string SourceInputsSha256 = "96db60a4af26ea9ae3f16d9cd57d2d87ba222c8f58409830b0139b94d9c7febc";
    public const string EncountersSha256 = "72f44716404385d8705a7183cffaeebaaf3246f5344992dcdcbe1c9429f40702";
    public const string ItemSourceInputsSha256 = "ede1ef73d368019fde21ef9c86f430623dca6ce4152ff0763b6e24224f7a6076";
    public const string WildSourceWitnessSha256 = "5dad838b1d721ccd2eb7ddf59995a5a8066d8ebd25ad707bbe1b6ad3bcb83a48";
    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/five-island-rules", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash) throw new InvalidDataException("Five Island source bytes differ: " + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } }) ?? throw new InvalidDataException("Missing Five Island source document.");
    }
    public static FiveIslandTrainerDocument LoadDocument(string root)
    {
        var document = Read<FiveIslandTrainerDocument>(root, "trainers.json", TrainersSha256);
        _ = LoadTrainers(document); return document;
    }
    public static FiveIslandTrainerCatalog LoadTrainers(string root) => LoadTrainers(LoadDocument(root));
    private static FiveIslandTrainerCatalog LoadTrainers(FiveIslandTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty) throw new InvalidDataException("Missing Five Island trainer records.");
        var result = new FiveIslandTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.ScriptEntry, StringComparer.Ordinal));
        FiveIslandTrainerPartyFactory.ValidateCatalog(result); return result;
    }
    public static FiveIslandOwnedPokemonCatalog LoadOwned(SixIslandOwnedPokemonCatalog predecessor)
    {
        ArgumentNullException.ThrowIfNull(predecessor);
        var species = predecessor.Catalog.Species;
        foreach (var id in FiveIslandOwnedPokemonCatalog.AdditionalSpecies)
            species = species.Add(id, FiveIslandOwnedPokemonCatalog.ExpectedSpecies(predecessor, id));
        return FiveIslandOwnedPokemonCatalog.Authenticate(predecessor, predecessor.Catalog with { Species = species });
    }
    public static FiveIslandOwnedPokemonCatalog LoadOwned(string root, SixIslandOwnedPokemonCatalog predecessor)
    {
        var document = Read<JsonElement>(root, "pokemon.json", PokemonSha256);
        if (document.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            document.GetProperty("profile").GetString() != FiveIslandOwnedPokemonCatalog.Profile ||
            document.GetProperty("canonicalCatalogSha256").GetString() != PretCanonicalPokemonCatalog.CatalogSha256)
            throw new InvalidDataException("Five Island owned source differs.");
        return LoadOwned(predecessor);
    }
    public static FiveIslandOpponentPokemonCatalog LoadOpponents(SixIslandOpponentPokemonCatalog predecessor,
        FiveIslandOwnedPokemonCatalog owned) => FiveIslandOpponentPokemonCatalog.Authenticate(predecessor, owned);
    public static JsonElement LoadEncounterDeclarations(string root) => Read<JsonElement>(root, "encounters.json", EncountersSha256);
    public static JsonElement LoadRules(string root) => Read<JsonElement>(root, "rules.json", RulesSha256);
    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        foreach (var inputs in new[] { Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256),
            Read<JsonElement>(root, "item-source-input-hashes.json", ItemSourceInputsSha256) })
        {
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("Five Island source evidence identity");
        foreach (var row in inputs.GetProperty("dependencyInputs").EnumerateArray())
        {
            var path = row.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != row.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != row.GetProperty("sha256").GetString())
                throw new InvalidDataException("Five Island dependency differs: " + path);
        }
        }
        _ = Read<JsonElement>(root, "pokemon.json", PokemonSha256); _ = LoadRules(root);
        _ = LoadEncounterDeclarations(root); _ = PretFiveIslandItems.LoadDocument(root);
        _ = Read<JsonElement>(root, "wild-source-witness.json", WildSourceWitnessSha256);
    }
}
