using System.Collections.Immutable;
using System.Text.Json;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record LeagueRivalTrainerDocument(ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles) : RegionalTrainerDocument(Trainers, Battles);

/// <summary>Regional trainer definitions and battle occurrences.</summary>
public static class PretLeagueRivalRules
{
    private static readonly RegionalTrainerLoader<LeagueRivalTrainerDocument, LeagueRivalTrainerCatalog> Loader = new(
        "content/generated/data/trainer-battles/league-rival-rules.json", "Missing League rival source document.",
        document => document.ByTrainer((trainers, battles) => new LeagueRivalTrainerCatalog(trainers, battles),
            "Missing League rival trainer or battle records."), LeagueRivalTrainerPartyFactory.ValidateCatalog);

    public static LeagueRivalTrainerDocument LoadDocument(string root) => Loader.LoadDocument(root, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    public static LeagueRivalTrainerCatalog LoadTrainers(string root) => Loader.LoadCatalog(root, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
}
