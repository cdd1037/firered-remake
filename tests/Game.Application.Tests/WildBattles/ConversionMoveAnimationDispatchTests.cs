using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

[TestClass,TestCategory("Game")]
public sealed class ConversionMoveAnimationDispatchTests
{
    private static SourceMechanicsCatalog Mechanics=>TestAssets.Cached(()=>SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static MoveId Move(int id)=>new("pret:MOVE_"+(id==160?"CONVERSION":"CONVERSION_2"));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> e,int i)=>new("conversion",new("test"),"test",new(),CampaignDoubleBattleStage.Combat,e,i,[],[],0,0,[],[],[],[],0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> e,int i)=>new("conversion",null!,new(),e,i,null!,null!,CampaignBattlePhase.Events,[],0,[]);
    private static WildBattleCombatant Mon(string species,params string[] names)
    {
        var p=SourcePokemonFactory.Create(Mechanics,new(new("pret:SPECIES_"+species),50,42,IvPolicy.Fixed(31),PersonalityPolicy.Fixed(0)),new(0)).Pokemon;
        var moves=names.Select(name=>{var m=new MoveId("pret:MOVE_"+name);Assert.IsTrue(Mechanics.Catalog.Species[p.Species].LevelUpMoves.Any(x=>x.Move==m&&x.Level<=50));int pp=Mechanics.Catalog.Moves[m].Pp;return new PokemonMoveSlot(m,pp,pp);}).ToImmutableArray();
        return new(p with{Moves=moves});
    }
    private static WildBattleCombatant[] Bodies(int id,int actor,bool different=true)
    {var b=Enumerable.Range(0,4).Select(_=>Mon("SNORLAX","TACKLE")).ToArray();b[actor]=Mon("PORYGON",different?[id==160?"CONVERSION":"CONVERSION_2","PSYBEAM"]:[id==160?"CONVERSION":"CONVERSION_2"]);return b;}
    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, bool single = false)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame {
            Layout = single ? SourceBattleLayout.Singles : SourceBattleLayout.PresetDoubles,
            KoReplacementTiming = single ? KoReplacementTiming.SourceImmediate : KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version,
            BattleKind = single ? WildBattleCalledBattleKind.Wild : WildBattleCalledBattleKind.Trainer,
            SourceAbsentMask = (byte)(single ? 12 : 0), TurnStartAbsentMask = (byte)(single ? 12 : 0), Rng = new(31),
            Parties = [new(BattleSide.Player, single ? [Member(bodies[0])] : [Member(bodies[0]), Member(bodies[2])]),
                new(BattleSide.Opponent, single ? [Member(bodies[1])] : [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => single && i >= 2 ? new SourceBattleSlot(new(i), null, 0, null)
                : new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State with { Rng = new(0) };
    }

    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty); return result;
    }


    private static SourceKernelResult Successful(int id,int actor,bool single=false)
    {
        var frame=Frame(Bodies(id,actor),single);
        if(id==176){var prior=Resolve(frame,actor^1,actor);Assert.IsTrue(prior.Events.OfType<SourceHpChangedEvent>().Any(e=>e.Target.Value==actor&&e.HpAfter<e.HpBefore));frame=prior.State;}
        return Resolve(frame,actor,actor);
    }
    private static void AssertProjection(SourceKernelResult result,int id,int actor,bool expected,bool single)
    {
        var e=result.Events;string events=JsonSerializer.Serialize(e),state=JsonSerializer.Serialize(result.State),rng=JsonSerializer.Serialize(result.Trace);var used=e.OfType<SourceMoveUsedEvent>().Single(x=>x.Move==Move(id));int count=0;
        for(int i=0;i<e.Length;i++)
        {
            CampaignMoveAnimationDecision At(SourceBattleScene scene)=>single?AnimationDispatchFixture.AtAttackText(Single(e,i),true,scene):AnimationDispatchFixture.AtAttackText(Double(e,i),scene);
            var decision=At(SourceBattleScene.On);Assert.AreEqual(decision,At(SourceBattleScene.On));Assert.IsNull(At(SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single?AnimationDispatchFixture.AtResult(Single(e,i),true,SourceBattleScene.On).Dispatch:AnimationDispatchFixture.AtResult(Double(e,i),SourceBattleScene.On).Dispatch);
            Assert.IsNull(single?AnimationDispatchFixture.StatAtResult(Single(e,i),true):AnimationDispatchFixture.StatAtResult(Double(e,i)));
            if(e[i]==used&&expected)
            {var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);var overlay=e.OfType<SourceCalledOverlayChangedEvent>().Single(x=>x.Move==Move(id));var pp=e.OfType<SourcePpCommandEvent>().Single(x=>x.CurrentMove==Move(id));Assert.AreEqual(pp.Sequence+1,overlay.Sequence);Assert.AreEqual(actor,pp.PpOwner.Value);Assert.AreEqual(actor,pp.Target.Value);Assert.AreEqual(overlay.Sequence,cue.ResultSequence);Assert.AreEqual(id,cue.SourceMoveId);Assert.AreEqual(actor,cue.Actor.Value);Assert.AreEqual(actor,cue.Target.Value);count++;}
            else Assert.IsNull(decision.Dispatch);
        }
        Assert.AreEqual(expected?1:0,count);Assert.AreEqual(events,JsonSerializer.Serialize(e));Assert.AreEqual(state,JsonSerializer.Serialize(result.State));Assert.AreEqual(rng,JsonSerializer.Serialize(result.Trace));
    }
    [TestMethod][DataRow(160)][DataRow(176)]
    public void NaturalPorygonTypeChangeAdmitsOneRootAcrossAllFourSlotsAndBothSinglesSides(int id)
    {foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++){var result=Successful(id,actor,single);Assert.IsFalse(result.Events.OfType<SourceMoveFailedEvent>().Any());AssertProjection(result,id,actor,true,single);}}
    [TestMethod]
    public void NoDifferentMoveTypeAndNoLastLandedMoveFailWithoutAnimation()
    {foreach(bool single in new[]{false,true})foreach(int id in new[]{160,176}){var result=Resolve(Frame(Bodies(id,0,false),single),0,0);Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(x=>x.Move==Move(id)));Assert.IsFalse(result.Events.OfType<SourceCalledOverlayChangedEvent>().Any());AssertProjection(result,id,0,false,single);}}
    [TestMethod]
    public void RepeatedConversionConsumesCommittedNewTypeWithoutRecomputingSelection()
    {
        var first=Successful(160,0);Assert.AreEqual("TYPE_PSYCHIC",first.Events.OfType<SourceCalledOverlayChangedEvent>().Single().After!.Type1);
        // Conversion itself has Normal type, so it remains a valid new choice.
        var second=Resolve(first.State,0,0);Assert.AreEqual("TYPE_NORMAL",second.Events.OfType<SourceCalledOverlayChangedEvent>().Single().After!.Type1);AssertProjection(second,160,0,true,false);
    }
    [TestMethod][DataRow(160)][DataRow(176)]
    public void MalformedTypeReceiptOwnershipOrderingAndUnrelatedFieldChangesFailClosed(int id)
    {
        var result=Successful(id,0);var e=result.Events;int at=Array.FindIndex(e.ToArray(),x=>x is SourceCalledOverlayChangedEvent y&&y.Move==Move(id));var original=(SourceCalledOverlayChangedEvent)e[at];int used=Array.FindIndex(e.ToArray(),x=>x is SourceMoveUsedEvent y&&y.Move==Move(id));
        SourceCalledOverlayChangedEvent[] bad=[original with{Side=new(1)},original with{Source=new(1)},original with{Move=new("pret:MOVE_TACKLE")},original with{Sequence=original.Sequence+1},original with{Turn=original.Turn+1},original with{After=null},original with{Before=original.After},original with{After=original.After! with{Type2="TYPE_MYSTERY"}},original with{After=original.After! with{RulesVersion="invalid"}},original with{After=original.After! with{Ability="ABILITY_FORECAST"}}];
        foreach(var row in bad)Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(e.SetItem(at,row),used),SourceBattleScene.On).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(e.RemoveAt(at),used),SourceBattleScene.On).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(e.Insert(at+1,original with{Sequence=original.Sequence+1}),used),SourceBattleScene.On).Dispatch);
        int pp=Array.FindIndex(e.ToArray(),x=>x is SourcePpCommandEvent p&&p.CurrentMove==Move(id));var p=(SourcePpCommandEvent)e[pp];
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(e.SetItem(pp,p with{PpOwner=new(1)}),used),SourceBattleScene.On).Dispatch);
        Assert.IsNull(AnimationDispatchFixture.AtAttackText(Double(e.SetItem(pp,original).SetItem(at,p),used),SourceBattleScene.On).Dispatch);
    }
    [TestMethod]
    public void MissingRendererMetadataDoesNotAdmitOtherTypeChangeRoots()
    {
        var result=Successful(160,0);int used=Array.FindIndex(result.Events.ToArray(),x=>x is SourceMoveUsedEvent);var b=Double(result.Events,used);
        Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(b,SourceBattleScene.On,Mechanics.Catalog.Moves,(_,_)=>false).Dispatch);
    }
}
