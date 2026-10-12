using System.Collections.Immutable;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Trainers;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record PokemonLeagueTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves);

public sealed record PokemonLeagueOpponentDocument(string SourceCommit, string Profile, string CanonicalCatalogSha256,
    ImmutableArray<string> OwnedSpeciesAdded, ImmutableArray<PokemonSpeciesDefinition> OpponentSpecies);

/// <summary>Exact first League source teams and the minimal trainer-only species continuation.</summary>
public static class PretPokemonLeagueRules
{
    public const string TrainersSha256 = "2266f364fda5898c0f8c817a5164a2019392f6505b358dd1911357e81af335e0";
    public const string SourceInputsSha256 = "e1326d9de999b4e1be9280984be5ae33b88a8c272b679c03fe8b6d3112fe0414";

    public const string PokemonSha256 = "f3df67bfef5e83b8656437018f95f646b8d5041b7ae997170a73852afa935405";
    public const string RulesSha256 = "6a09b00a20b3fb7bc120ff2027d365164e8112f1059d79615e5bb697adf56664";

    private static T Read<T>(string root, string file, string hash)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/pokemon-league-rules", file));
        if (Convert.ToHexStringLower(SHA256.HashData(bytes)) != hash)
            throw new InvalidDataException("Pokemon League source bytes differ: " + file);
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase, Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } })
            ?? throw new InvalidDataException("Missing Pokemon League source document.");
    }

    public static PokemonLeagueTrainerDocument LoadDocument(string root)
    {
        var document = Read<PokemonLeagueTrainerDocument>(root, "trainers.json", TrainersSha256);
        _ = LoadTrainers(document);
        return document;
    }

    public static PokemonLeagueTrainerCatalog LoadTrainers(string root) =>
        LoadTrainers(Read<PokemonLeagueTrainerDocument>(root, "trainers.json", TrainersSha256));

    private static PokemonLeagueTrainerCatalog LoadTrainers(PokemonLeagueTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty)
            throw new InvalidDataException("Missing Pokemon League trainer or battle records.");
        var result = new PokemonLeagueTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        PokemonLeagueTrainerPartyFactory.ValidateCatalog(result);
        return result;
    }

    public static PokemonLeagueOpponentPokemonCatalog LoadOpponents(string root, VictoryRoadOpponentPokemonCatalog predecessor)
    {
        var document = Read<PokemonLeagueOpponentDocument>(root, "pokemon.json", PokemonSha256);
        if (document.SourceCommit != PokemonFactory.SourceCommit || document.Profile != PokemonLeagueOpponentPokemonCatalog.Profile ||
            document.CanonicalCatalogSha256 != PretCanonicalPokemonCatalog.CatalogSha256 ||
            document.OwnedSpeciesAdded.IsDefault || !document.OwnedSpeciesAdded.IsEmpty)
            throw new InvalidDataException("Pokemon League opponent projection differs.");
        return PokemonLeagueOpponentPokemonCatalog.Authenticate(predecessor, document.OpponentSpecies);
    }

    public static JsonElement LoadRules(string root) => Read<JsonElement>(root, "rules.json", RulesSha256);

    public static void ValidateSourceEvidence(string root)
    {
        _ = Read<JsonElement>(root, "source-input-hashes.json", SourceInputsSha256);
        _ = LoadRules(root);
    }
}
