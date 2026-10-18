using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Charge=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared=FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;
using Legal=FireRed.Tests.Application.WildBattles.ConditionalRecoveryAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// Controlled battle fixtures use authenticated natural/machine learners and public
// ordered kernel actions. Renderer availability comes only from generated packs.
[TestClass,TestCategory("Game")]
public sealed class SurfDigAnimationKernelTests
{
    private static MoveId Move(int id)=>Kernel.Id(id switch{57=>"SURF",330=>"MUDDY_WATER",91=>"DIG",_=>throw new ArgumentOutOfRangeException(nameof(id))});
    private static SourceBattleFrame Initial(int id,bool single,int actor=0,bool pressure=false,int? pp=null)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        bodies[actor]=(id switch{57=>Legal.MachineMon("SQUIRTLE","HM03","SURF"),330=>Kernel.Mon("MARSHTOMP","MUDDY_WATER",37),91=>Kernel.Mon("DIGLETT","DIG"),_=>throw new ArgumentOutOfRangeException(nameof(id))}) with{SpeedStage=12,AccuracyStage=12};
        if(pressure)for(int i=0;i<(single?2:4);i++)if((i&1)!=(actor&1))
        {bodies[i]=Kernel.Mon("DUSCLOPS","LEER",100) with{SpeedStage=0};Assert.AreEqual("ABILITY_PRESSURE",WildBattleEffective.Ability(bodies[i]));}
        var frame=Kernel.Frame(bodies,single);
        return pp is {} remaining?Charge.Body(frame,actor,b=>b with{Pokemon=b.Pokemon with{Moves=[b.Pokemon.Moves[0] with{CurrentPp=remaining}]}}):frame;
    }
    private static int Target(SourceBattleEvent row)=>row switch{SourceHpChangedEvent h=>h.Target.Value,SourceSubstituteDamagedEvent s=>s.Target.Value,_=>throw new ArgumentException("Expected owned damage receipt.")};
    private static SourceBattleEvent Hit(ImmutableArray<SourceBattleEvent> rows,int target)=>Envelope.Hits(rows).First(e=>Target(e)==target);
    private static CampaignMoveAnimationDispatch Check(SourceFourActionResult result,int id,bool single,SourceBattleEvent outcome,bool setup=false)
    {
        string before=JsonSerializer.Serialize(result);var rows=result.Events;var commands=rows.OfType<SourcePpCommandEvent>().ToArray();
        int target=setup?commands[0].Target.Value:Target(outcome);var pp=commands.Single(p=>p.Target.Value==target);
        var used=rows.OfType<SourceMoveUsedEvent>().SingleOrDefault();var attack=rows.OfType<SourceAttackStringEvent>().SingleOrDefault();SourceBattleEvent owner=setup?outcome:used!;
        Assert.AreEqual(Move(id),pp.CurrentMove);Assert.AreEqual(pp.CurrentMove,pp.ChosenMove);Assert.AreEqual(pp.Actor,pp.PpOwner);
        if(setup){Assert.IsNull(used);Assert.IsNull(attack);Assert.AreEqual(pp.Sequence+1,owner.Sequence);}
        else{Assert.IsNotNull(used);Assert.IsNotNull(attack);Assert.AreEqual(Move(id),used.Move);Assert.AreEqual(used.Move,attack.ChosenMove);Assert.AreEqual(used.Move,attack.CurrentMove);}
        int count=0;CampaignMoveAnimationDispatch? found=null;
        for(int i=0;i<rows.Length;i++)foreach(bool atResult in new[]{false,true})
        {
            var first=Charge.Project(rows,i,single,atResult);Assert.AreEqual(first,Charge.Project(rows,i,single,atResult));
            Assert.IsNull(Charge.Project(rows,i,single,atResult,SourceBattleScene.Off).Dispatch);
            if(id!=330)Assert.IsNull(Charge.Wave(rows,i,single));
            if(single)Assert.IsNull(atResult?AnimationDispatchFixture.AtResult(Charge.Single(rows,i),false,SourceBattleScene.On).Dispatch:AnimationDispatchFixture.AtAttackText(Charge.Single(rows,i),false,SourceBattleScene.On).Dispatch);
            if(rows[i]!=owner||atResult!=setup){Assert.IsNull(first.Dispatch,first.Reason);continue;}
            found=first.Dispatch;Assert.IsNotNull(found,first.Reason);count++;
            Assert.AreEqual(id,found.SourceMoveId);Assert.AreEqual(owner.Sequence,found.Sequence);Assert.AreEqual(outcome.Sequence,found.ResultSequence);
            Assert.AreEqual(pp.Sequence,found.PpSequence);Assert.AreEqual(pp.Sequence,found.SnapshotSequence);Assert.AreEqual(pp.Actor,found.Actor);Assert.AreEqual(target,found.Target.Value);
            Assert.AreEqual(id==91?(setup?(byte?)0:(byte?)1):null,found.MoveTurn);Assert.AreEqual(!setup,found.AcknowledgesCurrentReceipt);
            Assert.AreEqual(setup?CampaignMoveAnimationBoundary.BeforeChargeResult:CampaignMoveAnimationBoundary.AfterAttackText,found.Boundary);
            Assert.AreEqual(outcome is SourceSubstituteDamagedEvent,found.HitsSubstitute);Assert.AreEqual(attack?.Sequence,found.AttackSequence);
        }
        Assert.AreEqual(1,count);Assert.AreEqual(before,JsonSerializer.Serialize(result));return found!;
    }
    private static CampaignMoveAnimationDispatch Setup(SourceFourActionResult result,bool single)=>Check(result,91,single,result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge"&&e.Reason=="move"),true);

    [TestMethod,DataRow(57),DataRow(330)]
    public void PublishedSpreadRootsOwnOneAnimationAcrossAllPositionsTargetOrdersAndSubstitutes(int id)
    {
        Assert.IsTrue(AnimationDispatchFixture.Supports(id,Move(id).Value),"The real canonical pack must publish this root.");
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(int target in single?new[]{actor^1}:new[]{actor^1,actor^3})foreach(bool substitute in new[]{false,true})
        {
            var frame=Initial(id,single,actor);if(substitute)frame=Charge.Body(frame,target,b=>b with{CalledVolatile=new(){SubstituteHp=100}});
            var result=Charge.Ordered(frame,actor,target);var pp=result.Events.OfType<SourcePpCommandEvent>().ToArray();
            CollectionAssert.AreEqual(single?new[]{target}:new[]{target,target^2},pp.Select(p=>p.Target.Value).ToArray());
            Assert.AreEqual(Math.Max(0,pp[0].Before-pp[0].CommandCost),pp[0].After);
            if(!single){Assert.AreEqual(pp[0].After,pp[1].Before);Assert.AreEqual(pp[1].Before,pp[1].After);}
            Check(result,id,single,Hit(result.Events,target));
        }
    }

    [TestMethod,DataRow(57),DataRow(330)]
    public void SpreadPressureSpendsLastPpOnceAndFirstPreventedTargetYieldsToItsPartner(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var pressure=Charge.Ordered(Initial(id,single,pressure:true,pp:1),0,1);var pp=pressure.Events.OfType<SourcePpCommandEvent>().ToArray();
            Assert.AreEqual(single?2:3,pp[0].CommandCost);Assert.AreEqual(1,pp[0].Before);Assert.AreEqual(0,pp[0].After);
            foreach(var retained in pp.Skip(1)){Assert.AreEqual(0,retained.Before);Assert.AreEqual(0,retained.After);Assert.AreEqual(3,retained.CommandCost);}
            Check(pressure,id,single,Hit(pressure.Events,1));
        }
        foreach(int actor in Enumerable.Range(0,4))foreach(string branch in new[]{"protect","absorb","miss"})
        {
            int target=actor^1;var frame=Initial(id,false,actor);
            if(branch=="protect")frame=Charge.Body(frame,target,b=>b with{Protected=true});
            if(branch=="absorb")frame=Charge.Body(frame,target,_=>Kernel.Mon("VAPOREON","TACKLE",100) with{SpeedStage=0});
            if(branch=="miss"){frame=Charge.Body(frame,actor,b=>b with{AccuracyStage=0});frame=Charge.Body(frame,target,b=>b with{EvasionStage=12});}
            var result=branch=="miss"?Charge.Find(frame,actor,target,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(e=>e.Target.Value==target)&&Envelope.Hits(r.Events).Any(e=>Target(e)==(target^2)),"spread first-pass miss"):Charge.Ordered(frame,actor,target);
            if(branch=="absorb")Assert.IsTrue(result.Events.OfType<SourceAbilityPreventedEvent>().Any(e=>e.Target.Value==target));
            Check(result,id,false,Hit(result.Events,target^2));
        }
    }

    [TestMethod,DataRow(57),DataRow(330)]
    public void AllPreventedSpreadPassesNeverBorrowAnAnimationFromAttackText(int id)
    {
        foreach(bool single in new[]{false,true})foreach(string branch in new[]{"protect","absorb","miss"})
        {
            var frame=Initial(id,single);if(branch=="miss")frame=Charge.Body(frame,0,b=>b with{AccuracyStage=0});
            foreach(int target in single?new[]{1}:new[]{1,3})frame=Charge.Body(frame,target,b=>branch switch{
                "protect"=>b with{Protected=true},"absorb"=>Kernel.Mon("VAPOREON","TACKLE",100) with{SpeedStage=0},_=>b with{EvasionStage=12}});
            var result=branch=="miss"?Charge.Find(frame,0,1,r=>r.Events.OfType<SourceMoveMissedEvent>().Count()==(single?1:2),"spread all-pass miss"):Charge.Ordered(frame,0,1);
            Assert.IsTrue(result.Events.OfType<SourceMoveUsedEvent>().Any());Charge.Deny(result.Events,single);
        }
    }

    [TestMethod]
    public void DigSetupAndRetainedReleaseKeepThePreClearHiddenPoseAcrossAllPositions()
    {
        Assert.IsTrue(AnimationDispatchFixture.Supports(91,Move(91).Value));
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(bool substitute in new[]{false,true})
        {
            var setup=Charge.Ordered(Initial(91,single,actor),actor,actor^1);var setupCue=Setup(setup,single);MoveId?[] entry=new MoveId?[single?2:4];
            var visible=CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(setup.Events,setupCue,entry);Assert.IsNotNull(visible);Assert.IsTrue(visible.All(v=>v));
            var charged=Charge.CompleteTurn(setup);entry[actor]=Move(91);if(substitute)charged=Charge.Body(charged,actor^1,b=>b with{CalledVolatile=new(){SubstituteHp=100}});
            var release=Charge.Ordered(charged,actor,actor^1);var cue=Check(release,91,single,Hit(release.Events,actor^1));
            var pp=release.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(pp.Before,pp.After);Assert.IsNull(release.State.Slots[actor].Registers.Turn?.Lock);
            var hidden=CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(release.Events,cue,entry);Assert.IsNotNull(hidden);for(int slot=0;slot<hidden.Length;slot++)Assert.AreEqual(slot!=actor,hidden[slot]);
            var hold=new CampaignChargeAnimationPoseLifetime();Assert.IsTrue(hold.Capture(setupCue,setup.Events));Assert.IsNull(hold.Observe(setup.Events,null));Assert.IsTrue(hold.CompleteRelease(cue,release.Events));Assert.IsFalse(hold.CompleteRelease(cue,release.Events));Assert.IsNull(hold.Observe(release.Events,null));
        }
    }

    [TestMethod]
    public void DigPressureConsumesFinalPpDuringSetupAndReleasesAtZeroWithoutSecondSpend()
    {
        foreach(bool single in new[]{false,true})
        {
            var setup=Charge.Ordered(Initial(91,single,pressure:true,pp:1),0,1);Setup(setup,single);var first=setup.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(2,first.CommandCost);Assert.AreEqual(0,first.After);
            var release=Charge.Ordered(Charge.CompleteTurn(setup),0,1);Check(release,91,single,Hit(release.Events,1));var pp=release.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(0,pp.Before);Assert.AreEqual(0,pp.After);Assert.AreEqual(2,pp.CommandCost);
        }
    }

    [TestMethod]
    public void DigFailedReleaseKeepsItsPoseUntilTheAuthenticClearBatchIsConsumed()
    {
        foreach(bool single in new[]{false,true})foreach(string branch in new[]{"protect","miss","sleep","paralysis"})
        {
            var setup=Charge.Ordered(Initial(91,single),0,1);var cue=Setup(setup,single);var charged=Charge.CompleteTurn(setup);
            if(branch=="protect")charged=Charge.Body(charged,1,b=>b with{Protected=true});
            if(branch=="miss"){charged=Charge.Body(charged,0,b=>b with{AccuracyStage=0});charged=Charge.Body(charged,1,b=>b with{EvasionStage=12});}
            if(branch=="sleep")charged=Charge.Body(charged,0,b=>b with{Status=3});
            if(branch=="paralysis")charged=Charge.Body(charged,0,b=>b with{Status=WildBattleStatus.Paralysis});
            var failed=branch=="miss"?Charge.Find(charged,0,1,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),"Dig miss"):
                branch=="paralysis"?Charge.Find(charged,0,1,r=>r.Events.OfType<SourceActionBlockedEvent>().Any(e=>e.Side.Value==0&&e.Status==WildBattleStatus.Paralysis),"Dig paralysis"):Charge.Ordered(charged,0,1);
            Charge.Deny(failed.Events,single);var hold=new CampaignChargeAnimationPoseLifetime();Assert.IsTrue(hold.Capture(cue,setup.Events));
            bool Clears(SourceFourActionResult r)=>r.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Side.Value==0&&e.Volatile=="called-charge"&&e.Before==1&&e.After==0);
            var current=failed;int steps=0;
            while(true)
            {
                foreach(var page in current.Events)Assert.IsNull(hold.Observe(current.Events,page.Sequence));
                if(Clears(current))break;
                Assert.IsNull(hold.Observe(current.Events,null));Assert.IsTrue(++steps<32&&!current.CompletedCleanTurn);Assert.IsNotNull(current.Continuation);
                current=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,current.State,current.Continuation.Value);Assert.IsTrue(current.Succeeded,current.Fault);
            }
            Assert.AreEqual(0,hold.Observe(current.Events,null));Assert.IsNull(hold.Observe(current.Events,null));
            Assert.IsTrue(hold.Capture(cue,setup.Events));hold.Clear();Assert.IsNull(hold.Observe(current.Events,null));
            Assert.IsTrue(hold.Capture(cue,setup.Events));hold.DropSlot(0);Assert.IsNull(hold.Observe(current.Events,null));
        }
    }

    [TestMethod,DataRow(57),DataRow(330),DataRow(91)]
    public void SleepAndPpLostAfterPublicSelectionCannotProduceTheSelectedRoot(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var sleeping=Charge.Body(Initial(id,single),0,b=>b with{Status=3});Charge.Deny(Charge.Ordered(sleeping,0,1).Events,single);
            var frame=Initial(id,single);var choices=frame.Slots.Take(frame.BattlerCount).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,c.Slot.Value==0?new BattlerSlot(1):c.Slot)).ToImmutableArray();
            var started=SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog,frame,frame.Revision,choices);Assert.IsTrue(started.Succeeded,started.Fault);Assert.IsNotNull(started.Continuation);Assert.AreEqual(0,started.State.Queue[0].Slot.Value);
            var empty=Charge.Body(started.State,0,b=>b with{Pokemon=b.Pokemon with{Moves=[b.Pokemon.Moves[0] with{CurrentPp=0}]}});
            var result=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,empty,started.Continuation.Value);Assert.IsTrue(result.Succeeded,result.Fault);Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="no-pp"));Charge.Deny(result.Events,single);
        }
    }

    [TestMethod,DataRow(57),DataRow(330),DataRow(91)]
    public void ForeignMissingDuplicatePpAndChargeMarkersCannotAuthenticateActualRows(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var result=Charge.Ordered(Initial(id,single),0,1);
            foreach(var phase in id==91?new[]{result,Charge.Ordered(Charge.CompleteTurn(result),0,1)}:new[]{result})
            {
                var rows=phase.Events;foreach(var pp in rows.OfType<SourcePpCommandEvent>())
                {
                    int index=rows.IndexOf(pp);Charge.Deny(rows.SetItem(index,pp with{PpOwner=new(1)}),single);Charge.Deny(rows.SetItem(index,pp with{ChosenMove=Kernel.Id("METRONOME")}),single);
                    Charge.Deny(Shared.Number(rows.Remove(pp)),single);Charge.Deny(Shared.Number(rows.Insert(index,pp)),single);
                }
                foreach(var mark in rows.OfType<SourceMtMoonVolatileEvent>().Where(e=>e.Volatile=="called-charge"))
                {Charge.Deny(Shared.Number(rows.Remove(mark)),single);Charge.Deny(rows.SetItem(rows.IndexOf(mark),mark with{Side=new(1)}),single);Charge.Deny(Shared.Number(rows.Insert(rows.IndexOf(mark),mark)),single);}
            }
        }
    }

    [TestMethod,DataRow(57),DataRow(330),DataRow(91)]
    public void GenuineRowsRequireCurrentMetadataAndPublishedCanonicalScript(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var first=Charge.Ordered(Initial(id,single),0,1);
            foreach(var phase in id==91?new[]{first,Charge.Ordered(Charge.CompleteTurn(first),0,1)}:new[]{first})
            {
                var rows=phase.Events;bool setup=!rows.OfType<SourceMoveUsedEvent>().Any();SourceBattleEvent owner=setup?rows.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge"):rows.OfType<SourceMoveUsedEvent>().Single();int index=rows.IndexOf(owner);var moves=Kernel.Mechanics.Catalog.Moves;
                foreach(var metadata in new[]{null,moves.Remove(Move(id)),moves.SetItem(Move(id),moves[Move(id)] with{SourceMoveId=1}),moves.SetItem(Move(id),moves[Move(id)] with{Target="UNKNOWN_TARGET"})})
                {
                    var decision=single?(setup?AnimationDispatchFixture.AtResult(Charge.Single(rows,index),true,SourceBattleScene.On,metadata):AnimationDispatchFixture.AtAttackText(Charge.Single(rows,index),true,SourceBattleScene.On,metadata)):
                        setup?AnimationDispatchFixture.AtResult(Envelope.Double(rows,index),SourceBattleScene.On,metadata):AnimationDispatchFixture.AtAttackText(Envelope.Double(rows,index),SourceBattleScene.On,metadata);
                    Assert.IsNull(decision.Dispatch);
                }
                var unsupported=single?(setup?CampaignMoveAnimationPresentation.AtResult(Charge.Single(rows,index),true,SourceBattleScene.On,moves,(_,_)=>false):CampaignMoveAnimationPresentation.AtAttackText(Charge.Single(rows,index),true,SourceBattleScene.On,moves,(_,_)=>false)):
                    setup?CampaignMoveAnimationPresentation.AtResult(Envelope.Double(rows,index),SourceBattleScene.On,moves,(_,_)=>false):CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(rows,index),SourceBattleScene.On,moves,(_,_)=>false);
                Assert.IsNull(unsupported.Dispatch);
            }
        }
    }
}
