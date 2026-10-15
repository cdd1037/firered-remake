using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Creates regional trainer parties from the shared definitions and canonical Pokemon mechanics.</summary>
public static class ViridianGymTrainerPartyFactory
{
    public static TrainerId Giovanni { get; } = new("pret:TRAINER_LEADER_GIOVANNI");

    private static readonly ImmutableHashSet<string> BattleKeys = ImmutableHashSet.Create<string>(StringComparer.Ordinal,
        "ViridianCity_Gym_EventScript_Giovanni",
        "ViridianCity_Gym_EventScript_Jason",
        "ViridianCity_Gym_EventScript_Cole",
        "ViridianCity_Gym_EventScript_Atsushi",
        "ViridianCity_Gym_EventScript_Kiyo",
        "ViridianCity_Gym_EventScript_Takashi",
        "ViridianCity_Gym_EventScript_Samuel",
        "ViridianCity_Gym_EventScript_Yuji",
        "ViridianCity_Gym_EventScript_Warren");
    public static ImmutableArray<TrainerId> AllTrainers { get; } = [
        new("pret:TRAINER_BLACK_BELT_ATSUSHI"),
        new("pret:TRAINER_BLACK_BELT_KIYO"),
        new("pret:TRAINER_BLACK_BELT_TAKASHI"),
        new("pret:TRAINER_COOLTRAINER_SAMUEL"),
        new("pret:TRAINER_COOLTRAINER_WARREN"),
        new("pret:TRAINER_COOLTRAINER_YUJI"),
        new("pret:TRAINER_LEADER_GIOVANNI"),
        new("pret:TRAINER_TAMER_COLE"),
        new("pret:TRAINER_TAMER_JASON")
    ];
    public static ImmutableArray<TrainerId> SupportedTrainers => AllTrainers;
    /// <summary>The eight regular trainers marked fought by set_gym_trainers 8 after Giovanni's win.</summary>
    public static ImmutableArray<TrainerId> GymTrainers { get; } = AllTrainers.Where(t => t != Giovanni).ToImmutableArray();
    public static int BasePrize(ViridianGymTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("viridian_gym_trainer_id");
        return checked((trainer.DoubleBattle ? 8 : 4) * trainer.BaseMoney * trainer.Party[^1].Level);
    }
    public static TrainerPartyCreationResult Create(ViridianGymTrainerCatalog trainers, GameOpponentPokemonCatalog opponents,
        TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("viridian_gym_trainer_catalog");
        var source = opponents.Mechanics;
        bool customMoves = (trainer.PartyFlags & 1) != 0;
        foreach (var member in trainer.Party)
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("viridian_gym_trainer_species");
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        // PartyFlags=2 has a held item but retains the NPC factory's level-up moves.
        if (!customMoves) return made;
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, source.Catalog.Moves[m].Pp, source.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(ViridianGymTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesByScript.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleKeys.Contains(battle.ScriptEntry))
            throw new NotSupportedException("viridian_gym_trainer_battle_source");
    }
}
