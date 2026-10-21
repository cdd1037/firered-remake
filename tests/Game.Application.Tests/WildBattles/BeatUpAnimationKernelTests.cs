using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared = FireRed.Tests.Application.WildBattles.BeatUpAnimationKernelFixture;
using BodyFixture = FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class BeatUpAnimationKernelTests
{
    [TestMethod]
    public void PublicOrderedTurnsUseEligibleContributorOrdinalsAcrossAllSlotsAndSelectedAllies()
    {
        var definition=Kernel.Mechanics.Catalog.Moves[Shared.Move];Assert.AreEqual(251,definition.SourceMoveId);Assert.AreEqual("EFFECT_BEAT_UP",definition.Effect);
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        foreach(int target in single?new[]{actor^1}:new[]{actor^1,actor^2})
        {
            var result=Kernel.Ordered(Shared.OrderedFrame(single,actor),actor,target);
            var eligible=single&&actor==1?new[]{0}:new[]{0,3,5};
            CollectionAssert.AreEqual(eligible,result.Events.OfType<SourceCalledBeatUpMemberEvent>().Select(m=>m.PartyIndex).ToArray());
            Assert.IsTrue(result.Events.OfType<SourceCalledBeatUpMemberEvent>().All(m=>m.Side==(BattleSide)(actor&1)));
            Assert.AreEqual(eligible.Length,Envelope.Hits(result.Events).Length);Shared.Check(result,single);
        }
    }
    [TestMethod]
    public void CanonicalOwnedGiftEggIsSkippedWithoutChangingTheFirstContributorOrdinal()
    {
        var content=CurrentCampaignFixture.Catalog.PokemonContent!;var owned=content.Pokemon;
        var egg=SourceGiftEggRules.GiveEgg(content,content.Catalog.BreedingData!,SourceGiftEggRules.Togepi,1,new(42,"RED",0),4,new(0)).Egg;
        Assert.IsTrue(egg.IsEgg);Assert.IsNull(PokemonStateValidator.ValidateParty(content,egg));
        foreach(bool single in new[]{false,true})foreach(int actor in single?new[]{0}:new[]{0,2})
        {
            var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("SNORLAX","DEFENSE_CURL",100)).ToArray();
            var natural=Kernel.Mon("CHARMANDER","SCRATCH");
            // This controlled slot is an authenticated source egg move, rather than
            // claiming that campaign-unadmitted Sneasel can own a carried egg.
            Assert.IsTrue(content.Catalog.BreedingData!.Species[natural.Pokemon.Species].EggMoves.Contains(Shared.Move));
            int pp=content.Catalog.Moves[Shared.Move].Pp;
            bodies[actor]=natural with{AccuracyStage=12,Pokemon=natural.Pokemon with{Moves=[new(Shared.Move,pp,pp)]}};
            var frame=Kernel.Frame(bodies,single);var party=frame.Parties[0];
            var members=party.Members.Insert(0,new(egg.Pokemon,IsEgg:true));
            var slots=frame.Slots.Select(cell=>cell.Member is {Side:BattleSide.Player} key
                ?cell with{Member=new(BattleSide.Player,key.Index+1)}:cell).ToImmutableArray();
            frame=frame with{Campaign=owned,SourceCampaignRulesVersion=GamePokemonCatalog.Profile,
                Parties=frame.Parties.SetItem(0,party with{Members=members}),Slots=slots,
                EntryAbilities=frame.EntryAbilities! with{AppliedEntries=slots.Select(cell=>cell.Occupant).ToImmutableArray()}};
            Assert.IsNull(SourceBattleIdentityRules.Validate(frame));
            Assert.IsNull(SourcePokemonValidator.ValidateLive(owned.Mechanics,frame));
            var result=SourceBattleKernel.ResolveAction(owned.Mechanics.Catalog,frame,new(frame.Slots[actor].Occupant!.Value,frame.Revision,0,new(actor^1)));
            Assert.IsTrue(result.Succeeded,result.Fault);Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            CollectionAssert.AreEqual(single?new[]{1}:new[]{1,2},result.Events.OfType<SourceCalledBeatUpMemberEvent>().Select(m=>m.PartyIndex).ToArray());
            Assert.AreEqual(egg.Pokemon,result.State.Parties[0].Members[0].Pokemon);Shared.Check(result,single);
        }
    }
    [TestMethod]
    public void SixHealthyContributorsProduceSixReceiptsWithoutMultiHitSummary()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            var result=Envelope.Resolve(Shared.Frame(single,actor,gapped:false),actor,actor^1);
            CollectionAssert.AreEqual(Enumerable.Range(0,6).ToArray(),result.Events.OfType<SourceCalledBeatUpMemberEvent>().Select(m=>m.PartyIndex).ToArray());
            Shared.Check(result,single);
        }
    }
    [TestMethod,DataRow("substitute-break"),DataRow("substitute-survives"),DataRow("ko"),DataRow("endure")]
    public void SubstituteKoAndEndureKeepOnlyExecutedContributorHitPairs(string branch)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            int target=actor^1;var frame=branch=="ko"?Shared.OrderedFrame(single,actor):Shared.Frame(single,actor);
            frame=BodyFixture.Body(frame,target,body=>branch.StartsWith("substitute",StringComparison.Ordinal)
                ?body with{CalledVolatile=new(){SubstituteHp=branch=="substitute-break"?1:body.Pokemon.Stats.MaxHp/4}}
                :body with{Enduring=branch=="endure",Pokemon=body.Pokemon with{CurrentHp=1}});
            if(branch=="ko")
            {
                var result=Kernel.Ordered(frame,actor,target);Assert.AreEqual(1,result.Events.OfType<SourceCalledBeatUpMemberEvent>().Count());
                Assert.AreEqual(0,result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);
                Assert.IsTrue(result.Events.OfType<SourceFaintedEvent>().Any(f=>f.Side.Value==target));Shared.Check(result,single);continue;
            }
            var direct=Envelope.Resolve(frame,actor,target);Assert.AreEqual(3,direct.Events.OfType<SourceCalledBeatUpMemberEvent>().Count());
            if(branch=="substitute-break")
            {Assert.AreEqual(0,direct.Events.OfType<SourceSubstituteDamagedEvent>().Single().HpAfter);Assert.AreEqual(2,direct.Events.OfType<SourceHpChangedEvent>().Count());}
            else if(branch=="substitute-survives")
            {Assert.AreEqual(3,direct.Events.OfType<SourceSubstituteDamagedEvent>().Count());Assert.IsTrue(direct.Events.OfType<SourceSubstituteDamagedEvent>().Last().HpAfter>0);}
            else
            {
                Assert.AreEqual(3,direct.Events.OfType<SourceDamageAdjustedEvent>().Count(a=>a.Endured&&a.DamageAfter==0));
                Assert.IsTrue(direct.Events.OfType<SourceHpChangedEvent>().All(h=>h.HpBefore==1&&h.HpAfter==1&&h.Damage==0));
            }
            Shared.Check(direct,single);
        }
    }
    [TestMethod]
    public void BeatUpHasNoContactAndItsCommittedZeroLossHitsDoNotBorrowContactEffects()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=BodyFixture.Body(Shared.Frame(single),1,_=>Kernel.Mon("CARVANHA","BITE",100) with{Enduring=true,Pokemon=Kernel.Mon("CARVANHA","BITE",100).Pokemon with{CurrentHp=1}});
            Assert.AreEqual("ABILITY_ROUGH_SKIN",WildBattleEffective.Ability(frame.Slots[1].Body!));
            var result=Envelope.Resolve(frame);Assert.IsFalse(result.Events.OfType<SourceAbilityDamageEvent>().Any());
            Assert.IsFalse(result.Events.OfType<SourceStatusChangedEvent>().Any(e=>e.Reason=="contact"));
            Assert.AreEqual(frame.Slots[0].Body!.Pokemon.CurrentHp,result.State.Slots[0].Body!.Pokemon.CurrentHp);Shared.Check(result,single);
        }
    }
    [TestMethod,DataRow("protect"),DataRow("miss"),DataRow("no-contributors")]
    public void ActualBlockedAndEmptyEligibilityResultsCannotAuthorizeAnimation(string branch)
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Shared.Frame(single);
            if(branch=="protect")frame=BodyFixture.Body(frame,1,b=>b with{Protected=true});
            if(branch=="miss")frame=BodyFixture.Body(frame,0,b=>b with{AccuracyStage=0});
            if(branch=="no-contributors")
            {
                foreach(int slot in single?new[]{0}:new[]{0,2})frame=BodyFixture.Body(frame,slot,b=>b with{Status=WildBattleStatus.Poison});
                var party=frame.Parties[0];frame=frame with{Parties=frame.Parties.SetItem(0,party with{Members=party.Members.Select(m=>m with{Status=WildBattleStatus.Poison}).ToImmutableArray()})};
            }
            var result=branch=="miss"?Envelope.Find(frame,0,1,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),"Beat Up source miss"):Envelope.Resolve(frame);
            Assert.AreEqual(0,Envelope.Hits(result.Events).Length);Assert.AreEqual(0,result.Events.OfType<SourceCalledBeatUpMemberEvent>().Count());
            if(branch=="no-contributors")Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(f=>f.Reason=="no-eligible-contributor"));
            Shared.Deny(result.Events,single,branch);
        }
    }
    [TestMethod]
    public void PressureConsumesOneOwnedCommandAndSinglesUseExportedContributorsWithoutBackchannel()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=BodyFixture.Body(Shared.Frame(single),1,_=>Kernel.Mon("ARTICUNO","MIST",100));
            var result=Envelope.Resolve(frame);Assert.AreEqual(2,result.Events.OfType<SourcePpCommandEvent>().Single().CommandCost);Shared.Check(result,single);
            if(!single)continue;
            var hits=Envelope.Hits(result.Events);int at=result.Events.IndexOf(hits[1]);var exported=Shared.Single(result.Events,at);
            // The sole event stream carries the original contributor receipts, without a mirrored backchannel.
            Assert.AreEqual(result.Events,exported.Events);
            for(int i=0;i<result.Events.Length;i++)Assert.AreSame(result.Events[i],exported.Events[i]);
            Assert.AreEqual(3,exported.Events.OfType<SourceCalledBeatUpMemberEvent>().Count());
            Assert.AreEqual((byte)1,AnimationDispatchFixture.AtResult(exported,true,SourceBattleScene.On).Dispatch!.MoveTurn);
            var member=exported.Events.OfType<SourceCalledBeatUpMemberEvent>().First();int mi=exported.Events.IndexOf(member);
            Assert.IsNull(AnimationDispatchFixture.AtResult(exported with{Events=exported.Events.SetItem(mi,member with{Side=BattleSide.Opponent})},true,SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(exported with{Events=exported.Events.RemoveAt(mi)},true,SourceBattleScene.On).Dispatch);
        }
    }
    [TestMethod]
    public void ActualMetronomeCalledBeatUpOwnsEachAuthenticatedContributorHit()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=BodyFixture.Body(Shared.Frame(single),0,_=>Kernel.Mon("TOGETIC","METRONOME",29));
            frame=frame with{Rng=new(CallerPreludeAnimationKernelFixture.MetronomeSeed(251))};
            var result=Envelope.Resolve(frame);Assert.IsTrue(result.Events.OfType<SourceCalledMoveTransitionEvent>().Any(t=>t.Invocation.CurrentMove==Shared.Move));
            Assert.IsTrue(result.Events.OfType<SourceCalledBeatUpMemberEvent>().Any());
            var hits = Envelope.Hits(result.Events);
            var contributors = result.Events.OfType<SourceCalledBeatUpMemberEvent>().ToArray();
            Assert.AreEqual(contributors.Length, hits.Length); Assert.IsTrue(hits.Length > 0);
            Assert.IsFalse(result.Events.OfType<SourceMultiHitEvent>().Any());
            var cues = CalledLeafAnimationKernelFixture.CheckMany(result.Events, result, single, Kernel.Id("METRONOME"), 251,
                hits.Select(hit => (hit, CampaignMoveAnimationBoundary.BeforeHitResult, (BattlerSlot?)null)).ToArray());
            for (int ordinal = 0; ordinal < hits.Length; ordinal++)
            {
                Assert.AreEqual((byte)ordinal, cues[ordinal].MoveTurn);
                Assert.IsTrue(contributors[ordinal].Sequence < hits[ordinal].Sequence);
                if (ordinal + 1 < hits.Length) Assert.IsTrue(hits[ordinal].Sequence < contributors[ordinal + 1].Sequence);
            }
        }
    }
    [TestMethod]
    public void MissingOrDuplicatedContributorsOrHitsAndPendingTruncationFailClosed()
    {
        foreach(bool single in new[]{false,true})
        {
            var events=Envelope.Resolve(Shared.Frame(single)).Events;var members=events.OfType<SourceCalledBeatUpMemberEvent>().ToArray();var hits=Envelope.Hits(events);
            foreach(var member in members)Shared.Deny(events.Remove(member),single,"Orphan hit");
            foreach(var hit in hits)Shared.Deny(events.Remove(hit),single,"Unpaired contributor");
            int first=events.IndexOf(members[0]),second=events.IndexOf(members[1]),last=events.IndexOf(members[^1]);
            Shared.Deny(Shared.Number(events.Insert(first+1,members[0])),single,"Duplicate contributor before hit");
            Shared.Deny(Shared.Number(events.Insert(events.IndexOf(hits[0])+1,hits[0])),single,"Duplicate hit without contributor");
            Shared.Deny(events.Take(last+1).ToImmutableArray(),single,"Truncated pending contributor");
            Shared.Deny(Shared.Number(events.Add(members[^1] with{PartyIndex=5})),single,"Dangling contributor after complete pair");
            var summary=new SourceMultiHitEvent(0,events[0].Turn,new(0),new(1),Shared.Move,3,3);
            Shared.Deny(Shared.Number(events.Add(summary)),single,"Invented multihit summary");
            // A prefix containing only complete committed pairs has no source planned-hit
            // summary. It is a valid bounded window; do not infer absent party members.
            var completePrefix=events.Take(second).ToImmutableArray();Shared.Check(completePrefix,completePrefix,single);
        }
    }
    [TestMethod]
    public void ContributorSideIndexSpeciesSequenceAndHitIdentityAreValidated()
    {
        foreach(bool single in new[]{false,true})
        {
            var events=Envelope.Resolve(Shared.Frame(single)).Events;var member=events.OfType<SourceCalledBeatUpMemberEvent>().ElementAt(1);int mi=events.IndexOf(member);
            foreach(var wrong in new[]{member with{Side=BattleSide.Opponent},member with{PartyIndex=-1},member with{PartyIndex=6},member with{PartyIndex=0},
                member with{Species=default},member with{Sequence=events[mi-1].Sequence}})
                Shared.Deny(events.SetItem(mi,wrong),single,"Malformed contributor");
            var crossed=events.SetItem(mi,member with{Turn=member.Turn+1});
            foreach(var hit in Envelope.Hits(events).Skip(1))Assert.IsNull(Shared.Project(crossed,events.IndexOf(hit),single).Dispatch,"A member from another turn cannot own this hit.");
            var hp=events.OfType<SourceHpChangedEvent>().First();int hi=events.IndexOf(hp);
            foreach(var wrong in new[]{hp with{Actor=new(1)},hp with{Target=new(0)},hp with{Move=Kernel.Id("TACKLE")},hp with{HpBefore=0},
                hp with{HpAfter=-1},hp with{HpAfter=hp.HpBefore+1},hp with{Damage=-1},hp with{RandomPercent=84},hp with{Turn=hp.Turn+1},hp with{Sequence=member.Sequence}})
                Shared.Deny(events.SetItem(hi,wrong),single,"Malformed hit");
        }
    }
    [TestMethod]
    public void ExtraContributorAfterBodyKoFailsWhileSubstituteBreakPermitsTheNextMember()
    {
        foreach(bool single in new[]{false,true})
        {
            var events=Envelope.Resolve(Shared.Frame(single)).Events;var first=events.OfType<SourceHpChangedEvent>().First();
            Shared.Deny(events.SetItem(events.IndexOf(first),first with{HpAfter=0}),single,"Contributor after body KO");
            var substitute=new SourceSubstituteDamagedEvent(first.Sequence,first.Turn,first.Actor,first.Target,first.Move,1,0,first.Damage,first.Critical,first.RandomPercent);
            Shared.Check(events.SetItem(events.IndexOf(first),substitute),events,single);
            foreach(var bad in new[]{substitute with{Actor=new(1)},substitute with{Target=new(0)},substitute with{Move=Kernel.Id("TACKLE")},substitute with{HpBefore=0},substitute with{HpAfter=-1}})
                Shared.Deny(events.SetItem(events.IndexOf(first),bad),single,"Malformed substitute owner");
        }
    }
    [TestMethod]
    public void AuthenticDirectEnvelopePpAndMetadataRemainMandatory()
    {
        foreach(bool single in new[]{false,true})
        {
            var events=Envelope.Resolve(Shared.Frame(single)).Events;var attack=events.OfType<SourceAttackStringEvent>().Single();var pp=events.OfType<SourcePpCommandEvent>().Single();
            int ai=events.IndexOf(attack),pi=events.IndexOf(pp);var used=events.OfType<SourceMoveUsedEvent>().Single();
            foreach(var bad in new[]{attack with{Actor=new(1)},attack with{ChosenMove=Kernel.Id("METRONOME")},attack with{CurrentMove=Kernel.Id("TACKLE")},attack with{Sequence=used.Sequence}})
                Shared.Deny(events.SetItem(ai,bad),single,"Unowned direct attack");
            foreach(var bad in new[]{pp with{Actor=new(1)},pp with{PpOwner=new(1)},pp with{Target=new(0)},pp with{ChosenMove=Kernel.Id("METRONOME")},
                pp with{CurrentMove=Kernel.Id("TACKLE")},pp with{PpSlot=3},pp with{Before=pp.Before+1},pp with{After=pp.Before},pp with{CommandCost=0}})
                Shared.Deny(events.SetItem(pi,bad),single,"Unowned direct PP");
            Shared.Deny(events.RemoveAt(ai),single,"Missing attack");Shared.Deny(events.RemoveAt(pi),single,"Missing PP");
            Shared.Deny(Shared.Number(events.Insert(pi+1,pp)),single,"Duplicate PP");
            var first=Envelope.Hits(events)[0];int cursor=events.IndexOf(first);var moves=Kernel.Mechanics.Catalog.Moves;
            foreach(var metadata in new[]{null,moves.Remove(Shared.Move),moves.SetItem(Shared.Move,moves[Shared.Move] with{SourceMoveId=4})})
                Assert.IsNull(single?AnimationDispatchFixture.AtResult(Shared.Single(events,cursor),true,SourceBattleScene.On,metadata).Dispatch
                    :AnimationDispatchFixture.AtResult(Envelope.Double(events,cursor),SourceBattleScene.On,metadata).Dispatch);
            Assert.IsNull(single?CampaignMoveAnimationPresentation.AtResult(Shared.Single(events,cursor),true,SourceBattleScene.On,moves,(_,_)=>false).Dispatch
                :CampaignMoveAnimationPresentation.AtResult(Envelope.Double(events,cursor),SourceBattleScene.On,moves,(_,_)=>false).Dispatch);
            SourceBattleEvent[] failures=[new SourceMoveFailedEvent(0,pp.Turn,pp.Actor,pp.Target,Shared.Move,"protected"),
                new SourceMoveMissedEvent(0,pp.Turn,pp.Actor,pp.Target,Shared.Move),new SourceAbilityPreventedEvent(0,pp.Turn,pp.Actor,pp.Target,Shared.Move,"ABILITY_WONDER_GUARD")];
            foreach(var failed in failures)Shared.Deny(Shared.Number(events.Insert(cursor,failed)),single,"Blocked invocation");
        }
    }
    [TestMethod]
    public void NextActionCannotCompletePendingContributorAndItsOrdinalRestartsAtZero()
    {
        var first=Envelope.Resolve(Shared.Frame()).Events;var next=Envelope.Resolve(Shared.Frame(actor:2),actor:2,target:3).Events;
        var combined=Shared.Number(first.Concat(next));int nextHit=Array.FindIndex(combined.ToArray(),first.Length,e=>e is SourceHpChangedEvent);
        Assert.AreEqual((byte)0,Shared.Project(combined,nextHit,false).Dispatch!.MoveTurn);
        var last=first.OfType<SourceCalledBeatUpMemberEvent>().Last();var truncated=first.Take(first.IndexOf(last)+1).ToArray();
        combined=Shared.Number(truncated.Concat(next));
        for(int i=0;i<truncated.Length;i++)Assert.IsNull(Shared.Project(combined,i,false).Dispatch,"A following invocation cannot supply the missing hit.");
        nextHit=Array.FindIndex(combined.ToArray(),truncated.Length,e=>e is SourceHpChangedEvent);
        Assert.AreEqual((byte)0,Shared.Project(combined,nextHit,false).Dispatch!.MoveTurn);
    }
}
