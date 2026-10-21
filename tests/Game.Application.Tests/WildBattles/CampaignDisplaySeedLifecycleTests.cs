using System.Collections.Immutable;
using System.Reflection;
using Game.Domain.WildBattle;
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
            Assert.IsTrue(batch.NextEventIndex<=before.Events.Length);
            var unchanged=batch.AdvanceThrough(before.Events,before.EventIndex<before.Events.Length?before.Events[before.EventIndex].Sequence:long.MaxValue);
            Assert.AreEqual(batch,unchanged);
            Send(session,new("ack"));
        }
        var ready=session.State.WildBattle!;
        Assert.AreEqual(CampaignBattlePhase.ChooseMove,ready.Phase);
        Assert.AreEqual(ready.Events.Length,ready.DisplayBatch!.NextEventIndex);
        Send(session,new("battle_move",MoveSlot:0));
        Assert.IsNotNull(session.State.WildBattle?.DisplayBatch);
        Drain(session);
    }

    [TestMethod]
    public void HiddenRewardAndShiftReceiptsKeepTheCanonicalCursorAndDoNotExposeFutureHp()
    {
        var session = Controlled([Owned("PIDGEY", 30, 101, "GROWL")], Owned("SNORLAX", 30, 201, "GROWL"));
        var initial = session.State; var battle = initial.WildBattle!;
        var seed = CampaignBattleDisplayBodies.FromSlots(battle.Frame.Slots);
        int hp = seed.Bodies[1].Pokemon.CurrentHp;
        ImmutableArray<SourceBattleEvent> events = [
            new SourceRewardLearningEvent(1, 1, new(BattleSide.Player, 0), new(30, new("pret:MOVE_GROWL")), SourceRewardLearningOutcome.Declined, null),
            new SourceTrainerOpponentSelectedEvent(2, 1, new(BattleSide.Opponent, 0)),
            new SourceTrainerShiftOfferedEvent(3, 1, new(BattleSide.Opponent, 0)),
            new SourceHpChangedEvent(4, 1, new(0), new(1), new("pret:MOVE_TACKLE"), hp, hp - 3, 3, false, 100),
            new SourceHpChangedEvent(5, 1, new(0), new(1), new("pret:MOVE_TACKLE"), hp - 3, hp - 10, 7, false, 100)];
        var prepare = typeof(CampaignSession).GetMethod("PrepareWildPresentation", BindingFlags.Instance | BindingFlags.NonPublic)!;
        var state = (CampaignState)prepare.Invoke(session, [initial with { WildBattle = battle with {
            Events = events, EventIndex = 0, Phase = CampaignBattlePhase.Events, DisplayBatch = new(seed) } }])!;
        var visible = state.WildBattle!;
        Assert.AreEqual(3, visible.EventIndex); Assert.AreSame(events[3], visible.Events[visible.EventIndex]);
        Assert.AreEqual(4, visible.DisplayBatch!.NextEventIndex);
        Assert.AreEqual(hp - 3, visible.DisplayOpponent.CurrentHp);
        Assert.AreEqual(hp, seed.Bodies[1].Pokemon.CurrentHp);
        Assert.AreEqual(initial.World.Rng, state.World.Rng); Assert.AreEqual(initial.Party, state.Party);
        typeof(CampaignSession).GetField("_commands", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(session,
            new CommandSession<CampaignState, CampaignCommand>(state, session.RulesVersion, content.ContentVersion, session.Revision));
        Send(session, new("ack"));
        var next = session.State.WildBattle!;
        Assert.AreEqual(4, next.EventIndex); Assert.AreSame(events[4], next.Events[next.EventIndex]);
        Assert.AreEqual(5, next.DisplayBatch!.NextEventIndex); Assert.AreEqual(hp - 10, next.DisplayOpponent.CurrentHp);
        Assert.AreEqual(initial.World.Rng, session.State.World.Rng); Assert.AreEqual(initial.Party, session.State.Party);
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
