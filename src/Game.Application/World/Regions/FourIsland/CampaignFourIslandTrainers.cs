using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.Trainers;
using Game.Domain.World;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private const string FourIslandGruntActor = "LOCALID_ICEFALL_ROCKET3";

    private static bool IsFourIslandGrunt(TrainerId trainer) => trainer.Value == "pret:TRAINER_TEAM_ROCKET_GRUNT_45";

    private void RequireFourIslandTrainerOccurrence(CampaignState state, TrainerId trainer,
        ForestTrainerBattleDefinition source, bool noIntro)
    {
        var player = state.World.Player;
        // The coordinate scene starts at (11/12/13,18), then its original player
        // movement walks up twice, right once and up once before this no-intro call.
        // Rocket3 remains a source 0x0 actor; it does not own the scene program.
        if (!IsFourIslandGrunt(trainer) || !noIntro || source.Mode != 3 || source.BattleTypeFlags != 8 ||
            source.ScriptEntry != FourIslandLoreleiScene || player.MapId.Value != FourIslandBackMap ||
            player.X is < 12 or > 14 || player.Y != 15 || player.Elevation != 3 || player.Facing != Direction.North ||
            !state.Script.Locked || state.Script.Wait?.Kind != StoryWaitKind.Battle ||
            state.Script.Stack.LastOrDefault()?.ProgramId != FourIslandLoreleiScene ||
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
}
