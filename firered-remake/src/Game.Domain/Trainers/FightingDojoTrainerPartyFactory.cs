using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;
namespace Game.Domain.Trainers;

/// <summary>Exact original Dojo singles through the existing trainer construction kernel.</summary>
public static partial class FightingDojoTrainerPartyFactory
{
    public static ImmutableArray<TrainerId> AllTrainers { get; } = new[] { "KOICHI", "MIKE", "HIDEKI", "AARON", "HITOSHI" }.Select(n => new TrainerId("pret:TRAINER_BLACK_BELT_" + n)).ToImmutableArray();
    public static ImmutableArray<TrainerId> SupportedTrainers => AllTrainers;
    public static int BasePrize(FightingDojoTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("fighting_dojo_trainer_id");
        return checked((trainer.DoubleBattle ? 8 : 4) * trainer.BaseMoney * trainer.Party[^1].Level);
    }
    public static TrainerPartyCreationResult Create(FightingDojoTrainerCatalog trainers,
        GameOpponentPokemonCatalog opponents, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("fighting_dojo_trainer_catalog");
        foreach (var member in trainer.Party)
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("fighting_dojo_trainer_species");
        var source = opponents.Mechanics;
        return TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
    }

    public static void ValidateCatalog(FightingDojoTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesByScript.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleScripts.Contains(battle.ScriptEntry))
            throw new NotSupportedException("fighting_dojo_trainer_battle_source");
    }
}
