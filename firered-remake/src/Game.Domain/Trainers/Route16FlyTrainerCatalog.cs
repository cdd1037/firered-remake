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
    private static readonly ImmutableDictionary<string, string> BattleHashes = new Dictionary<string, string> {
        ["Route16_EventScript_Lea"] = "2981443dbe0ff44883a1775cc1880d6b234415fb88b8201d88bda71059296799",
        ["Route16_EventScript_Jed"] = "711b51de8a91372a2559e3c36d20a6400e39727ce3af263b247ce5c703a44860",
    }.ToImmutableDictionary(StringComparer.Ordinal);

    public static void ValidateCatalog(Route16FlyTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (catalog.SourceCommit != TrainerPartyFactory.SourceCommit || catalog.Profile != Profile ||
            catalog.Trainers is null || catalog.Trainers.Count != 1 || catalog.BattlesByScript is null || catalog.BattlesByScript.Count != 2 ||
            !catalog.Trainers.TryGetValue(TrainerId, out var trainer) || trainer is null ||
            CampaignSourceFingerprint.Compute(trainer) != "52aceb08c9ea7a52fee686499f0e208158f03b306f9d38a2eb30704156edf412")
            throw new ArgumentException("route16_double_trainer_source");
        foreach (var (script, hash) in BattleHashes)
            if (!catalog.BattlesByScript.TryGetValue(script, out var battle) || battle is null ||
                CampaignSourceFingerprint.Compute(battle) != hash)
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
