using Game.Domain.WildBattle;
using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.Trainers;

/// <summary>Source early-rival script parameters. This is data; it does not execute victory/whiteout callbacks.</summary>
public sealed record Route22TrainerBattleDefinition(TrainerId Trainer, int PlayerStarterVar, SpeciesId PlayerStarter,
    int Mode, int RivalFlags, uint BattleTypeFlags, int WonTrainerFlag, int BasePrizeMoney,
    string ScriptEntry, string DefeatText, string VictoryText, string EntryScript, string Callback, TrainerSourceRef Source);
public sealed record Route22TrainerCatalog(ImmutableDictionary<TrainerId, TrainerDefinition> Trainers,
    ImmutableDictionary<TrainerId, Route22TrainerBattleDefinition> Battles);

/// <summary>Only the three source Route22 early-rival custom-move parties; never a level-nine natural learnset.</summary>
public static class Route22TrainerPartyFactory
{
    public static ImmutableArray<TrainerId> SupportedTrainers { get; } =
        new[] { "SQUIRTLE", "BULBASAUR", "CHARMANDER" }
            .Select(x => new TrainerId("pret:TRAINER_RIVAL_ROUTE22_EARLY_" + x)).ToImmutableArray();

    public static TrainerId ForPlayerStarter(SpeciesId starter) => starter.Value switch
    {
        "pret:SPECIES_BULBASAUR" => SupportedTrainers[0],
        "pret:SPECIES_CHARMANDER" => SupportedTrainers[1],
        "pret:SPECIES_SQUIRTLE" => SupportedTrainers[2],
        _ => throw new NotSupportedException("Only the three original player starters select an early Route22 rival.")
    };

    public static TrainerPartyCreationResult Create(Route22TrainerCatalog trainers, PokemonCatalog pokemon,
        TrainerId trainerId, RngState rng)
    {

        ValidateCatalog(trainers);
        PokemonFactory.ValidateCatalog(pokemon);
        if (!trainers.Trainers.TryGetValue(trainerId, out var trainer))
            throw new NotSupportedException("Only the three Route22 early rivals are supported.");
        foreach (var member in trainer.Party)
        {
            if (!pokemon.Species.TryGetValue(member.Species, out var species) || !Route22TrainerAi.ValidSpecies(species))
                throw new NotSupportedException("Rival species metadata differs from the pinned source.");
            foreach (var id in member.CustomMoves.Where(m => m.Value != "pret:MOVE_NONE"))
                if (!pokemon.Moves.TryGetValue(id, out var move) || !Route22TrainerAi.ValidMove(move))
                    throw new NotSupportedException("Rival custom move metadata differs from the pinned source.");
        }
        var created = TrainerPartyFactory.CreateValidated(trainer, pokemon, rng);
        // Source SetMonData follows CreateMon. Compact persisted slots omit two trailing MOVE_NONE.
        return created with { Party = created.Party.Select((member, i) => member with {
            Pokemon = member.Pokemon with { Moves = trainer.Party[i].CustomMoves.TakeWhile(m => m.Value != "pret:MOVE_NONE")
                .Select(m => new PokemonMoveSlot(m, pokemon.Moves[m].Pp, pokemon.Moves[m].Pp)).ToImmutableArray() }
        }).ToImmutableArray() };
    }

    public static void ValidateCatalog(Route22TrainerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        TrainerCatalogValidation.Validate(catalog.Trainers, catalog.Battles.Values, ValidateBattle);
    }

    public static void ValidateBattle(Route22TrainerBattleDefinition battle)
    {
        ArgumentNullException.ThrowIfNull(battle);
        int branch = SupportedTrainers.IndexOf(battle.Trainer);
        if (branch < 0) throw new NotSupportedException("Unknown Route22 early rival.");
        string rival = branch switch { 0 => "Squirtle", 1 => "Bulbasaur", _ => "Charmander" };
        string starter = branch switch { 0 => "BULBASAUR", 1 => "CHARMANDER", _ => "SQUIRTLE" };
        if (battle.PlayerStarterVar != 2 - branch || battle.PlayerStarter.Value != "pret:SPECIES_" + starter ||
            battle.Mode != 9 || battle.RivalFlags != 0 || battle.BattleTypeFlags != 8 || battle.WonTrainerFlag != 1609 + branch ||
            battle.BasePrizeMoney != 144 || battle.ScriptEntry != "Route22_EventScript_EarlyRival" + rival ||
            battle.DefeatText != "Route22_Text_EarlyRivalDefeat" || battle.VictoryText != "Text_RivalVictory" ||
            battle.EntryScript != "EventScript_DoNoIntroTrainerBattle" || battle.Callback != "CB2_EndTrainerBattle")
            throw new NotSupportedException("Route22 battle declaration differs; this is not the lab tutorial/heal battle.");
    }

}
