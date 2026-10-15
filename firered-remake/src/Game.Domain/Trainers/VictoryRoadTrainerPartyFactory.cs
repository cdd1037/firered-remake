using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace Game.Domain.Trainers;

/// <summary>Creates regional trainer parties from the shared definitions and canonical Pokemon mechanics.</summary>
public static class VictoryRoadTrainerPartyFactory
{

    private static readonly ImmutableHashSet<string> BattleKeys = ImmutableHashSet.Create<string>(StringComparer.Ordinal,
        "VictoryRoad_1F_EventScript_Rolando",
        "VictoryRoad_1F_EventScript_Naomi",
        "VictoryRoad_2F_EventScript_Dawson",
        "VictoryRoad_2F_EventScript_Daisuke",
        "VictoryRoad_2F_EventScript_Nelson",
        "VictoryRoad_2F_EventScript_Vincent",
        "VictoryRoad_2F_EventScript_Gregory",
        "VictoryRoad_3F_EventScript_George",
        "VictoryRoad_3F_EventScript_Alexa",
        "VictoryRoad_3F_EventScript_Colby",
        "VictoryRoad_3F_EventScript_Caroline",
        "VictoryRoad_3F_EventScript_Ray",
        "VictoryRoad_3F_EventScript_Tyra");
    public static ImmutableArray<TrainerId> AllTrainers { get; } = [
        new("pret:TRAINER_BLACK_BELT_DAISUKE"),
        new("pret:TRAINER_COOLTRAINER_ALEXA"),
        new("pret:TRAINER_COOLTRAINER_CAROLINE"),
        new("pret:TRAINER_COOLTRAINER_COLBY"),
        new("pret:TRAINER_COOLTRAINER_GEORGE"),
        new("pret:TRAINER_COOLTRAINER_NAOMI"),
        new("pret:TRAINER_COOLTRAINER_ROLANDO"),
        new("pret:TRAINER_COOL_COUPLE_RAY_TYRA"),
        new("pret:TRAINER_JUGGLER_GREGORY"),
        new("pret:TRAINER_JUGGLER_NELSON"),
        new("pret:TRAINER_POKEMANIAC_DAWSON"),
        new("pret:TRAINER_TAMER_VINCENT")
    ];
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = AllTrainers.Where(t => t != VictoryRoadDoubleTrainerEncounter.TrainerId).ToImmutableArray();
    public static int BasePrize(VictoryRoadTrainerCatalog trainers, TrainerId id)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("victory_road_trainer_id");
        return checked((trainer.DoubleBattle ? 8 : 4) * trainer.BaseMoney * trainer.Party[^1].Level);
    }
    /// <summary>Preserves the existing Cinnabar-scoped Ray/Tyra construction and predecessor callers.</summary>
    public static TrainerPartyCreationResult Create(VictoryRoadTrainerCatalog trainers, GameOpponentPokemonCatalog opponents,
        TrainerId id, RngState rng)
    {
        if (opponents is null)
            throw new NotSupportedException("victory_road_trainer_catalog");
        return CreateCore(trainers, opponents.Catalog, opponents.Mechanics, id, rng);
    }

    private static TrainerPartyCreationResult CreateCore(VictoryRoadTrainerCatalog trainers, PokemonCatalog opponents,
        SourceMechanicsCatalog source, TrainerId id, RngState rng)
    {
        ValidateCatalog(trainers);
        if (!trainers.Trainers.TryGetValue(id, out var trainer)) throw new NotSupportedException("victory_road_trainer_catalog");
        bool customMoves = (trainer.PartyFlags & 1) != 0;
        foreach (var member in trainer.Party)
            if (!opponents.Species.ContainsKey(member.Species)) throw new NotSupportedException("victory_road_trainer_species");
        var made = TrainerPartyFactory.CreateValidated(trainer, source.Catalog, rng, source);
        // PartyFlags=2 has a held item but retains the NPC factory's level-up moves.
        if (!customMoves) return made;
        return made with { Party = made.Party.Select((p, i) => p with { Pokemon = p.Pokemon with {
            Moves = trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, source.Catalog.Moves[m].Pp, source.Catalog.Moves[m].Pp)).ToImmutableArray()
        } }).ToImmutableArray() };
    }
    public static void ValidateCatalog(VictoryRoadTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.BattlesByScript.Values, ValidateBattle);
    }
    public static void ValidateBattle(CeruleanTrainerBattleDefinition battle)
    {
        if (battle is null || !BattleKeys.Contains(battle.ScriptEntry))
            throw new NotSupportedException("victory_road_trainer_battle_source");
    }
}
