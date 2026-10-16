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
    private bool IsLostCaveTrainer(TrainerId trainer) => _content.LostCaveTrainers?.Trainers.ContainsKey(trainer) == true;

    private void RequireLostCaveTrainerOccurrence(CampaignState state, TrainerId trainer,
        ForestTrainerBattleDefinition source, bool noIntro)
    {
        // trainerbattle_no_intro does not call SetMapVarsToTrainer. The source
        // map scene therefore preserves any stale VAR_LAST_TALKED; actor identity
        // comes from the authenticated local ID and completed source movements.
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
            actor.X != 5 || actor.Y != 7 || actor.Elevation != 3 || actor.Facing != Direction.South)
            throw new InvalidDataException("lost_cave_selphy_source_occurrence");
    }
}
