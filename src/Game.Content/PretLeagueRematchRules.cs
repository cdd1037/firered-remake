using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record LeagueRematchTrainerDocument(ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles) : RegionalTrainerDocument(Trainers, Battles);

/// <summary>Regional trainer definitions and battle occurrences.</summary>
public static class PretLeagueRematchRules
{
    private static readonly RegionalTrainerLoader<LeagueRematchTrainerDocument, LeagueRematchTrainerCatalog> Loader = new(
        "content/generated/data/regions/league-rematch/trainers.json", "Missing Pokemon League source document.",
        document => document.ByTrainer((trainers, battles) => new LeagueRematchTrainerCatalog(trainers, battles),
            "Missing Pokemon League trainer or battle records."), LeagueRematchTrainerPartyFactory.ValidateCatalog);

    public static LeagueRematchTrainerDocument LoadDocument(string root) => Loader.LoadDocument(root);
    public static LeagueRematchTrainerCatalog LoadTrainers(string root) => Loader.LoadCatalog(root);
}
