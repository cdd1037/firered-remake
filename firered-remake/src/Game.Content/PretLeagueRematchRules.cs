using System.Collections.Immutable;
using System.Text.Json;
using System.Text.Json.Serialization;
using Game.Domain.Trainers;
using Game.Domain.Pokemon;

namespace Game.Content;

public sealed record LeagueRematchTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves);

public sealed record LeagueRematchOpponentDocument(string SourceCommit, string Profile, ImmutableArray<string> OwnedSpeciesAdded,
    ImmutableArray<PokemonSpeciesDefinition> OpponentSpecies);

/// <summary>Exact upgraded League source teams and the minimal trainer-only species continuation.</summary>
public static class PretLeagueRematchRules
{

    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/league-rematch-rules", file));
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase,Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) } })
            ?? throw new InvalidDataException("Missing Pokemon League source document.");
    }

    public static LeagueRematchTrainerDocument LoadDocument(string root)
    {
        var document = Read<LeagueRematchTrainerDocument>(root,"trainers.json");
        _ = LoadTrainers(document);
        return document;
    }

    public static LeagueRematchTrainerCatalog LoadTrainers(string root) =>
        LoadTrainers(Read<LeagueRematchTrainerDocument>(root,"trainers.json"));

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
        var document = Read<LeagueRematchOpponentDocument>(root,"pokemon.json");
        if (            document.OwnedSpeciesAdded.IsDefault || !document.OwnedSpeciesAdded.IsEmpty)
            throw new InvalidDataException("Pokemon League opponent projection differs.");
        return LeagueRematchOpponentPokemonCatalog.Authenticate(predecessor, document.OpponentSpecies);
    }

    public static JsonElement LoadRules(string root) => Read<JsonElement>(root,"rules.json");
}
