using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using static FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class FocusPunchAnimationKernelTests
{
    private static readonly MoveId Focus=Id("FOCUS_PUNCH");
    private static WildBattleCombatant[] Participants(int actor)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Mon("SNORLAX","DEFENSE_CURL",100)).ToArray();
        bodies[actor]=Mon("VIGOROTH","FOCUS_PUNCH") with{AccuracyStage=12};return bodies;
    }
    // Run the actual public turn preparation and preceding actions. Focus Punch
    // has negative source priority; a standalone action would miss interruption.
    private static SourceFourActionResult Prepared(SourceBattleFrame frame,int actor,int target,int? interrupter=null)
    {
        string before=JsonSerializer.Serialize(frame);
        var choices=frame.Slots.Take(frame.BattlerCount).Select(s=>new SourceActionSelection(s.Occupant!.Value,SourceActionKind.Move,0,
            new(s.Slot.Value==actor?target:s.Slot.Value==interrupter?actor:s.Slot.Value))).ToImmutableArray();
        var next=SourceFourActionTurns.BeginActions(Mechanics.Catalog,frame,frame.Revision,choices);
        Assert.IsTrue(next.Succeeded,next.Fault);var events=next.Events;var trace=next.Trace;
        Assert.AreEqual(1,events.OfType<SourceFocusPunchPreparedEvent>().Count(p=>p.Actor.Value==actor));
        for(int step=0;step<8;step++)
        {
            Assert.IsNotNull(next.Continuation,"The selected prepared action must execute.");
            bool actorNext=next.State.Queue[next.State.NextAction].Slot.Value==actor;
            next=SourceFourActionTurns.Advance(Mechanics.Catalog,next.State,next.Continuation.Value);
            Assert.IsTrue(next.Succeeded,next.Fault);Assert.IsNull(SourceBattleIdentityRules.Validate(next.State));
            events=events.AddRange(next.Events);trace=trace.AddRange(next.Trace);
            if(!actorNext)continue;
            Assert.AreEqual(before,JsonSerializer.Serialize(frame));return next with{Events=events,Trace=trace};
        }
        throw new InvalidOperationException("Prepared Focus Punch did not execute in the bounded public turn.");
    }
    private static SourceFourActionResult FindPrepared(SourceBattleFrame frame,int actor,int target,Func<SourceFourActionResult,bool> condition,int? interrupter=null)
    {
        for(uint seed=0;seed<1024;seed++){var result=Prepared(frame with{Rng=new(seed)},actor,target,interrupter);if(condition(result))return result;}
        throw new InvalidOperationException("No requested Focus Punch branch in 1024 public-turn seeds.");
    }
    private static SourceBattleEvent[] FocusHits(ImmutableArray<SourceBattleEvent> events)=>events.Where(e=>e is SourceHpChangedEvent h&&h.Move==Focus||e is SourceSubstituteDamagedEvent s&&s.Move==Focus).ToArray();
    private static CampaignMoveAnimationDecision Text(ImmutableArray<SourceBattleEvent> events,int cursor,bool single,SourceBattleScene scene=SourceBattleScene.On)=>single
        ?AnimationDispatchFixture.AtAttackText(Single(events,cursor),true,scene):AnimationDispatchFixture.AtAttackText(Double(events,cursor),scene);
    private static CampaignMoveAnimationDecision Result(ImmutableArray<SourceBattleEvent> events,int cursor,bool single,SourceBattleScene scene=SourceBattleScene.On)=>single
        ?AnimationDispatchFixture.AtResult(Single(events,cursor),true,scene):AnimationDispatchFixture.AtResult(Double(events,cursor),scene);
    private static void DenyFocus(ImmutableArray<SourceBattleEvent> events,bool single)
    {for(int i=0;i<events.Length;i++){Assert.AreNotEqual(264,Text(events,i,single).Dispatch?.SourceMoveId);Assert.AreNotEqual(264,Result(events,i,single).Dispatch?.SourceMoveId);}}
    private static CampaignMoveAnimationDispatch CheckHit(SourceFourActionResult result,bool single,int actor,int target)
    {
        string before=JsonSerializer.Serialize(result);var events=result.Events;var setup=events.OfType<SourceFocusPunchPreparedEvent>().Single();
        var used=events.OfType<SourceMoveUsedEvent>().Single(e=>e.Move==Focus);var attack=events.OfType<SourceAttackStringEvent>().Single(e=>e.CurrentMove==Focus);
        var pp=events.OfType<SourcePpCommandEvent>().Single(e=>e.CurrentMove==Focus);var hit=FocusHits(events).Single();
        Assert.AreEqual(actor,setup.Actor.Value);Assert.AreEqual(actor,used.Actor.Value);Assert.AreEqual(target,used.Target.Value);Assert.AreEqual(0,setup.ChosenSlot);
        Assert.IsTrue(setup.Sequence<attack.Sequence&&attack.Sequence<used.Sequence&&used.Sequence<pp.Sequence&&pp.Sequence<hit.Sequence);
        Assert.AreEqual(used.Actor,pp.PpOwner);Assert.AreEqual(used.Slot,pp.PpSlot);Assert.AreEqual(used.PpBefore,pp.Before);Assert.AreEqual(used.PpAfter,pp.After);Assert.AreEqual(Math.Max(0,pp.Before-pp.CommandCost),pp.After);
        Assert.IsFalse(events.OfType<SourceFocusPunchLostEvent>().Any());CampaignMoveAnimationDispatch? found=null;
        for(int i=0;i<events.Length;i++)
        {
            var text=Text(events,i,single);var outcome=Result(events,i,single);
            Assert.AreEqual(text,Text(events,i,single));Assert.AreEqual(outcome,Result(events,i,single));
            Assert.IsNull(Text(events,i,single,SourceBattleScene.Off).Dispatch);Assert.IsNull(Result(events,i,single,SourceBattleScene.Off).Dispatch);
            Assert.AreNotEqual(264,outcome.Dispatch?.SourceMoveId,"The hit receipt must not replay the text-owned root.");
            if(events[i]!=used){Assert.AreNotEqual(264,text.Dispatch?.SourceMoveId);continue;}
            found=text.Dispatch;Assert.IsNotNull(found,text.Reason);Assert.AreEqual(264,found.SourceMoveId);Assert.AreEqual(used.Sequence,found.Sequence);Assert.AreEqual(attack.Sequence,found.AttackSequence);
            Assert.AreEqual(pp.Sequence,found.PpSequence);Assert.AreEqual(hit.Sequence,found.ResultSequence);Assert.AreEqual(hit is SourceSubstituteDamagedEvent,found.HitsSubstitute);Assert.IsTrue(found.AcknowledgesCurrentReceipt);
        }
        Assert.IsNotNull(found);Assert.AreEqual(before,JsonSerializer.Serialize(result));return found;
    }

    [TestMethod][DataRow(true,0)][DataRow(true,1)][DataRow(false,0)][DataRow(false,1)][DataRow(false,2)][DataRow(false,3)]
    public void ActualPreparedTurnHitOwnsOneDedicatedRootAcrossAllSixLayouts(bool single,int actor)
    {
        Assert.AreEqual("EFFECT_FOCUS_PUNCH",Mechanics.Catalog.Moves[Focus].Effect);Assert.AreEqual(264,Mechanics.Catalog.Moves[Focus].SourceMoveId);
        foreach(int target in single?new[]{actor^1}:new[]{actor^1,actor^2})CheckHit(Prepared(Frame(Participants(actor),single),actor,target),single,actor,target);
    }

    [TestMethod][DataRow("TACKLE","RATTATA")][DataRow("EMBER","CHARMANDER")]
    public void ActualEarlierPhysicalAndSpecialHitsInterruptWithoutAHitRoot(string move,string species)
    {
        foreach(bool single in new[]{true,false})for(int actor=0;actor<(single?2:4);actor++)
        {
            int foe=actor^1;var bodies=Participants(actor);bodies[foe]=Mon(species,move,20) with{AccuracyStage=12};
            var result=Prepared(Frame(bodies,single),actor,foe,foe);var lost=result.Events.OfType<SourceFocusPunchLostEvent>().Single();
            Assert.AreEqual(actor,lost.Actor.Value);Assert.IsTrue(move=="TACKLE"?lost.PhysicalDamage>0:lost.SpecialDamage>0);
            Assert.AreEqual(0,FocusHits(result.Events).Length);Assert.IsFalse(result.Events.OfType<SourceAttackStringEvent>().Any(e=>e.CurrentMove==Focus));Assert.IsFalse(result.Events.OfType<SourceMoveUsedEvent>().Any(e=>e.Move==Focus));
            var pp=result.Events.OfType<SourcePpCommandEvent>().Single(e=>e.CurrentMove==Focus);Assert.IsTrue(pp.Sequence<lost.Sequence);Assert.AreEqual(1,pp.Before-pp.After);DenyFocus(result.Events,single);
        }
    }

    [TestMethod][DataRow(true)][DataRow(false)]
    public void ActorSubstituteAbsorbsEarlierDamageAndTargetSubstituteRetainsHitOwnership(bool single)
    {
        var bodies=Participants(0);bodies[0]=bodies[0] with{CalledVolatile=new(){SubstituteHp=bodies[0].Pokemon.Stats.MaxHp/4}};bodies[1]=Mon("RATTATA","TACKLE",20) with{AccuracyStage=12,CalledVolatile=new(){SubstituteHp=1}};
        var result=Prepared(Frame(bodies,single),0,1,1);Assert.IsTrue(result.Events.OfType<SourceSubstituteDamagedEvent>().Any(e=>e.Target.Value==0&&e.Move==Id("TACKLE")));
        Assert.IsTrue(CheckHit(result,single,0,1).HitsSubstitute);Assert.AreEqual(0,result.Events.OfType<SourceSubstituteDamagedEvent>().Single(e=>e.Move==Focus).HpAfter);
    }

    [TestMethod][DataRow(true)][DataRow(false)]
    public void CommittedCriticalHitStillPlaysOneRootAndDoesNotReplayAtCriticalText(bool single)
    {
        var result=FindPrepared(Frame(Participants(0),single),0,1,r=>r.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Move==Focus&&e.Critical));
        CheckHit(result,single,0,1);var hit=result.Events.OfType<SourceHpChangedEvent>().Single(e=>e.Move==Focus);var critical=result.Events.OfType<SourceCriticalHitEvent>().Single(e=>e.Actor.Value==0);
        Assert.IsTrue(hit.Sequence<critical.Sequence);Assert.AreNotEqual(264,Text(result.Events,result.Events.IndexOf(critical),single).Dispatch?.SourceMoveId);
    }

    [TestMethod][DataRow("miss")][DataRow("protected")][DataRow("immune")]
    public void ActualPreparedMissProtectionAndImmunityNeverInventHitPlayback(string branch)
    {
        foreach(bool single in new[]{true,false})
        {
            var bodies=Participants(0);
            if(branch=="miss")bodies[0]=bodies[0] with{AccuracyStage=0};
            if(branch=="protected")bodies[1]=bodies[1] with{Protected=true};
            if(branch=="immune")bodies[1]=Mon("GASTLY","NIGHT_SHADE",50);
            var frame=Frame(bodies,single);var result=branch=="miss"?FindPrepared(frame,0,1,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(e=>e.Move==Focus)):Prepared(frame,0,1,branch=="immune"?1:null);
            if(branch=="immune")Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e=>e.Move==Focus&&e.Effectiveness==WildBattleEffectiveness.NoEffect));
            else if(branch=="protected")Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Move==Focus));
            Assert.IsFalse(result.Events.OfType<SourceFocusPunchLostEvent>().Any());DenyFocus(result.Events,single);
        }
    }

    [TestMethod][DataRow(true)][DataRow(false)]
    public void ForgedMissingForeignAndUnpaidPpCannotBorrowTheSuccessfulPreparedHit(bool single)
    {
        var result=Prepared(Frame(Participants(0),single),0,1);CheckHit(result,single,0,1);var events=result.Events;
        var pp=events.OfType<SourcePpCommandEvent>().Single(e=>e.CurrentMove==Focus);int at=events.IndexOf(pp);var used=events.OfType<SourceMoveUsedEvent>().Single(e=>e.Move==Focus);
        foreach(var invalid in new[]{pp with{Actor=new(1)},pp with{PpOwner=new(1)},pp with{Target=new(0)},pp with{CurrentMove=Id("TACKLE")},pp with{ChosenMove=Id("METRONOME")},pp with{PpSlot=3},pp with{Before=pp.Before+1},pp with{After=pp.Before},pp with{CommandCost=0}})
            DenyFocus(events.SetItem(at,invalid),single);
        DenyFocus(events.RemoveAt(at),single);DenyFocus(events.Insert(at+1,pp with{Sequence=pp.Sequence+1}),single);
        var attack=events.OfType<SourceAttackStringEvent>().Single(e=>e.CurrentMove==Focus);DenyFocus(events.Remove(attack),single);
        foreach(var invalid in new[]{attack with{Actor=new(1)},attack with{ChosenMove=Id("METRONOME")},attack with{Sequence=used.Sequence}})DenyFocus(events.Replace(attack,invalid),single);
        int cursor=events.IndexOf(used);var metadata=Mechanics.Catalog.Moves.ToDictionary(p=>p.Key,p=>p.Value);metadata[Focus]=metadata[Focus] with{SourceMoveId=5};
        foreach(IReadOnlyDictionary<MoveId,PokemonMoveDefinition>? definitions in new IReadOnlyDictionary<MoveId,PokemonMoveDefinition>?[]{null,metadata})
            Assert.IsNull((single?AnimationDispatchFixture.AtAttackText(Single(events,cursor),true,SourceBattleScene.On,definitions):AnimationDispatchFixture.AtAttackText(Double(events,cursor),SourceBattleScene.On,definitions)).Dispatch);
        Assert.IsNull((single?CampaignMoveAnimationPresentation.AtAttackText(Single(events,cursor),true,SourceBattleScene.On,Mechanics.Catalog.Moves,(_,_)=>false):CampaignMoveAnimationPresentation.AtAttackText(Double(events,cursor),SourceBattleScene.On,Mechanics.Catalog.Moves,(_,_)=>false)).Dispatch);
    }
}
