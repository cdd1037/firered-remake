using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class CampaignBattleDisplayBodiesTests
{
    private static SourceMechanicsCatalog Mechanics=>TestAssets.Cached(()=>SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static WildBattleCombatant Mon(string species="CASTFORM")=>new(SourcePokemonFactory.Create(Mechanics,
        new(new("pret:SPECIES_"+species),30,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon);
    private static ImmutableArray<SourceBattleSlot> Slots(bool doubles=false)=>Enumerable.Range(0,4).Select(i=>
        !doubles&&i>=2?new SourceBattleSlot(new(i),null,0,null):new(new(i),new((BattleSide)(i&1),i/2),1,Mon())).ToImmutableArray();
    private static SourceCalledOverlayChangedEvent Form(long sequence,int slot,SourceCastformForm form)=>
        new(sequence,1,new(slot),new("pret:MOVE_NONE"),null,new(){CastformForm=form});

    [TestMethod,DataRow(false),DataRow(true)]
    public void SourceSlotShapeAndFaintedBodiesAreRetained(bool doubles)
    {
        var slots=Slots(doubles);var body=slots[1].Body!;
        slots=slots.SetItem(1,slots[1] with{Body=body with{Pokemon=body.Pokemon with{CurrentHp=0}}});
        var display=CampaignBattleDisplayBodies.FromSlots(slots);
        Assert.AreEqual(doubles?4:2,display.Bodies.Length);
        Assert.AreEqual(0,display.Bodies[1].Pokemon.CurrentHp);
        Assert.AreEqual(slots[1].Member,display.Members[1]);
        Assert.ThrowsExactly<InvalidDataException>(()=>CampaignBattleDisplayBodies.FromSlots(slots.SetItem(0,slots[0] with{Body=null})));
        Assert.ThrowsExactly<InvalidDataException>(()=>CampaignBattleDisplayBodies.FromSlots(slots.SetItem(1,slots[1] with{Slot=new(3)})));
    }

    [TestMethod]
    public void SkippedCanonicalEventsAndRepeatedTrainerMirrorSequenceApplyOnce()
    {
        var seed=CampaignBattleDisplayBodies.FromSlots(Slots());
        ImmutableArray<SourceBattleEvent> events=[new SourceStartedEvent(1,1),Form(2,1,SourceCastformForm.Rainy),Form(4,1,SourceCastformForm.Snowy),new SourceTurnReadyEvent(5,1,0)];
        var initial=new CampaignBattleDisplayBatch(seed);
        var first=initial.AdvanceThrough(events,2);
        Assert.AreEqual(2,first.NextEventIndex);
        Assert.AreEqual(SourceCastformForm.Rainy,first.Display.Bodies[1].CalledOverlay!.CastformForm);
        Assert.AreEqual(first,first.AdvanceThrough(events,2));
        Assert.IsNull(initial.Display.Bodies[1].CalledOverlay);
        var final=first.AdvanceThrough(events,long.MaxValue);
        Assert.AreEqual(4,final.NextEventIndex);
        Assert.AreEqual(SourceCastformForm.Snowy,final.Display.Bodies[1].CalledOverlay!.CastformForm);
    }

    [TestMethod]
    public void TransformCopiesEventTimeDonorBeforeLaterWeatherChange()
    {
        var seed=CampaignBattleDisplayBodies.FromSlots(Slots());
        var donor=seed.Bodies[1].Pokemon;var actor=seed.Bodies[0].Pokemon;
        ImmutableArray<SourceBattleEvent> events=[Form(1,1,SourceCastformForm.Sunny),
            new SourceTransformedEvent(2,1,new(0),new(1),actor.Species,donor.Species,donor.Personality,donor.OriginalTrainerId,donor),
            Form(3,1,SourceCastformForm.Rainy)];
        var batch=new CampaignBattleDisplayBatch(seed).AdvanceThrough(events,long.MaxValue);
        Assert.AreEqual(SourceCastformForm.Sunny,batch.Display.Bodies[0].CalledOverlay!.CastformForm);
        Assert.AreEqual(SourceCastformForm.Rainy,batch.Display.Bodies[1].CalledOverlay!.CastformForm);
        Assert.IsNotNull(batch.Display.Bodies[0].Transform);
    }

    [TestMethod]
    public void ReplacementOwnsItsMembershipBeforeEntryFormReceipt()
    {
        var seed=CampaignBattleDisplayBodies.FromSlots(Slots()).Apply(Form(1,0,SourceCastformForm.Sunny));
        var replacement=Mon();
        ImmutableArray<SourceBattleEvent> events=[new SourceCalledPartySwitchedEvent(2,1,new(0),0,2,WildBattleCalledSwitchKind.Voluntary)
            {Replacement=new(replacement,new())},Form(3,0,SourceCastformForm.Snowy)];
        var switched=new CampaignBattleDisplayBatch(seed).AdvanceThrough(events,2);
        Assert.IsNull(switched.Display.Bodies[0].CalledOverlay);
        Assert.AreEqual(new PartyMemberKey(BattleSide.Player,2),switched.Display.Members[0]);
        Assert.AreEqual(SourceCastformForm.Snowy,switched.AdvanceThrough(events,3).Display.Bodies[0].CalledOverlay!.CastformForm);
    }

    [TestMethod]
    public void HpProjectionIsImmutableAndUsesCommittedReceipt()
    {
        var seed=CampaignBattleDisplayBodies.FromSlots(Slots());int hp=seed.Bodies[1].Pokemon.CurrentHp;
        var after=seed.Apply(new SourceHpChangedEvent(1,1,new(0),new(1),new("pret:MOVE_POUND"),hp,hp-3,3,false,100));
        Assert.AreEqual(hp,seed.Bodies[1].Pokemon.CurrentHp);Assert.AreEqual(hp-3,after.Bodies[1].Pokemon.CurrentHp);
    }

    [TestMethod]
    public void RealWildEntryCapturesPreAbilityBodiesWithoutChangingReceiptsOrRng()
    {
        var player=Mon();var enemy=Mon("KYOGRE");
        var result=SourceWildBattlePolicy.Start(Mechanics,[player],0,enemy,new(37));
        Assert.IsTrue(result.Succeeded,result.Fault);Assert.IsFalse(result.EntrySlots.IsDefaultOrEmpty);
        Assert.IsNull(result.EntrySlots[0].Body!.CalledOverlay);
        var projected=new CampaignBattleDisplayBatch(CampaignBattleDisplayBodies.FromSlots(result.EntrySlots)).AdvanceThrough(result.Events,long.MaxValue);
        Assert.AreEqual(result.State!.Get(new(0)).Body!.CalledOverlay,projected.Display.Bodies[0].CalledOverlay);
        string before=JsonSerializer.Serialize(result);
        var repeated=SourceWildBattlePolicy.Start(Mechanics,[player],0,enemy,new(37));
        Assert.AreEqual(before,JsonSerializer.Serialize(repeated));
        Assert.IsFalse(before.Contains("EntrySlots",StringComparison.Ordinal));
        Assert.AreEqual(result.State.Rng,repeated.State!.Rng);
    }

    [TestMethod]
    public void TransientBatchIsExcludedFromCampaignBattleSerialization()
    {
        var slots=Slots();var frame=new SourceBattleFrame{Slots=slots};
        var battle=new CampaignWildBattle("test",null!,frame,[],0,slots[0].Body!.Pokemon,slots[1].Body!.Pokemon,CampaignBattlePhase.Events,[],0,[])
            {DisplayBatch=new(CampaignBattleDisplayBodies.FromSlots(slots))};
        Assert.IsFalse(JsonSerializer.Serialize(battle).Contains("DisplayBatch",StringComparison.Ordinal));
    }
}
