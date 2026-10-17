using Game.Domain.Battle;
namespace Game.Application;

public sealed partial class CampaignSession
{
    private static CampaignWildBattle AdvanceDisplayThrough(CampaignWildBattle battle,long sequence)
    {
        var batch=battle.DisplayBatch??throw new InvalidDataException("display_body_seed_missing");
        return battle with{DisplayBatch=batch.AdvanceThrough(battle.SourceEvents,sequence)};
    }

    private static CampaignBattleDisplayBatch BeginDisplayBatch(CampaignWildBattle previous)
    {
        if(previous.DisplayBatch is not {} old || old.NextEventIndex!=previous.SourceEvents.Length)
            throw new InvalidDataException("display_body_previous_batch_pending");
        // Called before the committed result replaces Frame. This is the next batch's input.
        return new(CampaignBattleDisplayBodies.FromSlots(previous.Frame.Slots));
    }
}
