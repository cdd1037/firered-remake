using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Game.Domain.World;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Charge=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Caller=FireRed.Tests.Application.WildBattles.CallerPreludeAnimationKernelFixture;
using Called=FireRed.Tests.Application.WildBattles.CalledLeafAnimationKernelFixture;
using Shared=FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// Source-natural learners and real public action/switch continuations. Controlled
// stages, status and reserves are integration fixtures, not route-play evidence.
[TestClass,TestCategory("Game")]
public sealed class BatonPassAnimationKernelTests
{
    private static MoveId Move=>Kernel.Id("BATON_PASS");
    private static SourceBattleFrame Initial(bool single,int actor=0,bool reserves=true)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        bodies[actor]=Kernel.Mon("GIRAFARIG","BATON_PASS") with{SpeedStage=12};
        var frame=Kernel.Frame(bodies,single);
        if(reserves)foreach(var side in single?new[]{BattleSide.Player}:new[]{BattleSide.Player,BattleSide.Opponent})
        {
            var reserve=Kernel.Mon("MAGIKARP","SPLASH",100,personality:19+(uint)side);var party=frame.Party(side);
            frame=frame with{Parties=frame.Parties.SetItem((int)side,party with{Members=party.Members.Add(new(reserve.Pokemon))})};
        }
        Assert.IsNull(SourceFourActionTurns.ValidateCapability(Kernel.Mechanics.Catalog,frame));return frame;
    }
    private static SourceBattleFrame SabrinaMrMime()
    {
        // An actual authenticated opponent roster supplies singles reserves;
        // a wild opponent is never given a fabricated multi-member party.
        var campaign=(ICampaignCatalog)CurrentCampaignFixture.Catalog;
        var setup=SourceTrainerEncounterEntry.Prepare(new(42),"baton-pass-animation");
        var made=TrainerPartyFactory.Create(campaign.OrdinaryTrainerCatalogs.Single(c => c.Trainers.ContainsKey(new("pret:TRAINER_LEADER_SABRINA"))).Trainers,campaign.OpponentPokemon!,new("pret:TRAINER_LEADER_SABRINA"),setup.Rng);
        var rewards=new SourceFourRewardState{Context=new(SourceRewardBattleKind.OrdinaryTrainer,42,"RED","MAPSEC_SAFFRON_CITY"),
            Metadata=[new(new(BattleSide.Player,0),"RED","MAPSEC_PALLET_TOWN","ITEM_POKE_BALL",SourceRewardPokerusHistory.NeverInfected)]};
        var entry=SourceTrainerEncounterEntry.Begin(Kernel.Mechanics.Catalog,setup,made,[Kernel.Mon("WAILORD","SPLASH",100)],0,rewards:rewards);
        Assert.IsTrue(entry.Succeeded,entry.Fault);var frame=entry.State!;
        var choices=ImmutableArray.Create(new SourceActionSelection(frame.Slots[0].Occupant!.Value,SourceActionKind.Move,0,new(0)),
            new SourceActionSelection(frame.Slots[1].Occupant!.Value,SourceActionKind.Switch,Incoming:new(BattleSide.Opponent,1)));
        var started=SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog,frame,frame.Revision,choices);Assert.IsTrue(started.Succeeded,started.Fault);
        frame=Charge.CompleteTurn(started);Assert.AreEqual(1,frame.Slots[1].Member!.Value.Index);
        Assert.AreEqual(new SpeciesId("pret:SPECIES_MR_MIME"),frame.Slots[1].Body!.Pokemon.Species);
        Assert.AreEqual(Move,frame.Slots[1].Body!.Pokemon.Moves[2].Move);
        frame=Charge.Body(frame,0,b=>b with{SpeedStage=0});frame=Charge.Body(frame,1,b=>b with{SpeedStage=12});return frame;
    }
    private static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> rows,int index,bool single,bool result=false,SourceBattleScene scene=SourceBattleScene.On)=>
        Charge.Project(rows,index,single,result,scene);
    private static void Deny(ImmutableArray<SourceBattleEvent> rows,bool single)=>Caller.DenyLeaf(rows,single,226);
    private static CampaignMoveAnimationDispatch Check(ImmutableArray<SourceBattleEvent> rows,bool single)
    {
        string before=JsonSerializer.Serialize(rows);var used=rows.OfType<SourceMoveUsedEvent>().Single();var attack=rows.OfType<SourceAttackStringEvent>().Single();
        var pp=rows.OfType<SourcePpCommandEvent>().Single();var required=rows.OfType<SourceCalledPartySwitchRequiredEvent>().Single();
        Assert.AreEqual(Move,used.Move);Assert.AreEqual(used.Actor,used.Target);Assert.AreEqual(Move,attack.ChosenMove);Assert.AreEqual(Move,attack.CurrentMove);
        Assert.AreEqual(used.Actor,pp.Actor);Assert.AreEqual(used.Actor,pp.Target);Assert.AreEqual(used.Actor,pp.PpOwner);Assert.AreEqual(used.Slot,pp.PpSlot);
        Assert.AreEqual(Move,pp.ChosenMove);Assert.AreEqual(Move,pp.CurrentMove);Assert.AreEqual(used.PpBefore,pp.Before);Assert.AreEqual(used.PpAfter,pp.After);
        Assert.AreEqual(1,pp.CommandCost);Assert.AreEqual(pp.Before-1,pp.After);Assert.AreEqual(pp.Sequence+1,required.Sequence);
        Assert.AreEqual(WildBattleCalledSwitchKind.BatonPass,required.Pending.Kind);Assert.AreEqual(used.Actor,required.Pending.Slot);
        Assert.IsNotNull(required.Pending.Invocation);Assert.AreEqual(used.Actor,required.Pending.Invocation.PpOwner);
        Assert.IsFalse(rows.OfType<SourceCalledPartySwitchedEvent>().Any());int count=0;CampaignMoveAnimationDispatch? found=null;
        for(int i=0;i<rows.Length;i++)foreach(bool result in new[]{false,true})
        {
            var decision=Project(rows,i,single,result);Assert.AreEqual(decision,Project(rows,i,single,result));Assert.IsNull(Project(rows,i,single,result,SourceBattleScene.Off).Dispatch);
            if(single)Assert.IsNull(result?AnimationDispatchFixture.AtResult(Charge.Single(rows,i),false,SourceBattleScene.On).Dispatch:
                AnimationDispatchFixture.AtAttackText(Charge.Single(rows,i),false,SourceBattleScene.On).Dispatch);
            if(result||rows[i]!=used){Assert.IsNull(decision.Dispatch,decision.Reason);continue;}
            found=decision.Dispatch;Assert.IsNotNull(found,decision.Reason);count++;
            Assert.AreEqual(226,found.SourceMoveId);Assert.AreEqual(used.Actor,found.Actor);Assert.AreEqual(used.Actor,found.Target);
            Assert.AreEqual(used.Sequence,found.Sequence);Assert.AreEqual(attack.Sequence,found.AttackSequence);Assert.AreEqual(pp.Sequence,found.PpSequence);
            Assert.AreEqual(pp.Sequence,found.SnapshotSequence);Assert.AreEqual(required.Sequence,found.ResultSequence);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText,found.Boundary);Assert.IsTrue(found.AcknowledgesCurrentReceipt);
            Assert.IsFalse(found.HitsSubstitute);Assert.IsNull(found.MoveTurn);Assert.IsNull(found.MovePower);Assert.IsNull(found.MoveDamage);
        }
        Assert.AreEqual(1,count);Assert.AreEqual(before,JsonSerializer.Serialize(rows));return found!;
    }

    [TestMethod]
    public void RealOrderedActionsOwnTheOutgoingChoiceBoundaryAtEveryPosition()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            var frame=single&&actor==1?SabrinaMrMime():Initial(single,actor);int slot=single&&actor==1?2:0;
            string before=JsonSerializer.Serialize(frame);var result=Charge.Ordered(frame,actor,actor,slot);Check(result.Events,single);
            Assert.AreEqual(SourceFourActionPhase.ActionSwitchPending,result.State.FourTurn!.Phase);Assert.IsNotNull(result.Continuation);
            var pending=result.State.PendingSwitches.Single();var outgoing=frame.Slots[actor].Member!.Value;
            CollectionAssert.AreEqual(SourceBattleIdentityRules.LegalReserveIndices(frame,outgoing.Side).ToArray(),pending.LegalPartyIndices.ToArray());
            Assert.AreEqual(frame.Slots[actor].Occupant,result.State.Slots[actor].Occupant);
            Assert.AreEqual(frame.Slots[actor].Body!.Pokemon.Moves[slot].CurrentPp-1,result.State.Party(outgoing.Side).Members[outgoing.Index].Pokemon.Moves[slot].CurrentPp);
            var sync=result.Events.OfType<SourceCalledPartyChangedEvent>().Single(e=>e.Reason=="active-sync");
            Assert.AreEqual(new BattlerSlot(actor),sync.SourceSlot);Assert.AreEqual(outgoing.Index,sync.PartyIndex);
            Assert.AreEqual(result.State.Party(outgoing.Side).Members[outgoing.Index],sync.After);
            var blocked=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,result.State,result.Continuation.Value);Assert.IsFalse(blocked.Succeeded);
            Assert.AreEqual(before,JsonSerializer.Serialize(frame));
            Assert.AreEqual(JsonSerializer.Serialize(result),JsonSerializer.Serialize(Charge.Ordered(frame,actor,actor,slot)));
        }
    }

    [TestMethod]
    public void AuthenticatedSuccessPrefixCanAnimateBeforeChoiceAndNeverReplaysAfterEntry()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Charge.Body(Initial(single),0,b=>b with{AttackStage=8,CalledVolatile=new(){SubstituteHp=7}});
            var result=Charge.Ordered(frame,0,0);var required=result.Events.OfType<SourceCalledPartySwitchRequiredEvent>().Single();
            Check(result.Events,single);Check(result.Events.Take(result.Events.IndexOf(required)+1).ToImmutableArray(),single);
            var beforeChoice=JsonSerializer.Serialize(result);var pending=result.State.PendingSwitches.Single();Assert.IsNotNull(result.Continuation);
            var entered=SourceFourActionTurns.ChooseActionSwitch(Kernel.Mechanics.Catalog,result.State,result.Continuation.Value,new(0),new(BattleSide.Player,pending.LegalPartyIndices[0]));
            Assert.IsTrue(entered.Succeeded,entered.Fault);Assert.IsNull(SourceBattleIdentityRules.Validate(entered.State));Deny(entered.Events,single);
            Assert.AreEqual(8,entered.State.Slots[0].Body!.AttackStage);Assert.AreEqual(7,entered.State.Slots[0].Body!.CalledVolatile!.SubstituteHp);
            Assert.AreEqual(WildBattleCalledSwitchKind.BatonPass,entered.Events.OfType<SourceCalledPartySwitchedEvent>().Single().Kind);
            Assert.IsFalse(entered.Events.OfType<SourceAttackStringEvent>().Any(e=>e.CurrentMove==Move));Assert.AreEqual(beforeChoice,JsonSerializer.Serialize(result));
        }
    }

    [TestMethod]
    public void SelfTargetIgnoresPressureAndCanSpendTheLastPpAtANonzeroLearnedSlot()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Initial(single);var growl=Kernel.Mon("GIRAFARIG","GROWL").Pokemon.Moves[0];
            frame=Charge.Body(frame,0,b=>b with{Pokemon=b.Pokemon with{Moves=[growl,b.Pokemon.Moves[0] with{CurrentPp=1}]}});
            for(int slot=1;slot<frame.BattlerCount;slot++)frame=Charge.Body(frame,slot,b=>b with{CalledOverlay=new(){Ability="ABILITY_PRESSURE"}});
            var result=Charge.Ordered(frame,0,0,1);Check(result.Events,single);var pp=result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(1,pp.PpSlot);Assert.AreEqual(1,pp.Before);Assert.AreEqual(0,pp.After);Assert.AreEqual(1,pp.CommandCost);
            var sync=result.Events.OfType<SourceCalledPartyChangedEvent>().Single();Assert.AreEqual(growl,sync.After.Pokemon.Moves[0]);
            Assert.AreEqual(0,sync.After.Pokemon.Moves[1].CurrentPp);
        }
    }

    [TestMethod]
    public void MissingReservesSleepAndTauntNeverAuthenticateTheAnimation()
    {
        foreach(bool single in new[]{false,true})foreach(string branch in new[]{"no-reserves","sleep","taunt"})
        {
            var frame=Initial(single,reserves:branch!="no-reserves");
            if(branch=="sleep")frame=Charge.Body(frame,0,b=>b with{Status=3});
            if(branch=="taunt")
            {
                frame=Charge.Body(frame,0,b=>b with{CalledVolatile=new(){TauntTurnsRemaining=2}});
                var choices=frame.Slots.Take(frame.BattlerCount).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,c.Slot)).ToImmutableArray();
                var rejected=SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog,frame,frame.Revision,choices);
                Assert.IsFalse(rejected.Succeeded);Assert.AreEqual("source_four_action_selection_taunted",rejected.Fault);
                Assert.IsTrue(rejected.Events.IsEmpty);Assert.AreEqual(frame,rejected.State);continue;
            }
            var result=Charge.Ordered(frame,0,0);Deny(result.Events,single);Assert.IsTrue(result.State.PendingSwitches.IsEmpty);
            Assert.IsFalse(result.Events.OfType<SourceCalledPartySwitchRequiredEvent>().Any());
            if(branch=="no-reserves")
            {Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="no-eligible-reserve"));Assert.AreEqual(1,result.Events.OfType<SourcePpCommandEvent>().Count());}
        }
    }

    [TestMethod]
    public void SuccessfulWakeKeepsTheActualPreAttackStatusAndOutgoingSync()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Charge.Body(Initial(single),0,b=>b with{Status=1});var result=Charge.Ordered(frame,0,0);Check(result.Events,single);
            var sync=result.Events.OfType<SourceCalledPartyChangedEvent>().Single();Assert.AreEqual(1u,sync.Before.Status);Assert.AreEqual(0u,sync.After.Status);
            var status=result.Events.OfType<SourceStatusChangedEvent>().Single(e=>e.Before==1&&e.After==0);
            Deny(result.Events.Remove(status),single);
        }
    }

    [TestMethod]
    public void RealOrderedMetronomeLeafKeepsCallerPpAndTheSuspendedInvocation()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Charge.Body(Initial(single),0,_=>Kernel.Mon("TOGETIC","METRONOME",100) with{SpeedStage=12});
            var choices=frame.Slots.Take(frame.BattlerCount).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,c.Slot)).ToImmutableArray();
            SourceFourActionResult? result=null;
            for(uint seed=0;seed<65536;seed++)
            {
                var started=SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog,frame with{Rng=new(seed)},frame.Revision,choices);
                Assert.IsTrue(started.Succeeded,started.Fault);Assert.AreEqual(0,started.State.Queue[0].Slot.Value);
                if(WildBattleSourceCallers.Metronome(started.State.Rng).SourceMoveId!=226)continue;
                Assert.IsNotNull(started.Continuation);result=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,started.State,started.Continuation.Value);
                Assert.IsTrue(result.Succeeded,result.Fault);Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));break;
            }
            Assert.IsNotNull(result,"Require an actual public ordered Metronome Baton Pass within the bounded source seeds.");
            var required=result.Events.OfType<SourceCalledPartySwitchRequiredEvent>().Single();
            Called.Check(result.Events,result,single,Kernel.Id("METRONOME"),226,required);
            Assert.AreEqual(Kernel.Id("METRONOME"),required.Pending.Invocation!.OriginalChosenMove);
            Assert.AreEqual(Kernel.Id("METRONOME"),required.Pending.Invocation.ChosenMove);Assert.AreEqual(Move,required.Pending.Invocation.CurrentMove);
            var sync=result.Events.OfType<SourceCalledPartyChangedEvent>().Single();Assert.AreEqual(Kernel.Id("METRONOME"),sync.After.Pokemon.Moves[0].Move);
            Assert.AreEqual(sync.Before.Pokemon.Moves[0].CurrentPp-1,sync.After.Pokemon.Moves[0].CurrentPp);
            var altered=required with{Pending=required.Pending with{Invocation=required.Pending.Invocation with{CalledMove=Move}}};
            Deny(result.Events.SetItem(result.Events.IndexOf(required),altered),single);
        }
    }

    [TestMethod]
    public void ChoiceReceiptRequiresUniqueOrderedReservesAndItsExactSuspendedSourceRegisters()
    {
        foreach(bool single in new[]{false,true})
        {
            var rows=Charge.Ordered(Initial(single),0,0).Events;var required=rows.OfType<SourceCalledPartySwitchRequiredEvent>().Single();
            var p=required.Pending;var inv=p.Invocation!;int at=rows.IndexOf(required);
            var badPending=new[]{p with{Kind=WildBattleCalledSwitchKind.Voluntary},p with{Kind=WildBattleCalledSwitchKind.FaintedReplacement},
                p with{Slot=new(1)},p with{LegalPartyIndices=[]},p with{LegalPartyIndices=[-1]},
                p with{LegalPartyIndices=[6]},p with{LegalPartyIndices=[2,2]},p with{LegalPartyIndices=[3,2]},p with{LegalPartyIndices=[0]},p with{Invocation=null}};
            foreach(var bad in badPending)Deny(rows.SetItem(at,required with{Pending=bad}),single);
            // Default ImmutableArray is deliberately malformed and cannot pass
            // through the shared JSON snapshot helper. Exercise the same public
            // projection directly, preserving the default value in both modes.
            var uninitialized=rows.SetItem(at,required with{Pending=p with{LegalPartyIndices=default}});
            var unchanged=uninitialized.ToArray();
            for(int i=0;i<uninitialized.Length;i++)foreach(bool result in new[]{false,true})
                Assert.IsNull(Project(uninitialized,i,single,result).Dispatch,"Uninitialized legal reserves must fail closed.");
            CollectionAssert.AreEqual(unchanged,uninitialized.ToArray());
            Assert.IsTrue(((SourceCalledPartySwitchRequiredEvent)uninitialized[at]).Pending.LegalPartyIndices.IsDefault);
            var badInvocations=new[]{inv with{Actor=new(1)},inv with{Target=new(1)},inv with{PpOwner=new(1)},inv with{ChosenSlot=1},inv with{PpSlot=1},
                inv with{OriginalChosenMove=Kernel.Id("METRONOME")},inv with{ChosenMove=Kernel.Id("METRONOME")},inv with{CurrentMove=Kernel.Id("SPLASH")},
                inv with{CalledMove=Move},inv with{CancellerPhase=WildBattleAttackCancellerPhase.Start},inv with{Flags=WildBattleInvocationFlags.Obeys},
                inv with{Flags=inv.Flags|WildBattleInvocationFlags.NoPpDeduct},inv with{Flags=inv.Flags|WildBattleInvocationFlags.DisobedientMove}};
            foreach(var bad in badInvocations)Deny(rows.SetItem(at,required with{Pending=p with{Invocation=bad}}),single);
            Deny(rows.Remove(required),single);Deny(Shared.Number(rows.Insert(at+1,required)),single);
            Deny(rows.SetItem(at,required with{Turn=required.Turn+1}),single);Deny(rows.SetItem(at,required with{Sequence=required.Sequence+1}),single);
        }
    }

    [TestMethod]
    public void PpAndOutgoingPartyWritesCannotBeDiscardedOrReassigned()
    {
        foreach(bool single in new[]{false,true})
        {
            var rows=Charge.Ordered(Initial(single),0,0).Events;var pp=rows.OfType<SourcePpCommandEvent>().Single();int p=rows.IndexOf(pp);
            var sync=rows.OfType<SourceCalledPartyChangedEvent>().Single();int s=rows.IndexOf(sync);
            foreach(var bad in new[]{pp with{PpOwner=new(1)},pp with{Target=new(1)},pp with{ChosenMove=Kernel.Id("METRONOME")},pp with{CurrentMove=Kernel.Id("SPLASH")},
                pp with{PpSlot=1},pp with{After=pp.Before},pp with{CommandCost=2},pp with{Turn=pp.Turn+1}})Deny(rows.SetItem(p,bad),single);
            foreach(var bad in new[]{sync with{SourceSlot=new(1)},sync with{SourceSlot=null},sync with{Side=BattleSide.Opponent},sync with{PartyIndex=6},
                sync with{Reason="switch"},sync with{TransientMoveSlots=null},sync with{TransientMoveSlots=1},sync with{After=sync.After with{HeldItem="ITEM_ORAN_BERRY"}},
                sync with{After=sync.After with{Pokemon=sync.After.Pokemon with{CurrentHp=sync.After.Pokemon.CurrentHp-1}}},
                sync with{After=sync.After with{Pokemon=sync.After.Pokemon with{Moves=[sync.After.Pokemon.Moves[0] with{CurrentPp=pp.After-1}]}}},
                sync with{After=sync.After with{Status=WildBattleStatus.Burn}}})Deny(rows.SetItem(s,bad),single);
            Deny(Shared.Number(rows.Insert(s+1,sync)),single);
            Deny(Shared.Number(rows.Insert(s,new SourceMtMoonVolatileEvent(0,sync.Turn,new(0),"unrelated",0,1,"move"))),single);
            Deny(rows.SetItem(s,sync with{Turn=sync.Turn+1}),single);
        }
    }

    [TestMethod]
    public void DefinitionAndRendererAvailabilityRemainTheSharedCatalogContract()
    {
        foreach(bool single in new[]{false,true})
        {
            var rows=Charge.Ordered(Initial(single),0,0).Events;int at=rows.IndexOf(rows.OfType<SourceMoveUsedEvent>().Single());var moves=Kernel.Mechanics.Catalog.Moves;
            foreach(var metadata in new[]{null,moves.Remove(Move),moves.SetItem(Move,moves[Move] with{SourceMoveId=1}),
                moves.SetItem(Move,moves[Move] with{Power=1}),moves.SetItem(Move,moves[Move] with{Target="MOVE_TARGET_SELECTED"})})
                Assert.IsNull(single?AnimationDispatchFixture.AtAttackText(Charge.Single(rows,at),true,SourceBattleScene.On,metadata).Dispatch:
                    AnimationDispatchFixture.AtAttackText(Envelope.Double(rows,at),SourceBattleScene.On,metadata).Dispatch);
            Assert.IsNull(single?CampaignMoveAnimationPresentation.AtAttackText(Charge.Single(rows,at),true,SourceBattleScene.On,moves,(_,_)=>false).Dispatch:
                CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(rows,at),SourceBattleScene.On,moves,(_,_)=>false).Dispatch);
        }
    }
}
