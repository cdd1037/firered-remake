using System.Text.Json;
using Game.Application;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.World;

namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class CampaignDisplaySeedLifecycleTests:WildBattleFixture
{
    [TestMethod]
    public void PublicWildEntryAndNextBatchKeepTransientEventCursor()
    {
        var session=CampaignSession.RestoreCurrent(content,FieldFixture(),checkpoint.Generation);
        Send(session,new("turn",Direction:Direction.North));
        var first=session.State.WildBattle!;
        Assert.IsNotNull(first.DisplayBatch);
        for(int i=0;i<200&&session.State.WildBattle?.Phase==CampaignBattlePhase.Events;i++)
        {
            var before=session.State.WildBattle!;var batch=before.DisplayBatch!;
            Assert.IsTrue(batch.NextEventIndex<=before.SourceEvents.Length);
            var unchanged=batch.AdvanceThrough(before.SourceEvents,before.EventIndex<before.Events.Length?before.Events[before.EventIndex].Sequence:long.MaxValue);
            Assert.AreEqual(batch,unchanged);
            Send(session,new("ack"));
        }
        var ready=session.State.WildBattle!;
        Assert.AreEqual(CampaignBattlePhase.ChooseMove,ready.Phase);
        Assert.AreEqual(ready.SourceEvents.Length,ready.DisplayBatch!.NextEventIndex);
        Send(session,new("battle_move",MoveSlot:0));
        Assert.IsNotNull(session.State.WildBattle?.DisplayBatch);
        Drain(session);
    }

    [TestMethod]
    public void SupportedContinueRejectsActiveBattleAndFieldSaveRemainsUnchanged()
    {
        var field=FieldFixture();Assert.IsNull(validator.ValidatePlayerSave(field));
        var bytes=SaveCodec.Encode(field,checkpoint.Generation,validator.RulesVersion,content.ContentVersion);
        var restored=SaveCodec.Decode<CampaignState>(bytes,validator.RulesVersion,content.ContentVersion,validate:validator.ValidatePlayerSave);
        var continued=CampaignSession.RestoreCurrent(content,restored.State,restored.Generation);
        Assert.AreEqual(CanonicalJson.Hash(field),CanonicalJson.Hash(continued.State));
        var session=StartField();
        Assert.AreEqual("save_event_active",session.ValidatePlayerSave(session.State));
        string json=JsonSerializer.Serialize(session.State);
        Assert.IsFalse(json.Contains("DisplayBatch",StringComparison.Ordinal));
        var active=SaveCodec.Encode(session.State,session.Revision,session.RulesVersion,content.ContentVersion);
        Assert.ThrowsExactly<InvalidDataException>(()=>SaveCodec.Decode<CampaignState>(active,session.RulesVersion,content.ContentVersion,validate:session.ValidatePlayerSave));
        Assert.ThrowsExactly<InvalidDataException>(()=>CampaignSession.RestoreCurrent(content,session.State,session.Revision));
    }
}
