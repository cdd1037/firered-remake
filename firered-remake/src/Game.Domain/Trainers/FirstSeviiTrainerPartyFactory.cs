using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Creates regional trainer parties from the shared definitions and canonical Pokemon mechanics.</summary>
public static class FirstSeviiTrainerPartyFactory
{

    private static readonly ImmutableHashSet<FirstSeviiBattleKey> BattleKeys = ImmutableHashSet.CreateRange<FirstSeviiBattleKey>([
        new("ThreeIsland_BondBridge_EventScript_Alexis",2174),
        new("ThreeIsland_BondBridge_EventScript_Amira",2162),
        new("ThreeIsland_BondBridge_EventScript_Joy",2198),
        new("ThreeIsland_BondBridge_EventScript_Meg",2210),
        new("ThreeIsland_BondBridge_EventScript_Nikki",2138),
        new("ThreeIsland_BondBridge_EventScript_Tisha",2186),
        new("ThreeIsland_BondBridge_EventScript_Violet",2150),
        new("ThreeIsland_EventScript_BattleBikersScene",211),
        new("ThreeIsland_EventScript_BattleBikersScene",221),
        new("ThreeIsland_EventScript_BattleBikersScene",231),
        new("ThreeIsland_EventScript_BattleBikersScene",248)]);
    public static ImmutableArray<TrainerId> AllTrainers { get; } = [
        new("pret:TRAINER_AROMA_LADY_NIKKI"),
        new("pret:TRAINER_AROMA_LADY_VIOLET"),
        new("pret:TRAINER_BIKER_GOON"),
        new("pret:TRAINER_BIKER_GOON_2"),
        new("pret:TRAINER_BIKER_GOON_3"),
        new("pret:TRAINER_CUE_BALL_PAXTON"),
        new("pret:TRAINER_SWIMMER_FEMALE_TISHA"),
        new("pret:TRAINER_TUBER_ALEXIS"),
        new("pret:TRAINER_TUBER_AMIRA"),
        new("pret:TRAINER_TWINS_JOY_MEG")
    ];
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = AllTrainers.Where(t => t != FirstSeviiDoubleTrainerEncounter.TrainerId).ToImmutableArray();
    public static TrainerPartyCreationResult Create(FirstSeviiTrainerCatalog trainers, GameOpponentPokemonCatalog opponents,
        TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
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
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesBySource.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleKeys.Contains(FirstSeviiBattleKey.For(battle)))
            throw new NotSupportedException("first_sevii_trainer_battle_source");
    }
}
