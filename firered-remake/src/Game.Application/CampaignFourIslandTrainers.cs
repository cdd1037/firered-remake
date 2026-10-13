using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string FourIslandGruntActor = "LOCALID_ICEFALL_ROCKET3";
    private const int FourIslandGruntBattleLine = 48;

    private bool IsFourIslandTrainer(TrainerId trainer) => _content.FourIslandEnabled &&
        trainer == FourIslandTrainerPartyFactory.Grunt45 &&
        _content.FourIslandTrainers?.Trainers.TryGetValue(trainer, out var source) == true && !source.DoubleBattle;

    private int FourIslandTrainerSourceLine(string script)
    {
        var instruction = script == FourIslandTrainerPartyFactory.BattleScript
            ? _content.FindProgram(script)?.Instructions.SingleOrDefault(i => i.Op == StoryOp.TrainerBattle) : null;
        if (instruction is null || instruction.Opcode != "trainerbattle_no_intro" ||
            instruction.Source.Path != FourIslandBackSource || instruction.Source.Symbol != script ||
            instruction.Source.Line != FourIslandGruntBattleLine ||
            !instruction.Args.SequenceEqual(new[] { "TRAINER_TEAM_ROCKET_GRUNT_45", "FourIsland_IcefallCave_Back_Text_GruntDefeat" }))
            throw new InvalidDataException("four_island_trainer_program_missing");
        return instruction.Source.Line;
    }

    private void RequireFourIslandTrainerOccurrence(CampaignState state, TrainerId trainer,
        ForestTrainerBattleDefinition source, bool noIntro)
    {
        var player = state.World.Player;
        // The coordinate scene starts at (11/12/13,18), then its original player
        // movement walks up twice, right once and up once before this no-intro call.
        // Rocket3 remains a source 0x0 actor; it does not own the scene program.
        if (!IsFourIslandTrainer(trainer) || !noIntro || source.Mode != 3 || source.BattleTypeFlags != 8 ||
            source.ScriptEntry != FourIslandTrainerPartyFactory.BattleScript || player.MapId.Value != FourIslandBackMap ||
            player.X is < 12 or > 14 || player.Y != 15 || player.Elevation != 3 || player.Facing != Direction.North ||
            !state.Script.Locked || state.Script.Wait?.Kind != StoryWaitKind.Battle ||
            state.Script.Stack.LastOrDefault()?.ProgramId != FourIslandTrainerPartyFactory.BattleScript ||
            state.Script.Variables.GetValueOrDefault(FourIslandSceneVariable) != 0 ||
            !NationalDexEnabledIn(state) || !RainbowQuestComplete(state) ||
            OpeningInventoryRules.Quantity(state.Inventory?.KeyItems ?? [], RainbowPassItem) != 1 ||
            state.BattledTrainers.Contains(trainer.Value) || state.Script.Flags.GetValueOrDefault("FLAG_HIDE_RUIN_VALLEY_SCIENTIST") ||
            state.Script.Flags.GetValueOrDefault("FLAG_HIDE_ICEFALL_CAVE_ROCKETS") ||
            state.LastTalked != FourIslandGruntActor || !state.Actors.TryGetValue(FourIslandGruntActor, out var actor) ||
            !actor.Active || actor.ScriptId != "0x0" || actor.SourceLocalId != 4 || actor.X != 10 || actor.Y != 15 ||
            actor.Elevation != 3 || actor.Facing != Direction.East ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId)
            throw new InvalidDataException("four_island_grunt_source_occurrence");
    }

    private bool FourIslandTrainerHistoryOriginMatches(CampaignForestTrainerHistory history) =>
        IsFourIslandTrainer(new(history.TrainerId)) && history.SourceScriptEntry is { } script &&
        script == FourIslandTrainerPartyFactory.BattleScript && history.SourceScriptLine == FourIslandTrainerSourceLine(script) &&
        _content.FourIslandTrainers!.BattlesByScript.TryGetValue(script, out var source) &&
        source.Trainer.Value == history.TrainerId && source.Mode == 3 && source.Source.Path == FourIslandBackSource;
}
