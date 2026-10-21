using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record SixIslandTrainerDocument(ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles) : RegionalTrainerDocument(Trainers, Battles);

/// <summary>Initial regional trainer definitions and battle occurrences.</summary>
public static class PretSixIslandRules
{
    private static readonly RegionalTrainerLoader<SixIslandTrainerDocument, SixIslandTrainerCatalog> Loader = new(
        "content/generated/data/regions/six-island/trainers.json", "Missing Six Island source document.",
        document => document.ByScript((trainers, battles) => new SixIslandTrainerCatalog(trainers, battles),
            "Missing Six Island trainer records."), SixIslandTrainerPartyFactory.ValidateCatalog);

    public static SixIslandTrainerDocument LoadDocument(string root) => Loader.LoadDocument(root);
    public static SixIslandTrainerCatalog LoadTrainers(string root) => Loader.LoadCatalog(root);
}
