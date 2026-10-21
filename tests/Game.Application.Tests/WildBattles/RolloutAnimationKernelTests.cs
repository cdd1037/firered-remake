using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

// Source-natural Marill/15; controlled speed, Defense Curl and Substitute are
// integration fixtures, not evidence of natural route acquisition.
[TestClass,TestCategory("Game")]
public sealed class RolloutAnimationKernelTests
{
    private static MoveId Move=>Kernel.Id("ROLLOUT");
    private static WildBattleCombatant[] Bodies(int actor=0,bool curled=false,int moveId=205)
    {
        var rows=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        rows[actor]=Kernel.Mon(moveId==301?"SPHEAL":"MARILL",moveId==301?"ICE_BALL":"ROLLOUT",moveId==301?13:15) with{AccuracyStage=12,SpeedStage=12,DefenseCurled=curled};return rows;
    }
    private static SourceBattleFrame Body(SourceBattleFrame frame,int slot,Func<WildBattleCombatant,WildBattleCombatant> change)
    {
        var cell=frame.Slots[slot];var body=change(cell.Body!);var key=cell.Member!.Value;var party=frame.Parties[(int)key.Side];
        var member=party.Members[key.Index] with{Pokemon=WildBattleTransformationRules.PersistentPokemon(body),Status=WildBattleTransformationRules.PersistentStatus(body),HeldItem=body.HeldItem,KnockedOffItem=body.KnockedOffItem};
        return frame with{Slots=frame.Slots.SetItem(slot,cell with{Body=body}),Parties=frame.Parties.SetItem((int)key.Side,party with{Members=party.Members.SetItem(key.Index,member)})};
    }
    private static (SourceBattleFrame State,ImmutableArray<SourceBattleEvent> Events) Turn(SourceBattleFrame frame,int actor,int target)
    {
        var choices=frame.Slots.Where(s=>frame.CanAct(s.Slot)).Select(s=>{
            var locked=s.Registers.Turn?.Lock;var move=Kernel.Mechanics.Catalog.Moves[locked?.Move??s.Body!.Pokemon.Moves[0].Move];
            return new SourceActionSelection(s.Occupant!.Value,SourceActionKind.Move,locked?.PpSlot??0,locked?.Target??new(s.Slot.Value==actor?target:move.Target=="MOVE_TARGET_USER"?s.Slot.Value:s.Slot.Value^1));
        }).ToImmutableArray();
        var result=SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog,frame,frame.Revision,choices);Assert.IsTrue(result.Succeeded,result.Fault);var events=result.Events.ToList();int steps=0;
        while(result.State.FourTurn is not null)
        {
            Assert.IsNotNull(result.Continuation);Assert.IsTrue(++steps<40);
            var restored=JsonSerializer.Deserialize<SourceBattleFrame>(JsonSerializer.Serialize(result.State))!;
            var replay=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,restored,result.Continuation.Value);
            result=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,result.State,result.Continuation.Value);
            Assert.IsTrue(result.Succeeded,result.Fault);Assert.AreEqual(JsonSerializer.Serialize(result),JsonSerializer.Serialize(replay));events.AddRange(result.Events);
        }
        Assert.IsTrue(result.CompletedCleanTurn);Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));return(result.State,events.ToImmutableArray());
    }
    private static CampaignMoveAnimationDispatch Check(ImmutableArray<SourceBattleEvent> events,bool single,int counter,int power,int actor,int target,int moveId=205)
    {
        var move=Kernel.Id(moveId==301?"ICE_BALL":"ROLLOUT");string before=JsonSerializer.Serialize(events);var hits=events.Where(e=>e is SourceHpChangedEvent h&&h.Actor.Value==actor&&h.Move==move||e is SourceSubstituteDamagedEvent s&&s.Actor.Value==actor&&s.Move==move).ToArray();Assert.AreEqual(1,hits.Length);
        SourceBattleEvent hit=hits[0];Assert.AreEqual(power,hit is SourceHpChangedEvent h?h.PreparedBasePower:((SourceSubstituteDamagedEvent)hit).PreparedBasePower);
        var candidates=new List<CampaignMoveAnimationDispatch>();
        for(int i=0;i<events.Length;i++)
        {
            var d=Kernel.Project(events,i,single);Assert.AreEqual(d,Kernel.Project(events,i,single));Assert.IsNull(Kernel.Project(events,i,single,SourceBattleScene.Off).Dispatch);
            if(d.Dispatch?.SourceMoveId==moveId)candidates.Add(d.Dispatch);
            var atResult=single?AnimationDispatchFixture.AtResult(Envelope.Single(events,i),true,SourceBattleScene.On):AnimationDispatchFixture.AtResult(Envelope.Double(events,i),SourceBattleScene.On);
            Assert.IsFalse(atResult.Dispatch?.SourceMoveId==moveId);
        }
        Assert.AreEqual(1,candidates.Count);var cue=candidates[0];Assert.AreEqual((byte)counter,cue.RolloutCounter);Assert.AreEqual((ushort)power,cue.MovePower);Assert.AreEqual(new BattlerSlot(actor),cue.Actor);Assert.AreEqual(new BattlerSlot(target),cue.Target);Assert.AreEqual(hit.Sequence,cue.ResultSequence);Assert.AreEqual(hit is SourceSubstituteDamagedEvent,cue.HitsSubstitute);
        var attack=events.OfType<SourceAttackStringEvent>().Single(e=>e.Actor.Value==actor&&e.CurrentMove==move);Assert.AreEqual(attack.Sequence,cue.AttackSequence);
        var pp=events.OfType<SourcePpCommandEvent>().Where(e=>e.Actor.Value==actor&&e.CurrentMove==move).ToArray();
        if(counter==1){Assert.AreEqual(1,pp.Length);Assert.AreEqual(pp[0].Sequence,cue.PpSequence);Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText,cue.Boundary);}
        else{Assert.AreEqual(0,pp.Length);Assert.IsNull(cue.PpSequence);Assert.AreEqual(attack.Sequence,cue.Sequence);Assert.AreEqual(CampaignMoveAnimationBoundary.AfterRetainedAttackText,cue.Boundary);}
        Assert.AreEqual(before,JsonSerializer.Serialize(events));return cue;
    }
    private static void Deny(ImmutableArray<SourceBattleEvent> events,bool single,int moveId=205)
    {for(int i=0;i<events.Length;i++)Assert.IsFalse(Kernel.Project(events,i,single).Dispatch?.SourceMoveId==moveId);}
    [TestMethod]
    public void IceBallNaturalFiveTurnCountersUseCanonicalDirectAndRetainedAnimationOwnership()
    {
        // Compact source branches: both singles sides, a rear doubles attacker,
        // and a friendly doubles target with the authentic Defense Curl power.
        foreach(var (single,actor,target,curled) in new[]{(true,0,1,false),(true,1,0,false),(false,2,3,false),(false,3,1,true)})
        {
            var frame=Kernel.Frame(Bodies(actor,curled,301),single);int pp=frame.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp;
            for(int counter=1;counter<=5;counter++)
            {
                var result=Turn(frame,actor,target);Check(result.Events,single,counter,(30<<(counter-1))*(curled?2:1),actor,target,301);frame=result.State;
                Assert.AreEqual(pp-1,frame.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);Assert.AreEqual(5-counter,frame.Slots[actor].Registers.Turn?.Lock?.TurnsRemaining??0);
                if(counter is 1 or 3)
                {
                    int marker=Array.FindIndex(result.Events.ToArray(),e=>e is SourceMtMoonVolatileEvent {Volatile:"called-rollout"});Assert.IsTrue(marker>=0);Deny(result.Events.RemoveAt(marker),single,301);
                    int hit=Array.FindIndex(result.Events.ToArray(),e=>e is SourceHpChangedEvent h&&h.Move==Kernel.Id("ICE_BALL"));Assert.IsTrue(hit>=0);var owned=(SourceHpChangedEvent)result.Events[hit];Deny(result.Events.SetItem(hit,owned with{PreparedBasePower=null}),single,301);
                }
            }
            Assert.IsNull(frame.Slots[actor].Registers.Turn?.Lock);
        }
    }
    [TestMethod]
    public void IceBallRetainedZeroPpAndBrokenSubstituteKeepTheOwnedSecondCounter()
    {
        foreach(bool single in new[]{false,true})
        {
            var first=Envelope.Resolve(Kernel.Frame(Bodies(moveId:301),single));var frame=Body(first.State,0,b=>b with{Pokemon=b.Pokemon with{Moves=b.Pokemon.Moves.SetItem(0,b.Pokemon.Moves[0] with{CurrentPp=0})}});
            frame=Body(frame,1,b=>b with{CalledVolatile=new(){SubstituteHp=1}});var result=Envelope.Resolve(frame);
            var cue=Check(result.Events,single,2,60,0,1,301);Assert.IsTrue(cue.HitsSubstitute);Assert.AreEqual(0,result.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp);Assert.AreEqual(0,result.State.Slots[1].Body!.CalledVolatile?.SubstituteHp??0);
        }
    }
    [TestMethod]
    public void FiveNaturalCompleteTurnsPreserveOnePpAndPreparedPowerIncludingFinalRemovedLock()
    {
        foreach(bool single in new[]{false,true})foreach(bool curled in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(int target in single?new[]{actor^1}:new[]{actor^1,actor^2})
        {
            var frame=Kernel.Frame(Bodies(actor,curled),single);int pp=frame.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp;
            for(int hit=1;hit<=5;hit++)
            {
                var result=Turn(frame,actor,target);Check(result.Events,single,hit,(30<<(hit-1))*(curled?2:1),actor,target);frame=result.State;
                Assert.AreEqual(pp-1,frame.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);Assert.AreEqual(5-hit,frame.Slots[actor].Registers.Turn?.Lock?.TurnsRemaining??0);
            }
            Assert.IsNull(frame.Slots[actor].Registers.Turn?.Lock);
        }
    }
    [TestMethod]
    public void RetainedZeroPpAndBrokenSubstituteUseTheirEventTimeCounterAndRoute()
    {
        foreach(bool single in new[]{false,true})
        {
            var first=Envelope.Resolve(Kernel.Frame(Bodies(),single));var frame=Body(first.State,0,b=>b with{Pokemon=b.Pokemon with{Moves=b.Pokemon.Moves.SetItem(0,b.Pokemon.Moves[0] with{CurrentPp=0})}});
            frame=Body(frame,1,b=>b with{CalledVolatile=new(){SubstituteHp=1}});var result=Envelope.Resolve(frame);
            var cue=Check(result.Events,single,2,60,0,1);Assert.IsTrue(cue.HitsSubstitute);Assert.AreEqual(0,result.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp);Assert.AreEqual(0,result.State.Slots[1].Body!.CalledVolatile?.SubstituteHp??0);
        }
    }
    [TestMethod]
    public void FailedRetainedHitAndNewStartNeverReuseCounterOrAnimation()
    {
        foreach(bool single in new[]{false,true})
        {
            var first=Envelope.Resolve(Kernel.Frame(Bodies(),single));var failed=Envelope.Resolve(Body(first.State,1,b=>b with{Protected=true}));Deny(failed.Events,single);Assert.IsNull(failed.State.Slots[0].Registers.Turn?.Lock);
            var again=Envelope.Resolve(Body(failed.State,1,b=>b with{Protected=false}));Check(again.Events,single,1,30,0,1);
        }
    }
    [TestMethod]
    public void MissingForeignDuplicateMisorderedCountersAndAlteredPowerCannotAuthenticate()
    {
        foreach(bool single in new[]{false,true})foreach(bool retained in new[]{false,true})
        {
            var first=Envelope.Resolve(Kernel.Frame(Bodies(),single));var result=retained?Envelope.Resolve(first.State):first;var rows=result.Events;
            int marker=Array.FindIndex(rows.ToArray(),e=>e is SourceMtMoonVolatileEvent v&&v.Volatile=="called-rollout"),hit=Array.FindIndex(rows.ToArray(),e=>e is SourceHpChangedEvent h&&h.Move==Move);Assert.IsTrue(marker>=0&&hit>marker);
            var m=(SourceMtMoonVolatileEvent)rows[marker];Deny(rows.RemoveAt(marker),single);Deny(rows.SetItem(marker,m with{Side=new(1)}),single);Deny(rows.SetItem(marker,m with{Before=0,After=0,Reason="finished"}),single);Deny(rows.Insert(marker,m),single);Deny(rows.SetItem(marker,m with{Sequence=rows[hit].Sequence+1}),single);
            var h=(SourceHpChangedEvent)rows[hit];foreach(int? power in new int?[]{null,0,-1,31,65536})Deny(rows.SetItem(hit,h with{PreparedBasePower=power}),single);
        }
    }
    [TestMethod]
    public void RealOneHpEndureAndFocusBandKeepTheirAnimationWithoutReconstructingPower()
    {
        foreach(bool single in new[]{false,true})foreach(bool retained in new[]{false,true})foreach(bool band in new[]{false,true})
        {
            var start=Kernel.Frame(Bodies(),single);if(retained)start=Envelope.Resolve(start).State;
            var frame=Body(start,1,b=>b with{Enduring=!band,HeldItem=band?"ITEM_FOCUS_BAND":b.HeldItem,Pokemon=b.Pokemon with{CurrentHp=1}});
            SourceFourActionResult? found=null;
            for(uint seed=0;seed<128;seed++)
            {
                // Failed Focus Band rolls legitimately KO the one-HP target.
                // The queued owner must process that branch even though only
                // successful survival receipts are selected for this assertion.
                var fresh=frame with{Rng=new(seed)};
                var choices=fresh.Slots.Take(fresh.BattlerCount).Select(s=>new SourceActionSelection(s.Occupant!.Value,SourceActionKind.Move,0,
                    s.Registers.Turn?.Lock?.Target??(s.Slot.Value==0?new(1):s.Slot))).ToImmutableArray();
                var begin=SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog,fresh,fresh.Revision,choices);
                Assert.IsTrue(begin.Succeeded,begin.Fault);Assert.IsNotNull(begin.Continuation);Assert.AreEqual(0,begin.State.Queue[0].Slot.Value);
                var attempt=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,begin.State,begin.Continuation.Value);
                Assert.IsTrue(attempt.Succeeded,attempt.Fault);Assert.IsNull(SourceBattleIdentityRules.Validate(attempt.State));
                if(attempt.Events.OfType<SourceDamageAdjustedEvent>().Any(e=>e.HungOn==band&&e.Endured!=band)){found=attempt;break;}
            }
            Assert.IsNotNull(found,"Bounded actual damage-adjustment branch required");var cue=Check(found.Events,single,retained?2:1,retained?60:30,0,1);Assert.AreEqual(1,found.State.Slots[1].Body!.Pokemon.CurrentHp);
            var adjusted=found.Events.OfType<SourceDamageAdjustedEvent>().Single();Assert.AreEqual(0,adjusted.DamageAfter);int index=found.Events.IndexOf(adjusted);Deny(found.Events.SetItem(index,adjusted with{Target=new(0)}),single);Deny(found.Events.SetItem(index,adjusted with{DamageAfter=1}),single);
        }
    }
    [TestMethod]
    public void CalledInitialRolloutOwnsItsPreparedCounterWithoutChangingPowerExportRng()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=CallerPreludeAnimationKernelFixture.Frame(118,single) with{Rng=new(CallerPreludeAnimationKernelFixture.MetronomeSeed(205))};var called=Envelope.Resolve(frame);Assert.IsTrue(called.Events.OfType<SourceCalledMoveTransitionEvent>().Any(e=>e.Invocation.CurrentMove==Move));var hit = called.Events.OfType<SourceHpChangedEvent>().Single();
            Assert.AreEqual(30, hit.PreparedBasePower);
            var marker = called.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "called-rollout");
            Assert.AreEqual(0, marker.Before); Assert.AreEqual(4, marker.After); Assert.AreEqual("move", marker.Reason);
            var cue = CalledLeafAnimationKernelFixture.Check(called.Events, called, single, Kernel.Id("METRONOME"), 205, hit);
            Assert.AreEqual((byte?)1, cue.RolloutCounter); Assert.AreEqual((ushort?)30, cue.MovePower);
        }
        var ordinary=Envelope.Resolve(Kernel.Frame(Bodies()));var curled=Envelope.Resolve(Kernel.Frame(Bodies(curled:true)));
        Assert.AreEqual(JsonSerializer.Serialize(ordinary.Trace),JsonSerializer.Serialize(curled.Trace));var first=ordinary.Events.OfType<SourceHpChangedEvent>().Single();var twice=curled.Events.OfType<SourceHpChangedEvent>().Single();Assert.AreEqual(30,first.PreparedBasePower);Assert.AreEqual(60,twice.PreparedBasePower);
        var exported=(SourceHpChangedEvent)OverlayHitAnimationKernelFixture.Single([first], 0).Events[0];Assert.AreEqual(first.PreparedBasePower,exported.PreparedBasePower);
    }
}
