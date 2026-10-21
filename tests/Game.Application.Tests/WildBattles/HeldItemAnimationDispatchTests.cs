using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class HeldItemAnimationDispatchTests
{
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events,int index=0)=>new("held",new("test"),"test",new(),CampaignDoubleBattleStage.Combat,events,index,[],[],0,0,[],[],[],[],0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events,int index=0)=>new("held",null!,new(),events,index,null!,null!,CampaignBattlePhase.Events,[],0,[]);
    private static CampaignHeldItemAnimationDispatch? At(CampaignDoubleBattle battle,MoveId?[]? entry=null)=>CampaignHeldItemAnimationPresentation.AtResult(battle,SourceBattleScene.On,entry??new MoveId?[4]);
    private static SourceHeldItemConsumedEvent Consumed(int slot,int seq,string item,int before=10,int after=10)=>new(seq,1,new(slot),item,before,after,after-before);
    private static SourceHeldItemHealEvent Heal(int slot,int seq=3)=>new(seq,1,new(slot),new(slot^1),new("pret:MOVE_NONE"),"ITEM_LEFTOVERS",0,10,12,2);
    private static ImmutableArray<SourceBattleEvent> Lum(int slot)=>[
        new SourceConfusionChangedEvent(1,1,new(slot),3,0,"held-berry"),new SourceStatusChangedEvent(2,1,new(slot),WildBattleStatus.Burn,0,"held-berry"),Consumed(slot,3,"ITEM_LUM_BERRY")];
    [TestMethod]
    [DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void CureGroupStartsBeforeSilentReceiptsOnceAndUsesConsumedIdentity(int slot)
    {
        var events=Lum(slot);var battle=Double(events);var result=At(battle);
        Assert.IsNotNull(result);Assert.AreEqual(1L,result.Sequence);Assert.AreEqual(3L,result.ReceiptSequence);Assert.AreEqual(slot,result.Affected.Value);Assert.AreEqual("held",result.BattleId);
        for(int i=1;i<events.Length;i++)Assert.IsNull(At(battle with{EventIndex=i}));
        Assert.AreEqual(events,battle.Events);
    }
    [TestMethod]
    [DataRow(0)][DataRow(1)]
    public void CanonicalSinglesExportUsesTheSameTypedGroup(int slot)
    {
        var battle=Single(Lum(slot));var entry=new MoveId?[2];
        var result=CampaignHeldItemAnimationPresentation.AtResult(battle,true,SourceBattleScene.On,entry);
        Assert.IsNotNull(result);Assert.AreEqual(slot,result.Affected.Value);Assert.AreEqual(1L,result.Sequence);Assert.AreEqual(3L,result.ReceiptSequence);
        Assert.IsNull(CampaignHeldItemAnimationPresentation.AtResult(battle with{EventIndex=2},true,SourceBattleScene.On,entry));
        Assert.IsNull(CampaignHeldItemAnimationPresentation.AtResult(battle,false,SourceBattleScene.On,entry));
        Assert.IsNull(CampaignHeldItemAnimationPresentation.AtResult(battle,true,SourceBattleScene.Off,entry));
        Assert.IsNull(CampaignHeldItemAnimationPresentation.AtResult(battle,true,SourceBattleScene.On,entry,true));
    }
    private static ImmutableArray<SourceBattleEvent> Group(string kind,int slot)=>kind switch
    {
        "white-herb"=>[new SourceStatChangedEvent(1,1,new(slot),new(slot),WildBattleStat.Attack,3,6),new SourceStatChangedEvent(2,1,new(slot),new(slot),WildBattleStat.Defense,5,6),Consumed(slot,3,"ITEM_WHITE_HERB") with{StatChanges=[new(WildBattleStat.Attack,3,6),new(WildBattleStat.Defense,5,6)]}],
        "focus"=>[new SourceFocusEnergyEvent(1,1,new(slot)),Consumed(slot,2,"ITEM_LANSAT_BERRY") with{FocusEnergyRaised=true}],
        "pp"=>[Consumed(slot,1,"ITEM_LEPPA_BERRY") with{PpRecovery=new(new("pret:MOVE_TACKLE"),0,0,10)}],
        "hp"=>[Consumed(slot,1,"ITEM_ORAN_BERRY",10,20)],
        "flavor"=>[new SourceConfusionChangedEvent(1,1,new(slot),0,3,"flavor-berry"),Consumed(slot,2,"ITEM_FIGY_BERRY",10,20) with{Flavor="spicy"}],
        "mental"=>[new SourceInfatuationChangedEvent(1,1,new(slot),new(slot^1),null,"mental-herb"),Consumed(slot,2,"ITEM_MENTAL_HERB")],
        _=>throw new ArgumentOutOfRangeException(nameof(kind))
    };
    [TestMethod]
    [DataRow("white-herb")][DataRow("focus")][DataRow("pp")][DataRow("hp")][DataRow("flavor")][DataRow("mental")]
    public void TypedActivationMetadataGroupsAllFourPositionsWithoutItemSpecificAnimationVariants(string kind)
    {
        for(int slot=0;slot<4;slot++)
        {
            var events=Group(kind,slot);var battle=Double(events);var result=At(battle);
            Assert.IsNotNull(result,kind);Assert.AreEqual(slot,result.Affected.Value);Assert.AreEqual(events[0].Sequence,result.Sequence);Assert.AreEqual(events[^1].Sequence,result.ReceiptSequence);
            for(int i=1;i<events.Length;i++)Assert.IsNull(At(battle with{EventIndex=i}));
        }
    }
    [TestMethod]
    public void NonconsumedHealTargetsItsActorAndExcludesSurvivalTheftKnockoffAndEmptyEffects()
    {
        for(int slot=0;slot<4;slot++)
        {
            var result=At(Double([Heal(slot)]));Assert.IsNotNull(result);Assert.AreEqual(slot,result.Affected.Value);Assert.AreEqual(result.Sequence,result.ReceiptSequence);
            Assert.IsNull(At(Double([Heal(slot) with{HpRestored=0,HpAfter=10}])));
        }
        Assert.IsNull(At(Double([new SourceHeldItemSurvivalEvent(1,1,new(0),new("pret:MOVE_TACKLE"),"ITEM_FOCUS_BAND")])));
        Assert.IsNull(At(Double([new SourceHeldItemKnockedOffEvent(1,1,new(0),new(1),new("pret:MOVE_KNOCK_OFF"),"ITEM_ORAN_BERRY")])));
        Assert.IsNull(At(Double([new SourceHeldItemStolenEvent(1,1,new(0),new(1),new("pret:MOVE_THIEF"),"ITEM_ORAN_BERRY")])));
        Assert.IsNull(At(Double([Consumed(0,1,"ITEM_NONE",10,20)])));
        Assert.IsNull(At(Double([Consumed(0,1,"ITEM_ORAN_BERRY")])));
    }
    [TestMethod]
    public void MalformedOrInterruptedGroupsCannotBorrowAnotherEffectReceipt()
    {
        var good=Group("white-herb",0);var item=(SourceHeldItemConsumedEvent)good[^1];
        foreach(var bad in new[]{
            good.SetItem(2,item with{StatChanges=[new(WildBattleStat.Attack,2,6),new(WildBattleStat.Defense,5,6)]}),
            good.SetItem(0,new SourceStatChangedEvent(1,1,new(1),new(0),WildBattleStat.Attack,3,6)),
            good.SetItem(1,new SourceTurnReadyEvent(2,1,0)),
            good.SetItem(1,new SourceStatChangedEvent(1,1,new(0),new(0),WildBattleStat.Defense,5,6)),
            good.SetItem(1,new SourceStatChangedEvent(2,2,new(0),new(0),WildBattleStat.Defense,5,6)),
            good.RemoveAt(2)})
            for(int i=0;i<bad.Length;i++)Assert.IsNull(At(Double(bad,i)));
        Assert.IsNull(At(Double(Group("focus",0).RemoveAt(0))));
        Assert.IsNull(At(Double([Consumed(0,1,"ITEM_LEPPA_BERRY") with{PpRecovery=new(new("pret:MOVE_TACKLE"),0,10,10)}])));
    }
    private static SourcePpCommandEvent Pp(int slot,string move,int seq=1,int turn=1)=>new(seq,turn,new(slot),new(slot^1),0,new("pret:MOVE_METRONOME"),new("pret:MOVE_"+move),10,9,1,0);
    private static SourceMtMoonVolatileEvent Charge(int slot,int seq,int after,string reason="move")=>new(seq,1,new(slot),"called-charge",after==0?1:0,after,reason);
    [TestMethod]
    [DataRow("FLY")][DataRow("BOUNCE")][DataRow("DIG")][DataRow("DIVE")]
    public void SameBatchChargeUsesCurrentPpMoveAndReleaseOrSleepClearAtExactReceipt(string move)
    {
        for(int slot=0;slot<4;slot++)
        {
            var events=ImmutableArray.Create<SourceBattleEvent>(Pp(slot,move),Charge(slot,2,1),Heal(slot));
            Assert.IsNull(At(Double(events,2)));
            Assert.IsNotNull(At(Double(events.Insert(2,Charge(slot,3,0,"released")).SetItem(3,Heal(slot,4)),3)));
            var sleep=events.Insert(2,new SourceStatusChangedEvent(3,1,new(slot),0,2,"move")).SetItem(3,Heal(slot,4));
            Assert.IsNull(At(Double(sleep,3)),"Status alone does not preempt source end-turn charge cleanup.");
            Assert.IsNotNull(At(Double(sleep.Insert(3,Charge(slot,4,0,"sleep")).SetItem(4,Heal(slot,5)),4)));
        }
    }
    [TestMethod]
    public void EntryChargeSnapshotCarriesAcrossBatchesAndSwitchOrReleaseClearsWithoutReadingFinalFrame()
    {
        MoveId?[] entry=[new("pret:MOVE_DIG"),null,new("pret:MOVE_FLY"),null];var saved=entry.ToArray();
        Assert.IsNull(At(Double([Heal(0)]),entry));Assert.IsNotNull(At(Double([Heal(1)]),entry));Assert.IsNull(At(Double([Heal(2)]),entry));
        Assert.IsNotNull(At(Double([Charge(0,1,0,"released"),Heal(0)],1),entry));
        Assert.IsNotNull(At(Double([Charge(2,1,0,"paralysis"),Heal(2)],1),entry));
        Assert.IsNotNull(At(Double([new SourceCalledPartySwitchedEvent(1,1,new(2),0,1,WildBattleCalledSwitchKind.Forced),Heal(2)],1),entry));
        CollectionAssert.AreEqual(saved,entry);
        foreach(string move in new[]{"RAZOR_WIND","SOLAR_BEAM","SKULL_BASH","SKY_ATTACK"})
            Assert.IsNotNull(At(Double([Heal(0)]),[new("pret:MOVE_"+move),null,null,null]));
        var single=Single([Heal(0)]);Assert.IsNull(CampaignHeldItemAnimationPresentation.AtResult(single,true,SourceBattleScene.On,[new("pret:MOVE_DIG"),null]));
        var released=Single([Charge(0,1,0,"sleep"),Heal(0)],1);Assert.IsNotNull(CampaignHeldItemAnimationPresentation.AtResult(released,true,SourceBattleScene.On,[new("pret:MOVE_DIG"),null]));
    }
    [TestMethod]
    public void LaterChargeStateCannotLeakBackwardsAndMissingCurrentChargeReceiptFailsClosed()
    {
        Assert.IsNull(At(Double([Heal(0,1),Charge(0,2,0,"released")]),[new("pret:MOVE_DIG"),null,null,null]));
        Assert.IsNotNull(At(Double([Heal(0,1),Pp(0,"FLY",2),Charge(0,3,1)])));
        Assert.IsNull(At(Double([Charge(0,1,1),Heal(0)],1)));
        Assert.IsNull(At(Double([Pp(0,"FLY",1,0),Charge(0,2,1),Heal(0)],2)));
        Assert.IsNotNull(At(Double([Charge(0,1,1),Charge(0,2,0,"released"),Heal(0)],2)));
    }
    [TestMethod]
    public void ExplicitSceneSubstitutePhaseAndEntryShapeGatesApplyWithoutMutation()
    {
        var battle=Double([Heal(0)]);var entry=new MoveId?[4];
        Assert.IsNull(CampaignHeldItemAnimationPresentation.AtResult(battle,SourceBattleScene.Off,entry));
        Assert.IsNull(CampaignHeldItemAnimationPresentation.AtResult(battle,SourceBattleScene.On,entry,true));
        Assert.IsNull(At(battle with{Stage=CampaignDoubleBattleStage.Finished}));
        Assert.Throws<ArgumentException>(()=>CampaignHeldItemAnimationPresentation.AtResult(battle,SourceBattleScene.On,new MoveId?[2]));
        Assert.Throws<ArgumentNullException>(()=>CampaignHeldItemAnimationPresentation.AtResult(battle,SourceBattleScene.On,null!));
        Assert.Throws<ArgumentException>(()=>CampaignHeldItemAnimationPresentation.AtResult(Single([Heal(0)]),true,SourceBattleScene.On,new MoveId?[4]));
    }
    // Same natural-Poochyena/real item-phase fixtures as BerryItemTests. These
    // receipts are emitted by the public turn lifecycle, never fabricated here.
    private static WildBattleCombatant KernelMon(SourceMechanicsCatalog mechanics,string species="POOCHYENA",int level=30,string move="HOWL")
    {
        var pokemon=SourcePokemonFactory.Create(mechanics,new(new("pret:SPECIES_"+species),level,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
        MoveId id=new("pret:MOVE_"+move);
        Assert.IsTrue(mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m=>m.Move==id&&m.Level<=level));
        int pp=mechanics.Catalog.Moves[id].Pp;
        return new(pokemon with{Moves=[new(id,pp,pp)]});
    }
    private static SourceBattleFrame KernelFrame(PokemonCatalog catalog,WildBattleCombatant[] bodies)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body)=>new(body.Pokemon,body.HeldItem,WildBattleTransformationRules.PersistentStatus(body));
        SourceBattleFrame frame=new(){Layout=SourceBattleLayout.PresetDoubles,KoReplacementTiming=KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion=SourceBattleKernel.Version,BattleKind=WildBattleCalledBattleKind.Trainer,Rng=new(31),
            Parties=[new(BattleSide.Player,[Member(bodies[0]),Member(bodies[2])]),new(BattleSide.Opponent,[Member(bodies[1]),Member(bodies[3])])],
            Slots=Enumerable.Range(0,4).Select(i=>new SourceBattleSlot(new(i),new((BattleSide)(i&1),i/2),1,bodies[i])).ToImmutableArray()};
        var entry=SourceFourEntryAbilities.InitializeEntry(catalog,frame,frame.Revision);
        Assert.IsTrue(entry.Succeeded,entry.Fault);Assert.IsFalse(entry.Events.OfType<SourceHeldItemConsumedEvent>().Any());
        return entry.State;
    }
    private static ImmutableArray<SourceActionSelection> KernelChoices(PokemonCatalog catalog,SourceBattleFrame frame)=>frame.Slots
        .Where(c=>frame.CanAct(c.Slot)).Select(c=>new SourceActionSelection(c.Occupant!.Value,SourceActionKind.Move,0,
            c.Registers.Turn?.Lock?.Target??(catalog.Moves[c.Body!.Pokemon.Moves[0].Move].Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED"?c.Slot:c.Slot.Opposite))).ToImmutableArray();
    private static MoveId?[] EntryCharges(SourceBattleFrame frame)=>frame.Slots.Select(c=>
        c.Registers.Turn?.Lock is {Kind:WildBattleCalledLockKind.Charge} charge?(MoveId?)charge.Move:null).ToArray();
    private static List<(SourceBattleFrame Before,SourceFourActionResult Result)> KernelTurn(PokemonCatalog catalog,SourceBattleFrame frame)
    {
        var result=SourceFourActionTurns.BeginActions(catalog,frame,frame.Revision,KernelChoices(catalog,frame));
        List<(SourceBattleFrame Before,SourceFourActionResult Result)> batches=[];
        for(int i=0;i<16;i++)
        {
            Assert.IsTrue(result.Succeeded,result.Fault);Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            batches.Add((frame,result));
            if(result.CompletedCleanTurn)return batches;
            Assert.IsNotNull(result.Continuation);frame=result.State;
            result=SourceFourActionTurns.Advance(catalog,frame,result.Continuation.Value);
        }
        Assert.Fail("Public turn did not complete within its action/end-turn boundaries.");return batches;
    }
    [TestMethod]
    public void PublicKernelLumCureEmitsEarliestConfusionThenStatusAndConsumedAtAllFourPositions()
    {
        var mechanics=CurrentCampaignFixture.Catalog.PokemonContent!.Pokemon.Mechanics;var catalog=mechanics.Catalog;
        for(int slot=0;slot<4;slot++)
        {
            var bodies=Enumerable.Range(0,4).Select(_=>KernelMon(mechanics)).ToArray();
            bodies[slot]=bodies[slot] with{HeldItem="ITEM_LUM_BERRY",Status=WildBattleStatus.Burn,ConfusionTurnsRemaining=3};
            // The other natural Howl user moves first, so cure occurs before the holder's canceller.
            bodies[(slot+1)%4]=KernelMon(mechanics,level:70);
            var turn=KernelTurn(catalog,KernelFrame(catalog,bodies));
            var batch=turn.Single(b=>b.Result.Events.OfType<SourceHeldItemConsumedEvent>().Any());
            var events=batch.Result.Events;var consumed=events.OfType<SourceHeldItemConsumedEvent>().Single();
            int end=events.IndexOf(consumed);Assert.IsTrue(end>=2);
            var confusion=events[end-2] as SourceConfusionChangedEvent;var status=events[end-1] as SourceStatusChangedEvent;
            Assert.IsNotNull(confusion);Assert.IsNotNull(status);Assert.AreEqual(slot,consumed.Side.Value);
            Assert.AreEqual("held-berry",confusion.Reason);Assert.AreEqual(3,confusion.Before);Assert.AreEqual(0,confusion.After);
            Assert.AreEqual(WildBattleStatus.Burn,status.Before);Assert.AreEqual(0u,status.After);
            Assert.IsTrue(batch.Result.State.Slots[slot].Body is {HeldItem:"ITEM_NONE",Status:0,ConfusionTurnsRemaining:0});
            var battle=Double(events) with{Frame=batch.Result.State};var entry=EntryCharges(batch.Before);
            var matches=Enumerable.Range(0,events.Length).Select(i=>At(battle with{EventIndex=i},entry)).Where(x=>x is not null).ToArray();
            Assert.AreEqual(1,matches.Length);var match=matches[0]!;
            Assert.AreEqual(confusion.Sequence,match.Sequence);Assert.AreEqual(consumed.Sequence,match.ReceiptSequence);Assert.AreEqual(slot,match.Affected.Value);
        }
    }
    [TestMethod]
    public void PublicKernelDigTurnsSuppressChargedLeftoversAndRestoreAnimationAfterRelease()
    {
        var mechanics=CurrentCampaignFixture.Catalog.PokemonContent!.Pokemon.Mechanics;var catalog=mechanics.Catalog;
        for(int slot=0;slot<4;slot++)
        {
            var bodies=Enumerable.Range(0,4).Select(_=>KernelMon(mechanics,level:70)).ToArray();
            var holder=KernelMon(mechanics,"DIGLETT",30,"DIG");
            bodies[slot]=holder with{HeldItem="ITEM_LEFTOVERS",Pokemon=holder.Pokemon with{CurrentHp=holder.Pokemon.Stats.MaxHp/2}};
            var frame=KernelFrame(catalog,bodies);
            for(int turn=0;turn<2;turn++)
            {
                var before=frame;var batches=KernelTurn(catalog,frame);frame=batches[^1].Result.State;
                var events=batches.SelectMany(b=>b.Result.Events).ToImmutableArray();
                var change=events.OfType<SourceMtMoonVolatileEvent>().Single(e=>e.Volatile=="called-charge");
                Assert.AreEqual(slot,change.Side.Value);Assert.AreEqual(turn==0?1:0,change.After);Assert.AreEqual(turn==0?"move":"released",change.Reason);
                var heal=events.OfType<SourceHeldItemHealEvent>().Single();Assert.AreEqual(slot,heal.Actor.Value);Assert.IsTrue(heal.HpRestored>0);
                Assert.IsTrue(change.Sequence<heal.Sequence);
                if(turn==0)
                {
                    var pp=events.OfType<SourcePpCommandEvent>().Single(e=>e.Actor.Value==slot);
                    Assert.AreEqual(new MoveId("pret:MOVE_DIG"),pp.CurrentMove);Assert.IsTrue(pp.Sequence<change.Sequence);
                    Assert.IsNotNull(frame.Slots[slot].Registers.Turn?.Lock);
                }
                else Assert.IsNull(frame.Slots[slot].Registers.Turn?.Lock);
                // Both whole-turn replay and the real command-sized event batch must agree.
                var whole=At(Double(events,events.IndexOf(heal)) with{Frame=frame},EntryCharges(before));
                var batch=batches.Single(b=>b.Result.Events.Contains(heal));
                var actual=At(Double(batch.Result.Events,batch.Result.Events.IndexOf(heal)) with{Frame=batch.Result.State},EntryCharges(batch.Before));
                if(turn==0){Assert.IsNull(whole);Assert.IsNull(actual);}
                else
                {
                    Assert.IsNotNull(whole);Assert.IsNotNull(actual);Assert.AreEqual(whole,actual);
                    Assert.AreEqual(heal.Sequence,actual.Sequence);Assert.AreEqual(heal.Sequence,actual.ReceiptSequence);Assert.AreEqual(slot,actual.Affected.Value);
                }
            }
        }
    }
}
