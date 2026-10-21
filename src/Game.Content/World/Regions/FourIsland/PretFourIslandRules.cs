using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record FourIslandTrainerDocument(ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles) : RegionalTrainerDocument(Trainers, Battles);

/// <summary>Initial regional trainer definitions and battle occurrences.</summary>
public static class PretFourIslandRules
{
    private static readonly RegionalTrainerLoader<FourIslandTrainerDocument, OrdinaryTrainerCatalog> Loader = new(
        "content/generated/data/regions/four-island/trainers.json", "Missing Four Island source document.",
        document =>
        {
            if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty)
                throw new InvalidDataException("Missing Four Island trainer records.");
            return new(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles, "four-island-trainer");
        }, OrdinaryTrainerSpecialSourceValidation.Validate);

    public static FourIslandTrainerDocument LoadDocument(string root) => Loader.LoadDocument(root);
    public static OrdinaryTrainerCatalog LoadTrainers(string root) => Loader.LoadCatalog(root);
}
