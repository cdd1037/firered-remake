using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Kernel=FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope=FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Charge=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Caller=FireRed.Tests.Application.WildBattles.CallerPreludeAnimationKernelFixture;
using Shared=FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class GhostSongAnimationKernelTests
{
    private static MoveId Move(int id)=>Kernel.Id(id switch{174=>"CURSE",194=>"DESTINY_BOND",195=>"PERISH_SONG",_=>throw new ArgumentOutOfRangeException(nameof(id))});
    private static SourceBattleFrame Initial(int id,bool single,int actor=0,bool ghost=true)
    {
        // Natural learners; low-level opponents keep even minimum-Speed Curse
        // first. Battle statuses, PP and stages below are controlled fixtures.
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",1) with{SpeedStage=0}).ToArray();
        bodies[actor]=(id switch{174=>Kernel.Mon(ghost?"GASTLY":"SLOWPOKE","CURSE"),
            194=>Kernel.Mon("GASTLY","DESTINY_BOND"),195=>Kernel.Mon("LAPRAS","PERISH_SONG"),_=>throw new ArgumentOutOfRangeException(nameof(id))}) with{SpeedStage=12};
        return Kernel.Frame(bodies,single);
    }
    private static WildBattleCombatant Teach(WildBattleCombatant body,string name)
    {
        var catalog=Kernel.Mechanics.Catalog;
        var tutors=TestAssets.Cached(()=>PretMoveTutors.Load(TestAssets.Root,catalog).Catalog);
        var move=Kernel.Id(name);var tutor=tutors.Definitions.Values.Single(t=>t.Move==move);
        var party=new PartyPokemon(1,body.Pokemon,"TEST","TEST",0,2,"MAP_PALLET_TOWN",50,4,"ITEM_POKE_BALL","ITEM_NONE");
        var taught=MoveTutorRules.Teach(tutors,catalog,party,tutor.Index);
        Assert.IsTrue(taught.Succeeded,taught.Error);return body with{Pokemon=taught.Pokemon.Pokemon};
    }
    private static int Target(int id,int actor,bool ghost=true)=>id==174&&ghost?actor^1:actor;
    private static SourceBattleFrame Ability(SourceBattleFrame f,int slot,string value)=>Charge.Body(f,slot,b=>b with{CalledOverlay=(b.CalledOverlay??new()) with{Ability=value}});
    private static SourceBattleFrame Timer(SourceBattleFrame f,int slot,int? value)=>Charge.Body(f,slot,b=>b with{CalledVolatile=(b.CalledVolatile??new()) with{PerishSongTimer=value}});
    private static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> rows)=>Shared.Number(rows);
    private static void Deny(ImmutableArray<SourceBattleEvent> rows,bool single,int id)=>Caller.DenyLeaf(rows,single,id);
    private static SourceFourActionResult Begin(SourceBattleFrame frame,int actor,int target,int moveSlot=0)
    {
        var choices=frame.Slots.Take(frame.BattlerCount).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,
            c.Slot.Value==actor?moveSlot:0,c.Slot.Value==actor?new BattlerSlot(target):c.Slot)).ToImmutableArray();
        var started=SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog,frame,frame.Revision,choices);
        Assert.IsTrue(started.Succeeded,started.Fault);Assert.AreEqual(actor,started.State.Queue[0].Slot.Value);Assert.IsNotNull(started.Continuation);return started;
    }
    private static SourceFourActionResult Advance(SourceFourActionResult started,SourceBattleFrame? frame=null)
    {
        Assert.IsNotNull(started.Continuation);var result=SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog,frame??started.State,started.Continuation.Value);
        Assert.IsTrue(result.Succeeded,result.Fault);Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));return result;
    }
    private static CampaignMoveAnimationDispatch Check(SourceFourActionResult result,int id,bool single,byte? turn=null)
    {
        var rows=result.Events;var used=rows.OfType<SourceMoveUsedEvent>().Single();var attack=rows.OfType<SourceAttackStringEvent>().Single();
        var pp=rows.OfType<SourcePpCommandEvent>().First();SourceBattleEvent owner=id switch{
            194=>rows.OfType<SourceTowerVolatileEvent>().Single(e=>e.Effect=="destiny-bond"&&e.Applied),
            195=>rows.OfType<SourceMtMoonVolatileEvent>().Last(e=>e.Volatile=="perish-song"&&e.Reason=="move"),
            _=>turn==0?rows.OfType<SourceTowerVolatileEvent>().Single(e=>e.Effect=="curse"&&e.Applied):rows[rows.IndexOf(pp)+1]};
        Assert.AreEqual(Move(id),used.Move);Assert.AreEqual(used.Move,attack.CurrentMove);Assert.AreEqual(used.Move,attack.ChosenMove);
        Assert.AreEqual(attack.Sequence+1,used.Sequence);Assert.AreEqual(used.Sequence+1,pp.Sequence);
        Assert.AreEqual(used.Actor,pp.PpOwner);Assert.AreEqual(used.PpBefore,pp.Before);Assert.AreEqual(used.PpAfter,pp.After);
        if(rows.OfType<SourceFaintedEvent>().SingleOrDefault(e=>e.Side==used.Actor) is {} faint)
        {
            if(used.Actor.Side==BattleSide.Player)
            {
                var applied=faint.Friendship;Assert.IsNotNull(applied);
                var sync=rows.OfType<SourceCalledPartyChangedEvent>().Single(e=>e.SourceSlot==used.Actor);
                Assert.AreEqual(new PartyMemberKey(sync.Side,sync.PartyIndex),applied.Member);
                Assert.AreEqual(sync.Before.Pokemon.Friendship,applied.Before);Assert.AreEqual(sync.After.Pokemon.Friendship,applied.After);
                Assert.AreEqual(used.Turn,faint.Turn);Assert.AreEqual(faint.Turn,sync.Turn);Assert.IsTrue(owner.Sequence<faint.Sequence&&faint.Sequence<sync.Sequence);
            }
            else Assert.IsNull(faint.Friendship);
        }
        string before=JsonSerializer.Serialize(result);int count=0;CampaignMoveAnimationDispatch? found=null;
        for(int i=0;i<rows.Length;i++)foreach(bool atResult in new[]{false,true})
        {
            var first=Charge.Project(rows,i,single,atResult);Assert.AreEqual(first,Charge.Project(rows,i,single,atResult));
            Assert.IsNull(Charge.Project(rows,i,single,atResult,SourceBattleScene.Off).Dispatch);
            Assert.IsNull(Charge.Wave(rows,i,single),"None of these source scripts invokes General_StatsChange.");
            if(single)Assert.IsNull(atResult?AnimationDispatchFixture.AtResult(Charge.Single(rows,i),false,SourceBattleScene.On).Dispatch:
                AnimationDispatchFixture.AtAttackText(Charge.Single(rows,i),false,SourceBattleScene.On).Dispatch);
            if(atResult||rows[i]!=used){Assert.IsNull(first.Dispatch,first.Reason);continue;}
            found=first.Dispatch;Assert.IsNotNull(found,first.Reason);count++;
            Assert.AreEqual(id,found.SourceMoveId);Assert.AreEqual(used.Sequence,found.Sequence);Assert.AreEqual(attack.Sequence,found.AttackSequence);
            Assert.AreEqual(pp.Sequence,found.PpSequence);Assert.AreEqual(pp.Sequence,found.SnapshotSequence);Assert.AreEqual(owner.Sequence,found.ResultSequence);
            Assert.AreEqual(used.Actor,found.Actor);Assert.AreEqual(used.Target,found.Target);Assert.AreEqual(turn,found.MoveTurn);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText,found.Boundary);Assert.IsTrue(found.AcknowledgesCurrentReceipt);
            Assert.IsFalse(found.HitsSubstitute);Assert.IsNull(found.MovePower);Assert.IsNull(found.MoveDamage);
        }
        Assert.AreEqual(1,count);Assert.AreEqual(before,JsonSerializer.Serialize(result));return found!;
    }

    [TestMethod,DataRow(194),DataRow(195),DataRow(174)]
    public void NaturalOrderedActionsOwnExactlyOneRootAcrossAllPositionsAndBothEnvelopes(int id)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)foreach(bool ghost in id==174?new[]{false,true}:new[]{true})
        {
            var frame=Initial(id,single,actor,ghost);string before=JsonSerializer.Serialize(frame);
            var result=Charge.Ordered(frame,actor,Target(id,actor,ghost));Check(result,id,single,id==174?(byte)(ghost?0:1):null);
            Assert.AreEqual(before,JsonSerializer.Serialize(frame));
        }
    }

    [TestMethod]
    public void DestinyBondRepeatedUseClearsThenSetsButBlockedAttemptOnlyClears()
    {
        foreach(bool single in new[]{false,true})
        {
            var first=Charge.Ordered(Initial(194,single),0,0);Check(first,194,single);var frame=Charge.CompleteTurn(first);
            var repeat=Charge.Ordered(frame,0,0);Check(repeat,194,single);
            var flags=repeat.Events.OfType<SourceTowerVolatileEvent>().Where(e=>e.Effect=="destiny-bond").ToArray();
            Assert.AreEqual(2,flags.Length);Assert.IsFalse(flags[0].Applied);Assert.AreEqual("action-attempt",flags[0].Reason);
            Assert.IsTrue(flags[1].Applied);Assert.AreEqual("move",flags[1].Reason);
            Assert.IsTrue(flags[0].Sequence<repeat.Events.OfType<SourceAttackStringEvent>().Single().Sequence);
            frame=Charge.Body(Charge.CompleteTurn(repeat),0,b=>b with{Status=3});var blocked=Charge.Ordered(frame,0,0);
            Assert.IsFalse(blocked.State.Slots[0].Body!.DestinyBond);Assert.IsFalse(blocked.Events.OfType<SourceTowerVolatileEvent>().Any(e=>e.Applied));Deny(blocked.Events,single,194);
        }
    }

    [TestMethod]
    public void LaterDestinyBondKnockoutNeverReplaysItsSetupAnimation()
    {
        foreach(bool single in new[]{false,true})
        {
            var setup=Charge.Ordered(Initial(194,single),0,0);var frame=Charge.CompleteTurn(setup);
            frame=Charge.Body(frame,0,b=>b with{SpeedStage=0,Pokemon=b.Pokemon with{CurrentHp=1}});
            frame=Charge.Body(frame,1,_=>Kernel.Mon("SQUIRTLE","BITE",100) with{SpeedStage=12});
            var hit=Charge.Ordered(frame,1,0);Assert.IsTrue(hit.Events.OfType<SourceTowerHpEvent>().Any(e=>e.Effect=="destiny-bond"));Deny(hit.Events,single,194);
        }
    }

    [TestMethod]
    public void PerishSongHealthyMixedSoundproofAndExistingTimersFollowNumericRecipients()
    {
        foreach(bool single in new[]{false,true})foreach(string branch in new[]{"healthy","actor-proof","target-proof","mixed","all-active","all-proof","all-mixed"})
        {
            var frame=Initial(195,single);var expected=new List<int>();var blocked=new List<int>();
            for(int slot=0;slot<frame.BattlerCount;slot++)
            {
                bool proof=branch=="all-proof"||branch=="actor-proof"&&slot==0||branch=="target-proof"&&slot==1||(branch is "mixed" or "all-mixed")&&slot==1;
                bool active=branch=="all-active"||branch=="all-mixed"&&slot!=1||branch=="mixed"&&slot==0;
                if(proof){frame=Ability(frame,slot,"ABILITY_SOUNDPROOF");blocked.Add(slot);}
                if(active)frame=Timer(frame,slot,1);
                if(!proof&&!active)expected.Add(slot);
            }
            var result=Charge.Ordered(frame,0,0);var markers=result.Events.OfType<SourceMtMoonVolatileEvent>().Where(e=>e.Volatile=="perish-song").ToArray();
            CollectionAssert.AreEqual(expected.ToArray(),markers.Select(e=>e.Side.Value).ToArray());
            if(expected.Count==0)
            {Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="perish-song-already-active"));Assert.IsFalse(result.Events.OfType<SourceAbilityActivatedEvent>().Any(e=>e.Ability=="ABILITY_SOUNDPROOF"));Deny(result.Events,single,195);}
            else
            {
                Check(result,195,single);CollectionAssert.AreEqual(blocked.ToArray(),result.Events.OfType<SourceAbilityActivatedEvent>().Where(e=>e.Ability=="ABILITY_SOUNDPROOF").Select(e=>e.Side.Value).ToArray());
                Assert.IsTrue(markers.All(e=>e.Before==-1&&e.After==3&&e.Reason=="move"));
            }
            for(int slot=0;slot<frame.BattlerCount;slot++)if(frame.Slots[slot].Body!.CalledVolatile?.PerishSongTimer is {} timer)
                Assert.AreEqual(timer,result.State.Slots[slot].Body!.CalledVolatile!.PerishSongTimer);
        }
    }

    [TestMethod,DataRow(1),DataRow(2),DataRow(5)]
    public void PerishPressureComesAfterMarkersIncludesPartnersAndMaySpendZeroRemainingPp(int before)
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Initial(195,single);for(int slot=1;slot<frame.BattlerCount;slot++)frame=Ability(frame,slot,"ABILITY_PRESSURE");
            frame=Charge.Body(frame,0,b=>b with{Pokemon=b.Pokemon with{Moves=[b.Pokemon.Moves[0] with{CurrentPp=before}]}});
            var result=Charge.Ordered(frame,0,0);Check(result,195,single);var pp=result.Events.OfType<SourcePpCommandEvent>().ToArray();
            Assert.AreEqual(2,pp.Length);Assert.AreEqual(1,pp[0].CommandCost);Assert.AreEqual(frame.BattlerCount-1,pp[1].CommandCost);
            Assert.AreEqual(before-1,pp[1].Before);Assert.AreEqual(Math.Max(0,before-frame.BattlerCount),pp[1].After);
            Assert.IsTrue(result.Events.OfType<SourceMtMoonVolatileEvent>().Where(e=>e.Volatile=="perish-song").All(e=>e.Sequence<pp[1].Sequence));
            for(int slot=0;slot<frame.BattlerCount;slot++)frame=Timer(frame,slot,2);
            var failed=Charge.Ordered(frame,0,0);Assert.AreEqual(2,failed.Events.OfType<SourcePpCommandEvent>().Count());Deny(failed.Events,single,195);
        }
    }

    [TestMethod]
    public void PerishPressureUsesValidNonzeroSlotWhilePermanentAndMimicDuplicatesStayInvalid()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Ability(Initial(195,single),1,"ABILITY_PRESSURE");
            var growl=Kernel.Mon("LAPRAS","GROWL").Pokemon.Moves[0];
            frame=Charge.Body(frame,0,b=>b with{Pokemon=b.Pokemon with{Moves=[growl,b.Pokemon.Moves[0]]}});
            var result=Charge.Ordered(frame,0,0,1);Check(result,195,single);var pp=result.Events.OfType<SourcePpCommandEvent>().ToArray();
            Assert.AreEqual(1,pp[0].PpSlot);Assert.AreEqual(1,pp[1].PpSlot);Assert.AreEqual(4,pp[1].Before);Assert.AreEqual(3,pp[1].After);
            var sync=result.Events.OfType<SourceCalledPartyChangedEvent>().Single(e=>e.Reason=="active-sync");
            Assert.AreEqual(growl,sync.After.Pokemon.Moves[0]);Assert.AreEqual(3,sync.After.Pokemon.Moves[1].CurrentPp);
            Deny(result.Events.SetItem(result.Events.IndexOf(pp[1]),pp[1] with{PpSlot=0,Before=growl.CurrentPp,After=growl.CurrentPp-1}),single,195);
            var duplicate=Charge.Body(Initial(195,single),0,b=>b with{Pokemon=b.Pokemon with{Moves=[b.Pokemon.Moves[0],b.Pokemon.Moves[0]]}});
            var selections=duplicate.Slots.Take(duplicate.BattlerCount).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,c.Slot.Value==0?1:0,c.Slot)).ToImmutableArray();
            var invalid=SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog,duplicate,duplicate.Revision,selections);
            Assert.IsFalse(invalid.Succeeded);Assert.AreEqual("pokemon_move_duplicate",invalid.Fault);Assert.IsTrue(invalid.Events.IsEmpty);
            // A real tutor adds Mimic, and a real donor invocation supplies the
            // attempted duplicate; the source explicitly rejects already-known.
            var copying=Charge.Body(Initial(195,single),0,b=>Teach(b,"MIMIC"));
            copying=Charge.Body(copying,1,_=>Kernel.Mon("LAPRAS","PERISH_SONG") with{SpeedStage=0});
            copying=Envelope.Resolve(copying,1,1).State;
            var failed=Charge.Ordered(copying,0,1,1);
            Assert.IsTrue(failed.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="copy-already-known"));
            Assert.IsFalse(failed.Events.OfType<SourceMoveCopiedEvent>().Any());Deny(failed.Events,single,195);
        }
    }

    [TestMethod]
    public void PerishActualResidualCountdownAndRepeatedFailureNeverReplayTheRoot()
    {
        foreach(bool single in new[]{false,true})
        {
            var result=Charge.Ordered(Initial(195,single),0,0);Check(result,195,single);bool countdown=false;int steps=0;
            while(!result.CompletedCleanTurn)
            {
                Assert.IsTrue(++steps<=16);result=Advance(result);Deny(result.Events,single,195);
                countdown|=result.Events.OfType<SourceMtMoonVolatileEvent>().Any(e=>e.Volatile=="perish-song"&&e.Reason!="move");
            }
            Assert.IsTrue(countdown);Assert.AreEqual(2,result.State.Slots[0].Body!.CalledVolatile!.PerishSongTimer);
            var repeat=Charge.Ordered(result.State,0,0);Deny(repeat.Events,single,195);
            Assert.IsTrue(repeat.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="perish-song-already-active"));
        }
    }

    [TestMethod]
    public void NonGhostCurseRequiresThreeOrderedStatsAndAtLeastOneChangeWithoutGenericWave()
    {
        foreach(bool single in new[]{false,true})for(int mask=0;mask<8;mask++)
        {
            var frame=Charge.Body(Initial(174,single,ghost:false),0,b=>b with{SpeedStage=(mask&1)!=0?0:6,AttackStage=(mask&2)!=0?12:6,DefenseStage=(mask&4)!=0?12:6});
            var result=Charge.Ordered(frame,0,0);
            if(mask==7){Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="stat-limits"));Deny(result.Events,single,174);continue;}
            Check(result,174,single,1);var pp=result.Events.OfType<SourcePpCommandEvent>().Single();int at=result.Events.IndexOf(pp)+1;
            var group=result.Events.Skip(at).Take(3).ToArray();CollectionAssert.AreEqual(new[]{WildBattleStat.Speed,WildBattleStat.Attack,WildBattleStat.Defense},group.Select(e=>e switch{
                SourceStatChangedEvent s=>s.Stat,SourceStatAtMinimumEvent s=>s.Stat,SourceStatAtMaximumEvent s=>s.Stat,_=>throw new InvalidOperationException()}).ToArray());
            Assert.IsTrue(group.OfType<SourceStatChangedEvent>().Any());Deny(Number(result.Events.RemoveAt(at+2)),single,174);
            Deny(Number(result.Events.SetItem(at,group[1]).SetItem(at+1,group[0])),single,174);
        }
    }

    [TestMethod]
    public void CurseWhiteHerbAndCostStatusCureKeepTheirSeparateOwnedTail()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Charge.Body(Initial(174,single,ghost:false),0,b=>b with{SpeedStage=6,HeldItem="ITEM_WHITE_HERB"});
            var stats=Charge.Ordered(frame,0,0);Check(stats,174,single,1);Assert.AreEqual(6,stats.State.Slots[0].Body!.SpeedStage);
            Assert.IsTrue(stats.Events.OfType<SourceHeldItemConsumedEvent>().Any(e=>e.Item=="ITEM_WHITE_HERB"));
            frame=Charge.Body(Initial(174,single),0,b=>b with{Status=WildBattleStatus.Burn,HeldItem="ITEM_RAWST_BERRY"});
            var ghost=Charge.Ordered(frame,0,1);Check(ghost,174,single,0);Assert.IsTrue(ghost.Events.OfType<SourceHeldItemConsumedEvent>().Any(e=>e.Item=="ITEM_RAWST_BERRY"));
        }
    }

    [TestMethod]
    public void GhostCurseProtectBypassAndRealFlySureHitBoundaryMatchTheSourceScript()
    {
        foreach(bool single in new[]{false,true})
        {
            foreach(string branch in new[]{"protect","substitute","already-cursed"})
            {
                var frame=Charge.Body(Initial(174,single),1,b=>branch switch{"protect"=>b with{Protected=true},"substitute"=>b with{CalledVolatile=new(){SubstituteHp=10}},_=>b with{Cursed=true}});
                var result=Charge.Ordered(frame,0,1);if(branch=="protect")Check(result,174,single,0);else Deny(result.Events,single,174);
            }
            var flying=Charge.Charged(19,single,1);flying=Charge.Body(flying,0,_=>Kernel.Mon("GASTLY","CURSE",100) with{SpeedStage=12});
            for(int slot=1;slot<flying.BattlerCount;slot++)flying=Charge.Body(flying,slot,b=>b with{SpeedStage=0});
            var missed=Charge.Ordered(flying,0,1);Assert.IsTrue(missed.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="semi-invulnerable"));Deny(missed.Events,single,174);
            var target=flying.Slots[1];var marked=flying with{Slots=flying.Slots.SetItem(1,target with{Registers=target.Registers with{Turn=target.Registers.Turn! with{SureHitSource=new(0),SureHitTurnsRemaining=2}}})};
            Check(Charge.Ordered(marked,0,1),174,single,0);
        }
    }

    [TestMethod]
    public void CurseBranchUsesActualExecutionTypeAfterSelectionAndRetargetsBeforePp()
    {
        foreach(bool single in new[]{false,true})foreach(bool originallyGhost in new[]{false,true})
        {
            var start=Begin(Initial(174,single,ghost:originallyGhost),0,originallyGhost?1:0);
            var changed=Charge.Body(start.State,0,b=>b with{CalledOverlay=(b.CalledOverlay??new()) with{Type1=originallyGhost?"TYPE_NORMAL":"TYPE_GHOST",Type2=originallyGhost?"TYPE_NORMAL":"TYPE_GHOST"}});
            var result=Advance(start,changed);var cue=Check(result,174,single,originallyGhost?(byte)1:(byte)0);
            if(originallyGhost)Assert.AreEqual(new BattlerSlot(0),cue.Target);else Assert.AreNotEqual(cue.Actor.Side,cue.Target.Side);
        }
    }

    [TestMethod]
    public void GhostCurseCostMayFaintItsActorAndOwnItsImmediateReplacementOrOutcomeTail()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            var frame=Charge.Body(Initial(174,single,actor),actor,b=>b with{Status=WildBattleStatus.Poison,Pokemon=b.Pokemon with{CurrentHp=1}});
            var result=Charge.Ordered(frame,actor,actor^1);Check(result,174,single,0);
            var cost=result.Events.OfType<SourceTowerHpEvent>().Single(e=>e.Effect=="curse-cost");Assert.AreEqual(0,cost.HpAfter);
            Assert.IsTrue(result.Events.OfType<SourceFaintedEvent>().Any(e=>e.Side.Value==actor));
            Assert.IsTrue(result.Events.OfType<SourceStatusChangedEvent>().Any(e=>e.Reason=="faint"&&e.After==0));
        }
        var replacement=Charge.Body(Initial(174,true),0,b=>b with{Pokemon=b.Pokemon with{CurrentHp=1}});var reserve=Kernel.Mon("WAILORD","SPLASH",100);
        replacement=replacement with{Parties=replacement.Parties.SetItem(0,replacement.Parties[0] with{Members=replacement.Parties[0].Members.Add(new(reserve.Pokemon))})};
        var needsReplacement=Charge.Ordered(replacement,0,1);Check(needsReplacement,174,true,0);
        Assert.AreEqual(new BattlerSlot(0),needsReplacement.Events.OfType<SourceCalledPartySwitchRequiredEvent>().Single().Pending.Slot);
        var terminal=Charge.Body(Initial(174,false),0,b=>b with{Pokemon=b.Pokemon with{CurrentHp=1}});
        terminal=Charge.Body(terminal,2,b=>b with{Pokemon=b.Pokemon with{CurrentHp=0}});terminal=terminal with{SourceAbsentMask=(byte)(terminal.SourceAbsentMask|4),TurnStartAbsentMask=(byte)(terminal.TurnStartAbsentMask|4)};
        // Build choices only for eligible active occupants, as the public kernel requires.
        var choices=terminal.Slots.Where(c=>!terminal.IsSourceAbsent(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,c.Slot.Value==0?new BattlerSlot(1):c.Slot)).ToImmutableArray();
        var start=SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog,terminal,terminal.Revision,choices);Assert.IsTrue(start.Succeeded,start.Fault);
        var ended=Advance(start);Check(ended,174,false,0);Assert.AreEqual(WildBattleOutcome.Lost,ended.Events.OfType<SourceEndedEvent>().Single().Outcome);
    }

    [TestMethod]
    public void ActuallyTransformedGhostActorKeepsOriginalPartyMovesWhileCostStillAuthenticates()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Charge.Body(Initial(174,single,1),0,_=>Kernel.Mon("DITTO","TRANSFORM",100) with{SpeedStage=12});
            frame=Charge.Body(frame,1,b=>b with{SpeedStage=0});var transformed=Charge.Ordered(frame,0,1);
            Assert.IsTrue(transformed.Events.OfType<SourceTransformedEvent>().Any());frame=Charge.CompleteTurn(transformed);
            frame=Charge.Body(frame,0,b=>b with{SpeedStage=12,Pokemon=b.Pokemon with{CurrentHp=b.Pokemon.Stats.MaxHp}});
            var result=Charge.Ordered(frame,0,1);Check(result,174,single,0);
            var sync=result.Events.OfType<SourceCalledPartyChangedEvent>().Single(e=>e.Reason=="active-sync"&&e.SourceSlot==new BattlerSlot(0));
            Assert.AreEqual(Kernel.Id("TRANSFORM"),sync.Before.Pokemon.Moves[0].Move);Assert.IsTrue(sync.Before.Pokemon.Moves.SequenceEqual(sync.After.Pokemon.Moves));
            Assert.IsTrue(sync.Before.Pokemon.CurrentHp>sync.After.Pokemon.CurrentHp);
        }
    }

    [TestMethod]
    public void TransformedGhostCostFaintAuthenticatesOriginalPartyFriendshipAndCopiedPp()
    {
        foreach(bool single in new[]{false,true})
        {
            var original=Kernel.Mon("DITTO","TRANSFORM",100);
            original=original with{SpeedStage=12,Pokemon=original.Pokemon with{Friendship=200}};
            var frame=Charge.Body(Initial(174,single,1),0,_=>original);
            frame=Charge.Body(frame,1,b=>b with{SpeedStage=0});
            var transformed=Charge.Ordered(frame,0,1);Assert.IsTrue(transformed.Events.OfType<SourceTransformedEvent>().Any());
            frame=Charge.Body(Charge.CompleteTurn(transformed),0,b=>b with{SpeedStage=12,Pokemon=b.Pokemon with{CurrentHp=1}});
            var result=Charge.Ordered(frame,0,1);Check(result,174,single,0);
            var faint=result.Events.OfType<SourceFaintedEvent>().Single();var applied=faint.Friendship;Assert.IsNotNull(applied);
            Assert.AreEqual(200,applied.Before);Assert.AreEqual(199,applied.After);
            var sync=result.Events.OfType<SourceCalledPartyChangedEvent>().Single(e=>e.SourceSlot==new BattlerSlot(0));
            Assert.AreEqual(original.Pokemon.Species,sync.After.Pokemon.Species);
            Assert.AreEqual(Kernel.Id("TRANSFORM"),sync.After.Pokemon.Moves[0].Move);
            Assert.IsTrue(sync.Before.Pokemon.Moves.SequenceEqual(sync.After.Pokemon.Moves));
            Assert.AreEqual(199,result.State.Party(BattleSide.Player).Members[0].Pokemon.Friendship);
            Deny(result.Events.SetItem(result.Events.IndexOf(faint),faint with{Friendship=null}),single,174);
            Deny(result.Events.SetItem(result.Events.IndexOf(faint),faint with{Friendship=applied with{After=200}}),single,174);
        }
    }

    [TestMethod]
    public void RealMetronomeTransformPreservesAnOriginalCurseSlotWithoutInferringFromEqualPp()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Charge.Body(Initial(174,single,1),0,_=>Teach(Kernel.Mon("GENGAR","CURSE",100),"METRONOME") with{SpeedStage=12});
            frame=Charge.Body(frame,1,b=>b with{SpeedStage=0});SourceFourActionResult? transformed=null;
            for(uint seed=0;seed<65536;seed++)
            {
                var start=Begin(frame with{Rng=new(seed)},0,1,1);
                var target=SourceMoveTargeting.ResolveLearned(start.State,new(0),new(1),Kernel.Mechanics.Catalog.Moves[Kernel.Id("METRONOME")]);
                Assert.IsTrue(target.Succeeded,target.Fault);if(WildBattleSourceCallers.Metronome(target.Rng).SourceMoveId!=144)continue;
                var candidate=Advance(start);
                // The called leaf resolves its own target. Keep only an actual
                // Transform of the intended Curse donor, including in doubles.
                if(!candidate.Events.OfType<SourceTransformedEvent>().Any(e=>e.Actor==new BattlerSlot(0)&&
                    e.Target==new BattlerSlot(1)&&e.BattleView.Moves[0].Move==Move(174)))continue;
                transformed=candidate;break;
            }
            Assert.IsNotNull(transformed);Assert.IsTrue(transformed.Events.OfType<SourceTransformedEvent>().Any());
            frame=Charge.CompleteTurn(transformed);frame=Charge.Body(frame,0,b=>b with{SpeedStage=12,Pokemon=b.Pokemon with{CurrentHp=b.Pokemon.Stats.MaxHp}});
            var result=Charge.Ordered(frame,0,1);Check(result,174,single,0);
            var sync=result.Events.OfType<SourceCalledPartyChangedEvent>().Single(e=>e.SourceSlot==new BattlerSlot(0));
            Assert.AreEqual((byte)15,sync.TransientMoveSlots);Assert.AreEqual(Move(174),sync.Before.Pokemon.Moves[0].Move);
            Assert.IsTrue(sync.Before.Pokemon.Moves.SequenceEqual(sync.After.Pokemon.Moves));
            int at=result.Events.IndexOf(sync);Deny(result.Events.SetItem(at,sync with{TransientMoveSlots=0}),single,174);
            Deny(result.Events.SetItem(at,sync with{TransientMoveSlots=null}),single,174);
            Assert.AreEqual("source_event_active_sync_persistence",SourceBattleEventHooks.ValidateRouting(result.State,sync with{TransientMoveSlots=0}));
        }
    }

    [TestMethod]
    public void RealMimicPreservesOnlyItsCopiedCurseSlotAtTheLaterCostReceipt()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Charge.Body(Initial(174,single,1),0,_=>Teach(Kernel.Mon("GENGAR","LICK",100),"MIMIC") with{SpeedStage=12});
            frame=Charge.Body(frame,1,b=>b with{SpeedStage=0});frame=Envelope.Resolve(frame,1,0).State;
            var copied=Charge.Ordered(frame,0,1,1);
            Assert.AreEqual(Move(174),copied.Events.OfType<SourceMoveCopiedEvent>().Single().CopiedMove);
            frame=Charge.CompleteTurn(copied);
            var result=Charge.Ordered(frame,0,1,1);Check(result,174,single,0);
            var sync=result.Events.OfType<SourceCalledPartyChangedEvent>().Single(e=>e.SourceSlot==new BattlerSlot(0));
            Assert.AreEqual((byte)2,sync.TransientMoveSlots);Assert.AreEqual(Kernel.Id("MIMIC"),sync.Before.Pokemon.Moves[1].Move);
            Assert.IsTrue(sync.Before.Pokemon.Moves.SequenceEqual(sync.After.Pokemon.Moves));
            Deny(result.Events.SetItem(result.Events.IndexOf(sync),sync with{TransientMoveSlots=1}),single,174);
            Assert.AreEqual("source_event_active_sync_persistence",SourceBattleEventHooks.ValidateRouting(result.State,sync with{TransientMoveSlots=15}));
        }
    }

    [TestMethod,DataRow(194),DataRow(195),DataRow(174)]
    public void ActualSleepAndPpLostAfterSelectionCannotBorrowAResult(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var sleeping=Charge.Body(Initial(id,single),0,b=>b with{Status=3});Deny(Charge.Ordered(sleeping,0,Target(id,0)).Events,single,id);
            var start=Begin(Initial(id,single),0,Target(id,0));var empty=Charge.Body(start.State,0,b=>b with{Pokemon=b.Pokemon with{Moves=[b.Pokemon.Moves[0] with{CurrentPp=0}]}});
            var result=Advance(start,empty);Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="no-pp"));Deny(result.Events,single,id);
        }
    }

    [TestMethod,DataRow(174),DataRow(195)]
    public void ActualMetronomeGhostSongLeavesAnimateWithoutChangingCallerOwnership(int id)
    {
        Assert.IsFalse(WildBattleSourceCallers.IsDirectMetronomeChoice(194));
        foreach(bool single in new[]{false,true})
        {
            var bodies=Caller.Bodies(118);for(int i=1;i<4;i++)bodies[i]=Kernel.Mon("WAILORD","SPLASH",1) with{SpeedStage=0};
            var frame=Kernel.Frame(bodies,single);SourceFourActionResult? found=null;
            for(uint seed=0;seed<65536;seed++)
            {
                var start=Begin(frame with{Rng=new(seed)},0,1);var target=SourceMoveTargeting.ResolveLearned(start.State,new(0),new(1),Kernel.Mechanics.Catalog.Moves[Kernel.Id("METRONOME")]);
                Assert.IsTrue(target.Succeeded,target.Fault);if(WildBattleSourceCallers.Metronome(target.Rng).SourceMoveId!=id)continue;
                found=Advance(start);break;
            }
            Assert.IsNotNull(found);Caller.Check(found.Events,found,118,single);var pp = found.Events.OfType<SourcePpCommandEvent>().First();
            SourceBattleEvent outcome = id == 195
                ? found.Events.OfType<SourceMtMoonVolatileEvent>().Last(e => e.Volatile == "perish-song" && e.Reason == "move")
                : found.Events[found.Events.IndexOf(pp) + 1];
            var cue = CalledLeafAnimationKernelFixture.Check(found.Events, found, single, Kernel.Id("METRONOME"), id, outcome);
            Assert.AreEqual(id == 174 ? (byte?)1 : null, cue.MoveTurn);
            var invoked=found.Events.OfType<SourceCalledMoveTransitionEvent>().Single().Invocation;
            Assert.AreEqual(Move(id),invoked.CurrentMove);Assert.AreEqual(Kernel.Id("METRONOME"),invoked.ChosenMove);
        }
    }

    [TestMethod,DataRow(194),DataRow(195),DataRow(174)]
    public void NewRootsRejectForeignDuplicateAndMissingInvocationEvidence(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var rows=Charge.Ordered(Initial(id,single),0,Target(id,0)).Events;var attack=rows.OfType<SourceAttackStringEvent>().Single();
            var used=rows.OfType<SourceMoveUsedEvent>().Single();var pp=rows.OfType<SourcePpCommandEvent>().First();int pi=rows.IndexOf(pp);
            foreach(var bad in new[]{pp with{PpOwner=new(1)},pp with{Actor=new(1)},pp with{Target=pp.Target.Opposite},pp with{CommandCost=0},
                pp with{PpSlot=3},pp with{ChosenMove=Kernel.Id("METRONOME")},pp with{CurrentMove=Kernel.Id("TACKLE")},pp with{After=pp.Before},pp with{Turn=pp.Turn+1}})Deny(rows.SetItem(pi,bad),single,id);
            Deny(Number(rows.Remove(pp)),single,id);Deny(Number(rows.Insert(pi,pp)),single,id);
            Deny(rows.SetItem(rows.IndexOf(attack),attack with{ChosenMove=Kernel.Id("METRONOME")}),single,id);
            Deny(Number(rows.Add(new SourceMoveFailedEvent(0,used.Turn,used.Actor,used.Target,used.Move,"foreign"))),single,id);
            Deny(Number(rows.Add(new SourceMtMoonVolatileEvent(0,used.Turn,used.Actor,"foreign",0,1,"move"))),single,id);
            if(id==194)
            {
                var flag=rows.OfType<SourceTowerVolatileEvent>().Single(e=>e.Applied);int at=rows.IndexOf(flag);
                Deny(rows.SetItem(at,flag with{Applied=false,Reason="action-attempt"}),single,id);Deny(rows.SetItem(at,flag with{Side=new(1)}),single,id);
                Deny(Number(rows.Insert(at,flag)),single,id);Deny(Number(rows.RemoveAt(at)),single,id);
            }
            if(id==195)
            {
                var markers=rows.OfType<SourceMtMoonVolatileEvent>().Where(e=>e.Volatile=="perish-song").ToArray();int at=rows.IndexOf(markers[0]);
                Deny(Number(rows.Insert(at,markers[0])),single,id);Deny(Number(rows.Where(e=>!markers.Any(m=>m==e))),single,id);
                Deny(Number(rows.SetItem(at,markers[1]).SetItem(at+1,markers[0])),single,id);
                Deny(rows.SetItem(at,markers[0] with{Before=3,After=2,Reason="countdown"}),single,id);
                Deny(Number(rows.Insert(at+markers.Length,new SourceAbilityActivatedEvent(0,used.Turn,markers[0].Side,"ABILITY_SOUNDPROOF"))),single,id);
            }
        }
    }

    [TestMethod]
    public void GhostCostAndPerishSyncRequireExactSourceOwnerWithoutOtherPartyMutation()
    {
        foreach(bool single in new[]{false,true})foreach(int id in new[]{174,195})
        {
            var frame=Initial(id,single);if(id==195)frame=Ability(frame,1,"ABILITY_PRESSURE");
            var unrelated=Kernel.Mon(id==174?"GASTLY":"LAPRAS",id==174?"LICK":"GROWL").Pokemon.Moves[0];
            frame=Charge.Body(frame,0,b=>b with{Pokemon=b.Pokemon with{Moves=b.Pokemon.Moves.Add(unrelated)}});
            var rows=Charge.Ordered(frame,0,Target(id,0)).Events;var sync=rows.OfType<SourceCalledPartyChangedEvent>().Single(e=>e.Reason=="active-sync");int si=rows.IndexOf(sync);
            Assert.AreEqual(new BattlerSlot(0),sync.SourceSlot);Assert.AreEqual((byte)0,sync.TransientMoveSlots);
            Deny(rows.SetItem(si,sync with{TransientMoveSlots=null}),single,id);
            Deny(rows.SetItem(si,sync with{TransientMoveSlots=15}),single,id);
            Deny(rows.SetItem(si,sync with{TransientMoveSlots=16}),single,id);
            Deny(rows.SetItem(si,sync with{SourceSlot=null}),single,id);
            Deny(rows.SetItem(si,sync with{SourceSlot=new(single?1:2)}),single,id);
            Deny(rows.SetItem(si,sync with{After=sync.After with{Pokemon=sync.After.Pokemon with{Friendship=(sync.After.Pokemon.Friendship+1)%256}}}),single,id);
            Deny(rows.SetItem(si,sync with{After=sync.After with{Pokemon=sync.After.Pokemon with{Moves=sync.After.Pokemon.Moves.SetItem(0,sync.After.Pokemon.Moves[0] with{CurrentPp=sync.After.Pokemon.Moves[0].CurrentPp+1})}}}),single,id);
            Assert.AreEqual(unrelated,sync.Before.Pokemon.Moves[1]);Assert.AreEqual(unrelated,sync.After.Pokemon.Moves[1]);
            Deny(rows.SetItem(si,sync with{After=sync.After with{Pokemon=sync.After.Pokemon with{Moves=sync.After.Pokemon.Moves.SetItem(1,unrelated with{CurrentPp=unrelated.CurrentPp-1})}}}),single,id);
            if(id==174)
            {
                var cost=rows.OfType<SourceTowerHpEvent>().Single(e=>e.Effect=="curse-cost");int ci=rows.IndexOf(cost);
                foreach(var bad in new[]{cost with{Side=new(1)},cost with{Effect="destiny-bond"},cost with{Damage=cost.Damage+1},cost with{HpAfter=cost.HpBefore},cost with{HpBefore=0}})Deny(rows.SetItem(ci,bad),single,id);
                Deny(Number(rows.RemoveAt(ci)),single,id);Deny(Number(rows.Insert(ci,cost)),single,id);Deny(Number(rows.RemoveAt(si)),single,id);
                Deny(Number(rows.Add(new SourceFaintedEvent(0,cost.Turn,new(0)))),single,id);
            }
            else
            {
                var extra=rows.OfType<SourcePpCommandEvent>().Last();int ei=rows.IndexOf(extra);
                foreach(var bad in new[]{extra with{Before=extra.Before+1},extra with{CommandCost=0},extra with{CommandCost=4},extra with{PpOwner=new(1)},extra with{RngCursor=extra.RngCursor+1}})Deny(rows.SetItem(ei,bad),single,id);
                Deny(Number(rows.Insert(ei,extra)),single,id);Deny(Number(rows.Remove(extra)),single,id);
            }
        }
    }

    [TestMethod]
    public void SourceSlotProvenanceSurvivesExportAndJsonAndRejectsOtherPartyBindings()
    {
        foreach(bool single in new[]{false,true})
        {
            var result=Charge.Ordered(Initial(174,single),0,1);
            var sync=result.Events.OfType<SourceCalledPartyChangedEvent>().Single(e=>e.Reason=="active-sync");
            Assert.IsNull(SourceBattleEventHooks.ValidateRouting(result.State,sync));
            var restored=JsonSerializer.Deserialize<SourceBattleEvent>(JsonSerializer.Serialize<SourceBattleEvent>(sync));
            Assert.IsInstanceOfType<SourceCalledPartyChangedEvent>(restored);
            Assert.AreEqual(sync.SourceSlot,((SourceCalledPartyChangedEvent)restored).SourceSlot);
            Assert.AreEqual(sync.TransientMoveSlots,((SourceCalledPartyChangedEvent)restored).TransientMoveSlots);
            var legacy=OverlayHitAnimationKernelFixture.Single([sync], 0).Events[0];Assert.IsInstanceOfType<SourceCalledPartyChangedEvent>(legacy);
            Assert.AreEqual(new BattlerSlot(0),((SourceCalledPartyChangedEvent)legacy).SourceSlot);
            Assert.AreEqual(sync.TransientMoveSlots,((SourceCalledPartyChangedEvent)legacy).TransientMoveSlots);
            Assert.AreEqual("source_event_active_sync_persistence",SourceBattleEventHooks.ValidateRouting(result.State,sync with{TransientMoveSlots=15}));
            Assert.AreEqual("source_event_active_sync_persistence",SourceBattleEventHooks.ValidateRouting(result.State,sync with{TransientMoveSlots=16}));
            Assert.AreEqual("source_event_active_sync_persistence",SourceBattleEventHooks.ValidateRouting(result.State,sync with{TransientMoveSlots=null}));
            Assert.AreEqual("source_event_active_sync_identity",SourceBattleEventHooks.ValidateRouting(result.State,sync with{SourceSlot=new(single?1:2)}));
            if(!single)Assert.AreEqual("source_event_active_sync_identity",SourceBattleEventHooks.ValidateRouting(result.State,sync with{PartyIndex=1}));
            Assert.AreEqual("source_event_active_sync_identity",SourceBattleEventHooks.ValidateRouting(result.State,sync with{Reason="foreign"}));
            // Optional provenance does not make historical default receipts invalid.
            Assert.IsNull(SourceBattleEventHooks.ValidateRouting(result.State,sync with{SourceSlot=null,TransientMoveSlots=null}));
        }
    }

    [TestMethod,DataRow(194),DataRow(195),DataRow(174)]
    public void EffectMetadataScriptAndSceneRemainAuthoritative(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var rows=Charge.Ordered(Initial(id,single),0,Target(id,0)).Events;int owner=rows.IndexOf(rows.OfType<SourceMoveUsedEvent>().Single());
            var moves=Kernel.Mechanics.Catalog.Moves;var move=Move(id);
            foreach(var metadata in new[]{null,moves.Remove(move),moves.SetItem(move,moves[move] with{Power=1}),moves.SetItem(move,moves[move] with{Target="MOVE_TARGET_UNKNOWN"}),
                moves.SetItem(move,moves[move] with{Target=id==174?"MOVE_TARGET_USER":"MOVE_TARGET_SELECTED"}),moves.SetItem(move,moves[move] with{SourceMoveId=1})})
                Assert.IsNull(single?AnimationDispatchFixture.AtAttackText(Charge.Single(rows,owner),true,SourceBattleScene.On,metadata).Dispatch:
                    AnimationDispatchFixture.AtAttackText(Envelope.Double(rows,owner),SourceBattleScene.On,metadata).Dispatch);
            Assert.IsNull(single?CampaignMoveAnimationPresentation.AtAttackText(Charge.Single(rows,owner),true,SourceBattleScene.On,moves,(_,_)=>false).Dispatch:
                CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(rows,owner),SourceBattleScene.On,moves,(_,_)=>false).Dispatch);
        }
    }
}
