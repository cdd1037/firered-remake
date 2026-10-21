using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using F=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Caller=FireRed.Tests.Application.WildBattles.CallerPreludeAnimationKernelFixture;
using Numbering=FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class MementoAnimationKernelTests
{
    private static MoveId Move=>Kernel.Id("MEMENTO");
    private static SourceBattleFrame Initial(bool single,int actor=0)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",1) with{SpeedStage=0}).ToArray();
        bodies[actor]=Kernel.Mon("KOFFING","MEMENTO") with{SpeedStage=12};return Kernel.Frame(bodies,single);
    }
    private static CampaignStatAnimationDispatch? Wave(ImmutableArray<SourceBattleEvent> rows,int i,bool single)=>single?
        CampaignStatAnimationPresentation.AtResult(Envelope.Single(rows,i),true,moves:Kernel.Mechanics.Catalog.Moves):
        CampaignStatAnimationPresentation.AtResult(Envelope.Double(rows,i),moves:Kernel.Mechanics.Catalog.Moves);
    private static void Deny(ImmutableArray<SourceBattleEvent> rows,bool single)
    {for(int i=0;i<rows.Length;i++){Assert.IsNull(Kernel.Project(rows,i,single).Dispatch);Assert.IsNull(Wave(rows,i,single));}}
    private static void Check(ImmutableArray<SourceBattleEvent> rows,bool single,int? expectedWave)
    {
        string before=JsonSerializer.Serialize(rows);var used=rows.OfType<SourceMoveUsedEvent>().Single();var attack=rows.OfType<SourceAttackStringEvent>().Single();var pp=rows.OfType<SourcePpCommandEvent>().Single();var hp=rows.OfType<SourceHpChangedEvent>().Single();
        Assert.IsTrue(AnimationDispatchFixture.Supports(262,Move.Value));Assert.AreEqual(used.Actor,hp.Actor);Assert.AreEqual(used.Actor,hp.Target);Assert.AreEqual(0,hp.HpAfter);Assert.AreEqual(hp.HpBefore,hp.Damage);Assert.AreEqual(pp.Sequence+1,hp.Sequence);
        var faint=rows.OfType<SourceFaintedEvent>().Single();Assert.AreEqual(used.Actor,faint.Side);
        var sync=rows.OfType<SourceCalledPartyChangedEvent>().Single(s=>s.SourceSlot==used.Actor);
        if(used.Actor.Side==BattleSide.Player)
        {
            var friendship=faint.Friendship;Assert.IsNotNull(friendship);
            Assert.AreEqual(new PartyMemberKey(sync.Side,sync.PartyIndex),friendship.Member);
            Assert.AreEqual(sync.Before.Pokemon.Friendship,friendship.Before);Assert.AreEqual(sync.After.Pokemon.Friendship,friendship.After);
            Assert.AreEqual(used.Turn,faint.Turn);Assert.AreEqual(faint.Turn,sync.Turn);Assert.IsTrue(hp.Sequence<faint.Sequence&&faint.Sequence<sync.Sequence);
        }
        else Assert.IsNull(faint.Friendship);
        int roots=0,waves=0;
        for(int i=0;i<rows.Length;i++)
        {
            var decision=Kernel.Project(rows,i,single);if(rows[i]==used){var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);roots++;Assert.AreEqual(262,cue.SourceMoveId);Assert.AreEqual(used.Actor,cue.Actor);Assert.AreEqual(used.Target,cue.Target);Assert.AreEqual(hp.Sequence,cue.ResultSequence);Assert.AreEqual(pp.Sequence,cue.SnapshotSequence);Assert.AreEqual(attack.Sequence,cue.AttackSequence);Assert.IsFalse(cue.HitsSubstitute);Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText,cue.Boundary);}else Assert.IsNull(decision.Dispatch);
            Assert.IsNull(Caller.Result(rows,i,single).Dispatch);Assert.IsNull(Kernel.Project(rows,i,single,SourceBattleScene.Off).Dispatch);
            if(Wave(rows,i,single) is {} wave){waves++;Assert.AreEqual(expectedWave,wave.AnimationArgument);Assert.AreEqual(used.Target,wave.Affected);Assert.AreEqual(262,wave.SourceMoveId);Assert.IsInstanceOfType<SourceStatChangedEvent>(rows[i]);}
        }
        Assert.AreEqual(1,roots);Assert.AreEqual(expectedWave is null?0:1,waves);Assert.AreEqual(before,JsonSerializer.Serialize(rows));Assert.AreEqual(before,JsonSerializer.Serialize(JsonSerializer.Deserialize<ImmutableArray<SourceBattleEvent>>(before)));
        if(single){var legacy=Envelope.Single(rows,rows.IndexOf(used));Assert.IsNotNull(AnimationDispatchFixture.AtAttackText(legacy,true,SourceBattleScene.On).Dispatch);}
    }
    [TestMethod]
    public void RealSelfCostAndPairedReductionOwnOneRootAndOneWaveForEveryPositionAndSelectedAlly()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(int target in Enumerable.Range(0,single?2:4).Where(x=>x!=actor))
        {var result=F.Ordered(Initial(single,actor),actor,target);Check(result.Events,single,58);Assert.AreEqual(0,result.State.Slots[actor].Body!.Pokemon.CurrentHp);Assert.AreEqual(4,result.State.Slots[target].Body!.AttackStage);Assert.AreEqual(-2,result.State.Slots[target].Body!.SpecialAttackStageDelta);}
    }
    [TestMethod,DataRow("substitute",null),DataRow("mist",null),DataRow("clear-body",null),DataRow("white-smoke",null),DataRow("hyper-cutter",49),DataRow("attack-minimum",49),DataRow("spatk-minimum",46),DataRow("clamp",58)]
    public void PostAnimationPreventionsAndPartialStatCapsKeepTheirSourceRootAndWave(string branch,int? wave)
    {
        foreach(bool single in new[]{false,true})
        {
            var f=Initial(single);
            if(branch=="substitute")f=F.Body(f,1,b=>b with{CalledVolatile=new(){SubstituteHp=1}});
            if(branch=="mist")f=f with{Sides=f.Sides.SetItem(1,f.Sides[1] with{MistTurnsRemaining=5})};
            if(branch is "clear-body" or "white-smoke" or "hyper-cutter")f=F.Body(f,1,b=>b with{CalledOverlay=new(){Ability=branch=="clear-body"?"ABILITY_CLEAR_BODY":branch=="white-smoke"?"ABILITY_WHITE_SMOKE":"ABILITY_HYPER_CUTTER"}});
            if(branch=="attack-minimum")f=F.Body(f,1,b=>b with{AttackStage=0});if(branch=="spatk-minimum")f=F.Body(f,1,b=>b with{SpecialAttackStageDelta=-6});if(branch=="clamp")f=F.Body(f,1,b=>b with{AttackStage=1,SpecialAttackStageDelta=-5});
            Check(F.Ordered(f,0,1).Events,single,wave);
        }
    }
    [TestMethod,DataRow("protect"),DataRow("both-minimum"),DataRow("asleep"),DataRow("no-pp")]
    public void PreAnimationFailureCannotBorrowSelfCostOrFinalFaint(string branch)
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Initial(single);if(branch=="protect")frame=F.Body(frame,1,b=>b with{Protected=true});if(branch=="both-minimum")frame=F.Body(frame,1,b=>b with{AttackStage=0,SpecialAttackStageDelta=-6});if(branch=="asleep")frame=F.Body(frame,0,b=>b with{Status=3});
            SourceFourActionResult result;
            if(branch=="no-pp")
            {var started=Begin(frame);frame=F.Body(started.State,0,b=>b with{Pokemon=b.Pokemon with{Moves=[b.Pokemon.Moves[0] with{CurrentPp=0}]}});result=Advance(started,frame);}
            else result=F.Ordered(frame,0,1);
            Deny(result.Events,single);Assert.AreEqual(branch=="protect",result.Events.OfType<SourceHpChangedEvent>().Any());Assert.AreEqual(branch=="protect",result.Events.OfType<SourceFaintedEvent>().Any());
        }
    }
    [TestMethod]
    public void LowHpPressureFaintStatusAndWhiteHerbUseCommittedPartyTailWithoutHalfHpAssumption()
    {
        foreach(bool single in new[]{false,true})foreach(int hp in new[]{1,7})
        {
            var f=F.Body(Initial(single),0,b=>b with{Status=WildBattleStatus.Burn,Pokemon=b.Pokemon with{CurrentHp=hp,Moves=[b.Pokemon.Moves[0] with{CurrentPp=1}]}});
            f=F.Body(f,1,b=>b with{CalledOverlay=new(){Ability="ABILITY_PRESSURE"},HeldItem="ITEM_WHITE_HERB"});var rows=F.Ordered(f,0,1).Events;Check(rows,single,58);Assert.AreEqual(2,rows.OfType<SourcePpCommandEvent>().Single().CommandCost);Assert.AreEqual(hp,rows.OfType<SourceHpChangedEvent>().Single().Damage);Assert.IsTrue(rows.OfType<SourceStatusChangedEvent>().Any(s=>s.Reason=="faint"));Assert.IsTrue(rows.OfType<SourceHeldItemConsumedEvent>().Any(s=>s.Item=="ITEM_WHITE_HERB"));
        }
    }
    [TestMethod,DataRow("FLY","RAYQUAZA"),DataRow("BOUNCE","PONYTA"),DataRow("DIG","DIGLETT"),DataRow("DIVE","WAILMER")]
    public void AuthenticSemiInvulnerableTargetKeepsRootAndStatWaveWithoutRevealingTheCharge(string charge,string species)
    {
        foreach(bool single in new[]{false,true})
        {
            // The shared Dive fixture is source-HM-compatible controlled evidence;
            // the other entrants learn their charge move naturally in this source.
            var body=charge=="DIVE"?F.Bodies(291,1)[1]:Kernel.Mon(species,charge,100);
            var frame=F.Body(Initial(single),1,_=>body with{SpeedStage=0});var setup=Envelope.Resolve(frame,1,0);
            Assert.IsTrue(setup.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Side==new BattlerSlot(1)&&e.Volatile=="called-charge"&&e.After==1));
            Assert.AreEqual(Kernel.Id(charge),setup.State.Slots[1].Registers.Turn!.Lock!.Move);
            var result=F.Ordered(setup.State,0,1);Check(result.Events,single,58);
            Assert.AreEqual(Kernel.Id(charge),result.State.Slots[1].Registers.Turn!.Lock!.Move);
            Assert.IsFalse(result.Events.OfType<SourceMoveMissedEvent>().Any());
            var used=result.Events.OfType<SourceMoveUsedEvent>().Single();var cue=Kernel.Project(result.Events,result.Events.IndexOf(used),single).Dispatch;Assert.IsNotNull(cue);
            MoveId?[] entry=new MoveId?[single?2:4];entry[1]=Kernel.Id(charge);
            var visibility=CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(result.Events,cue,entry);Assert.IsNotNull(visibility);Assert.IsTrue(visibility[0]);Assert.IsFalse(visibility[1]);
        }
    }
    [TestMethod]
    public void MissingForeignOrForgedCostStatFaintAndSyncEvidenceCannotAnimate()
    {
        foreach(bool single in new[]{false,true})
        {
            var rows=F.Ordered(Initial(single),0,1).Events;var hp=rows.OfType<SourceHpChangedEvent>().Single();var pp=rows.OfType<SourcePpCommandEvent>().Single();var faint=rows.OfType<SourceFaintedEvent>().Single();var sync=rows.OfType<SourceCalledPartyChangedEvent>().Single(s=>s.SourceSlot==new BattlerSlot(0));var stats=rows.OfType<SourceStatChangedEvent>().ToArray();
            foreach(var bad in new[]{hp with{Target=new(1)},hp with{Actor=new(1)},hp with{Damage=hp.Damage-1},hp with{HpAfter=1},hp with{Critical=true},hp with{PreparedAnimationPower=0},hp with{PreparedAnimationWeather=0}})Deny(rows.SetItem(rows.IndexOf(hp),bad),single);
            foreach(var bad in new[]{pp with{PpOwner=new(1)},pp with{CommandCost=0},pp with{ChosenMove=Kernel.Id("METRONOME")}})Deny(rows.SetItem(rows.IndexOf(pp),bad),single);
            foreach(var e in new SourceBattleEvent[]{hp,pp,faint,sync,stats[0],stats[1]}){Deny(Numbering.Number(rows.Remove(e)),single);Deny(Numbering.Number(rows.Insert(rows.IndexOf(e),e)),single);}
            Deny(rows.SetItem(rows.IndexOf(faint),faint with{Side=new(1)}),single);Deny(rows.SetItem(rows.IndexOf(stats[0]),stats[0] with{After=5}),single);
            Deny(rows.SetItem(rows.IndexOf(sync),sync with{SourceSlot=new(1)}),single);Deny(rows.SetItem(rows.IndexOf(sync),sync with{After=sync.After with{Pokemon=sync.After.Pokemon with{Friendship=1}}}),single);
            Deny(Numbering.Number(rows.SetItem(rows.IndexOf(stats[0]),stats[1]).SetItem(rows.IndexOf(stats[1]),stats[0])),single);
        }
    }
    [TestMethod]
    public void FaintFriendshipMetadataCannotBorrowAnotherMemberTurnOrValue()
    {
        foreach(bool single in new[]{false,true})
        {
            var result=F.Ordered(Initial(single),0,1);var rows=result.Events;Check(rows,single,58);
            var faint=rows.OfType<SourceFaintedEvent>().Single();var applied=faint.Friendship;Assert.IsNotNull(applied);
            int at=rows.IndexOf(faint);
            foreach(var invalid in new[]{
                faint with{Friendship=null},faint with{Turn=faint.Turn+1},faint with{Side=new(1)},
                faint with{Friendship=applied with{Member=new(BattleSide.Player,1)}},
                faint with{Friendship=applied with{Member=new(BattleSide.Opponent,0)}},
                faint with{Friendship=applied with{Before=applied.Before+1}},
                faint with{Friendship=applied with{After=applied.After+1}},
                faint with{Friendship=applied with{Before=-1}},faint with{Friendship=applied with{Before=256}},
                faint with{Friendship=applied with{After=-1}},faint with{Friendship=applied with{After=applied.Before+1}}})
                Deny(rows.SetItem(at,invalid),single);
            var sync=rows.OfType<SourceCalledPartyChangedEvent>().Single(s=>s.SourceSlot==faint.Side);
            Deny(rows.SetItem(rows.IndexOf(sync),sync with{PartyIndex=1}),single);
            Assert.IsNull(SourceBattleEventHooks.ValidateRouting(result.State,faint));
            // Missing metadata remains valid for ordinary manually authored receipts.
            Assert.IsNull(SourceBattleEventHooks.ValidateRouting(result.State,faint with{Friendship=null}));
            foreach(var invalid in new[]{
                faint with{Side=new(1)},faint with{Friendship=applied with{Member=new(BattleSide.Player,1)}},
                faint with{Friendship=applied with{Member=new(BattleSide.Opponent,0)}},
                faint with{Friendship=applied with{After=applied.After+1}},
                faint with{Friendship=applied with{Before=-1}},faint with{Friendship=applied with{Before=256}},
                faint with{Friendship=applied with{After=-1}},faint with{Friendship=applied with{After=applied.Before+1}}})
                Assert.AreEqual("source_event_faint_friendship",SourceBattleEventHooks.ValidateRouting(result.State,invalid));
            var opponent=F.Ordered(Initial(single,1),1,0);var opponentFaint=opponent.Events.OfType<SourceFaintedEvent>().Single();
            Assert.IsNull(opponentFaint.Friendship);
            var borrowed=opponentFaint with{Friendship=applied};
            Deny(opponent.Events.SetItem(opponent.Events.IndexOf(opponentFaint),borrowed),single);
            Assert.AreEqual("source_event_faint_friendship",SourceBattleEventHooks.ValidateRouting(opponent.State,borrowed));
        }
    }
    [TestMethod,DataRow(0,0),DataRow(1,0),DataRow(199,194),DataRow(200,190),DataRow(255,245)]
    public void SourceFaintFriendshipBandsAndZeroClampKeepRootWithoutRngChanges(int before,int after)
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=F.Body(Initial(single),0,b=>b with{Pokemon=b.Pokemon with{Friendship=before}});
            frame=F.Body(frame,1,_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0});
            var result=F.Ordered(frame,0,1);Check(result.Events,single,58);
            var faint=result.Events.OfType<SourceFaintedEvent>().Single();Assert.IsNotNull(faint.Friendship);
            Assert.AreEqual(before,faint.Friendship.Before);Assert.AreEqual(after,faint.Friendship.After);
            Assert.AreEqual(after,result.State.Party(BattleSide.Player).Members[0].Pokemon.Friendship);
            var control=F.Ordered(F.Body(frame,0,b=>b with{Pokemon=b.Pokemon with{Friendship=0}}),0,1);
            Assert.AreEqual(control.State.Rng,result.State.Rng);CollectionAssert.AreEqual(control.Trace.ToArray(),result.Trace.ToArray());
            if(before==0)
            {
                var optional=result.Events.SetItem(result.Events.IndexOf(faint),faint with{Friendship=null});
                var used=optional.OfType<SourceMoveUsedEvent>().Single();
                Assert.IsNotNull(Kernel.Project(optional,optional.IndexOf(used),single).Dispatch);
            }
        }
    }
    private static SourceFourActionResult Begin(SourceBattleFrame frame)
    {
        var choices=frame.Slots.Take(frame.BattlerCount).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,c.Slot.Value==0?new BattlerSlot(1):c.Slot)).ToImmutableArray();var s=SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog,frame,frame.Revision,choices);Assert.IsTrue(s.Succeeded,s.Fault);Assert.IsNotNull(s.Continuation);return s;
    }
    private static SourceFourActionResult Advance(SourceFourActionResult started,SourceBattleFrame? frame=null)
    {var r=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,frame??started.State,started.Continuation!.Value);Assert.IsTrue(r.Succeeded,r.Fault);return r;}
    [TestMethod]
    public void GenuineMetronomeLeafRetainsCallerPpAndOneSelfCostRoot()
    {
        foreach(bool single in new[]{false,true})
        {
            var bodies=Caller.Bodies(118);for(int i=1;i<4;i++)bodies[i]=Kernel.Mon("WAILORD","SPLASH",1) with{SpeedStage=0};var frame=Kernel.Frame(bodies,single);SourceFourActionResult? found=null;
            for(uint seed=0;seed<65536;seed++)
            {var start=Begin(frame with{Rng=new(seed)});var target=SourceMoveTargeting.ResolveLearned(start.State,new(0),new(1),Kernel.Mechanics.Catalog.Moves[Kernel.Id("METRONOME")]);Assert.IsTrue(target.Succeeded,target.Fault);if(WildBattleSourceCallers.Metronome(target.Rng).SourceMoveId!=262)continue;found=Advance(start);break;}
            Assert.IsNotNull(found);var hp=found.Events.OfType<SourceHpChangedEvent>().Single(e=>e.Move==Move);var cue=CalledLeafAnimationKernelFixture.Check(found.Events,found,single,Kernel.Id("METRONOME"),262,hp);Assert.AreEqual(new BattlerSlot(0),cue.Actor);Assert.AreEqual(found.Events.OfType<SourceCalledMoveTransitionEvent>().Single().Invocation.Target,cue.Target);Assert.AreEqual(1,found.Events.Select((_,i)=>Wave(found.Events,i,single)).Count(w=>w is not null));
            var faint=found.Events.OfType<SourceFaintedEvent>().Single();var sync=found.Events.OfType<SourceCalledPartyChangedEvent>().Single(e=>e.SourceSlot==cue.Actor);
            Assert.IsNotNull(faint.Friendship);Assert.AreEqual(sync.Before.Pokemon.Friendship,faint.Friendship.Before);Assert.AreEqual(sync.After.Pokemon.Friendship,faint.Friendship.After);
            Assert.AreEqual(new PartyMemberKey(sync.Side,sync.PartyIndex),faint.Friendship.Member);
        }
    }
}
