using Game.Domain.Events;

namespace Game.Application;

public sealed partial class CampaignSession
{
    private CampaignState CompleteFightingDojoMasterTurn(CampaignState before, CampaignState after)
    {
        if (!_content.FightingDojoEnabled || before.World.Player.MapId.Value != FightingDojoMap ||
            after.World.Player.MapId != before.World.Player.MapId || before.Script.Wait?.Kind != StoryWaitKind.Presentation ||
            before.Script.Stack.Length != 1 || !after.Script.Completed || after.Script.Wait is not null)
            return after;
        var frame = before.Script.Stack[^1];
        int line = frame.ProgramId switch
        {
            "SaffronCity_Dojo_EventScript_TriggerMasterBattleLeft" => 8,
            "SaffronCity_Dojo_EventScript_TriggerMasterBattleRight" => 16,
            _ => 0
        };
        if (line == 0) return after;
        var program = _content.FindProgram(frame.ProgramId);
        int index = frame.InstructionIndex - 1;
        if (program is null || index < 0 || index >= program.Instructions.Length ||
            program.Instructions[index].Op != StoryOp.WaitMovement || program.Instructions[index].Opcode != "waitmovement" ||
            program.Instructions[index].Source != new StorySource(FightingDojoSource, frame.ProgramId, line) ||
            !program.Instructions[index].Args.SequenceEqual(["0"]))
            throw new InvalidDataException("fighting_dojo_master_turn_source");
        // These coordinate scripts only turn Koichi and release. Resume ordinary
        // sight after that turn, before another player action can leave his sight.
        return CheckForestTrainerSight(after);
    }
}
