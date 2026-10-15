using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>One original shared trainer and its two first-visit interaction scripts.</summary>
public sealed record Route16FlyTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<string, CeruleanTrainerBattleDefinition> BattlesByScript);

public static class Route16FlyTrainerPartyFactory
{
    public static TrainerId TrainerId { get; } = new("pret:TRAINER_YOUNG_COUPLE_LEA_JED");
    public const int SharedTrainerFlag = 0x500 + 489;
    public const int VictoryMoney = 1624;
    private static readonly ImmutableHashSet<string> BattleScripts = ImmutableHashSet.Create<string>(StringComparer.Ordinal,
        "Route16_EventScript_Lea",
        "Route16_EventScript_Jed");

    public static void ValidateCatalog(Route16FlyTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesByScript.Values, null);
    }

    public static TrainerPartyCreationResult Create(Route16FlyTrainerCatalog trainers,
        GamePokemonCatalog owned, RngState rng)
    {
        ValidateCatalog(trainers);
        if (owned is null || trainers.Trainers[TrainerId].Party.Any(p => !owned.Opponents.Catalog.Species.ContainsKey(p.Species)))
            throw new ArgumentException("route16_double_owned_catalog");
        // The shared generator mints the same private source-party receipt used by ordinary trainers.
        return TrainerPartyFactory.CreateValidated(trainers.Trainers[TrainerId], owned.Mechanics.Catalog, rng, owned.Mechanics);
    }
}
