using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using F=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared=FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class RoundShadowChargeKernelTests
{
    [TestMethod,DataRow(19),DataRow(291),DataRow(340)]
    public void ActualSetupAndReleaseKeepTheirPpOwnersTurnsAndPreClearSpriteSnapshots(int id)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(bool substitute in new[]{false,true})
        {
            var frame=F.Initial(id,single,actor);string initial=JsonSerializer.Serialize(frame);var setup=F.Ordered(frame,actor,actor^1);var marker=setup.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge");
            var pp=setup.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(pp.Sequence+1,marker.Sequence);Assert.IsFalse(setup.Events.Any(e=>e is SourceAttackStringEvent or SourceMoveUsedEvent));var cue=F.Check(setup.Events,id,single,marker,marker,0);
            MoveId?[] entry=new MoveId?[single?2:4];var visibility=CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(setup.Events,cue,entry);Assert.IsNotNull(visibility);Assert.IsTrue(visibility.All(v=>v));Assert.AreEqual(initial,JsonSerializer.Serialize(frame));
            var charged=F.CompleteTurn(setup);entry[actor]=F.Move(id);if(substitute)charged=F.Body(charged,actor^1,b=>b with{CalledVolatile=new(){SubstituteHp=100}});
            string before=JsonSerializer.Serialize(charged);var release=F.Ordered(charged,actor,actor^1);var used=release.Events.OfType<SourceMoveUsedEvent>().Single();var hit=Envelope.Hits(release.Events).First();var dispatched=F.Check(release.Events,id,single,used,hit,1);
            pp=release.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(pp.Before,pp.After);Assert.AreEqual(1,pp.CommandCost);Assert.IsNull(release.State.Slots[actor].Registers.Turn?.Lock);
            var flags=CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(release.Events,dispatched,entry);Assert.IsNotNull(flags);for(int slot=0;slot<flags.Length;slot++)Assert.AreEqual(slot!=actor,flags[slot],"Status3 cleanup does not reveal the actor before its authored release.");
            Assert.AreEqual(before,JsonSerializer.Serialize(charged));CollectionAssert.AreEqual(entry,Enumerable.Range(0,entry.Length).Select(i=>i==actor?(MoveId?)F.Move(id):null).ToArray());
        }
    }
    [TestMethod,DataRow(19),DataRow(291),DataRow(340)]
    public void FinalPpUnderPressureIsSpentOnceAndTheZeroPpReleaseStillHasItsExactMarker(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var charged=F.Charged(id,single,pp:1,pressure:true);Assert.AreEqual(0,charged.Slots[0].Body!.Pokemon.Moves[0].CurrentPp);var release=F.Ordered(charged,0,1);var pp=release.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(0,pp.Before);Assert.AreEqual(0,pp.After);Assert.AreEqual(2,pp.CommandCost);
            F.Check(release.Events,id,single,release.Events.OfType<SourceMoveUsedEvent>().Single(),Envelope.Hits(release.Events).First(),1);
        }
    }
    [TestMethod,DataRow(19),DataRow(291),DataRow(340)]
    public void SetupAndReleaseRejectForeignMissingDuplicateAndMalformedOwnership(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var setup=F.Ordered(F.Initial(id,single),0,1).Events;var pp=setup.OfType<SourcePpCommandEvent>().Single();var mark=setup.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge");
            F.Deny(setup.Remove(pp),single);F.Deny(setup.SetItem(setup.IndexOf(pp),pp with{PpOwner=new(1)}),single);F.Deny(setup.SetItem(setup.IndexOf(mark),mark with{Before=1}),single);F.Deny(Shared.Number(setup.Insert(setup.IndexOf(mark)+1,mark)),single);
            var release=F.Ordered(F.Charged(id,single),0,1).Events;pp=release.OfType<SourcePpCommandEvent>().Single();mark=release.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge");var owner=release.OfType<SourceMoveUsedEvent>().Single();var cue=F.Project(release,release.IndexOf(owner),single,false).Dispatch;Assert.IsNotNull(cue);
            MoveId?[] entry=new MoveId?[single?2:4];entry[0]=F.Move(id);
            foreach(var bad in new[]{release.Remove(mark),release.SetItem(release.IndexOf(mark),mark with{Side=new(1)}),release.SetItem(release.IndexOf(mark),mark with{Reason="sleep"}),Shared.Number(release.Insert(release.IndexOf(mark),mark)),release.SetItem(release.IndexOf(pp),pp with{PpOwner=new(1)}),release.SetItem(release.IndexOf(pp),pp with{After=pp.Before-1})})F.Deny(bad,single);
            Assert.IsNull(CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(release.Remove(mark),cue,entry));Assert.IsNull(CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(release.SetItem(release.IndexOf(mark),mark with{Side=new(1)}),cue,entry));
        }
    }
    [TestMethod,DataRow(19),DataRow(291),DataRow(340)]
    public void FailedReleaseNeverBorrowsTheSetupAndTheOwnedPoseEndsOnlyAfterTheResultBatch(int id)
    {
        foreach(bool single in new[]{false,true})foreach(string branch in new[]{"miss","protected","paralysis","sleep"})
        {
            var setup=F.Ordered(F.Initial(id,single),0,1);var marker=setup.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge");var setupCue=F.Check(setup.Events,id,single,marker,marker,0);var charged=F.CompleteTurn(setup);
            if(branch=="miss"){charged=F.Body(charged,0,b=>b with{AccuracyStage=0});charged=F.Body(charged,1,b=>b with{EvasionStage=12});}
            if(branch=="protected")charged=F.Body(charged,1,b=>b with{Protected=true});
            if(branch=="sleep")charged=F.Body(charged,0,b=>b with{Status=3});
            if(branch=="paralysis")charged=F.Body(charged,0,b=>b with{Status=WildBattleStatus.Paralysis});
            var failed=branch=="miss"?F.Find(charged,0,1,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),$"{id}-release-miss"):
                branch=="paralysis"?F.Find(charged,0,1,r=>r.Events.OfType<SourceActionBlockedEvent>().Any(e=>e.Side.Value==0&&e.Move==F.Move(id)&&e.Status==WildBattleStatus.Paralysis),$"{id}-release-paralysis"):F.Ordered(charged,0,1);
            F.Deny(failed.Events,single);
            bool Clears(SourceFourActionResult result)=>result.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Side.Value==0&&e.Volatile=="called-charge"&&e.Before==1&&e.After==0);
            var batches=new List<SourceFourActionResult>{failed};var completed=failed;
            if(branch=="sleep")
            {
                Assert.IsTrue(failed.Events.OfType<SourceActionBlockedEvent>().Any(e=>e.Side.Value==0&&e.Move==F.Move(id)&&WildBattleStatus.IsSleeping(e.Status)));
                Assert.IsFalse(Clears(failed),"The actual sleep canceller has no charge-clear receipt; end-turn preparation owns that clear.");
                while(!Clears(completed))
                {
                    Assert.IsTrue(batches.Count<32&&!completed.CompletedCleanTurn,"The public turn must expose its sleep charge clear.");Assert.IsNotNull(completed.Continuation);
                    completed=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,completed.State,completed.Continuation.Value);Assert.IsTrue(completed.Succeeded,completed.Fault);batches.Add(completed);
                }
            }
            Assert.IsTrue(Clears(completed),$"{id}/{single}/{branch}: require the tested actor's genuine clear.");
            var lifetime=new CampaignChargeAnimationPoseLifetime();Assert.IsTrue(lifetime.Capture(setupCue,setup.Events));
            foreach(var batch in batches)
            {
                foreach(var page in batch.Events)Assert.IsNull(lifetime.Observe(batch.Events,page.Sequence),"The held setup must survive every still-visible result page, including the final one.");
                if(!Clears(batch))Assert.IsNull(lifetime.Observe(batch.Events,null),"An unrelated or sleep-blocked batch cannot fabricate the later clear.");
            }
            Assert.AreEqual(0,lifetime.Observe(completed.Events,null));Assert.IsNull(lifetime.Observe(completed.Events,null));
            Assert.IsTrue(lifetime.Capture(setupCue,setup.Events));Assert.IsNull(lifetime.Observe(completed.Events,completed.Events[^1].Sequence));
            var next=F.CompleteTurn(completed);var nextAction=F.Ordered(next,0,1);Assert.AreEqual(0,lifetime.Observe(nextAction.Events,nextAction.Events[0].Sequence),"Higher-sequence batch proves the pending result batch was consumed.");
        }
    }
    [TestMethod,DataRow(19),DataRow(291),DataRow(340)]
    public void CompletedPoseTrackerRejectsForeignAndAmbiguousClearReceipts(int id)
    {
        var setup=F.Ordered(F.Initial(id,false),0,1);var marker=setup.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge");var cue=F.Check(setup.Events,id,false,marker,marker,0);var released=F.Ordered(F.CompleteTurn(setup),0,1).Events;var clear=released.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge");
        foreach(var bad in new[]{released.Remove(clear),released.SetItem(released.IndexOf(clear),clear with{Side=new(1)}),released.SetItem(released.IndexOf(clear),clear with{Before=0}),Shared.Number(released.Insert(0,clear))})
        {var lifetime=new CampaignChargeAnimationPoseLifetime();Assert.IsTrue(lifetime.Capture(cue,setup.Events));Assert.IsNull(lifetime.Observe(bad,null));}
        var owner=new CampaignChargeAnimationPoseLifetime();Assert.IsFalse(owner.Capture(cue with{Actor=new(1)},setup.Events));Assert.IsNull(owner.Observe(released,null));Assert.IsTrue(owner.Capture(cue,setup.Events));owner.Clear();Assert.IsNull(owner.Observe(released,null));
    }
    [TestMethod,DataRow(19),DataRow(291),DataRow(340)]
    public void ChargeOwnedHoldSurvivesRepeatedCursorAndLaterHeldItemPagesButNotCancelOrReplacement(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var initial=F.Body(F.Initial(id,single),0,b=>b with{HeldItem="ITEM_SHELL_BELL",Pokemon=b.Pokemon with{CurrentHp=1}});
            var setup=F.Ordered(initial,0,1);var marker=setup.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge");var cue=F.Check(setup.Events,id,single,marker,marker,0);
            var release=F.Ordered(F.CompleteTurn(setup),0,1);var hit=Envelope.Hits(release.Events).First();F.Check(release.Events,id,single,release.Events.OfType<SourceMoveUsedEvent>().Single(),hit,1);
            Assert.IsTrue(release.State.Slots[0].Body!.Pokemon.CurrentHp>1,"Actual Shell Bell callback follows the successful release hit.");
            var suffix=release.Events.Where(e=>e.Sequence>hit.Sequence).ToArray();Assert.IsTrue(suffix.Length>0,"The invocation includes its genuine post-hit callback receipts.");
            var hold=new CampaignChargeAnimationPoseLifetime();Assert.IsTrue(hold.Capture(cue,setup.Events));
            Assert.IsNull(hold.Observe(setup.Events,null),"ACK of setup is not release.");
            foreach(var page in release.Events)
            {Assert.IsNull(hold.Observe(release.Events,page.Sequence));Assert.IsNull(hold.Observe(release.Events,page.Sequence),"A repeated cursor must never release the hold.");}
            Assert.AreEqual(0,hold.Observe(release.Events,null));Assert.IsNull(hold.Observe(release.Events,null));
            Assert.IsTrue(hold.Capture(cue,setup.Events));Assert.IsNull(hold.Observe(release.Events,release.Events[^1].Sequence));Assert.AreEqual(0,hold.Observe([],release.Events[^1].Sequence+1));
            Assert.IsTrue(hold.Capture(cue,setup.Events));hold.DropSlot(1);Assert.AreEqual(0,hold.Observe(release.Events,null),"An unrelated replacement cannot erase charge ownership.");
            Assert.IsTrue(hold.Capture(cue,setup.Events));hold.DropSlot(0);Assert.IsNull(hold.Observe(release.Events,null),"Replacing the source member invalidates its completed pose.");
            Assert.IsTrue(hold.Capture(cue,setup.Events));hold.Clear();Assert.IsNull(hold.Observe(release.Events,null),"Cancel or battle replacement cannot retain a charge-owned hold.");
        }
    }

    [TestMethod]
    public void TwoActualChargedActorsKeepIndependentOwnershipAcrossAThirdActorsAction()
    {
        var bodies=F.Bodies(19);bodies[2]=F.Bodies(291,2)[2] with{SpeedStage=11};var first=F.Ordered(Kernel.Frame(bodies,false),0,1);
        var mark0=first.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge");var cue0=F.Check(first.Events,19,false,mark0,mark0,0);
        Assert.IsNotNull(first.Continuation);var second=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,first.State,first.Continuation.Value);Assert.IsTrue(second.Succeeded,second.Fault);
        var mark2=second.Events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge");Assert.AreEqual(2,mark2.Side.Value);var cue2=F.Check(second.Events,291,false,mark2,mark2,0);
        var hold=new CampaignChargeAnimationPoseLifetime();Assert.IsTrue(hold.Capture(cue0,first.Events));Assert.IsTrue(hold.Capture(cue2,second.Events));
        Assert.IsNotNull(second.Continuation);var third=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,second.State,second.Continuation.Value);Assert.IsTrue(third.Succeeded,third.Fault);Assert.IsNull(hold.Observe(third.Events,null));
        var release=F.Ordered(F.CompleteTurn(third),0,1);var used=release.Events.OfType<SourceMoveUsedEvent>().Single();var releaseCue=F.Check(release.Events,19,false,used,Envelope.Hits(release.Events).First(),1);Assert.IsTrue(hold.CompleteRelease(releaseCue,release.Events));Assert.IsFalse(hold.CompleteRelease(releaseCue,release.Events));
        Assert.IsNotNull(release.Continuation);var dive=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,release.State,release.Continuation.Value);Assert.IsTrue(dive.Succeeded,dive.Fault);Assert.IsTrue(dive.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Side.Value==2&&e.Volatile=="called-charge"&&e.After==0));
        Assert.IsNull(hold.Observe(dive.Events,dive.Events[^1].Sequence));Assert.AreEqual(2,hold.Observe(dive.Events,null));Assert.IsNull(hold.Observe(dive.Events,null));
    }

}
