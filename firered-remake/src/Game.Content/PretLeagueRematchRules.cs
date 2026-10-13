using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Trainers;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record LeagueRematchTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves);

public sealed record LeagueRematchOpponentDocument(string SourceCommit, string Profile, string CanonicalCatalogSha256,
    ImmutableArray<string> OwnedSpeciesAdded, ImmutableArray<PokemonSpeciesDefinition> OpponentSpecies);

/// <summary>Exact upgraded League source teams and the minimal trainer-only species continuation.</summary>
public static class PretLeagueRematchRules
{
    public const string TrainersSha256 = "7ee74615cb9f6bd3c0b43739a15af7c8445d2733f35cb69d217c7516407fa106";
    public const string SourceInputsSha256 = "99cdf95c56528938716e6b32d955d4f3dfd50a317d7d3ad18f5f8d3e2c95eb86";

    public const string PokemonSha256 = "d9e9657f331699b88a9909d1bbb8dce05797ee0714e4c980730884aab4725b39";
    public const string RulesSha256 = "d73879a1a9283582fcdfc7c190d132f83b5417a5f2edec61c6ccf62d37dcccf5";

    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/league-rematch-rules", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("Pokemon League source bytes differ: " + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } })
            ?? throw new InvalidDataException("Missing Pokemon League source document.");
    }

    public static LeagueRematchTrainerDocument LoadDocument(string root)
    {
        var document = Read<LeagueRematchTrainerDocument>(root, "trainers.json", TrainersSha256);
        _ = LoadTrainers(document);
        return document;
    }

    public static LeagueRematchTrainerCatalog LoadTrainers(string root) =>
        LoadTrainers(Read<LeagueRematchTrainerDocument>(root, "trainers.json", TrainersSha256));

    private static LeagueRematchTrainerCatalog LoadTrainers(LeagueRematchTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty)
            throw new InvalidDataException("Missing Pokemon League trainer or battle records.");
        var result = new LeagueRematchTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        LeagueRematchTrainerPartyFactory.ValidateCatalog(result);
        return result;
    }

    public static LeagueRematchOpponentPokemonCatalog LoadOpponents(string root, CeruleanCaveOpponentPokemonCatalog predecessor)
    {
        var document = Read<LeagueRematchOpponentDocument>(root, "pokemon.json", PokemonSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != LeagueRematchOpponentPokemonCatalog.Profile ||
            document.CanonicalCatalogSha256 != PretCanonicalPokemonCatalog.CatalogSha256 ||
            document.OwnedSpeciesAdded.IsDefault || !document.OwnedSpeciesAdded.IsEmpty)
            throw new InvalidDataException("Pokemon League opponent projection differs.");
        return LeagueRematchOpponentPokemonCatalog.Authenticate(predecessor, document.OpponentSpecies);
    }

    public static JsonElement LoadRules(string root) => Read<JsonElement>(root, "rules.json", RulesSha256);

    public static void ValidateSourceEvidence(string root, string? dependencyRoot = null)
    {
        var inputs = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        if (inputs.GetProperty("sourceCommit").GetString() != PokemonFactory.SourceCommit ||
            inputs.GetProperty("sourceTree").GetString() != PretCanonicalPokemonCatalog.SourceTree)
            throw new InvalidDataException("league_rematch_source_identity");
        foreach (var row in inputs.GetProperty("predecessorInputs").EnumerateArray())
        {
            var path = row.GetProperty("path").GetString()!;
            var bytes = File.ReadAllBytes(Path.Combine(dependencyRoot ?? root, path));
            if (bytes.Length != row.GetProperty("byteLength").GetInt32() ||
                Convert.ToHexStringLower(SHA256.HashData(bytes)) != row.GetProperty("sha256").GetString())
                throw new InvalidDataException("league_rematch_dependency_source:" + path);
        }
        _ = LoadRules(root);
    }
}
