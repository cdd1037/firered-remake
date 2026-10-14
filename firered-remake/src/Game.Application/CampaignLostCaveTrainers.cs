using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string LostCaveSelphyActor = "LOCALID_LOST_CAVE_SELPHY";
    private const string LostCaveSelphyScene = "FiveIsland_LostCave_Room10_EventScript_FindSelphyScene";
    private const string LostCaveSelphySceneVariable = "VAR_MAP_SCENE_FIVE_ISLAND_LOST_CAVE_ROOM10";
    private bool IsLostCaveTrainer(TrainerId trainer) => _content.LostCaveEnabled &&
        _content.LostCaveTrainers?.Trainers.ContainsKey(trainer) == true;

    private int LostCaveTrainerSourceLine(string script)
    {
        if (_content.LostCaveTrainers?.BattlesByScript.TryGetValue(script, out var source) != true || source is null)
            throw new InvalidDataException("lost_cave_trainer_script_missing");
        LostCaveTrainerPartyFactory.ValidateBattle(source);
        bool noIntro = source.Mode == 3;
        return RequireTrainerInstruction(source, script, noIntro ? "trainerbattle_no_intro" : "trainerbattle_single",
            noIntro ? [source.Trainer.Value[5..], source.DefeatText] : [source.Trainer.Value[5..], source.IntroText, source.DefeatText],
            noIntro ? 28 : source.Source.StartLine + 1, "lost_cave_trainer_program_missing");
    }

    private void RequireLostCaveTrainerOccurrence(CampaignState state, TrainerId trainer,
        ForestTrainerBattleDefinition source, bool noIntro)
    {
        if (source.Mode == 0)
        {
            RequireOrdinaryTrainerOccurrence(state, trainer, source, noIntro, TrainerRegion.LostCave,
                "lost_cave_trainer_source_occurrence");
            return;
        }
        var player = state.World.Player;
        if (!IsLostCaveTrainer(trainer) || trainer.Value != "pret:TRAINER_LADY_SELPHY" || !noIntro ||
            source.Mode != 3 || source.BattleTypeFlags != 8 || source.ScriptEntry != LostCaveSelphyScene ||
            player.MapId.Value != "map.five-island-lost-cave-room10" || player.X != 5 || player.Y != 8 || player.Elevation != 3 ||
            !state.Script.Locked || state.Script.QuestLogState != 0 || state.Script.Wait?.Kind != StoryWaitKind.Battle ||
            state.Script.Stack.LastOrDefault()?.ProgramId != LostCaveSelphyScene ||
            state.Script.Variables.GetValueOrDefault(LostCaveSelphySceneVariable) != 0 ||
            state.Script.Flags.GetValueOrDefault("FLAG_HIDE_LOST_CAVE_SELPHY") || state.BattledTrainers.Contains(trainer.Value) ||
            state.LastTalked != LostCaveSelphyActor || !state.Actors.TryGetValue(LostCaveSelphyActor, out var actor) ||
            !actor.Active || actor.Invisible || actor.ScriptId != "0x0" || actor.SourceLocalId != 1 ||
            actor.X != 5 || actor.Y != 7 || actor.Elevation != 3 || actor.Facing != Direction.South ||
            state.Script.Variables.GetValueOrDefault("VAR_LAST_TALKED") != actor.SourceLocalId)
            throw new InvalidDataException("lost_cave_selphy_source_occurrence");
    }

    private bool LostCaveTrainerHistoryOriginMatches(CampaignForestTrainerHistory history) =>
        IsLostCaveTrainer(new(history.TrainerId)) && history.SourceScriptEntry is { } script &&
        _content.LostCaveTrainers!.BattlesByScript.TryGetValue(script, out var source) &&
        source.Trainer.Value == history.TrainerId && source.Mode is 0 or 3 &&
        history.SourceScriptLine == LostCaveTrainerSourceLine(script);
}
