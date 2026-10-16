using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

public sealed record PewterTrainerBattleDefinition(TrainerId Trainer, int Mode, uint BattleTypeFlags,
    string ScriptEntry, string IntroText, string DefeatText, string PostBattleText,
    string? PostBattleScript, string? MusicOption, string EntryScript, string Callback, TrainerSourceRef Source);
public sealed record PewterTrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, PewterTrainerBattleDefinition> Battles);

/// <summary>The two original Pewter Gym custom-move, no-item parties. Existing lab/forest factories are unchanged.</summary>
public static class PewterTrainerPartyFactory
{
    public static TrainerId Liam { get; } = new("pret:TRAINER_CAMPER_LIAM");
    public static TrainerId Brock { get; } = new("pret:TRAINER_LEADER_BROCK");
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } = [Liam, Brock];

    public static TrainerPartyCreationResult Create(PewterTrainerCatalog trainers, PokemonCatalog pokemon,
        TrainerId trainerId, RngState rng)
    {
        ValidateCatalog(trainers);
        PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(trainerId, out var trainer))
            throw new NotSupportedException("Only Liam and Brock are supported by this trainer profile.");
        // Authenticate the executable six-move closure before any random OT draw.
        foreach (var member in trainer.Party)
            foreach (var id in member.CustomMoves.Where(m => m.Value != "pret:MOVE_NONE"))
                if (!pokemon.Moves.TryGetValue(id, out var move) || !PewterTrainerAi.ValidMove(move))
                    throw new NotSupportedException("Pewter custom move metadata differs from source.");
        var created = TrainerPartyFactory.CreateValidated(trainer, pokemon, rng);
        // SetMonMoveSlot after CreateMon changes no PID, IV, OT or RNG. The persisted representation
        // omits the source trailing MOVE_NONE slots, as the existing PokemonInstance contract requires.
        return created with { Party = created.Party.Select((member, i) => member with {
            Pokemon = member.Pokemon with { Moves = trainer.Party[i].CustomMoves
                .TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, pokemon.Moves[m].Pp, pokemon.Moves[m].Pp)).ToImmutableArray() }
        }).ToImmutableArray() };
    }

    public static void ValidateCatalog(PewterTrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }

    public static void ValidateBattle(PewterTrainerBattleDefinition battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        if (!SupportedTrainers.Contains(battle.Trainer)) throw new NotSupportedException("Unknown Pewter trainer.");
        bool brock = battle.Trainer == Brock;
        string name = brock ? "Brock" : "Liam";
        if (battle.Mode != (brock ? 1 : 0) || battle.BattleTypeFlags != 8 ||
            battle.ScriptEntry != "PewterCity_Gym_EventScript_" + name ||
            battle.IntroText != "PewterCity_Gym_Text_" + name + "Intro" ||
            battle.DefeatText != "PewterCity_Gym_Text_" + name + "Defeat" ||
            battle.PostBattleText != "PewterCity_Gym_Text_" + name + "PostBattle" ||
            battle.PostBattleScript != (brock ? "PewterCity_Gym_EventScript_DefeatedBrock" : null) ||
            battle.MusicOption != (brock ? "NO_MUSIC" : null) ||
            battle.EntryScript != "EventScript_TryDoNormalTrainerBattle" || battle.Callback != "CB2_EndTrainerBattle")
            throw new NotSupportedException("Unsupported Pewter battle script parameters.");
    }

}
