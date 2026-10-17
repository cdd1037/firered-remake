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

[TestClass, TestCategory("Game")]
public sealed class HealingItemMoveAnimationDispatchTests
{
    private static MoveId Move(int id)=>new("pret:MOVE_"+(id==135?"SOFT_BOILED":"MILK_DRINK"));
    private static SourceMechanicsCatalog Mechanics => TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("future-sight", new("test"), "test", new(), CampaignDoubleBattleStage.Combat, events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("future-sight", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };

    private static WildBattleCombatant NaturalMon(string species, int level, string name)
    {
        var move = new MoveId("pret:MOVE_" + name);
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level));
        int pp = Mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }

    private static WildBattleCombatant[] Bodies(int id,int actor,int hpMode)
    {
        var bodies=Enumerable.Range(0,4).Select(_=>NaturalMon("SNORLAX",100,"DEFENSE_CURL") with{SpeedStage=0}).ToArray();
        var mon=NaturalMon(id==135?"CHANSEY":"MILTANK",id==135?13:19,id==135?"SOFT_BOILED":"MILK_DRINK") with{SpeedStage=12};
        int max=mon.Pokemon.Stats.MaxHp;int hp=hpMode==0?1:hpMode==1?max-1:max;
        bodies[actor]=mon with{Pokemon=mon.Pokemon with{CurrentHp=hp}};return bodies;
    }

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

    private static SourceFourActionResult Begin(SourceBattleFrame frame, int actor, int target)
    {
        var choices = frame.Slots.Take(frame.BattlerCount).Select(c => new SourceActionSelection(c.Occupant!.Value,
            SourceActionKind.Move, 0, new(c.Slot.Value == actor ? target : c.Slot.Value))).ToImmutableArray();
        return SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
    }

    private static SourceFourActionResult Next(SourceFourActionResult result)
    {
        Assert.IsNotNull(result.Continuation, "Expected a retained public turn continuation.");
        var next = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        Assert.IsTrue(next.Succeeded, next.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(next.State)); return next;
    }

    private static void AssertProjection(ImmutableArray<SourceBattleEvent> events,SourceBattleFrame state,ImmutableArray<RandomTrace> trace,int id,int actor,bool expected,bool single=false)
    {
        string before=JsonSerializer.Serialize(events),saved=JsonSerializer.Serialize(state),rng=JsonSerializer.Serialize(trace);var move=Move(id);
        var used=events.OfType<SourceMoveUsedEvent>().Single(e=>e.Move==move);var attack=events.OfType<SourceAttackStringEvent>().Single(e=>e.CurrentMove==move);var pp=events.OfType<SourcePpCommandEvent>().Single(e=>e.CurrentMove==move);
        Assert.AreEqual(actor,used.Actor.Value);Assert.AreEqual(actor,used.Target.Value);Assert.AreEqual(actor,pp.Target.Value);Assert.AreEqual(actor,pp.PpOwner.Value);Assert.AreEqual(1,pp.CommandCost);Assert.AreEqual(pp.Before-1,pp.After);
        int count=0;
        for(int i=0;i<events.Length;i++)
        {
            CampaignMoveAnimationDecision At(SourceBattleScene scene)=>single?CampaignMoveAnimationPresentation.AtAttackText(Single(events,i),true,scene):CampaignMoveAnimationPresentation.AtAttackText(Double(events,i),scene);
            var decision=At(SourceBattleScene.On);Assert.AreEqual(decision,At(SourceBattleScene.On));Assert.IsNull(At(SourceBattleScene.Off).Dispatch);
            var hit=single?CampaignMoveAnimationPresentation.AtHitResult(Single(events,i),true,SourceBattleScene.On):CampaignMoveAnimationPresentation.AtHitResult(Double(events,i),SourceBattleScene.On);Assert.IsNull(hit.Dispatch);
            if(events[i]==used&&expected){var receipt=events.OfType<SourceRecoveredEvent>().Single(e=>e.Move==move);var cue=decision.Dispatch;Assert.IsNotNull(cue,decision.Reason);count++;Assert.AreEqual(id,cue.SourceMoveId);Assert.AreEqual(actor,cue.Actor.Value);Assert.AreEqual(actor,cue.Target.Value);Assert.AreEqual(receipt.Sequence,cue.ResultSequence);Assert.AreEqual(attack.Sequence,cue.AttackSequence);Assert.AreEqual(pp.Sequence,cue.PpSequence);}
            else Assert.IsNull(decision.Dispatch);
        }
        Assert.AreEqual(expected?1:0,count);Assert.AreEqual(before,JsonSerializer.Serialize(events));Assert.AreEqual(saved,JsonSerializer.Serialize(state));Assert.AreEqual(rng,JsonSerializer.Serialize(trace));
    }
    [TestMethod][DataRow(135)][DataRow(208)]
    public void NaturalRecoveryLearnersHealSinglesWithUnclippedAndClippedAmounts(int id)
    {
        for(int mode=0;mode<2;mode++)
        {
            var bodies=Bodies(id,0,mode);var result=Resolve(Frame(bodies,true),0,0);int before=bodies[0].Pokemon.CurrentHp,max=bodies[0].Pokemon.Stats.MaxHp;
            var receipt=result.Events.OfType<SourceRecoveredEvent>().Single();Assert.AreEqual(before,receipt.HpBefore);Assert.AreEqual(Math.Min(max,before+max/2),receipt.HpAfter);Assert.AreEqual(receipt.HpAfter-before,receipt.Recovered);
            AssertProjection(result.Events,result.State,result.Trace,id,0,true,true);
        }
    }
    [TestMethod][DataRow(135)][DataRow(208)]
    public void OrderedRecoveryOwnsAllFourActorSlotsAndNormalizesSelectedTargets(int id)
    {
        for(int actor=0;actor<4;actor++)foreach(int selected in new[]{actor,actor^1,actor^2})
        {
            var result=Begin(Frame(Bodies(id,actor,1)),actor,selected);for(int n=0;!result.Events.OfType<SourceMoveUsedEvent>().Any(e=>e.Move==Move(id))&&n<12;n++)result=Next(result);
            AssertProjection(result.Events,result.State,result.Trace,id,actor,true);
        }
    }
    [TestMethod][DataRow(135)][DataRow(208)]
    public void FullHpFailureConsumesSourcePpWithoutPlayingRecovery(int id)
    {
        var result=Resolve(Frame(Bodies(id,0,2),true),0,0);Assert.AreEqual(0,result.Events.OfType<SourceRecoveredEvent>().Count());Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any());AssertProjection(result.Events,result.State,result.Trace,id,0,false,true);
    }
    [TestMethod][DataRow(135)][DataRow(208)]
    public void RecoveryReceiptOwnershipAmountAndUniquenessFailClosed(int id)
    {
        var result=Resolve(Frame(Bodies(id,0,1)),0,0);var events=result.Events;var receipt=events.OfType<SourceRecoveredEvent>().Single();int index=events.IndexOf(receipt),used=events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single(e=>e.Move==Move(id)));
        foreach(var replacement in new[]{receipt with{Side=new(1)},receipt with{Move=new("pret:MOVE_RECOVER")},receipt with{HpAfter=receipt.HpBefore},receipt with{Recovered=receipt.Recovered+1},receipt with{Sequence=0}})
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(Double(events.SetItem(index,replacement),used),SourceBattleScene.On).Dispatch);
        Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(Double(events.RemoveAt(index),used),SourceBattleScene.On).Dispatch);
        Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(Double(events.Insert(index,receipt),used),SourceBattleScene.On).Dispatch);
    }
    [TestMethod][DataRow(135)][DataRow(208)]
    public void RecoveryRequiresSelfTargetEvenWhenMoveAndPpTargetsAgree(int id)
    {
        var result=Resolve(Frame(Bodies(id,0,1)),0,0);var events=result.Events;
        int used=events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single(e=>e.Move==Move(id)));
        foreach(int target in new[]{1,2})
        {
            var malformed=events.Select(e=>e switch{
                SourceMoveUsedEvent m when m.Move==Move(id)=>(SourceBattleEvent)(m with{Target=new(target)}),
                SourcePpCommandEvent p when p.CurrentMove==Move(id)=>p with{Target=new(target)},
                _=>e}).ToImmutableArray();
            Assert.IsNull(CampaignMoveAnimationPresentation.AtAttackText(Double(malformed,used),SourceBattleScene.On).Dispatch);
        }
    }
}
