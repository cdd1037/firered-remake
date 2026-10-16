using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Creates regional trainer parties from the shared definitions and canonical Pokemon mechanics.</summary>
public static class FirstSeviiTrainerPartyFactory
{

    public static TrainerPartyCreationResult Create(FirstSeviiTrainerCatalog trainers, GameOpponentPokemonCatalog opponents,
        TrainerId id, RngState rng)
    {
        ArgumentNullException.ThrowIfNull(trainers);
        if (opponents is null || !trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("first_sevii_trainer_catalog");
        var source = opponents.Mechanics;
        foreach (var member in trainer.Party)
            if (!opponents.Catalog.Species.ContainsKey(member.Species)) throw new NotSupportedException("first_sevii_trainer_species");
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        if ((trainer.PartyFlags & 1) == 0) return made;
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, source.Catalog.Moves[m].Pp, source.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(FirstSeviiTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesBySource.Values, null);
        if (catalog.BattlesBySource.Any(pair => pair.Key != FirstSeviiBattleKey.For(pair.Value)))
            throw new ArgumentException("first_sevii_trainer_occurrence_key");
    }
}
