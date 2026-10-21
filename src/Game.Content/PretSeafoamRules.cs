using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record SeafoamTrainerDocument(ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles) : RegionalTrainerDocument(Trainers, Battles);

/// <summary>Initial regional trainer definitions and battle occurrences.</summary>
public static class PretSeafoamRules
{
    private static readonly RegionalTrainerLoader<SeafoamTrainerDocument, SeafoamTrainerCatalog> Loader = new(
        "content/generated/data/regions/seafoam/trainers.json", "seafoam_source_missing",
        document => document.ByScript((trainers, battles) => new SeafoamTrainerCatalog(trainers, battles),
            "seafoam_trainers_missing"), SeafoamTrainerPartyFactory.ValidateCatalog);

    public static SeafoamTrainerDocument LoadDocument(string root) => Loader.LoadDocument(root);
    public static SeafoamTrainerCatalog LoadTrainers(string root) => Loader.LoadCatalog(root);
}
