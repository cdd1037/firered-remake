using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Change=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Shared=FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class SuperpowerAnimationKernelTests
{
    private static SourceBattleFrame Frame(bool single,int actor=0)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        // Nidoqueen learns Superpower at level 43 in the pinned source. Stages,
        // substitute and HP below are controlled kernel fixtures, not route play.
        bodies[actor]=Kernel.Mon("NIDOQUEEN","SUPERPOWER") with{AccuracyStage=12,SpeedStage=12};
        return Kernel.Frame(bodies,single);
    }
    private static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events,int index,bool single,
        bool result=false,SourceBattleScene scene=SourceBattleScene.On)=>single?(result?
        AnimationDispatchFixture.AtResult(Envelope.Single(events,index),true,scene):AnimationDispatchFixture.AtAttackText(Envelope.Single(events,index),true,scene)):(result?
        AnimationDispatchFixture.AtResult(Envelope.Double(events,index),scene):AnimationDispatchFixture.AtAttackText(Envelope.Double(events,index),scene));
    private static CampaignMoveAnimationDispatch Check(ImmutableArray<SourceBattleEvent> events,bool single)
    {
        string before=JsonSerializer.Serialize(events);var used=events.OfType<SourceMoveUsedEvent>().Single();var attack=events.OfType<SourceAttackStringEvent>().Single();
        var pp=events.OfType<SourcePpCommandEvent>().Single();var hit=Envelope.Hits(events).Single();CampaignMoveAnimationDispatch? root=null;
        Assert.AreEqual(Kernel.Id("SUPERPOWER"),used.Move);Assert.AreEqual(used.Move,attack.CurrentMove);Assert.AreEqual(used.Move,attack.ChosenMove);
        Assert.AreEqual(used.Move,pp.CurrentMove);Assert.AreEqual(used.Move,pp.ChosenMove);Assert.AreEqual(used.Actor,pp.PpOwner);Assert.AreEqual(used.Slot,pp.PpSlot);
        Assert.AreEqual(used.PpBefore,pp.Before);Assert.AreEqual(used.PpAfter,pp.After);Assert.AreEqual(Math.Max(0,pp.Before-pp.CommandCost),pp.After);
        Assert.IsTrue(attack.Sequence<used.Sequence&&used.Sequence<pp.Sequence&&pp.Sequence<hit.Sequence);
        for(int i=0;i<events.Length;i++)
        {
            var decision=Project(events,i,single);Assert.AreEqual(decision,Project(events,i,single));Assert.IsNull(Project(events,i,single,true).Dispatch);
            Assert.IsNull(Project(events,i,single,false,SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single?AnimationDispatchFixture.StatAtResult(Envelope.Single(events,i),true):AnimationDispatchFixture.StatAtResult(Envelope.Double(events,i)),"Self reductions must not replay this move or invent a generic one-stat wave.");
            if(single)Assert.IsNull(AnimationDispatchFixture.AtAttackText(Envelope.Single(events,i),false,SourceBattleScene.On).Dispatch);
            if(events[i]!=used){Assert.IsNull(decision.Dispatch);continue;}
            root=decision.Dispatch;Assert.IsNotNull(root,decision.Reason);Assert.AreEqual(276,root.SourceMoveId);Assert.AreEqual(used.Actor,root.Actor);Assert.AreEqual(used.Target,root.Target);
            Assert.AreEqual(attack.Sequence,root.AttackSequence);Assert.AreEqual(pp.Sequence,root.PpSequence);Assert.AreEqual(hit.Sequence,root.ResultSequence);
            Assert.AreEqual(used.Sequence,root.Sequence);Assert.AreEqual(hit is SourceSubstituteDamagedEvent,root.HitsSubstitute);Assert.IsTrue(root.AcknowledgesCurrentReceipt);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText,root.Boundary);Assert.IsNull(root.MoveTurn);Assert.IsNull(root.MovePower);Assert.IsNull(root.MoveDamage);
        }
        Assert.IsNotNull(root);Assert.AreEqual(before,JsonSerializer.Serialize(events));return root;
    }
    private static void Deny(ImmutableArray<SourceBattleEvent> events,bool single)
    {for(int i=0;i<events.Length;i++){Assert.IsNull(Project(events,i,single).Dispatch);Assert.IsNull(Project(events,i,single,true).Dispatch);}}
    private static void Reductions(SourceFourActionResult result,int actor,int attack,int defense)
    {
        var stats=result.Events.Where(e=>e is SourceStatChangedEvent or SourceStatAtMinimumEvent).ToArray();Assert.AreEqual(2,stats.Length);
        foreach(var (stat,before,row) in new[]{(WildBattleStat.Attack,attack,stats[0]),(WildBattleStat.Defense,defense,stats[1])})
        {
            if(before==0){Assert.IsInstanceOfType<SourceStatAtMinimumEvent>(row);var cap=(SourceStatAtMinimumEvent)row;Assert.AreEqual(actor,cap.Actor.Value);Assert.AreEqual(actor,cap.Target.Value);Assert.AreEqual(stat,cap.Stat);}
            else{Assert.IsInstanceOfType<SourceStatChangedEvent>(row);var changed=(SourceStatChangedEvent)row;Assert.AreEqual(actor,changed.Actor.Value);Assert.AreEqual(actor,changed.Target.Value);Assert.AreEqual(stat,changed.Stat);Assert.AreEqual(before,changed.Before);Assert.AreEqual(before-1,changed.After);}
        }
        Assert.IsTrue(Envelope.Hits(result.Events).Single().Sequence<stats[0].Sequence&&stats[0].Sequence<stats[1].Sequence);
        // SourceFaintCommands resets stages after the already committed reduction receipts.
        bool fainted=result.State.Slots[actor].Body!.Pokemon.CurrentHp==0;
        Assert.AreEqual(fainted?6:Math.Max(0,attack-1),result.State.Slots[actor].Body!.AttackStage);Assert.AreEqual(fainted?6:Math.Max(0,defense-1),result.State.Slots[actor].Body!.DefenseStage);
    }
    [TestMethod]
    public void RealSuperpowerDamageAndOrderedSelfReductionsOwnExactlyOneRootForEverySide()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            int target=actor^1;var frame=Frame(single,actor);string before=JsonSerializer.Serialize(frame);var result=Kernel.Ordered(frame,actor,target);
            var hp=result.Events.OfType<SourceHpChangedEvent>().Single();Assert.IsTrue(hp.Damage>0&&hp.HpAfter>0);Assert.AreEqual(target,hp.Target.Value);
            Reductions(result,actor,6,6);Check(result.Events,single);Assert.AreEqual(before,JsonSerializer.Serialize(frame));
            Assert.AreEqual(JsonSerializer.Serialize(result),JsonSerializer.Serialize(Kernel.Ordered(frame,actor,target)));
        }
    }
    [TestMethod]
    public void IndividualAndDoubleStatCapsDoNotSuppressTheAlreadyCommittedHit()
    {
        foreach(bool single in new[]{false,true})foreach(var (attack,defense) in new[]{(0,6),(6,0),(0,0),(1,12)})
        {var frame=Change.Body(Frame(single),0,b=>b with{AttackStage=attack,DefenseStage=defense});var result=Kernel.Ordered(frame);Reductions(result,0,attack,defense);Check(result.Events,single);}
    }
    [TestMethod]
    public void TargetSubstituteBreakAndKnockoutKeepActorReductionsAndOriginalHitOwnership()
    {
        foreach(bool single in new[]{false,true})foreach(string branch in new[]{"substitute","break","knockout"})
        {
            var frame=Change.Body(Frame(single),1,b=>branch=="knockout"?b with{Pokemon=b.Pokemon with{CurrentHp=1}}:b with{CalledVolatile=new(){SubstituteHp=branch=="break"?1:255}});
            var result=Kernel.Ordered(frame);Reductions(result,0,6,6);var cue=Check(result.Events,single);Assert.AreEqual(branch!="knockout",cue.HitsSubstitute);
            if(branch=="knockout")
            {
                var hit=result.Events.OfType<SourceHpChangedEvent>().Single();Assert.AreEqual(0,hit.HpAfter);
                Assert.IsTrue(result.Events.OfType<SourceFaintedEvent>().Any(e=>e.Side.Value==1&&e.Sequence>hit.Sequence));
                Assert.AreEqual(0,result.State.Parties[1].Members[0].Pokemon.CurrentHp);
            }
            else
            {
                var substitute=result.Events.OfType<SourceSubstituteDamagedEvent>().Single();Assert.AreEqual(branch=="break",substitute.HpAfter==0);
                // ConsumeSubstituteDamage clears an otherwise empty volatile record.
                Assert.IsNotNull(result.State.Slots[1].Body);Assert.AreEqual(substitute.HpAfter,result.State.Slots[1].Body!.CalledVolatile?.SubstituteHp??0);
            }
        }
    }
    [TestMethod]
    public void LaterRoughSkinActorKnockoutCannotEraseItsLaunchOrPriorSelfReductions()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Change.Body(Frame(single),1,_=>Kernel.Mon("SHARPEDO","BITE",100) with{SpeedStage=0});frame=Change.Body(frame,0,b=>b with{Pokemon=b.Pokemon with{CurrentHp=1}});
            var result=Kernel.Ordered(frame);var contact=result.Events.OfType<SourceAbilityDamageEvent>().Single();Assert.AreEqual("ABILITY_ROUGH_SKIN",contact.Ability);Assert.AreEqual(0,contact.Target.Value);Assert.AreEqual(0,contact.HpAfter);
            Reductions(result,0,6,6);Assert.IsTrue(result.Events.OfType<SourceStatChangedEvent>().Last().Sequence<contact.Sequence);Assert.AreEqual(0,result.State.Slots[0].Body!.Pokemon.CurrentHp);Check(result.Events,single);
        }
    }
    [TestMethod]
    public void ProtectionImmunityAndMalformedPpOrHitCannotBorrowTheSuperpowerRoot()
    {
        foreach(bool single in new[]{false,true})
        {
            foreach(string branch in new[]{"protect","immune"})
            {var frame=Change.Body(Frame(single),1,b=>branch=="protect"?b with{Protected=true}:Kernel.Mon("GASTLY","LICK",100));var result=Envelope.Resolve(frame);Deny(result.Events,single);Assert.IsFalse(result.Events.OfType<SourceStatChangedEvent>().Any());}
            var events=Envelope.Resolve(Frame(single)).Events;var pp=events.OfType<SourcePpCommandEvent>().Single();var hit=Envelope.Hits(events).Single();
            Deny(events.SetItem(events.IndexOf(pp),pp with{PpOwner=new(1)}),single);Deny(events.Remove(hit),single);Deny(Shared.Number(events.Insert(events.IndexOf(hit),hit)),single);
            var used=events.OfType<SourceMoveUsedEvent>().Single();int index=events.IndexOf(used);
            Assert.IsNull(single?CampaignMoveAnimationPresentation.AtAttackText(Envelope.Single(events,index),true,SourceBattleScene.On,Kernel.Mechanics.Catalog.Moves,(_,_)=>false).Dispatch:
                CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events,index),SourceBattleScene.On,Kernel.Mechanics.Catalog.Moves,(_,_)=>false).Dispatch);
        }
    }
}
