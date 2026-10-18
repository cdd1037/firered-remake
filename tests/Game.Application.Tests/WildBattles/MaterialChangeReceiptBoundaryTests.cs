using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Shared=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

// Public-kernel receipt order and event-time projection, independently of a view
// or final committed body. Controlled speed/HP are fixture setup, not route proof.
[TestClass,TestCategory("Game")]
public sealed class MaterialChangeReceiptBoundaryTests
{
    private static SourceBattleFrame Initial(bool single,int actor,bool substitute=false)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        bodies[actor]=Kernel.Mon(substitute?"MR_MIME":"DITTO",substitute?"SUBSTITUTE":"TRANSFORM",50) with{SpeedStage=12};
        return Kernel.Frame(bodies,single);
    }
    [TestMethod]
    public void TransformChangeFollowsOwnedPpAndProjectionChangesOnlyAtItsReceipt()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        foreach(int target in single?new[]{actor^1}:new[]{actor^1,actor^2,actor^3})
        {
            var initial=Initial(single,actor);var result=Shared.Ordered(initial,actor,target);var json=JsonSerializer.Serialize(result);
            var rows=result.Events;var attack=rows.OfType<SourceAttackStringEvent>().Single();var used=rows.OfType<SourceMoveUsedEvent>().Single();
            var pp=rows.OfType<SourcePpCommandEvent>().Single();var change=rows.OfType<SourceTransformedEvent>().Single();
            Assert.IsTrue(attack.Sequence<used.Sequence&&used.Sequence<pp.Sequence);Assert.AreEqual(pp.Sequence+1,change.Sequence);
            Assert.AreEqual(new BattlerSlot(actor),change.Actor);Assert.AreEqual(new BattlerSlot(target),change.Target);
            var seed=CampaignBattleDisplayBodies.FromSlots(initial.Slots);var before=new CampaignBattleDisplayBatch(seed).AdvanceThrough(rows,pp.Sequence);
            Assert.IsNull(before.Display.Bodies[actor].Transform);
            var after=before.AdvanceThrough(rows,change.Sequence);Assert.IsNotNull(after.Display.Bodies[actor].Transform);
            Assert.AreEqual(initial.Slots[target].Body!.Pokemon.Species,after.Display.Bodies[actor].Transform!.CopiedPokemon.Species);
            Assert.AreEqual(initial.Slots[actor].Body!.Pokemon.Personality,after.Display.Bodies[actor].Transform!.OriginalPokemon.Personality);
            Assert.AreEqual(after,after.AdvanceThrough(rows,change.Sequence));Assert.IsNull(before.Display.Bodies[actor].Transform);
            Assert.AreEqual(json,JsonSerializer.Serialize(result));
            if(single)
            {
                var exported=SourceBattleEventAdapter.Export(change) as WildBattleTransformedEvent;Assert.IsNotNull(exported);
                Assert.AreEqual(change.AppearancePersonality,exported.AppearancePersonality);Assert.AreEqual(change.AppearanceOriginalTrainerId,exported.AppearanceOriginalTrainerId);
            }
        }
    }
    [TestMethod]
    public void SubstitutePpPrecedesAttackAndMaterialMarkerFollowsCostWithoutReconstructingHp()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            var initial=Initial(single,actor,true);var result=Shared.Ordered(initial,actor,actor);var rows=result.Events;
            var pp=rows.OfType<SourcePpCommandEvent>().Single();var attack=rows.OfType<SourceAttackStringEvent>().Single();
            var used=rows.OfType<SourceMoveUsedEvent>().Single();var hp=rows.OfType<SourceHpChangedEvent>().Single();
            var marker=rows.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="substitute");
            Assert.AreEqual(pp.Sequence+1,attack.Sequence);Assert.AreEqual(attack.Sequence+1,used.Sequence);
            Assert.AreEqual(used.Sequence+1,hp.Sequence);Assert.AreEqual(hp.Sequence+1,marker.Sequence);
            Assert.AreEqual(new BattlerSlot(actor),hp.Actor);Assert.AreEqual(hp.Actor,hp.Target);Assert.AreEqual(hp.Actor,marker.Side);
            Assert.AreEqual(0,marker.Before);Assert.AreEqual(hp.HpBefore-hp.HpAfter,marker.After);Assert.AreEqual("move",marker.Reason);
            var seed=new CampaignBattleDisplayBatch(CampaignBattleDisplayBodies.FromSlots(initial.Slots));
            var attackDisplay=seed.AdvanceThrough(rows,used.Sequence);var costDisplay=attackDisplay.AdvanceThrough(rows,hp.Sequence);
            Assert.AreEqual(hp.HpBefore,attackDisplay.Display.Bodies[actor].Pokemon.CurrentHp);
            Assert.AreEqual(hp.HpAfter,costDisplay.Display.Bodies[actor].Pokemon.CurrentHp);
            Assert.AreEqual(0,costDisplay.Display.Bodies[actor].CalledVolatile?.SubstituteHp??0);
            var materialDisplay=costDisplay.AdvanceThrough(rows,marker.Sequence);
            Assert.AreEqual(marker.After,materialDisplay.Display.Bodies[actor].CalledVolatile!.SubstituteHp);
            Assert.AreEqual(materialDisplay,materialDisplay.AdvanceThrough(rows,marker.Sequence));
            Assert.AreEqual(hp.HpBefore,attackDisplay.Display.Bodies[actor].Pokemon.CurrentHp);
        }
    }
    [TestMethod]
    public void FailedSubstituteEmitsNeitherCostNorMaterialChangeButStillOwnsEarlierPp()
    {
        foreach(bool single in new[]{false,true})foreach(bool existing in new[]{false,true})
        {
            var frame=Initial(single,0,true);
            frame=Shared.Body(frame,0,b=>existing?b with{CalledVolatile=new(){SubstituteHp=20}}:
                b with{Pokemon=b.Pokemon with{CurrentHp=1}});
            var result=Shared.Ordered(frame,0,0);
            Assert.AreEqual(1,result.Events.OfType<SourcePpCommandEvent>().Count());
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(f=>f.Reason==(existing?"already-substitute":"substitute-insufficient-hp")));
            Assert.IsFalse(result.Events.OfType<SourceHpChangedEvent>().Any());
            Assert.IsFalse(result.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Volatile=="substitute"));
        }
    }
    [TestMethod]
    public void DonorDollDoesNotBlockTransformAndLaterDonorChangesDoNotRewriteCapturedBody()
    {
        var initial=Shared.Body(Initial(false,0),1,b=>b with{CalledVolatile=new(){SubstituteHp=20}});
        var result=Shared.Ordered(initial,0,1);var change=result.Events.OfType<SourceTransformedEvent>().Single();
        var projected=new CampaignBattleDisplayBatch(CampaignBattleDisplayBodies.FromSlots(initial.Slots)).AdvanceThrough(result.Events,change.Sequence);
        var captured=projected.Display.Bodies[0];Assert.IsNotNull(captured.Transform);
        var after=projected.Display.Apply(new SourceHpChangedEvent(change.Sequence+1,change.Turn,new(2),new(1),Kernel.Id("TACKLE"),
            projected.Display.Bodies[1].Pokemon.CurrentHp,1,10,false,100));
        Assert.AreEqual(captured,after.Bodies[0]);Assert.AreNotEqual(after.Bodies[1].Pokemon.CurrentHp,captured.Transform!.CopiedPokemon.CurrentHp);
    }
    [TestMethod]
    public void MaterialRequestUsesRealSingleOrDoubleProjectionCursorAndOneOwnedAttackPage()
    {
        foreach(bool single in new[]{false,true})foreach(bool substitute in new[]{false,true})
        for(int actor=0;actor<(single?2:4);actor++)
        {
            var initial=Initial(single,actor,substitute);int target=substitute?actor:actor^1;
            var result=Shared.Ordered(initial,actor,target);var rows=result.Events;var used=rows.OfType<SourceMoveUsedEvent>().Single();int at=rows.IndexOf(used);
            var decision=single?AnimationDispatchFixture.AtAttackText(Envelope.Single(rows,at),true,Game.Domain.Options.SourceBattleScene.On):
                AnimationDispatchFixture.AtAttackText(Envelope.Double(rows,at),Game.Domain.Options.SourceBattleScene.On);
            var dispatch=decision.Dispatch;Assert.IsNotNull(dispatch,decision.Reason);Assert.AreEqual(substitute?164:144,dispatch.SourceMoveId);
            Assert.IsTrue(dispatch.AcknowledgesCurrentReceipt);Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText,dispatch.Boundary);
            var marker=rows.Single(e=>substitute?e is SourceMtMoonVolatileEvent {Volatile:"substitute"}:e is SourceTransformedEvent);
            Assert.AreEqual(marker.Sequence,dispatch.ResultSequence);
            var visibility=CampaignMoveAnimationPresentation.SpriteVisibilityAtInvocation(rows,dispatch,Enumerable.Repeat<Game.Domain.Ids.MoveId?>(null,single?2:4).ToArray());
            Assert.IsNotNull(visibility);CollectionAssert.AreEqual(Enumerable.Repeat(true,single?2:4).ToArray(),visibility);
            var seed=CampaignBattleDisplayBodies.FromSlots(initial.Slots);int next=at+(single?1:0);var display=seed;
            for(int i=0;i<next;i++)display=display.Apply(rows[i]);
            string before=JsonSerializer.Serialize(display);
            var request=CampaignMoveAnimationPresentation.MaterialChangeAtInvocation(dispatch,display,rows,next,Kernel.Mechanics.Catalog.Moves);
            Assert.IsNotNull(request);Assert.AreEqual(display.Bodies[actor],request.BeforeBody);Assert.AreEqual(marker.Sequence,request.ResultSequence);
            var expected=display;for(int i=next;i<=rows.IndexOf(marker);i++)expected=expected.Apply(rows[i]);
            Assert.AreEqual(JsonSerializer.Serialize(expected.Bodies[actor]),JsonSerializer.Serialize(request.AfterBody));Assert.AreEqual(before,JsonSerializer.Serialize(display));
            Assert.IsNull(CampaignMoveAnimationPresentation.MaterialChangeAtInvocation(dispatch,display,rows,rows.IndexOf(marker)+1,Kernel.Mechanics.Catalog.Moves));
            Assert.IsNull(CampaignMoveAnimationPresentation.MaterialChangeAtInvocation(dispatch with{ResultSequence=marker.Sequence+1},display,rows,next,Kernel.Mechanics.Catalog.Moves));
            var behind=display with{Bodies=display.Bodies.SetItem(actor,display.Bodies[actor] with{CalledVolatile=new(){SubstituteHp=20}})};
            Assert.IsNull(CampaignMoveAnimationPresentation.MaterialChangeAtInvocation(dispatch,behind,rows,next,Kernel.Mechanics.Catalog.Moves));
            for(int i=0;i<rows.Length;i++)
            {
                var every=single?AnimationDispatchFixture.AtAttackText(Envelope.Single(rows,i),true,Game.Domain.Options.SourceBattleScene.On):
                    AnimationDispatchFixture.AtAttackText(Envelope.Double(rows,i),Game.Domain.Options.SourceBattleScene.On);
                Assert.AreEqual(i==at,every.Dispatch is not null,every.Reason);
            }
        }
    }
    [TestMethod]
    public void MaterialMatcherRejectsForeignPpOrResultAndMalformedSubstituteCost()
    {
        foreach(bool single in new[]{false,true})foreach(bool substitute in new[]{false,true})
        {
            var result=Shared.Ordered(Initial(single,0,substitute),0,substitute?0:1);var rows=result.Events;
            var used=rows.OfType<SourceMoveUsedEvent>().Single();var pp=rows.OfType<SourcePpCommandEvent>().Single();int at=rows.IndexOf(used);
            var changed=rows.SetItem(rows.IndexOf(pp),pp with{PpOwner=new(1)});
            void Denied(ImmutableArray<SourceBattleEvent> events)
            {
                var decision=single?AnimationDispatchFixture.AtAttackText(Envelope.Single(events,at),true,Game.Domain.Options.SourceBattleScene.On):
                    AnimationDispatchFixture.AtAttackText(Envelope.Double(events,at),Game.Domain.Options.SourceBattleScene.On);
                Assert.IsNull(decision.Dispatch,decision.Reason);
            }
            Denied(changed);
            if(substitute)
            {
                var hp=rows.OfType<SourceHpChangedEvent>().Single();Denied(rows.SetItem(rows.IndexOf(hp),hp with{Damage=hp.Damage+1}));
                var marker=rows.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="substitute");Denied(rows.SetItem(rows.IndexOf(marker),marker with{Side=new(1)}));
            }
            else
            {
                var marker=rows.OfType<SourceTransformedEvent>().Single();Denied(rows.SetItem(rows.IndexOf(marker),marker with{Actor=new(1)}));
                Denied(rows.SetItem(rows.IndexOf(marker),marker with{Sequence=pp.Sequence}));
            }
        }
    }

    [TestMethod]
    public void CopiedSubstituteKeepsTransformIdentityButChangesTheMaterialAtTheDollMarker()
    {
        foreach(bool single in new[]{false,true})
        {
            var initial=Shared.Body(Initial(single,0),1,_=>Kernel.Mon("MR_MIME","SUBSTITUTE",100) with{SpeedStage=0});
            var transformed=Shared.Ordered(initial,0,1);Assert.IsNotNull(transformed.State.Slots[0].Body!.Transform);
            var nextTurn=Shared.Body(Shared.CompleteTurn(transformed),0,b=>b with{SpeedStage=12});
            var result=Shared.Ordered(nextTurn,0,0);var rows=result.Events;var used=rows.OfType<SourceMoveUsedEvent>().Single();
            Assert.AreEqual(Kernel.Id("SUBSTITUTE"),used.Move);int at=rows.IndexOf(used);
            var dispatch=(single?AnimationDispatchFixture.AtAttackText(Envelope.Single(rows,at),true,Game.Domain.Options.SourceBattleScene.On):
                AnimationDispatchFixture.AtAttackText(Envelope.Double(rows,at),Game.Domain.Options.SourceBattleScene.On)).Dispatch;
            Assert.IsNotNull(dispatch);int cursor=at+(single?1:0);var display=CampaignBattleDisplayBodies.FromSlots(nextTurn.Slots);
            for(int i=0;i<cursor;i++)display=display.Apply(rows[i]);
            var request=CampaignMoveAnimationPresentation.MaterialChangeAtInvocation(dispatch,display,rows,cursor,Kernel.Mechanics.Catalog.Moves);
            Assert.IsNotNull(request);Assert.IsNotNull(request.BeforeBody.Transform);Assert.AreEqual(0,request.BeforeBody.CalledVolatile?.SubstituteHp??0);
            Assert.AreEqual(request.BeforeBody.Transform,request.AfterBody.Transform);Assert.IsTrue(request.AfterBody.CalledVolatile!.SubstituteHp>0);
            var marker=rows.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="substitute");Assert.AreEqual(marker.Sequence,request.ResultSequence);
        }
    }

}
