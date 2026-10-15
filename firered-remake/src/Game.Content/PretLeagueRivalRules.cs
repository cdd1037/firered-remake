using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record LeagueRivalTrainerDocument(string SourceCommit, string Profile, bool CompleteSourceRoster,
    ImmutableArray<TrainerDefinition> Trainers, ImmutableArray<CeruleanTrainerBattleDefinition> Battles,
    JsonElement ResolvedMoves);

/// <summary>Exact late Route22 source teams. Existing owned/opponent species and move metadata remain unchanged.</summary>
public static class PretLeagueRivalRules
{

    private static T Read<T>(string root,string file)
    {
        var bytes = File.ReadAllBytes(Path.Combine(root, "content/generated/league-rival-rules", file));
        return JsonSerializer.Deserialize<T>(bytes, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase })
            ?? throw new InvalidDataException("Missing League rival source document.");
    }

    public static LeagueRivalTrainerDocument LoadDocument(string root)
    {
        var document = Read<LeagueRivalTrainerDocument>(root,"trainers.json");
        _ = LoadTrainers(document);
        return document;
    }

    public static LeagueRivalTrainerCatalog LoadTrainers(string root) =>
        LoadTrainers(Read<LeagueRivalTrainerDocument>(root,"trainers.json"));

    private static LeagueRivalTrainerCatalog LoadTrainers(LeagueRivalTrainerDocument document)
    {
        if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty)
            throw new InvalidDataException("Missing League rival trainer or battle records.");
        var result = new LeagueRivalTrainerCatalog(document.SourceCommit, document.Profile, document.CompleteSourceRoster,
            document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles.ToImmutableDictionary(b => b.Trainer));
        LeagueRivalTrainerPartyFactory.ValidateCatalog(result);
        return result;
    }
}
