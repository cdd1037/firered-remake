using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Numbering=FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class RolePlayAnimationKernelTests
{
    private static SourceBattleFrame Initial(bool single,int actor=0)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();bodies[actor]=Kernel.Mon("KADABRA","ROLE_PLAY",50) with{SpeedStage=12};return Kernel.Frame(bodies,single);
    }
    private static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events,int index,bool single,bool result=false,SourceBattleScene scene=SourceBattleScene.On)
    {
        if(single){var b=Envelope.Single(events,index);return result?AnimationDispatchFixture.AtResult(b,true,scene):AnimationDispatchFixture.AtAttackText(b,true,scene);}
        return result?AnimationDispatchFixture.AtResult(Envelope.Double(events,index),scene):AnimationDispatchFixture.AtAttackText(Envelope.Double(events,index),scene);
    }
    private static void Deny(ImmutableArray<SourceBattleEvent> events,bool single)
    {for(int i=0;i<events.Length;i++)foreach(bool result in new[]{false,true})Assert.IsNull(Project(events,i,single,result).Dispatch);}
    private static CampaignMoveAnimationDispatch Check(ImmutableArray<SourceBattleEvent> events,bool single)
    {
        var before=JsonSerializer.Serialize(events);var used=events.OfType<SourceMoveUsedEvent>().Single();var attack=events.OfType<SourceAttackStringEvent>().Single();var pp=events.OfType<SourcePpCommandEvent>().Single();var copied=events.OfType<SourceCalledOverlayChangedEvent>().Single();int count=0;CampaignMoveAnimationDispatch? found=null;
        for(int i=0;i<events.Length;i++)foreach(bool result in new[]{false,true})
        {
            var decision=Project(events,i,single,result);Assert.AreEqual(decision,Project(events,i,single,result));Assert.IsNull(Project(events,i,single,result,SourceBattleScene.Off).Dispatch);
            if(events[i]!=used||result){Assert.IsNull(decision.Dispatch,decision.Reason);continue;}
            var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);found=cue;count++;Assert.AreEqual(272,cue.SourceMoveId);Assert.AreEqual(attack.Sequence,cue.AttackSequence);Assert.AreEqual(pp.Sequence,cue.PpSequence);Assert.AreEqual(copied.Sequence,cue.ResultSequence);Assert.AreEqual(used.Actor,cue.Actor);Assert.AreEqual(used.Target,cue.Target);Assert.IsFalse(cue.HitsSubstitute);Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
        }
        Assert.AreEqual(1,count);Assert.AreEqual(before,JsonSerializer.Serialize(events));return found!;
    }
    [TestMethod]
    public void NaturalRolePlayAuthenticatesActorOwnedAbilityCopyAcrossSidesAndPartnerTargets()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(int target in single?new[]{actor^1}:new[]{actor^1,actor^2,actor^3})
        {
            var frame=Initial(single,actor);var before=JsonSerializer.Serialize(frame);var result=Shared.Ordered(frame,actor,target);var cue=Check(result.Events,single);
            var request=CampaignAdditionalMonPresentation.RolePlay(cue,CampaignBattleDisplayBodies.FromSlots(frame.Slots),result.State,result.Events);Assert.IsNotNull(request);
            var selected=frame.Slots[target].Member!.Value;var identity=frame.Party(actor%2==0?BattleSide.Opponent:BattleSide.Player).Members[selected.Index].Pokemon;
            Assert.AreEqual(frame.Slots[target].Body!.Pokemon.SourceSpeciesId,request.Species);Assert.AreEqual(identity.Personality,request.Personality);Assert.AreEqual(identity.OriginalTrainerId,request.OriginalTrainerId);Assert.AreEqual(actor%2==0,request.Back);Assert.AreEqual(before,JsonSerializer.Serialize(frame));
        }
    }
    [TestMethod]
    public void SameAbilitySubstituteProtectionAndAccuracyDoNotInventFailures()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Initial(single);string ability=WildBattleEffective.Ability(frame.Slots[1].Body!);frame=Shared.Body(frame,0,b=>b with{CalledOverlay=new(){Ability=ability},AccuracyStage=0});frame=Shared.Body(frame,1,b=>b with{CalledVolatile=new(){SubstituteHp=40},Protected=true,EvasionStage=12});var result=Shared.Ordered(frame,0,1);Check(result.Events,single);Assert.IsFalse(result.Events.OfType<SourceMoveFailedEvent>().Any());
        }
    }
    [TestMethod,DataRow("ABILITY_NONE"),DataRow("ABILITY_WONDER_GUARD")]
    public void UncopyableTargetAbilityHasNoSuccessfulRoot(string ability)
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Shared.Body(Initial(single),1,b=>b with{CalledOverlay=new(){Ability=ability}});var result=Shared.Ordered(frame,0,1);
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="uncopyable-ability"));Assert.IsFalse(result.Events.OfType<SourceCalledOverlayChangedEvent>().Any());Deny(result.Events,single);
        }
    }
    [TestMethod,DataRow("side"),DataRow("source"),DataRow("move"),DataRow("none"),DataRow("wonder"),DataRow("type"),DataRow("duplicate"),DataRow("sequence"),DataRow("turn"),DataRow("owner"),DataRow("called")]
    public void RolePlayRejectsMalformedForeignOrDuplicateReceiptOwnership(string mutation)
    {
        // The legacy singles overlay receipt has no Source field. Its adapter
        // deliberately erases that field, so only canonical doubles can prove
        // foreign-source rejection; all other mutations cover both schemas.
        foreach(bool single in mutation=="source"?new[]{false}:new[]{false,true})
        {
            var events=Shared.Ordered(Initial(single),0,1).Events;var marker=events.OfType<SourceCalledOverlayChangedEvent>().Single();int i=events.IndexOf(marker);var pp=events.OfType<SourcePpCommandEvent>().Single();var attack=events.OfType<SourceAttackStringEvent>().Single();
            events=mutation switch{
                "side"=>events.SetItem(i,marker with{Side=new(1)}),"source"=>events.SetItem(i,marker with{Source=new(1)}),"move"=>events.SetItem(i,marker with{Move=Kernel.Id("SKILL_SWAP")}),"none"=>events.SetItem(i,marker with{After=marker.After! with{Ability="ABILITY_NONE"}}),"wonder"=>events.SetItem(i,marker with{After=marker.After! with{Ability="ABILITY_WONDER_GUARD"}}),"type"=>events.SetItem(i,marker with{After=marker.After! with{Type1="TYPE_FIRE"}}),"duplicate"=>Numbering.Number(events.Insert(i+1,marker)),"sequence"=>events.SetItem(i,marker with{Sequence=pp.Sequence}),"turn"=>events.SetItem(i,marker with{Turn=marker.Turn+1}),"owner"=>events.SetItem(events.IndexOf(pp),pp with{PpOwner=new(1)}),_=>events.SetItem(events.IndexOf(attack),attack with{ChosenMove=Kernel.Id("METRONOME")})};Deny(events,single);
        }
    }
    [TestMethod]
    public void AdditionalMonSelectionRejectsInventedAbilityIdentityMutationAndForeignPpTarget()
    {
        var frame=Initial(false);var result=Shared.Ordered(frame,0,1);var cue=Check(result.Events,false);var display=CampaignBattleDisplayBodies.FromSlots(frame.Slots);var copied=result.Events.OfType<SourceCalledOverlayChangedEvent>().Single();int at=result.Events.IndexOf(copied);
        var wrong=result.Events.SetItem(at,copied with{After=copied.After! with{Ability="ABILITY_FAKE"}});Assert.IsNull(CampaignAdditionalMonPresentation.RolePlay(cue,display,result.State,wrong));
        var pp=result.Events.OfType<SourcePpCommandEvent>().Single();wrong=result.Events.SetItem(result.Events.IndexOf(pp),pp with{Target=new(3)});Assert.IsNull(CampaignAdditionalMonPresentation.RolePlay(cue,display,result.State,wrong));
        var party=result.State.Parties[1];var member=party.Members[0];var changed=member with{Pokemon=member.Pokemon with{Personality=member.Pokemon.Personality^1}};var altered=result.State with{Parties=result.State.Parties.SetItem(1,party with{Members=party.Members.SetItem(0,changed)})};Assert.IsNull(CampaignAdditionalMonPresentation.RolePlay(cue,display,altered,result.Events));
        var appended=result.Events.Add(new SourceCalledPartyChangedEvent(result.Events[^1].Sequence+1,0,BattleSide.Opponent,0,"fixture-identity-change",member,changed));Assert.IsNull(CampaignAdditionalMonPresentation.RolePlay(cue,display,altered,appended));
    }
    [TestMethod]
    public void ActualTransformThenSameSideRolePlayUsesOppositeActorPartyIdentityAndBaseSpecies()
    {
        var frame=Initial(false);
        frame=Shared.Body(frame,1,_=>Kernel.Mon("UNOWN","HIDDEN_POWER",50,personality:0) with{SpeedStage=0});
        frame=Shared.Body(frame,2,_=>Kernel.Mon("DITTO","TRANSFORM",100) with{SpeedStage=0});
        frame=Shared.Body(frame,3,_=>{var other=Kernel.Mon("WAILORD","SPLASH",100,personality:3);return other with{SpeedStage=0,Pokemon=other.Pokemon with{OriginalTrainerId=0x12345678}};});
        var transformed=Envelope.Resolve(frame,2,1);
        var receipt=transformed.Events.OfType<SourceTransformedEvent>().Single();
        Assert.AreEqual(new BattlerSlot(2),receipt.Actor);Assert.AreEqual(new BattlerSlot(1),receipt.Target);
        var displayed=CampaignBattleDisplayBodies.FromSlots(frame.Slots);
        foreach(var change in transformed.Events)displayed=displayed.Apply(change);
        var target=displayed.Bodies[2];Assert.IsNotNull(target.Transform);
        Assert.AreEqual(Kernel.Mon("DITTO","TRANSFORM",100).Pokemon.Species,WildBattleTransformationRules.PersistentPokemon(target).Species);
        Assert.AreEqual(201,target.Transform.CopiedPokemon.SourceSpeciesId);
        Assert.AreEqual((uint)0,target.Transform.CopiedPokemon.Personality);
        var result=Envelope.Resolve(transformed.State,0,2);var cue=Check(result.Events,false);
        displayed=new CampaignBattleDisplayBatch(displayed).AdvanceThrough(result.Events,cue.SnapshotSequence).Display;
        Assert.AreEqual("ABILITY_LEVITATE",result.Events.OfType<SourceCalledOverlayChangedEvent>().Single().After!.Ability);
        var committed=result.State;
        var request=CampaignAdditionalMonPresentation.RolePlay(cue,displayed,committed,result.Events);Assert.IsNotNull(request);
        Assert.AreEqual(201,request.Species);Assert.AreEqual(UnownFormRules.RenderSpeciesId(3),request.RenderSpecies);
        Assert.AreEqual((uint)3,request.Personality);Assert.AreEqual((uint)0x12345678,request.OriginalTrainerId);
        Assert.AreNotEqual(target.Transform.CopiedPokemon.Personality,request.Personality);
        // Controlled malformed membership: target index2 exists on its own
        // party but is absent opposite; do not invent a zero-padded native cell.
        var player=committed.Parties[0];committed=committed with{Parties=committed.Parties.SetItem(0,player with{Members=player.Members.Add(player.Members[1])})};
        displayed=displayed with{Members=displayed.Members.SetItem(2,new(BattleSide.Player,2))};
        Assert.IsNull(CampaignAdditionalMonPresentation.RolePlay(cue,displayed,committed,result.Events));
    }
}
