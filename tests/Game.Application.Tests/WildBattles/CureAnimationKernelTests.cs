using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class CureAnimationKernelTests
{
    private static MoveId Move(int id)=>Kernel.Id(id switch{287=>"REFRESH",215=>"HEAL_BELL",312=>"AROMATHERAPY",_=>throw new ArgumentOutOfRangeException(nameof(id))});
    private static string Reason(int id)=>id==287?"refresh":id==215?"heal-bell":"aromatherapy";
    private static WildBattleCombatant[] Bodies(int id,int actor=0)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>Kernel.Mon("WAILORD","SPLASH",100) with{SpeedStage=0}).ToArray();
        bodies[actor]=(id switch{287=>Kernel.Mon("CHANSEY","REFRESH"),215=>Kernel.Mon("MILTANK","HEAL_BELL",60),
            312=>Kernel.Mon("ROSELIA","AROMATHERAPY",60),_=>throw new ArgumentOutOfRangeException(nameof(id))}) with{SpeedStage=12};
        return bodies;
    }
    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies,bool single)
    {
        // Entry initialization rejects invented initial overlays. Complete real
        // natural entry first, then install the controlled battle-only ability.
        var frame=Kernel.Frame(bodies.Select(b=>b with{CalledOverlay=null}).ToArray(),single);
        for(int i=0;i<frame.BattlerCount;i++)
            if(bodies[i].CalledOverlay is {} overlay)
                frame=frame with{Slots=frame.Slots.SetItem(i,frame.Slots[i] with{Body=frame.Slots[i].Body! with{CalledOverlay=overlay}})};
        return frame;
    }
    private static SourceKernelResult Resolve(SourceBattleFrame frame,int actor=0)=>Envelope.Resolve(frame,actor,actor);
    private static void Deny(ImmutableArray<SourceBattleEvent> events,bool single)=>Envelope.Deny(events,single,"Cure root requires an owned direct source command.");
    private static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> events)=>Shared.Number(events);
    private static SourceBattleEvent Owner(SourceKernelResult result,int id)=>id==287
        ?result.Events.OfType<SourceStatusChangedEvent>().Single(e=>e.Reason=="refresh")
        :result.Events.OfType<SourcePpCommandEvent>().Single();

    [TestMethod]
    public void RefreshAuthenticatesEveryCureableStatusAcrossAllSlotsAndSinglesExport()
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        foreach(uint status in new[]{WildBattleStatus.Poison,WildBattleStatus.Burn,WildBattleStatus.Paralysis,
            WildBattleSourceFieldEffects.ToxicPoison,WildBattleSourceFieldEffects.ToxicPoison|WildBattleSourceFieldEffects.ToxicCounterMask})
        {
            var bodies=Bodies(287,actor);bodies[actor]=bodies[actor] with{Status=status};
            var frame=Frame(bodies,single);
            var result=Envelope.Find(frame,actor,actor,r=>r.Events.OfType<SourceStatusChangedEvent>().Any(e=>e.Reason=="refresh"),"Refresh committed cure");
            var pp=result.Events.OfType<SourcePpCommandEvent>().Single();var cure=(SourceStatusChangedEvent)Owner(result,287);
            Assert.AreEqual(status,cure.Before);Assert.AreEqual(0u,cure.After);Assert.AreEqual(actor,cure.Side.Value);
            Assert.AreEqual(pp.Sequence+1,cure.Sequence);Assert.AreEqual(0u,result.State.Slots[actor].Body!.Status);
            if(WildBattleSourceFieldEffects.IsToxic(status))
            {
                var sync=result.Events.OfType<SourceCalledPartyChangedEvent>().Single(e=>e.Reason=="active-sync");
                Assert.AreEqual(WildBattleSourceFieldEffects.ToxicPoison,sync.Before.Status,"Party storage keeps Toxic without its live battle counter.");
                Assert.AreEqual(0u,sync.After.Status);Assert.AreEqual(status,cure.Before,"The status receipt keeps the actual live counter.");
                // The canonical singles export preserves each side of this
                // battle-versus-persistent status distinction unchanged.
                if(single)
                {
                    var exportedCure=(WildBattleStatusChangedEvent)SourceBattleEventAdapter.Export(cure);
                    var exportedSync=(WildBattleCalledPartyChangedEvent)SourceBattleEventAdapter.Export(sync);
                    Assert.AreEqual(cure.Before,exportedCure.Before);Assert.AreEqual(sync.Before.Status,exportedSync.Before.Status);
                }
            }
            Shared.Check(result,287,single,cure);
            if(single)
            {
                int cursor=result.Events.IndexOf(result.Events.OfType<SourceMoveUsedEvent>().Single());
                Assert.IsNotNull(AnimationDispatchFixture.AtAttackText(Envelope.Single(result.Events,cursor) with{SourceEvents=[]},true,SourceBattleScene.On).Dispatch,
                    "Singles must consume their exported receipt envelope rather than the source side channel.");
            }
        }
    }

    [TestMethod]
    public void HealthyRefreshFailsAfterPpAndSleepFreezeOrParalysisCancellersOwnNoRoot()
    {
        foreach(bool single in new[]{false,true})
        {
            var healthy=Resolve(Kernel.Frame(Bodies(287),single));
            Assert.AreEqual(1,healthy.Events.OfType<SourcePpCommandEvent>().Count());
            Assert.AreEqual("no-refreshable-status",healthy.Events.OfType<SourceMoveFailedEvent>().Single().Reason);
            Assert.IsFalse(healthy.Events.OfType<SourceStatusChangedEvent>().Any());Shared.Check(healthy,287,single,null);
            foreach(uint status in new[]{3u,WildBattleStatus.Freeze,WildBattleStatus.Paralysis})
            {
                var bodies=Bodies(287);bodies[0]=bodies[0] with{Status=status};
                var blocked=Envelope.Find(Frame(bodies,single),0,0,
                    r=>r.Events.OfType<SourceActionBlockedEvent>().Any(),"Refresh source action canceller");
                Assert.IsFalse(blocked.Events.OfType<SourcePpCommandEvent>().Any());Deny(blocked.Events,single);
            }
        }
    }

    [TestMethod, DataRow(215), DataRow(312)]
    public void HealthyPartyStillOwnsTheUnconditionalSourceRootWithoutInventingAnyCure(int id)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            var frame=Kernel.Frame(Bodies(id,actor),single);string before=JsonSerializer.Serialize(frame);
            var result=Kernel.Ordered(frame,actor,actor);var pp=result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.IsFalse(result.Events.OfType<SourceStatusChangedEvent>().Any());
            Assert.IsFalse(result.Events.OfType<SourceCalledPartyChangedEvent>().Any(e=>e.Reason==Reason(id)));
            Assert.IsFalse(result.Events.OfType<SourceAbilityPreventedEvent>().Any());
            Assert.IsTrue(result.Events.OfType<SourceCalledPartyChangedEvent>().Any(e=>e.Reason=="active-sync"),"The command still stores its actual spent PP.");
            Shared.Check(result,id,single,pp);Assert.AreEqual(before,JsonSerializer.Serialize(frame));
            if(single)
            {
                int cursor=result.Events.IndexOf(result.Events.OfType<SourceMoveUsedEvent>().Single());
                Assert.IsNotNull(AnimationDispatchFixture.AtAttackText(Envelope.Single(result.Events,cursor) with{SourceEvents=[]},true,SourceBattleScene.On).Dispatch);
            }
        }
    }

    [TestMethod, DataRow(215), DataRow(312)]
    public void PartyCuresPreserveRealReserveFaintedAndNightmareOrder(int id)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            var bodies=Bodies(id,actor);bodies[actor]=bodies[actor] with{Status=WildBattleStatus.Burn};
            if(!single)bodies[actor^2]=bodies[actor^2] with{Status=3,Nightmare=true};
            var frame=Frame(bodies,single);int side=actor&1;
            var reserve=Kernel.Mon("WAILORD","SPLASH",100).Pokemon;
            var soundproof=Kernel.Mon("VOLTORB","CHARGE").Pokemon;
            Assert.AreEqual("ABILITY_SOUNDPROOF",soundproof.AbilityId);
            var party=frame.Parties[side];int firstReserve=party.Members.Length;
            party=party with{Members=party.Members.Add(new(reserve with{CurrentHp=0},Status:WildBattleStatus.Freeze))
                .Add(new(soundproof,Status:WildBattleSourceFieldEffects.ToxicPoison))};
            frame=frame with{Parties=frame.Parties.SetItem(side,party)};
            var result=Resolve(frame,actor);var events=result.Events;
            Assert.AreEqual(0u,result.State.Parties[side].Members[firstReserve].Status,"A fainted reserve can still have its status cleared.");
            Assert.AreEqual(id==215?WildBattleSourceFieldEffects.ToxicPoison:0u,result.State.Parties[side].Members[firstReserve+1].Status);
            var changes=events.OfType<SourceCalledPartyChangedEvent>().Where(e=>e.Reason==Reason(id)).ToArray();
            Assert.IsTrue(changes.Length>0);CollectionAssert.AreEqual(changes.Select(e=>e.PartyIndex).Order().ToArray(),changes.Select(e=>e.PartyIndex).ToArray());
            Assert.IsTrue(changes.All(e=>e.Side==(BattleSide)side&&e.Before.Status!=0&&e.After.Status==0));
            if(!single)
            {
                var nightmare=events.OfType<SourceTowerVolatileEvent>().Single(e=>e.Effect=="nightmare");
                Assert.IsFalse(nightmare.Applied);Assert.AreEqual(Reason(id),nightmare.Reason);
                var cured=events.OfType<SourceStatusChangedEvent>().Single(e=>e.Side.Value==(actor^2));
                Assert.AreEqual(nightmare.Sequence+1,cured.Sequence);Assert.IsFalse(result.State.Slots[actor^2].Body!.Nightmare);
            }
            Shared.Check(result,id,single,Owner(result,id));
            if(single)
            {
                int cursor=events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single());var envelope=Envelope.Single(events,cursor) with{SourceEvents=[]};
                Assert.IsNotNull(AnimationDispatchFixture.AtAttackText(envelope,true,SourceBattleScene.On).Dispatch);
                int changedAt=events.IndexOf(changes[0]);var changed=(WildBattleCalledPartyChangedEvent)envelope.Events[changedAt];
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(envelope with{Events=envelope.Events.SetItem(changedAt,changed with{Reason="move"})},true,SourceBattleScene.On).Dispatch,
                    "The singles adapter must preserve the party outcome used to validate the command.");
            }
        }
    }

    [TestMethod, DataRow(215), DataRow(312)]
    public void SoundproofIsAMemberResultAndCannotSuppressThePartyRoot(int id)
    {
        foreach(bool single in new[]{false,true})foreach(bool healthy in new[]{false,true})foreach(bool blockActor in new[]{false,true})
        {
            var bodies=Bodies(id);uint status=healthy?0u:WildBattleStatus.Burn;
            bodies[0]=bodies[0] with{Status=status,CalledOverlay=blockActor?new(){Ability="ABILITY_SOUNDPROOF"}:null};
            if(!single)bodies[2]=bodies[2] with{Status=status,CalledOverlay=new(){Ability="ABILITY_SOUNDPROOF"}};
            var result=Resolve(Frame(bodies,single));var blocks=result.Events.OfType<SourceAbilityPreventedEvent>().ToArray();
            Assert.AreEqual(id==215?(blockActor?1:0)+(single?0:1):0,blocks.Length);
            Assert.AreEqual(id==215&&blockActor?status:0u,result.State.Slots[0].Body!.Status);
            if(!single)Assert.AreEqual(id==215?status:0u,result.State.Slots[2].Body!.Status);
            Shared.Check(result,id,single,Owner(result,id));
        }
    }

    [TestMethod]
    public void LaterLumCureOfSoundproofUserCannotEraseOrOwnHealBell()
    {
        foreach(bool single in new[]{false,true})
        {
            var bodies=Bodies(215);bodies[0]=bodies[0] with{Status=WildBattleStatus.Burn,HeldItem="ITEM_LUM_BERRY",CalledOverlay=new(){Ability="ABILITY_SOUNDPROOF"}};
            var result=Resolve(Frame(bodies,single));var prevention=result.Events.OfType<SourceAbilityPreventedEvent>().Single();
            var berry=result.Events.OfType<SourceStatusChangedEvent>().Single();Assert.AreEqual("held-berry",berry.Reason);
            Assert.IsTrue(prevention.Sequence<berry.Sequence);Assert.AreEqual(0u,result.State.Slots[0].Body!.Status);
            Shared.Check(result,215,single,Owner(result,215));
        }
    }

    [TestMethod, DataRow(287), DataRow(215), DataRow(312)]
    public void ActualMetronomeLeavesRemainOutsideDirectRootAdmission(int id)
    {
        uint seed=CallerPreludeAnimationKernelFixture.MetronomeSeed(id);
        foreach(bool single in new[]{false,true})
        {
            var bodies=Bodies(id);bodies[0]=Kernel.Mon("TOGETIC","METRONOME",29) with{Status=WildBattleStatus.Burn};
            var result=Kernel.Direct(Frame(bodies,single) with{Rng=new(seed)});
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e=>e.Result==Move(id)));
            Assert.IsTrue(result.Events.OfType<SourceStatusChangedEvent>().Any(e=>e.Reason==Reason(id)));
            CallerPreludeAnimationKernelFixture.DenyLeaf(result.Events,single,id);
        }
    }

    [TestMethod, DataRow(287), DataRow(215), DataRow(312)]
    public void AttackPpTargetScriptMetadataAndSceneRemainAuthoritative(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var bodies=Bodies(id);bodies[0]=bodies[0] with{Status=WildBattleStatus.Burn};
            var result=Resolve(Frame(bodies,single));var events=result.Events;
            var used=events.OfType<SourceMoveUsedEvent>().Single();var attack=events.OfType<SourceAttackStringEvent>().Single();var pp=events.OfType<SourcePpCommandEvent>().Single();
            int ui=events.IndexOf(used),ai=events.IndexOf(attack),pi=events.IndexOf(pp);
            foreach(var bad in new[]{attack with{Actor=new(1)},attack with{ChosenMove=Kernel.Id("METRONOME")},attack with{Turn=attack.Turn+1}})Deny(events.SetItem(ai,bad),single);
            foreach(var bad in new[]{pp with{PpOwner=new(1)},pp with{Actor=new(1)},pp with{Target=new(1)},pp with{PpSlot=3},
                pp with{CommandCost=0},pp with{CommandCost=2},pp with{CurrentMove=Kernel.Id("TACKLE")},pp with{Before=0,After=0},pp with{Turn=pp.Turn+1}})Deny(events.SetItem(pi,bad),single);
            foreach(var bad in new[]{used with{Target=new(1)},used with{Slot=4},used with{PpAfter=used.PpBefore}})Deny(events.SetItem(ui,bad),single);
            Deny(events.Remove(attack),single);Deny(events.Remove(pp),single);Deny(Number(events.Insert(pi,pp)),single);
            Deny(Number(events.Insert(pi+1,new SourceActionBlockedEvent(0,used.Turn,used.Actor,used.Move,WildBattleStatus.Paralysis))),single);
            Deny(Number(events.Insert(pi+1,new SourceMoveFailedEvent(0,used.Turn,used.Actor,used.Actor,used.Move,"no-refreshable-status"))),single);
            var moves=Kernel.Mechanics.Catalog.Moves;
            foreach(var metadata in new[]{null,moves.Remove(Move(id)),moves.SetItem(Move(id),moves[Move(id)] with{SourceMoveId=1}),
                moves.SetItem(Move(id),moves[Move(id)] with{Target="MOVE_TARGET_SELECTED"}),moves.SetItem(Move(id),moves[Move(id)] with{Power=1})})
                Assert.IsNull(single?AnimationDispatchFixture.AtAttackText(Envelope.Single(events,ui),true,SourceBattleScene.On,metadata).Dispatch:
                    AnimationDispatchFixture.AtAttackText(Envelope.Double(events,ui),SourceBattleScene.On,metadata).Dispatch);
            Assert.IsNull(single?CampaignMoveAnimationPresentation.AtAttackText(Envelope.Single(events,ui),true,SourceBattleScene.On,moves,(_,_)=>false).Dispatch:
                CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events,ui),SourceBattleScene.On,moves,(_,_)=>false).Dispatch);
            if(single)Assert.IsNull(AnimationDispatchFixture.AtAttackText(Envelope.Single(events,ui),false,SourceBattleScene.On).Dispatch);
        }
    }

    [TestMethod]
    public void RefreshRequiresOneImmediateOwnedSuccessfulRefreshStatus()
    {
        foreach(bool single in new[]{false,true})
        {
            var bodies=Bodies(287);bodies[0]=bodies[0] with{Status=WildBattleStatus.Burn};
            var events=Resolve(Frame(bodies,single)).Events;var cure=events.OfType<SourceStatusChangedEvent>().Single();int ci=events.IndexOf(cure);
            foreach(var bad in new[]{cure with{Side=new(1)},cure with{Reason="held-berry"},cure with{Before=0},cure with{Before=3},cure with{Before=WildBattleStatus.Freeze},
                cure with{Before=WildBattleSourceFieldEffects.ToxicPoison|WildBattleStatus.Burn},cure with{After=WildBattleStatus.Burn},cure with{Turn=cure.Turn+1}})
                Deny(events.SetItem(ci,bad),single);
            Deny(events.Remove(cure),single);Deny(Number(events.Insert(ci,cure)),single);
            Deny(Number(events.Insert(ci,new SourceMtMoonVolatileEvent(0,cure.Turn,cure.Side,"unrelated",0,1,"move"))),single);
        }
    }

    [TestMethod, DataRow(215), DataRow(312)]
    public void PartyCureRowsRejectForeignIdentityChangedPokemonAndMalformedStatuses(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var bodies=Bodies(id);bodies[0]=bodies[0] with{Status=WildBattleStatus.Burn};
            var events=Resolve(Frame(bodies,single)).Events;var party=events.OfType<SourceCalledPartyChangedEvent>().Single(e=>e.Reason==Reason(id));
            var status=events.OfType<SourceStatusChangedEvent>().Single();int pi=events.IndexOf(party),si=events.IndexOf(status);
            foreach(var bad in new[]{party with{Side=BattleSide.Opponent},party with{PartyIndex=-1},party with{PartyIndex=6},party with{Reason="move"},
                party with{Reason=id==215?"aromatherapy":"heal-bell"},party with{Before=party.Before with{Status=0}},
                party with{After=party.After with{Status=WildBattleStatus.Poison}},party with{After=party.After with{Pokemon=party.After.Pokemon with{CurrentHp=1}}},
                party with{Turn=party.Turn+1}})Deny(events.SetItem(pi,bad),single);
            foreach(var bad in new[]{status with{Side=new(1)},status with{Before=0},status with{After=WildBattleStatus.Burn},status with{Reason="move"},
                status with{Reason=id==215?"aromatherapy":"heal-bell"},status with{Turn=status.Turn+1}})Deny(events.SetItem(si,bad),single);
            Deny(Number(events.Insert(pi,party)),single);Deny(Number(events.Insert(si,status)),single);
            Deny(events.Remove(party),single);
            // A cure receipt is not an invocation prerequisite: the healthy
            // source command is equally valid. Validate every supplied outcome,
            // without demanding an invented terminal cure/completion receipt.
            var pp=events.OfType<SourcePpCommandEvent>().Single();
            Deny(Number(events.Insert(events.IndexOf(pp)+1,new SourceMtMoonVolatileEvent(0,pp.Turn,pp.Actor,"unrelated",0,1,"move"))),single);
        }
    }

    [TestMethod]
    public void SoundproofReceiptsRequireHealBellActorOwnedPresentTargetsAndOneResultPerMember()
    {
        foreach(bool single in new[]{false,true})
        {
            var bodies=Bodies(215);bodies[0]=bodies[0] with{CalledOverlay=new(){Ability="ABILITY_SOUNDPROOF"}};
            var events=Resolve(Frame(bodies,single)).Events;var block=events.OfType<SourceAbilityPreventedEvent>().Single();int bi=events.IndexOf(block);
            foreach(var bad in new[]{block with{Actor=new(1)},block with{Target=new(1)},block with{Target=new(single?1:3)},
                block with{Move=Kernel.Id("AROMATHERAPY")},block with{Ability="ABILITY_IMMUNITY"},block with{Turn=block.Turn+1}})Deny(events.SetItem(bi,bad),single);
            Deny(Number(events.Insert(bi,block)),single);
            Deny(Number(events.Insert(bi+1,new SourceStatusChangedEvent(0,block.Turn,block.Target,WildBattleStatus.Burn,0,"heal-bell"))),single);
            var aroma=Resolve(Kernel.Frame(Bodies(312),single)).Events;var pp=aroma.OfType<SourcePpCommandEvent>().Single();
            Deny(Number(aroma.Insert(aroma.IndexOf(pp)+1,block with{Move=Move(312),Turn=pp.Turn})),single);
        }
    }

    [TestMethod, DataRow(287), DataRow(215), DataRow(312)]
    public void UnrelatedAndUninterpretedRowsCannotHideBehindAnAuthenticCureCommand(int id)
    {
        foreach(bool single in new[]{false,true})
        {
            var bodies=Bodies(id);bodies[0]=bodies[0] with{Status=WildBattleStatus.Burn};
            var events=Resolve(Frame(bodies,single)).Events;var used=events.OfType<SourceMoveUsedEvent>().Single();
            int tail=Array.FindIndex(events.ToArray(),e=>e is SourceCalledPartyChangedEvent {Reason:"active-sync"});if(tail<0)tail=events.Length;
            SourceBattleEvent[] unrelated=[
                new SourceMtMoonVolatileEvent(0,used.Turn,new(1),"imprison",0,1,"move"),
                new SourceStatusChangedEvent(0,used.Turn,new(1),0,WildBattleStatus.Burn,"move"),
                new SourceStatusChangedEvent(0,used.Turn,new(1),WildBattleStatus.Burn,0,"held-berry"),
                new SourceConfusionChangedEvent(0,used.Turn,new(1),0,3,"move"),
                new SourceCriticalHitEvent(0,used.Turn,new(0),new(1)),
                new SourceRootsChangedEvent(0,used.Turn,new(1),true,"ingrain"),
                new SourceAbilityActivatedEvent(0,used.Turn,new(1),"ABILITY_FORECAST"),
                new SourceStatChangedEvent(0,used.Turn,new(1),new(1),WildBattleStat.Attack,6,7),
                new SourceHeldItemConsumedEvent(0,used.Turn,new(1),"ITEM_LUM_BERRY",100,100,0),
                // Pay Day becomes an uninterpreted singles receipt; its sequence
                // must not hide an unsupported outcome at the normalization seam.
                new SourcePayDayEvent(0,used.Turn,new(1),0,100)];
            foreach(var bad in unrelated)Deny(Number(events.Insert(tail,bad)),single);
            var sync=events.OfType<SourceCalledPartyChangedEvent>().FirstOrDefault(e=>e.Reason=="active-sync");
            if(sync is not null)
            {
                Deny(events.SetItem(events.IndexOf(sync),sync with{Side=BattleSide.Opponent}),single);
                Deny(events.SetItem(events.IndexOf(sync),sync with{After=sync.After with{Pokemon=sync.After.Pokemon with{CurrentHp=1}}}),single);
            }
        }
    }

    [TestMethod, DataRow(287), DataRow(215), DataRow(312)]
    public void RealMoveEndImmunityHasItsOwnAdjacentAbilityOwner(int id)
    {
        foreach(bool single in new[]{false,true})
        foreach(var branch in new[]{("ABILITY_IMMUNITY",WildBattleStatus.Poison),("ABILITY_LIMBER",WildBattleStatus.Paralysis),
            ("ABILITY_INSOMNIA",3u),("ABILITY_VITAL_SPIRIT",3u),("ABILITY_WATER_VEIL",WildBattleStatus.Burn),("ABILITY_MAGMA_ARMOR",WildBattleStatus.Freeze)})
        {
            var bodies=Bodies(id);bodies[0]=bodies[0] with{Status=WildBattleStatus.Burn};
            bodies[1]=bodies[1] with{Status=branch.Item2,CalledOverlay=new(){Ability=branch.Item1}};
            var result=Resolve(Frame(bodies,single));var events=result.Events;
            var ability=events.OfType<SourceAbilityActivatedEvent>().Single();
            var status=events.OfType<SourceStatusChangedEvent>().Single(e=>e.Side.Value==1);
            Assert.AreEqual(branch.Item1,ability.Ability);Assert.AreEqual(ability.Sequence+1,status.Sequence);
            Assert.AreEqual(0u,result.State.Slots[1].Body!.Status);Shared.Check(result,id,single,Owner(result,id));
            Deny(Number(events.Remove(ability)),single);
            Deny(events.SetItem(events.IndexOf(ability),ability with{Ability="ABILITY_SOUNDPROOF"}),single);
            Deny(events.SetItem(events.IndexOf(status),status with{Side=new(0)}),single);
            if(branch.Item1=="ABILITY_WATER_VEIL")
                Deny(Number(events.Insert(events.IndexOf(status),
                    new SourceTowerVolatileEvent(0,status.Turn,status.Side,"nightmare",false,status.Reason))),single);
        }
    }

    [TestMethod, DataRow(287), DataRow(215), DataRow(312)]
    public void ActualMoveEndHeldItemGroupIsAllowedButItsForeignOrMissingOwnerIsNot(int id)
    {
        foreach(bool single in new[]{false,true})foreach(bool herb in new[]{false,true})
        {
            var bodies=Bodies(id);bodies[0]=bodies[0] with{Status=WildBattleStatus.Burn};
            var frame=Frame(bodies,single);
            // Install the controlled held effect after ordinary entry so White
            // Herb is genuinely reached by this action's move-end phase.
            frame=ChargeCopyAnimationKernelFixture.Body(frame,1,b=>herb?b with{HeldItem="ITEM_WHITE_HERB",AttackStage=5}:
                b with{HeldItem="ITEM_LUM_BERRY",Status=WildBattleStatus.Burn,ConfusionTurnsRemaining=3});
            var result=Resolve(frame);var events=result.Events;var item=events.OfType<SourceHeldItemConsumedEvent>().Single();
            Assert.AreEqual(herb?"ITEM_WHITE_HERB":"ITEM_LUM_BERRY",item.Item);Assert.AreEqual(new BattlerSlot(1),item.Side);
            Shared.Check(result,id,single,Owner(result,id));
            Deny(Number(events.Remove(item)),single);Deny(events.SetItem(events.IndexOf(item),item with{Side=new(0)}),single);
        }
    }
}
