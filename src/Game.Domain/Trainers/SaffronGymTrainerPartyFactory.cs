using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Creates regional trainer parties from the shared definitions and canonical Pokemon mechanics.</summary>
public static class SaffronGymTrainerPartyFactory
{
    public static TrainerId Sabrina { get; } = new("pret:TRAINER_LEADER_SABRINA");

    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = [
        new("pret:TRAINER_CHANNELER_AMANDA"),
        new("pret:TRAINER_CHANNELER_STACY"),
        new("pret:TRAINER_CHANNELER_TASHA"),
        new("pret:TRAINER_LEADER_SABRINA"),
        new("pret:TRAINER_PSYCHIC_CAMERON"),
        new("pret:TRAINER_PSYCHIC_JOHAN"),
        new("pret:TRAINER_PSYCHIC_PRESTON"),
        new("pret:TRAINER_PSYCHIC_TYRON")
    ];
    public static TrainerPartyCreationResult Create(SaffronGymTrainerCatalog trainers, GameOpponentPokemonCatalog opponents,
        TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (opponents is null || !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("saffron_gym_trainer_catalog");
        var source = opponents.Mechanics;
        foreach (var member in trainer.Party)
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("saffron_gym_trainer_species");
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        if ((trainer.PartyFlags & 1) == 0) return made;
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, source.Catalog.Moves[m].Pp, source.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(SaffronGymTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !SupportedTrainers.Contains(battle.Trainer))
            throw new NotSupportedException("saffron_gym_trainer_battle_source");
    }
}
