using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared=FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;
using Change=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class HpChangeAnimationKernelTests
{
    private static (string Species,string Move) Spec(int id)=>id switch
    {284=>("CAMERUPT","ERUPTION"),323=>("WAILORD","WATER_SPOUT"),220=>("MISDREAVUS","PAIN_SPLIT"),_=>throw new ArgumentOutOfRangeException(nameof(id))};
    private static SourceBattleFrame Frame(int id,bool single,int actor=0,int? hp=null,bool substitute=false)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        var spec=Spec(id);bodies[actor]=Kernel.Mon(spec.Species,spec.Move) with{AccuracyStage=12,SpeedStage=12};
        if(hp is {} value)bodies[actor]=bodies[actor] with{Pokemon=bodies[actor].Pokemon with{CurrentHp=value}};
        if(substitute)foreach(int slot in Enumerable.Range(0,single?2:4).Where(slot=>(slot&1)!=(actor&1)))
            bodies[slot]=bodies[slot] with{CalledVolatile=new(){SubstituteHp=100}};
        return Kernel.Frame(bodies,single);
    }
    private static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events,int index,bool single,bool result=false,
        SourceBattleScene scene=SourceBattleScene.On)=>single?(result?AnimationDispatchFixture.AtResult(Envelope.Single(events,index),true,scene):
            AnimationDispatchFixture.AtAttackText(Envelope.Single(events,index),true,scene)):(result?
            AnimationDispatchFixture.AtResult(Envelope.Double(events,index),scene):AnimationDispatchFixture.AtAttackText(Envelope.Double(events,index),scene));
    private static CampaignMoveAnimationDispatch Check(ImmutableArray<SourceBattleEvent> events,bool single,int id,long result,int target,bool substitute=false)
    {
        string before=JsonSerializer.Serialize(events);var used=events.OfType<SourceMoveUsedEvent>().Single();CampaignMoveAnimationDispatch? owned=null;
        for(int i=0;i<events.Length;i++)
        {
            var decision=Project(events,i,single);Assert.AreEqual(decision,Project(events,i,single));Assert.IsNull(Project(events,i,single,true).Dispatch);
            Assert.IsNull(Project(events,i,single,false,SourceBattleScene.Off).Dispatch);
            if(events[i]!=used){Assert.IsNull(decision.Dispatch);continue;}
            owned=decision.Dispatch;Assert.IsNotNull(owned,decision.Reason);Assert.AreEqual(id,owned.SourceMoveId);Assert.AreEqual(used.Actor,owned.Actor);
            Assert.AreEqual(target,owned.Target.Value);Assert.AreEqual(result,owned.ResultSequence);Assert.AreEqual(substitute,owned.HitsSubstitute);
            Assert.IsTrue(owned.AcknowledgesCurrentReceipt);Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText,owned.Boundary);
        }
        Assert.IsNotNull(owned);Assert.AreEqual(before,JsonSerializer.Serialize(events));return owned;
    }
    private static void Deny(ImmutableArray<SourceBattleEvent> events,bool single)
    {for(int i=0;i<events.Length;i++){Assert.IsNull(Project(events,i,single).Dispatch,JsonSerializer.Serialize(events));Assert.IsNull(Project(events,i,single,true).Dispatch);}}
    private static SourceBattleEvent WithPower(SourceBattleEvent row,int? power)=>row switch
    {SourceHpChangedEvent h=>h with{PreparedBasePower=power},SourceSubstituteDamagedEvent h=>h with{PreparedBasePower=power},_=>throw new InvalidOperationException()};
    private static int? Power(SourceBattleEvent row)=>row switch{SourceHpChangedEvent h=>h.PreparedBasePower,SourceSubstituteDamagedEvent h=>h.PreparedBasePower,_=>null};
    private static byte Tier(int hp,int max)=>hp<max/4? (byte)0:hp<2*(max/4)?(byte)1:hp<3*(max/4)?(byte)2:(byte)3;
    private static CampaignBattleDisplayBodies Display(SourceBattleFrame initial,ImmutableArray<SourceBattleEvent> events,int next)
    {var display=CampaignBattleDisplayBodies.FromSlots(initial.Slots);for(int i=0;i<next;i++)display=display.Apply(events[i]);return display;}
    private static byte? Snapshot(SourceBattleFrame initial,ImmutableArray<SourceBattleEvent> events,CampaignMoveAnimationDispatch cue,int? next=null)
    {
        int index=Array.FindIndex(events.ToArray(),e=>e.Sequence==cue.Sequence);int cursor=next??index+(initial.Layout==SourceBattleLayout.Singles?1:0);
        return CampaignMoveAnimationPresentation.ActorHpQuarterTierAtInvocation(cue,Display(initial,events,cursor),events,cursor,Kernel.Mechanics.Catalog.Moves);
    }

    [TestMethod,DataRow(284),DataRow(323)]
    public void HpScaledRootsUseRealFrozenPowerForAllActorsBodiesSubstitutesAndSpreadPasses(int id)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(bool substitute in new[]{false,true})
        {
            var frame=Frame(id,single,actor,substitute:substitute);int max=frame.Slots[actor].Body!.Pokemon.Stats.MaxHp;
            frame=Change.Body(frame,actor,b=>b with{Pokemon=b.Pokemon with{CurrentHp=max/3}});string before=JsonSerializer.Serialize(frame);
            var result=Kernel.Ordered(frame,actor,actor^1);var hits=Envelope.Hits(result.Events);Assert.AreEqual(single?1:2,hits.Length);
            var first=hits[0];int target=first is SourceHpChangedEvent h?h.Target.Value:((SourceSubstituteDamagedEvent)first).Target.Value;
            var cue=Check(result.Events,single,id,first.Sequence,target,substitute);Assert.AreEqual((ushort)(max/3*150/max),cue.MovePower);
            Assert.IsTrue(hits.All(h=>Power(h)==cue.MovePower));Assert.AreEqual(Tier(max/3,max),Snapshot(frame,result.Events,cue));
            Assert.AreEqual(before,JsonSerializer.Serialize(frame));Assert.AreEqual(JsonSerializer.Serialize(result),JsonSerializer.Serialize(Kernel.Ordered(frame,actor,actor^1)));
        }
    }

    [TestMethod,DataRow(284),DataRow(323)]
    public void MissingOrInconsistentPreparedPowerAndForeignPpNeverAdmitHpScaledRoots(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var events=Envelope.Resolve(Frame(id,single)).Events;var hit=Envelope.Hits(events)[0];var pp=events.OfType<SourcePpCommandEvent>().First();
            foreach(int? wrong in new int?[]{null,-1,0,151,65536})Deny(events.SetItem(events.IndexOf(hit),WithPower(hit,wrong)),single);
            Deny(events.SetItem(events.IndexOf(pp),pp with{PpOwner=new(1)}),single);Deny(events.Remove(hit),single);
            Deny(Shared.Number(events.Insert(events.IndexOf(hit),hit)),single);
            if(!single){var second=Envelope.Hits(events)[1];Deny(events.SetItem(events.IndexOf(second),WithPower(second,1)),single);}
            var used=events.OfType<SourceMoveUsedEvent>().Single();var attack=events.OfType<SourceAttackStringEvent>().Single();
            Deny(events.SetItem(events.IndexOf(attack),attack with{ChosenMove=Kernel.Id("METRONOME")}),single);
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events,events.IndexOf(used)),SourceBattleScene.On,
                Kernel.Mechanics.Catalog.Moves,(_,_)=>false).Dispatch);
        }
    }

    [TestMethod,DataRow(284),DataRow(323)]
    public void PreventedFirstSpreadPassSelectsItsPartnersActualPpBoundary(int id)
    {
        foreach(string branch in new[]{"protect","absorb"})
        {
            var frame=Frame(id,false);frame=Change.Body(frame,1,b=>branch=="protect"?b with{Protected=true}:
                Kernel.Mon(id==323?"VAPOREON":"VULPIX",id==323?"TACKLE":"EMBER",100));
            var result=Envelope.Resolve(frame);var hit=Envelope.Hits(result.Events).Last();
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any()||result.Events.OfType<SourceAbilityPreventedEvent>().Any());
            var cue=Check(result.Events,false,id,hit.Sequence,3);var pp=result.Events.OfType<SourcePpCommandEvent>().Last();
            Assert.AreEqual(pp.Sequence,cue.PpSequence);Assert.AreEqual((byte)3,Snapshot(frame,result.Events,cue));
            var blocked=Envelope.Resolve(Change.Body(frame,3,b=>b with{Protected=true}));Deny(blocked.Events,false);
        }
    }

    [TestMethod]
    public void WoundedWaterAbsorbFirstPassKeepsTheSecondPassPowerAndPartyHpSnapshot()
    {
        var frame=Change.Body(Frame(323,false,hp:61),1,_=>Kernel.Mon("VAPOREON","TACKLE",100));
        frame=Change.Body(frame,1,b=>b with{Pokemon=b.Pokemon with{CurrentHp=b.Pokemon.Stats.MaxHp/2}});
        var result=Envelope.Resolve(frame);var healed=result.Events.OfType<SourceRecoveredEvent>().Single(e=>e.Move==Kernel.Id("WATER_SPOUT"));
        Assert.IsTrue(healed.Recovered>0);var hit=Envelope.Hits(result.Events).Single();
        var cue=Check(result.Events,false,323,hit.Sequence,3);var sync=result.Events.OfType<SourceCalledPartyChangedEvent>()
            .Single(e=>e.SourceSlot==new BattlerSlot(1));
        Assert.IsTrue(healed.Sequence<cue.PpSequence&&cue.ResultSequence<sync.Sequence,
            "Active synchronization belongs to final action-end, after both spread passes.");
        Assert.AreEqual((ushort)(61*150/frame.Slots[0].Body!.Pokemon.Stats.MaxHp),cue.MovePower);
        Assert.AreEqual((byte)1,Snapshot(frame,result.Events,cue));
        foreach(var malformed in new[]{healed with{HpBefore=healed.HpBefore+1,Recovered=healed.Recovered-1},
            healed with{HpAfter=healed.HpAfter+1,Recovered=healed.Recovered+1},healed with{Recovered=healed.Recovered+1},
            healed with{Move=Kernel.Id("RECOVER")},healed with{Side=new(0)},healed with{Turn=healed.Turn+1}})
            Assert.IsNull(Snapshot(frame,result.Events.SetItem(result.Events.IndexOf(healed),malformed),cue));
        var prevented=result.Events.OfType<SourceAbilityPreventedEvent>().Single();
        Assert.IsNull(Snapshot(frame,result.Events.SetItem(result.Events.IndexOf(prevented),prevented with{Actor=new(2)}),cue));
        var older=prevented with{Sequence=result.Events[0].Sequence-1};
        var replaced=result.Events.SetItem(result.Events.IndexOf(prevented),new SourceMoveFailedEvent(prevented.Sequence,
            prevented.Turn,prevented.Actor,prevented.Target,prevented.Move,"protected")).Insert(0,older);
        var used=replaced.OfType<SourceMoveUsedEvent>().Single();
        Assert.AreEqual(cue,Project(replaced,replaced.IndexOf(used),false).Dispatch);
        Assert.IsNull(Snapshot(frame,replaced,cue),"An older prevention cannot authenticate this pass's healing.");
        foreach(var foreign in new[]{new SourceHpChangedEvent(0,used.Turn,new(2),new(0),used.Move,61,1,60,false,100),
            new SourceHpChangedEvent(0,used.Turn,used.Actor,new(0),Kernel.Id("TACKLE"),61,1,60,false,100)})
        {
            int boundary=Array.FindIndex(result.Events.ToArray(),e=>e.Sequence==cue.PpSequence);
            var injected=Shared.Number(result.Events.Insert(boundary,foreign));var invocation=injected.OfType<SourceMoveUsedEvent>().Single();
            var selected=Project(injected,injected.IndexOf(invocation),false).Dispatch;Assert.IsNotNull(selected);
            Assert.IsNull(Snapshot(frame,injected,selected),"Foreign HP cannot alter this invocation's party-HP snapshot.");
        }
        var forged=sync with{After=sync.After with{Pokemon=sync.After.Pokemon with{Stats=sync.After.Pokemon.Stats with{MaxHp=65535}}}};
        Assert.AreEqual((byte)1,Snapshot(frame,result.Events.SetItem(result.Events.IndexOf(sync),forged),cue),
            "Later party synchronization cannot replace the earlier invocation snapshot.");
    }

    [TestMethod,DataRow(284),DataRow(323)]
    public void LaterDestinyBondKnockoutCannotEraseHpScaledLaunch(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Change.Body(Frame(id,single),1,_=>Kernel.Mon("GASTLY","DESTINY_BOND"));
            var bonded=Envelope.Resolve(frame,1,1);Assert.IsTrue(bonded.State.Slots[1].Body!.DestinyBond);
            var ready=Change.Body(bonded.State,1,b=>b with{Pokemon=b.Pokemon with{CurrentHp=1}});
            var result=Kernel.Ordered(ready,0,1);Assert.IsTrue(result.Events.OfType<SourceTowerHpEvent>().Any(e=>e.Effect=="destiny-bond"));
            Assert.AreEqual(0,result.State.Slots[0].Body!.Pokemon.CurrentHp);
            var hit=Envelope.Hits(result.Events)[0];var cue=Check(result.Events,single,id,hit.Sequence,1);
            Assert.AreEqual((byte)3,Snapshot(ready,result.Events,cue));
        }
    }

    [TestMethod]
    public void WaterSpoutSnapshotsStrictQuarterEdgesFromBothRealDisplayCursors()
    {
        foreach(bool single in new[]{false,true})
        {
            var initial=Frame(323,single);int max=initial.Slots[0].Body!.Pokemon.Stats.MaxHp,q=max/4;
            Assert.AreNotEqual(0,max%4,"Exercise source maxHP truncation.");
            foreach(int hp in new[]{1,q-1,q,q+1,2*q-1,2*q,2*q+1,3*q-1,3*q,3*q+1,max})
            {
                var frame=Change.Body(initial,0,b=>b with{Pokemon=b.Pokemon with{CurrentHp=hp}});var result=Envelope.Resolve(frame);
                var used=result.Events.OfType<SourceMoveUsedEvent>().Single();int at=result.Events.IndexOf(used);var cue=Project(result.Events,at,single).Dispatch;
                Assert.IsNotNull(cue);foreach(int cursor in new[]{at,at+1})Assert.AreEqual(Tier(hp,max),Snapshot(frame,result.Events,cue,cursor));
                Assert.IsNull(Snapshot(frame,result.Events,cue,at+2));
                var display=Display(frame,result.Events,at);string before=JsonSerializer.Serialize(display);
                foreach(int tinyMax in new[]{1,2,3})
                {
                    var body=display.Bodies[0];var tiny=display with{Bodies=display.Bodies.SetItem(0,body with{Pokemon=body.Pokemon with{CurrentHp=1,Stats=body.Pokemon.Stats with{MaxHp=tinyMax}}})};
                    Assert.AreEqual((byte)3,CampaignMoveAnimationPresentation.ActorHpQuarterTierAtInvocation(cue,tiny,result.Events,at,Kernel.Mechanics.Catalog.Moves));
                }
                Assert.AreEqual(before,JsonSerializer.Serialize(display));
            }
        }
    }

    [TestMethod]
    public void WaterSpoutSnapshotUsesEarlierDamageButIgnoresLaterDamageAndFinalState()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Change.Body(Frame(323,single),1,_=>Kernel.Mon("SNORLAX","TACKLE",100) with{AttackStage=0});int max=frame.Slots[0].Body!.Pokemon.Stats.MaxHp;
            frame=Change.Body(frame,0,b=>b with{Pokemon=b.Pokemon with{CurrentHp=max/4+1}});
            var damaged=Envelope.Resolve(frame,1,0);Assert.IsTrue(damaged.State.Slots[0].Body!.Pokemon.CurrentHp<max/4);
            var spout=Envelope.Resolve(damaged.State,0,1);var all=damaged.Events.AddRange(spout.Events);var used=spout.Events.OfType<SourceMoveUsedEvent>().Single();
            var cue=Project(all,all.IndexOf(used),single).Dispatch;Assert.IsNotNull(cue);Assert.AreEqual((byte)0,Snapshot(frame,all,cue));
            // This later actor HP update is separated by its real command boundary.
            var next=Change.Body(Change.Body(spout.State,0,b=>b with{SpeedStage=0}),1,b=>b with{SpeedStage=12});
            var later=Kernel.Ordered(next,1,0);
            var extended=all.AddRange(later.Events);Assert.AreEqual((byte)0,Snapshot(frame,extended,cue));
            Assert.IsTrue(later.State.Slots[0].Body!.Pokemon.CurrentHp<damaged.State.Slots[0].Body!.Pokemon.CurrentHp);
        }
    }

    [TestMethod]
    public void WaterSpoutTransformReadsCurrentPersistentHpAndOriginalMaximum()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Change.Body(Frame(323,single,1),0,_=>Kernel.Mon("DITTO","TRANSFORM"));var transformed=Envelope.Resolve(frame,0,1);
            var original=transformed.State.Slots[0].Body!;Assert.IsNotNull(original.Transform);int max=original.Transform.OriginalPokemon.Stats.MaxHp;
            Assert.AreNotEqual(max,original.Transform.CopiedPokemon.Stats.MaxHp);
            // A controlled current-HP reduction preserves the historical Transform
            // snapshot and uses the same persistent party adapter as live damage.
            var reduced=Change.Body(transformed.State,0,b=>b with{Pokemon=b.Pokemon with{CurrentHp=max/4-1}});
            Assert.AreEqual(max,reduced.Slots[0].Body!.Transform!.OriginalPokemon.CurrentHp);
            var result=Envelope.Resolve(reduced,0,1);var used=result.Events.OfType<SourceMoveUsedEvent>().Single();var cue=Project(result.Events,result.Events.IndexOf(used),single).Dispatch;
            Assert.IsNotNull(cue);Assert.AreEqual((byte)0,Snapshot(reduced,result.Events,cue));
        }
    }

    [TestMethod]
    public void PainSplitOwnsOneOrderedPairIncludingEqualHpAndCappedHealingForEveryActor()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        foreach(string branch in new[]{"actor-heals","actor-damages","equal","capped"})
        {
            int target=actor^1;var frame=Frame(220,single,actor);int maximum=frame.Slots[actor].Body!.Pokemon.Stats.MaxHp;
            frame=Change.Body(frame,actor,b=>b with{Pokemon=b.Pokemon with{CurrentHp=branch=="actor-heals"?1:branch=="equal"?50:maximum}});
            frame=Change.Body(frame,target,b=>b with{Pokemon=b.Pokemon with{CurrentHp=branch=="actor-damages"?1:branch=="equal"?50:b.Pokemon.Stats.MaxHp}});
            var result=Kernel.Ordered(frame,actor,target);var pair=result.Events.Where(e=>e is SourceHpChangedEvent or SourceRecoveredEvent).ToArray();Assert.AreEqual(2,pair.Length);
            var cue=Check(result.Events,single,220,pair[0].Sequence,target);Assert.IsNull(cue.MovePower);Assert.IsNull(Snapshot(frame,result.Events,cue));
            if(branch=="equal")Assert.IsTrue(pair.All(e=>e is SourceRecoveredEvent {Recovered:0}));
            if(branch=="capped")Assert.IsTrue(pair[0] is SourceRecoveredEvent {Recovered:0});
            int average=(frame.Slots[actor].Body!.Pokemon.CurrentHp+frame.Slots[target].Body!.Pokemon.CurrentHp)/2;
            Assert.AreEqual(Math.Min(average,maximum),result.State.Slots[actor].Body!.Pokemon.CurrentHp);
        }
    }

    [TestMethod]
    public void PainSplitFailurePartialSwappedDuplicateAndMalformedPairsFailClosed()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Frame(220,single,hp:1);var events=Envelope.Resolve(frame).Events;
            var pair=events.Where(e=>e is SourceHpChangedEvent or SourceRecoveredEvent).ToArray();int at=events.IndexOf(pair[0]);
            Deny(events.Remove(pair[0]),single);Deny(events.Remove(pair[1]),single);Deny(Shared.Number(events.Insert(at,pair[0])),single);
            Deny(Shared.Number(events.SetItem(at,pair[1]).SetItem(at+1,pair[0])),single);
            var actor=(SourceRecoveredEvent)pair[0];var target=(SourceHpChangedEvent)pair[1];
            var capped=actor with{HpAfter=actor.HpAfter-1,Recovered=actor.Recovered-1};
            Assert.IsTrue(capped.HpAfter>=capped.HpBefore);Deny(events.SetItem(at,capped),single);
            foreach(var sync in events.OfType<SourceCalledPartyChangedEvent>().Where(e=>e.Before.Pokemon.CurrentHp!=e.After.Pokemon.CurrentHp))
                Deny(events.Remove(sync),single);
            foreach(var bad in new SourceBattleEvent[]{actor with{Side=new(1)},actor with{Recovered=actor.Recovered+1},actor with{Move=Kernel.Id("RECOVER")},actor with{HpBefore=0}})
                Deny(events.SetItem(at,bad),single);
            foreach(var bad in new[]{target with{Actor=new(1)},target with{Target=new(0)},target with{Damage=target.Damage+1},target with{Critical=true},target with{RandomPercent=99},target with{PreparedBasePower=1},target with{PreparedAnimationPower=1}})
                Deny(events.SetItem(at+1,bad),single);
            foreach(string branch in new[]{"protect","substitute"})
            {
                var blocked=Envelope.Resolve(Change.Body(frame,1,b=>branch=="protect"?b with{Protected=true}:b with{CalledVolatile=new(){SubstituteHp=1}}));
                Assert.IsTrue(blocked.Events.OfType<SourceMoveFailedEvent>().Any());Deny(blocked.Events,single);
            }
            var pp=events.OfType<SourcePpCommandEvent>().Single();Deny(events.SetItem(events.IndexOf(pp),pp with{PpOwner=new(1)}),single);
            Deny(Shared.Number(events.Insert(at+2,new SourceMoveFailedEvent(0,actor.Turn,new(0),new(1),Kernel.Id("PAIN_SPLIT"),"substitute"))),single);
        }
    }
}
