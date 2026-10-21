using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record FiveIslandTrainerDocument(ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles) : RegionalTrainerDocument(Trainers, Battles);

/// <summary>Initial regional trainer definitions and battle occurrences.</summary>
public static class PretFiveIslandRules
{
    private static readonly RegionalTrainerLoader<FiveIslandTrainerDocument, OrdinaryTrainerCatalog> Loader = new(
        "content/generated/data/regions/five-island/trainers.json", "Missing Five Island source document.",
        document =>
        {
            if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty)
                throw new InvalidDataException("Missing Five Island trainer records.");
            return new(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles, "five-island-trainer");
        }, OrdinaryTrainerSpecialSourceValidation.Validate);

    public static FiveIslandTrainerDocument LoadDocument(string root) => Loader.LoadDocument(root);
    public static OrdinaryTrainerCatalog LoadTrainers(string root) => Loader.LoadCatalog(root);
}
