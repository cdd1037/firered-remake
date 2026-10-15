using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>One original shared trainer and its two first-visit interaction scripts.</summary>
public sealed record Route16FlyTrainerCatalog(string SourceCommit, string Profile,
    ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<string, CeruleanTrainerBattleDefinition> BattlesByScript);

public static class Route16FlyTrainerPartyFactory
{
    public const string Profile = "firered-route16-fly-house-v1";
    public static TrainerId TrainerId { get; } = new("pret:TRAINER_YOUNG_COUPLE_LEA_JED");
    public const int SharedTrainerFlag = 0x500 + 489;
    public const int VictoryMoney = 1624;
    private static readonly ImmutableHashSet<string> BattleScripts = ImmutableHashSet.Create<string>(StringComparer.Ordinal,
        "Route16_EventScript_Lea",
        "Route16_EventScript_Jed");

    public static void ValidateCatalog(Route16FlyTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != TrainerPartyFactory.SourceCommit || catalog.Profile != Profile ||
            catalog.Trainers is null || catalog.Trainers.Count != 1 || catalog.BattlesByScript is null || catalog.BattlesByScript.Count != 2 ||
            !catalog.Trainers.TryGetValue(TrainerId, out var trainer) || trainer is null)
            throw new ArgumentException("route16_double_trainer_source");
        foreach (var script in BattleScripts)
            if (!catalog.BattlesByScript.TryGetValue(script, out var battle) || battle is null)
                throw new ArgumentException("route16_double_script_source");
    }

    public static TrainerPartyCreationResult Create(Route16FlyTrainerCatalog trainers,
        OwnedBattlePokemonCatalog owned, RngState rng)
    {
        ValidateCatalog(trainers);
        if (owned is null || !ReferenceEquals(OwnedBattlePokemonCatalog.For(owned.Catalog), owned) ||
            !OwnedBattlePokemonCatalog.IsRegisteredProfileOrSuccessor(owned.RulesVersion, StoneEvolutionOwnedPokemonCatalog.Profile) ||
            trainers.Trainers[TrainerId].Party.Any(p => !owned.AdmitsTrainerOpponent(p.Species, owned.TrainerOpponentProfile)))
            throw new ArgumentException("route16_double_owned_catalog");
        // The shared generator mints the same private source-party receipt used by ordinary trainers.
        return TrainerPartyFactory.CreateValidated(trainers.Trainers[TrainerId], owned.Mechanics.Catalog, rng, owned.Mechanics);
    }
}
