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
using Charge=FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class UtilityAnimationKernelTests
{
    // Test cases select source-legal learners; production admission uses effect
    // metadata and the generated resource resolver, never this fixture list.
    private static MoveId Move(int id)=>Kernel.Id(id switch{281=>"YAWN",285=>"SKILL_SWAP",288=>"GRUDGE",191=>"SPIKES",_=>throw new ArgumentOutOfRangeException(nameof(id))});
    private static WildBattleCombatant[] Bodies(int id,int actor=0)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        bodies[actor]=(id switch{
            281=>Kernel.Mon("SLOWPOKE","YAWN"),
            285=>ConditionalRecoveryAnimationKernelFixture.MachineMon("ABRA","TM48","SKILL_SWAP","TELEPORT"),
            288=>Kernel.Mon("VULPIX","GRUDGE"),
            191=>Kernel.Mon("QWILFISH","SPIKES"),
            _=>throw new ArgumentOutOfRangeException(nameof(id))}) with{AccuracyStage=12,SpeedStage=12};
        Assert.AreEqual(id,Kernel.Mechanics.Catalog.Moves[Move(id)].SourceMoveId);return bodies;
    }
    private static SourceBattleFrame Initial(int id,bool single,int actor=0)=>Kernel.Frame(Bodies(id,actor),single);
    private static int Target(int id,int actor)=>id==288?actor:actor^1;
    private static SourceBattleFrame Ability(SourceBattleFrame frame,int slot,string ability)=>Charge.Body(frame,slot,b=>
        b with{CalledOverlay=(b.CalledOverlay??new()) with{Ability=ability}});
    private static SourceBattleEvent Owner(ImmutableArray<SourceBattleEvent> rows,int id)=>id switch{
        281=>rows.OfType<SourceYawnEvent>().Single(e=>e.Reason=="move"),
        285=>rows.OfType<SourceCalledOverlayChangedEvent>().Last(e=>e.Move==Move(id)),
        _=>rows.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile==(id==288?"grudge":"spikes")&&e.Reason=="move")};
    private static void Deny(ImmutableArray<SourceBattleEvent> rows,bool single)=>Charge.Deny(rows,single);
    private static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> rows)=>Shared.Number(rows);
    private static void Check(SourceFourActionResult result,int id,bool single)
    {
        string snapshot=JsonSerializer.Serialize(result);Shared.Check(result,id,single,Owner(result.Events,id));
        var used=result.Events.OfType<SourceMoveUsedEvent>().Single();int ui=result.Events.IndexOf(used);
        Assert.IsNotNull(Charge.Project(result.Events,ui,single,false).Dispatch,
            "Singles must authenticate the canonical receipt stream directly.");
        if(single)Assert.IsNull(AnimationDispatchFixture.AtAttackText(Charge.Single(result.Events,ui),false,SourceBattleScene.On).Dispatch);
        Assert.AreEqual(snapshot,JsonSerializer.Serialize(result));
    }

    [TestMethod,DataRow(281),DataRow(285),DataRow(288),DataRow(191)]
    public void ActualOrderedUtilityActionsOwnOneRootAcrossAllPositionsAndSinglesExport(int id)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            var frame=Initial(id,single,actor);string before=JsonSerializer.Serialize(frame);
            var result=Charge.Ordered(frame,actor,Target(id,actor));Check(result,id,single);
            Assert.AreEqual(before,JsonSerializer.Serialize(frame));
            var pp=result.Events.OfType<SourcePpCommandEvent>().Single();
            if(id==285)
            {
                var writes=result.Events.OfType<SourceCalledOverlayChangedEvent>().ToArray();Assert.AreEqual(2,writes.Length);
                Assert.AreEqual(actor,writes[0].Side.Value);Assert.AreEqual(actor^1,writes[1].Side.Value);
                Assert.AreEqual(pp.Sequence+1,writes[0].Sequence);Assert.AreEqual(pp.Sequence+2,writes[1].Sequence);
            }
            else Assert.AreEqual(pp.Sequence+1,Owner(result.Events,id).Sequence);
            if(id==191)
            {
                var layers=result.Events.OfType<SourceCalledSpikesChangedEvent>().Single();var attack=result.Events.OfType<SourceAttackStringEvent>().Single();
                Assert.AreEqual(layers.Sequence+1,attack.Sequence);Assert.AreEqual(0,layers.Before);Assert.AreEqual(1,layers.After);
                Assert.AreEqual((BattleSide)((actor&1)^1),layers.Side);
            }
        }
    }

    [TestMethod,DataRow(281),DataRow(285)]
    public void SelectedUtilitiesCanTargetOwnPartnerWithRealSlotOwnership(int id)
    {
        for(int actor=0;actor<4;actor++)Check(Charge.Ordered(Initial(id,false,actor),actor,actor^2),id,false);
    }

    [TestMethod]
    public void YawnUsesOnlySuccessfulDrowsinessAndAllActualFailureBranchesStayClosed()
    {
        foreach(bool single in new[]{false,true})foreach(string branch in new[]{"status","drowsy","substitute","protected","safeguard","insomnia","vital-spirit"})
        {
            var frame=Initial(281,single);
            frame=branch switch{
                "status"=>Charge.Body(frame,1,b=>b with{Status=WildBattleStatus.Burn}),
                "drowsy"=>Charge.Body(frame,1,b=>b with{YawnTurnsRemaining=1}),
                "substitute"=>Charge.Body(frame,1,b=>b with{CalledVolatile=new(){SubstituteHp=20}}),
                "protected"=>Charge.Body(frame,1,b=>b with{Protected=true}),
                "safeguard"=>frame with{Sides=frame.Sides.SetItem(1,frame.Sides[1] with{SafeguardTurnsRemaining=3})},
                _=>Ability(frame,1,branch=="insomnia"?"ABILITY_INSOMNIA":"ABILITY_VITAL_SPIRIT")};
            var failed=Charge.Ordered(frame,0,1);Assert.IsFalse(failed.Events.OfType<SourceYawnEvent>().Any(e=>e.Reason=="move"));Deny(failed.Events,single);
        }
        foreach(bool single in new[]{false,true})
        {
            var result=Charge.Ordered(Initial(281,single),0,1);var rows=result.Events;var mark=(SourceYawnEvent)Owner(rows,281);int i=rows.IndexOf(mark);
            foreach(var bad in new[]{mark with{Reason="countdown"},mark with{Before=1},mark with{After=1},mark with{Side=new(0)},mark with{Turn=mark.Turn+1}})
                Deny(rows.SetItem(i,bad),single);
            Deny(Number(rows.Remove(mark)),single);Deny(Number(rows.Insert(i,mark)),single);
            var next=Charge.CompleteTurn(result);Assert.AreEqual(1,next.Slots[1].Body!.YawnTurnsRemaining);
        }
    }

    [TestMethod]
    public void SkillSwapAcceptsEqualAndSingleNoneAbilitiesAndIgnoresSubstitute()
    {
        foreach(bool single in new[]{false,true})foreach(var abilities in new[]{("ABILITY_PRESSURE","ABILITY_PRESSURE"),("ABILITY_NONE","ABILITY_PRESSURE"),("ABILITY_PRESSURE","ABILITY_NONE")})
        {
            var frame=Ability(Ability(Initial(285,single),0,abilities.Item1),1,abilities.Item2);
            frame=Charge.Body(frame,1,b=>b with{CalledVolatile=new(){SubstituteHp=20}});
            var result=Charge.Ordered(frame,0,1);Check(result,285,single);
            Assert.AreEqual(abilities.Item2,WildBattleEffective.Ability(result.State.Slots[0].Body!));
            Assert.AreEqual(abilities.Item1,WildBattleEffective.Ability(result.State.Slots[1].Body!));
        }
    }

    [TestMethod]
    public void SkillSwapBlocksWonderGuardBothNoneAndProtectedTargets()
    {
        foreach(bool single in new[]{false,true})foreach(var abilities in new[]{("ABILITY_WONDER_GUARD","ABILITY_PRESSURE"),("ABILITY_PRESSURE","ABILITY_WONDER_GUARD"),("ABILITY_NONE","ABILITY_NONE")})
        {
            var result=Charge.Ordered(Ability(Ability(Initial(285,single),0,abilities.Item1),1,abilities.Item2),0,1);
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="uncopyable-ability"));
            Assert.IsFalse(result.Events.OfType<SourceCalledOverlayChangedEvent>().Any(e=>e.Move==Move(285)));Deny(result.Events,single);
        }
        foreach(bool single in new[]{false,true})
        {
            var result=Charge.Ordered(Charge.Body(Initial(285,single),1,b=>b with{Protected=true}),0,1);
            Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any());Deny(result.Events,single);
        }
    }

    [TestMethod]
    public void SkillSwapRequiresBothOrderedAbilityOnlyWritesAndMatchingKnownPriorAbilities()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Ability(Ability(Initial(285,single),0,"ABILITY_PRESSURE"),1,"ABILITY_LIMBER");
            var rows=Charge.Ordered(frame,0,1).Events;var writes=rows.OfType<SourceCalledOverlayChangedEvent>().ToArray();
            foreach(var row in writes)
            {
                int i=rows.IndexOf(row);
                foreach(var bad in new[]{row with{Side=row.Side.Opposite},row with{Move=Kernel.Id("ROLE_PLAY")},
                    row with{After=null},row with{After=row.After! with{Ability="ABILITY_WONDER_GUARD"}},
                    row with{After=row.After! with{Ability="ABILITY_IMMUNITY"}},row with{After=row.After! with{Type1="TYPE_FIRE"}},
                    row with{Turn=row.Turn+1}})Deny(rows.SetItem(i,bad),single);
                Deny(Number(rows.Remove(row)),single);Deny(Number(rows.Insert(i,row)),single);
            }
            Deny(Number(rows.SetItem(rows.IndexOf(writes[0]),writes[1]).SetItem(rows.IndexOf(writes[1]),writes[0])),single);
        }
    }

    [TestMethod]
    public void SpikesAuthenticatesEachLayerWithFieldPressureAndMaximumFailure()
    {
        foreach(bool single in new[]{false,true})for(int layers=0;layers<=3;layers++)
        {
            var frame=Initial(191,single);frame=frame with{Sides=frame.Sides.SetItem(1,frame.Sides[1] with{Spikes=layers})};
            foreach(int slot in Enumerable.Range(0,frame.BattlerCount).Where(i=>(i&1)==1))frame=Ability(frame,slot,"ABILITY_PRESSURE");
            frame=Charge.Body(frame,0,b=>b with{Pokemon=b.Pokemon with{Moves=[b.Pokemon.Moves[0] with{CurrentPp=1}]}});
            var result=Charge.Ordered(frame,0,1);var pp=result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(layers==3?1:single?2:3,pp.CommandCost);Assert.AreEqual(1,pp.Before);Assert.AreEqual(0,pp.After);
            Assert.AreEqual(Math.Min(3,layers+1),result.State.Sides[1].Spikes);
            if(layers==3)
            {
                Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="spikes-at-maximum"));
                Assert.IsFalse(result.Events.OfType<SourceCalledSpikesChangedEvent>().Any());Deny(result.Events,single);
            }
            else Check(result,191,single);
        }
    }

    [TestMethod]
    public void SpikesRequiresItsPreAttackLayerMutationAndItsPostPpMessage()
    {
        foreach(bool single in new[]{false,true})
        {
            var rows=Charge.Ordered(Initial(191,single),0,1).Events;var layer=rows.OfType<SourceCalledSpikesChangedEvent>().Single();
            var mark=(SourceMtMoonVolatileEvent)Owner(rows,191);int li=rows.IndexOf(layer),mi=rows.IndexOf(mark);
            foreach(var bad in new[]{layer with{Side=BattleSide.Player},layer with{Before=3,After=4},layer with{After=0},layer with{After=2},layer with{Turn=layer.Turn+1}})
                Deny(rows.SetItem(li,bad),single);
            foreach(var bad in new[]{mark with{Side=new(1)},mark with{Volatile="grudge"},mark with{After=0},mark with{Reason="rapid-spin"}})
                Deny(rows.SetItem(mi,bad),single);
            Deny(Number(rows.Remove(layer)),single);Deny(Number(rows.Remove(mark)),single);Deny(Number(rows.Insert(li,layer)),single);
            Deny(Number(rows.Insert(rows.IndexOf(rows.OfType<SourcePpCommandEvent>().Single())+1,layer)),single);
        }
    }

    [TestMethod]
    public void RepeatedGrudgeUsesNewSuccessfulSetupAfterTheRealActionAttemptClear()
    {
        foreach(bool single in new[]{false,true})
        {
            var first=Charge.Ordered(Initial(288,single),0,0);Check(first,288,single);
            var second=Charge.Ordered(Charge.CompleteTurn(first),0,0);Check(second,288,single);
            var marks=second.Events.OfType<SourceMtMoonVolatileEvent>().Where(e=>e.Volatile=="grudge").ToArray();
            Assert.AreEqual(2,marks.Length);Assert.AreEqual("action-attempt",marks[0].Reason);Assert.AreEqual(1,marks[0].Before);Assert.AreEqual(0,marks[0].After);
            Assert.AreEqual("move",marks[1].Reason);Assert.AreEqual(0,marks[1].Before);Assert.AreEqual(1,marks[1].After);
            int mi=second.Events.IndexOf(marks[1]);Deny(second.Events.SetItem(mi,marks[1] with{Reason="action-attempt"}),single);
        }
    }

    [TestMethod,DataRow(281),DataRow(285),DataRow(288),DataRow(191)]
    public void UtilityCancellersAndUnrelatedTailReceiptsCannotCreateRoots(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var blocked=Charge.Ordered(Charge.Body(Initial(id,single),0,b=>b with{Status=3}),0,Target(id,0));
            Assert.IsTrue(blocked.Events.OfType<SourceActionBlockedEvent>().Any());Deny(blocked.Events,single);
            var rows=Charge.Ordered(Initial(id,single),0,Target(id,0)).Events;var used=rows.OfType<SourceMoveUsedEvent>().Single();
            int tail=rows.IndexOf(Owner(rows,id))+1;
            SourceBattleEvent[] extras=[new SourceYawnEvent(0,used.Turn,new(1),2,1,"countdown"),
                new SourceMtMoonVolatileEvent(0,used.Turn,new(0),"grudge",1,0,"action-attempt"),
                new SourceStatusChangedEvent(0,used.Turn,new(1),0,3,"yawn"),
                new SourceMoveFailedEvent(0,used.Turn,used.Actor,used.Target,used.Move,"protected"),
                new SourceAbilityPreventedEvent(0,used.Turn,used.Actor,used.Target,used.Move,"ABILITY_INSOMNIA"),
                new SourcePayDayEvent(0,used.Turn,new(1),0,100)];
            foreach(var extra in extras)Deny(Number(rows.Insert(tail,extra)),single);
        }
    }

    [TestMethod,DataRow(281),DataRow(285),DataRow(288),DataRow(191)]
    public void RealMoveEndWhiteHerbTailIsIndependentAndRequiresItsItemOwner(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Charge.Body(Initial(id,single),1,b=>b with{HeldItem="ITEM_WHITE_HERB",AttackStage=5});
            var result=Charge.Ordered(frame,0,Target(id,0));Check(result,id,single);
            var item=result.Events.OfType<SourceHeldItemConsumedEvent>().Single();Assert.AreEqual("ITEM_WHITE_HERB",item.Item);
            Deny(Number(result.Events.Remove(item)),single);Deny(result.Events.SetItem(result.Events.IndexOf(item),item with{Side=new(0)}),single);
        }
    }

    [TestMethod]
    public void SkillSwapAcquiredImmunityTailDoesNotReplaceEitherSwapAssignment()
    {
        foreach(bool single in new[]{false,true})
        {
            var frame=Ability(Initial(285,single),0,"ABILITY_MAGMA_ARMOR");frame=Charge.Body(frame,1,b=>b with{Status=WildBattleStatus.Freeze});
            var result=Charge.Ordered(frame,0,1);Check(result,285,single);
            var ability=result.Events.OfType<SourceAbilityActivatedEvent>().Single(e=>e.Ability=="ABILITY_MAGMA_ARMOR");
            var status=result.Events.OfType<SourceStatusChangedEvent>().Single(e=>e.Side.Value==1);
            Assert.AreEqual(ability.Sequence+1,status.Sequence);Assert.AreEqual(0u,status.After);
            Deny(Number(result.Events.Remove(ability)),single);
        }
    }

    [TestMethod,DataRow(281),DataRow(285),DataRow(288),DataRow(191)]
    public void UtilityMetadataPpIdentityAndResolverRemainAuthoritative(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var rows=Charge.Ordered(Initial(id,single),0,Target(id,0)).Events;var used=rows.OfType<SourceMoveUsedEvent>().Single();
            var pp=rows.OfType<SourcePpCommandEvent>().Single();int pi=rows.IndexOf(pp),ui=rows.IndexOf(used);
            foreach(var bad in new[]{pp with{PpOwner=new(1)},pp with{Actor=new(1)},pp with{Target=pp.Target.Opposite},
                pp with{CommandCost=0},pp with{CommandCost=5},pp with{PpSlot=3},pp with{CurrentMove=Kernel.Id("TACKLE")},pp with{Turn=pp.Turn+1}})
                Deny(rows.SetItem(pi,bad),single);
            Deny(Number(rows.Remove(pp)),single);Deny(Number(rows.Insert(pi,pp)),single);
            var moves=Kernel.Mechanics.Catalog.Moves;
            foreach(var metadata in new[]{null,moves.Remove(Move(id)),moves.SetItem(Move(id),moves[Move(id)] with{Power=1}),
                moves.SetItem(Move(id),moves[Move(id)] with{SourceMoveId=1}),moves.SetItem(Move(id),moves[Move(id)] with{Target="MOVE_TARGET_BOTH"})})
                Assert.IsNull(single?AnimationDispatchFixture.AtAttackText(Charge.Single(rows,ui),true,SourceBattleScene.On,metadata).Dispatch:
                    AnimationDispatchFixture.AtAttackText(Envelope.Double(rows,ui),SourceBattleScene.On,metadata).Dispatch);
            Assert.IsNull(single?CampaignMoveAnimationPresentation.AtAttackText(Charge.Single(rows,ui),true,SourceBattleScene.On,moves,(_,_)=>false).Dispatch:
                CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(rows,ui),SourceBattleScene.On,moves,(_,_)=>false).Dispatch);
        }
    }
}
