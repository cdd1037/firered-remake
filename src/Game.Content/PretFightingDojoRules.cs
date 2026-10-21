using System.Collections.Immutable;
using Game.Domain.Trainers;

namespace Game.Content;

public sealed record FightingDojoTrainerDocument(ImmutableArray<TrainerDefinition> Trainers,
    ImmutableArray<CeruleanTrainerBattleDefinition> Battles) : RegionalTrainerDocument(Trainers, Battles);

/// <summary>Initial regional trainer definitions and battle occurrences.</summary>
public static class PretFightingDojoRules
{
    private static readonly RegionalTrainerLoader<FightingDojoTrainerDocument, OrdinaryTrainerCatalog> Loader = new(
        "content/generated/data/regions/fighting-dojo/trainers.json", "fighting_dojo_source_missing",
        document =>
        {
            if (document.Trainers.IsDefaultOrEmpty || document.Battles.IsDefaultOrEmpty)
                throw new InvalidDataException("fighting_dojo_trainers_missing");
            return new(document.Trainers.ToImmutableDictionary(t => t.Id), document.Battles, "fighting-dojo-trainer");
        }, OrdinaryTrainerSpecialSourceValidation.Validate);

    public static FightingDojoTrainerDocument LoadDocument(string root) => Loader.LoadDocument(root);
    public static OrdinaryTrainerCatalog LoadTrainers(string root) => Loader.LoadCatalog(root);
}
