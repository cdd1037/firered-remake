using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Charge=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared=FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;
using Caller=FireRed.Tests.Application.WildBattles.CallerPreludeAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// The existing Horn Drill/Guillotine OHKO contract is reused unchanged. Natural
// learners and public kernel calls supply receipts; no animation test computes KO.
[TestClass,TestCategory("Game")]
public sealed class SheerColdAnimationKernelTests
{
    private static MoveId Move=>Kernel.Id("SHEER_COLD");
    private static SourceBattleFrame Initial(bool single,int actor=0,int level=100)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("SNORLAX","DEFENSE_CURL",100) with{SpeedStage=0}).ToArray();
        bodies[actor]=Kernel.Mon("DEWGONG","SHEER_COLD",level) with{SpeedStage=12,AccuracyStage=12};return Kernel.Frame(bodies,single);
    }
    private static SourceFourActionResult Success(SourceBattleFrame frame,int actor=0,int target=1)=>
        Charge.Find(frame,actor,target,r=>Envelope.Hits(r.Events).Length==1,"Sheer Cold public ordered success");
    private static void Check(SourceFourActionResult result,bool single)
    {Shared.Check(result,329,single,Envelope.Hits(result.Events).Single());CheckLegacy(result.Events,single);}
    private static SourceFourActionResult OwnedAtActionSeed(SourceBattleFrame frame)
    {
        // The pinned vectors specify action-start RNG, independently of turn-order
        // draws. Obtain the real public lifecycle owner before setting that fixture
        // input: singles tryfaintmon cannot execute through an ownerless direct call.
        var choices=frame.Slots.Take(frame.BattlerCount).Select(c=>new SourceActionSelection(c.Occupant!.Value,
            SourceActionKind.Move,0,c.Slot.Value==0?new BattlerSlot(1):c.Slot)).ToImmutableArray();
        var started=SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog,frame,frame.Revision,choices);
        Assert.IsTrue(started.Succeeded,started.Fault);Assert.AreEqual(0,started.State.Queue[0].Slot.Value);Assert.IsNotNull(started.Continuation);
        var actionFrame=started.State with{Rng=frame.Rng};
        var selected=SourceMoveTargeting.ResolveLearned(actionFrame,new(0),new(1),Kernel.Mechanics.Catalog.Moves[actionFrame.Slots[0].Body!.Pokemon.Moves[0].Move]);
        Assert.IsTrue(selected.Succeeded,selected.Fault);Assert.IsTrue(selected.Trace.IsEmpty,"The pinned action-start seed must reach the source OHKO/caller command without a targeting draw.");
        var result=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,actionFrame,started.Continuation.Value);
        Assert.IsTrue(result.Succeeded,result.Fault);Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));return result;
    }
    private static void CheckLegacy(ImmutableArray<SourceBattleEvent> rows,bool single)
    {
        for(int i=0;i<rows.Length;i++)
        {
            Assert.AreEqual(Kernel.Project(rows,i,single),Charge.Project(rows,i,single,false));
            Assert.IsNull(Charge.Project(rows,i,single,true).Dispatch);Assert.IsNull(Charge.Wave(rows,i,single));
            if(single)Assert.IsNull(AnimationDispatchFixture.AtAttackText(Charge.Single(rows,i),false,SourceBattleScene.On).Dispatch);
        }
    }

    [TestMethod]
    public void NaturalSheerColdUsesTheExistingOhkoRootAtEveryPositionAndSelectedAlly()
    {
        var definition=Kernel.Mechanics.Catalog.Moves[Move];Assert.AreEqual("EFFECT_OHKO",definition.Effect);Assert.AreEqual("TYPE_ICE",definition.Type);Assert.AreEqual(1,definition.Power);Assert.AreEqual(30,definition.Accuracy);Assert.AreEqual(5,definition.Pp);Assert.AreEqual("MOVE_TARGET_SELECTED",definition.Target);
        Assert.IsFalse(definition.Flags.Contains("FLAG_MAKES_CONTACT"));Assert.IsFalse(definition.Flags.Contains("FLAG_KINGS_ROCK_AFFECTED"));Assert.IsTrue(AnimationDispatchFixture.Supports(329,Move.Value));
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(int target in single?new[]{actor^1}:new[]{actor^1,actor^2})
        {
            var result=Success(Initial(single,actor),actor,target);var hit=result.Events.OfType<SourceHpChangedEvent>().Single();Assert.AreEqual(hit.HpBefore,hit.Damage);Assert.AreEqual(0,hit.HpAfter);
            var pp=result.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(5,pp.Before);Assert.AreEqual(4,pp.After);Assert.AreEqual(1,pp.CommandCost);Check(result,single);
        }
    }

    [TestMethod,DataRow(0,1,true),DataRow(14,29,true),DataRow(193,30,false),DataRow(116,100,false)]
    public void SheerColdRetainsStrictSourceOhkoChanceRatherThanOrdinaryAccuracy(int seed,int sourceRoll,bool succeeds)
    {
        // Same pinned Cmd_tryKO/Random boundary vectors as HornMoveAnimationDispatchTests:
        // equal-level roll is Random()%100+1 < 30; stage extremes cannot change it.
        foreach(bool single in new[]{false,true})foreach(int accuracy in new[]{0,12})
        {
            var frame=Charge.Body(Initial(single),0,b=>b with{AccuracyStage=accuracy}) with{Rng=new((uint)seed)};
            var result=OwnedAtActionSeed(frame);var roll=result.Trace.Single(t=>t.Purpose.EndsWith(".ohko",StringComparison.Ordinal));Assert.AreEqual(sourceRoll,roll.Raw%100+1);
            Assert.IsFalse(result.Trace.Any(t=>t.Purpose.EndsWith(".accuracy",StringComparison.Ordinal)));
            if(succeeds){Assert.AreEqual(2,result.Trace.Length);Check(result,single);}
            else{Assert.AreEqual("ohko-miss",result.Events.OfType<SourceMoveFailedEvent>().Single().Reason);Assert.AreEqual(1,result.Trace.Length);Assert.AreEqual(0,Envelope.Hits(result.Events).Length);Charge.Deny(result.Events,single);}
        }
    }

    [TestMethod]
    public void HigherLevelSturdyAndProtectFailBeforeAnyOwnedAnimation()
    {
        foreach(bool single in new[]{false,true})foreach(string branch in new[]{"level","sturdy","protect"})
        {
            var frame=Initial(single,level:branch=="level"?34:100);
            if(branch=="sturdy")frame=Charge.Body(frame,1,_=>Kernel.Mon("PINECO","TACKLE",100) with{SpeedStage=0});
            if(branch=="protect")frame=Charge.Body(frame,1,b=>b with{Protected=true});
            var result=Charge.Ordered(frame,0,1);Assert.AreEqual(branch switch{"level"=>"ohko-level","sturdy"=>"ohko-sturdy",_=>"ohko-unreachable"},result.Events.OfType<SourceMoveFailedEvent>().Single().Reason);
            Assert.AreEqual(branch=="level"?1:0,result.Trace.Count(t=>t.Purpose.EndsWith(".ohko",StringComparison.Ordinal)));Assert.AreEqual(0,Envelope.Hits(result.Events).Length);
            if(branch=="sturdy")Assert.AreEqual("ABILITY_STURDY",result.Events.OfType<SourceAbilityPreventedEvent>().Single().Ability);
            Assert.AreEqual(1,result.Events.OfType<SourcePpCommandEvent>().Count());Charge.Deny(result.Events,single);
        }
    }

    [TestMethod]
    public void SubstituteAndOneHpEndureKeepTheirActualPositiveOrZeroLossReceipt()
    {
        foreach(bool single in new[]{false,true})foreach(bool substitute in new[]{false,true})foreach(bool endure in new[]{false,true})
        {
            var frame=Charge.Body(Initial(single),1,b=>b with{Enduring=endure,Pokemon=b.Pokemon with{CurrentHp=endure?1:b.Pokemon.CurrentHp},CalledVolatile=substitute?new(){SubstituteHp=1}:null});
            int before=frame.Slots[1].Body!.Pokemon.CurrentHp;var result=Success(frame);Check(result,single);
            if(substitute)
            {var hit=result.Events.OfType<SourceSubstituteDamagedEvent>().Single();Assert.AreEqual(1,hit.HpBefore);Assert.AreEqual(endure?1:0,hit.HpAfter);Assert.AreEqual(before,result.State.Slots[1].Body!.Pokemon.CurrentHp);Assert.IsFalse(result.Events.OfType<SourceHpChangedEvent>().Any());}
            else{var hit=result.Events.OfType<SourceHpChangedEvent>().Single();Assert.AreEqual(endure?1:0,hit.HpAfter);if(endure)Assert.AreEqual(0,hit.Damage);}
            if(endure)Assert.IsTrue(result.Events.OfType<SourceDamageAdjustedEvent>().Single().Endured);
        }
    }

    [TestMethod]
    public void OriginalIceAndGhostTargetsRemainHittableAndRoughSkinDoesNotBecomeContact()
    {
        foreach(bool single in new[]{false,true})foreach((string species,string move) in new[]{("DEWGONG","GROWL"),("GASTLY","LICK"),("SHARPEDO","BITE")})
        {
            var frame=Charge.Body(Initial(single),1,_=>Kernel.Mon(species,move,100) with{SpeedStage=0});var result=Success(frame);Check(result,single);
            Assert.AreEqual(0,result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);Assert.IsFalse(result.Events.OfType<SourceAbilityDamageEvent>().Any());
        }
    }

    [TestMethod]
    public void PressureClampsTheFinalPpWithoutChangingSuccessfulOwnership()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Charge.Body(Initial(single),0,b=>b with{Pokemon=b.Pokemon with{Moves=[b.Pokemon.Moves[0] with{CurrentPp=1}]}});
            frame=Charge.Body(frame,1,_=>Kernel.Mon("DUSCLOPS","LEER",100) with{SpeedStage=0});var result=Success(frame);Check(result,single);
            var pp=result.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(1,pp.Before);Assert.AreEqual(0,pp.After);Assert.AreEqual(2,pp.CommandCost);
        }
    }

    [TestMethod]
    public void ActualSuccessfulMetronomeLeafKeepsCallerOwnershipAndNeverBorrowsTheDirectRoot()
    {
        Assert.IsTrue(WildBattleSourceCallers.IsDirectMetronomeChoice(329));
        foreach(bool single in new[]{false,true})
        {
            var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",1) with{SpeedStage=0}).ToArray();bodies[0]=Kernel.Mon("TOGETIC","METRONOME",100) with{SpeedStage=12};
            var frame=Kernel.Frame(bodies,single) with{Rng=new(Caller.MetronomeSeed(329))};var result=OwnedAtActionSeed(frame);
            var invocation=result.Events.OfType<SourceCalledMoveTransitionEvent>().Single().Invocation;Assert.AreEqual(Move,invocation.CurrentMove);Assert.AreEqual(Kernel.Id("METRONOME"),invocation.ChosenMove);
            Assert.IsTrue(result.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Move==Move&&e.HpAfter==0));Caller.Check(result.Events,result,118,single);Caller.DenyLeaf(result.Events,single,329);
            for(int i=0;i<result.Events.Length;i++)foreach(bool atResult in new[]{false,true})Assert.AreNotEqual(329,Charge.Project(result.Events,i,single,atResult).Dispatch?.SourceMoveId);
        }
    }

    [TestMethod]
    public void ForeignMissingDuplicateAndCalledOwnershipRowsAreRejected()
    {
        foreach(bool single in new[]{false,true})
        {
            var rows=Success(Initial(single)).Events;var used=rows.OfType<SourceMoveUsedEvent>().Single();var attack=rows.OfType<SourceAttackStringEvent>().Single();var pp=rows.OfType<SourcePpCommandEvent>().Single();var hit=rows.OfType<SourceHpChangedEvent>().Single();
            foreach(var bad in new[]{pp with{PpOwner=new(1)},pp with{Actor=new(1)},pp with{Target=new(0)},pp with{ChosenMove=Kernel.Id("METRONOME")},pp with{CurrentMove=Kernel.Id("GUILLOTINE")},pp with{PpSlot=3},pp with{After=pp.Before},pp with{CommandCost=0}})Charge.Deny(rows.SetItem(rows.IndexOf(pp),bad),single);
            foreach(var owner in new SourceBattleEvent[]{pp,hit}){Charge.Deny(Shared.Number(rows.Remove(owner)),single);Charge.Deny(Shared.Number(rows.Insert(rows.IndexOf(owner),owner)),single);}
            foreach(var bad in new[]{hit with{Actor=new(1)},hit with{Target=new(0)},hit with{Move=Kernel.Id("HORN_DRILL")}})Charge.Deny(rows.SetItem(rows.IndexOf(hit),bad),single);
            Charge.Deny(rows.SetItem(rows.IndexOf(attack),attack with{ChosenMove=Kernel.Id("METRONOME")}),single);Charge.Deny(rows.SetItem(rows.IndexOf(used),used with{Move=Kernel.Id("GUILLOTINE")}),single);
        }
    }

    [TestMethod]
    public void ActualOhkoReceiptsRequireTheCanonical329ScriptAndCurrentMoveDefinition()
    {
        foreach(bool single in new[]{false,true})
        {
            var rows=Success(Initial(single)).Events;int index=rows.IndexOf(rows.OfType<SourceMoveUsedEvent>().Single());var moves=Kernel.Mechanics.Catalog.Moves;
            foreach(var metadata in new[]{null,moves.Remove(Move),moves.SetItem(Move,moves[Move] with{SourceMoveId=12}),moves.SetItem(Move,moves[Move] with{Target="UNKNOWN_TARGET"})})
                Assert.IsNull(single?AnimationDispatchFixture.AtAttackText(Charge.Single(rows,index),true,SourceBattleScene.On,metadata).Dispatch:AnimationDispatchFixture.AtAttackText(Envelope.Double(rows,index),SourceBattleScene.On,metadata).Dispatch);
            Assert.IsNull(single?CampaignMoveAnimationPresentation.AtAttackText(Charge.Single(rows,index),true,SourceBattleScene.On,moves,(_,_)=>false).Dispatch:CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(rows,index),SourceBattleScene.On,moves,(_,_)=>false).Dispatch);
        }
    }
}
