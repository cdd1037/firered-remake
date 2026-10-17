using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace FireRed.Tests.Application.WildBattles;

// Staged against the public Application projections and the public four-position
// kernel. The event-only fixtures are deliberately separate from natural content.
[TestClass, TestCategory("Game")]
public sealed class ProjectileMoveAnimationDispatchTests
{
    private static readonly (string Name, int Id, string Species, int Level, int Pp)[] Moves =
        [("SONIC_BOOM",49,"MAGNEMITE",16,20),("HYPER_BEAM",63,"DRAGONITE",75,5),
         ("WATERFALL",127,"GOLDEEN",38,15),("BONEMERANG",155,"CUBONE",25,10)];
    private static MoveId Id(string name) => new("pret:MOVE_" + name);
    private static SourceMechanicsCatalog Mechanics =>
        TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    private static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("projectile-moves", new("test"), "test", new(), CampaignDoubleBattleStage.Combat,
            events, index, [], [], 0, 0, [], [], [], [], 0);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("projectile-moves", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };
    private static CampaignMoveAnimationDecision AtText(ImmutableArray<SourceBattleEvent> events, int index) =>
        AnimationDispatchFixture.AtAttackText(Double(events, index), SourceBattleScene.On);
    private static CampaignMoveAnimationDecision AtHit(ImmutableArray<SourceBattleEvent> events, int index) =>
        AnimationDispatchFixture.AtHitResult(Double(events, index), SourceBattleScene.On);
    private static SourceBattleEvent[] Hits(ImmutableArray<SourceBattleEvent> events) =>
        events.Where(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent).ToArray();

    private static void AssertDispatches(ImmutableArray<SourceBattleEvent> events, int id, int actor = 0, int target = 1)
    {
        var hits = Hits(events); Assert.IsTrue(hits.Length > 0);
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        if (id != 155) { Assert.AreEqual(1, hits.Length); Assert.AreEqual(0, events.OfType<SourceMultiHitEvent>().Count()); }
        else
        {
            var summary = events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(hits.Length, summary.ActualHits);
            Assert.IsTrue(summary.ActualHits is >= 1 && summary.ActualHits <= summary.PlannedHits);
            Assert.AreEqual(2, summary.PlannedHits);
        }
        string before = JsonSerializer.Serialize(events); int count = 0;
        for (int index = 0; index < events.Length; index++)
        {
            var text = AtText(events, index); var hit = AtHit(events, index);
            int ordinal = Array.FindIndex(hits, h => h.Sequence == events[index].Sequence);
            bool eligible = id != 155 ? events[index] == used : ordinal >= 0;
            var decision = id != 155 ? text : hit;
            Assert.IsNull((id != 155 ? hit : text).Dispatch, "Only one receipt ownership model is allowed.");
            if (eligible)
            {
                var dispatch = decision.Dispatch; Assert.IsNotNull(dispatch, decision.Reason); count++;
                var receipt = id != 155 ? hits[0] : hits[ordinal];
                Assert.AreEqual(id, dispatch.SourceMoveId); Assert.AreEqual("projectile-moves", dispatch.BattleId);
                Assert.AreEqual(actor, dispatch.Actor.Value); Assert.AreEqual(target, dispatch.Target.Value);
                Assert.AreEqual(id != 155 ? used.Sequence : receipt.Sequence, dispatch.Sequence);
                Assert.AreEqual(receipt.Sequence, dispatch.ResultSequence);
                Assert.AreEqual(attack.Sequence, dispatch.AttackSequence); Assert.AreEqual(pp.Sequence, dispatch.PpSequence);
                Assert.AreEqual(receipt is SourceSubstituteDamagedEvent, dispatch.HitsSubstitute);
                Assert.AreEqual(id != 155, dispatch.AcknowledgesCurrentReceipt);
                if (id != 155) Assert.IsNull(dispatch.MoveTurn); else Assert.AreEqual((byte)ordinal, dispatch.MoveTurn);
                Assert.AreEqual(decision, id != 155 ? AtText(events, index) : AtHit(events, index));
            }
            else Assert.IsNull(decision.Dispatch, "Secondary, summary and faint receipts cannot replay the projectile root.");
            if (actor < 2 && target < 2)
            {
                var single = Single(events, index);
                Assert.AreEqual(text.Dispatch, AnimationDispatchFixture.AtAttackText(single, true, SourceBattleScene.On).Dispatch);
                Assert.AreEqual(hit.Dispatch, AnimationDispatchFixture.AtHitResult(single, true, SourceBattleScene.On).Dispatch);
            }
        }
        Assert.AreEqual(id != 155 ? 1 : hits.Length, count);
        Assert.AreEqual(before, JsonSerializer.Serialize(events), "Presentation may not mutate committed receipts.");
    }
    private static void AssertDenied(ImmutableArray<SourceBattleEvent> events, string branch, bool single = true)
    {
        for (int index = 0; index < events.Length; index++)
        {
            Assert.IsNull(AtText(events, index).Dispatch, branch + " text");
            Assert.IsNull(AtHit(events, index).Dispatch, branch + " hit");
            // Singles cannot represent extra party positions in any event field,
            // including PP ownership and recharge batches without MoveUsed.
            if (!single) continue;
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, index), true, SourceBattleScene.On).Dispatch, branch);
            Assert.IsNull(AnimationDispatchFixture.AtHitResult(Single(events, index), true, SourceBattleScene.On).Dispatch, branch);
        }
    }
    private static WildBattleCombatant NaturalMon(string species, int level, string name)
    {
        var move = Id(name); var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(m => m.Move == move && m.Level <= level),
            $"Natural source learnset required: {species}/{level}/{name}.");
        int pp = Mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }
    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var move = Moves.Single(m => m.Id == id);
        var bodies = Enumerable.Range(0, 4).Select(_ => NaturalMon("SNORLAX", 100, "DEFENSE_CURL") with { SpeedStage = 0 }).ToArray();
        bodies[actor] = NaturalMon(move.Species, move.Level, move.Name) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, uint seed)
    {
        static WildBattleCalledPartyMember Member(WildBattleCombatant body) =>
            new(body.Pokemon, body.HeldItem, WildBattleTransformationRules.PersistentStatus(body));
        var frame = new SourceBattleFrame {
            Layout = SourceBattleLayout.PresetDoubles, KoReplacementTiming = KoReplacementTiming.EndOfTurn,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version, BattleKind = WildBattleCalledBattleKind.Trainer, Rng = new(31),
            Parties = [new(BattleSide.Player, [Member(bodies[0]), Member(bodies[2])]), new(BattleSide.Opponent, [Member(bodies[1]), Member(bodies[3])])],
            Slots = Enumerable.Range(0, 4).Select(i => new SourceBattleSlot(new(i), new((BattleSide)(i & 1), i / 2), 1, bodies[i])).ToImmutableArray()
        };
        var entry = SourceFourEntryAbilities.InitializeEntry(Mechanics.Catalog, frame, frame.Revision);
        Assert.IsTrue(entry.Succeeded, entry.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(entry.State));
        return entry.State with { Rng = new(seed) };
    }
    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty); return result;
    }
    private static SourceFourActionResult Ordered(SourceBattleFrame frame, int actor, int target)
    {
        var selected = frame.Slots[actor].Body!.Pokemon.Moves[0].Move;
        var choices = frame.Slots.Select(c => {
            var move = Mechanics.Catalog.Moves[c.Body!.Pokemon.Moves[0].Move];
            int recipient = c.Slot.Value == actor ? target : move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? c.Slot.Value : c.Slot.Value ^ 1;
            return new SourceActionSelection(c.Occupant!.Value, SourceActionKind.Move, 0, new(recipient));
        }).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
        for (int step = 0; step < 12; step++)
        {
            Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            if (result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == actor && e.Move == selected)) return result;
            Assert.IsNotNull(result.Continuation, "The natural projectile move must execute within this public turn.");
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, result.State, result.Continuation.Value);
        }
        throw new InvalidOperationException("The selected projectile move did not execute in one bounded public turn.");
    }
    private static (uint Seed, SourceKernelResult Result) Find(WildBattleCombatant[] bodies,
        Func<SourceKernelResult, bool> predicate, string branch)
    {
        var entered = Frame(bodies, 0);
        // Bounded integration branch discovery, not an original-source golden.
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Resolve(entered with { Rng = new(seed) });
            if (predicate(result)) return (seed, result);
        }
        throw new InvalidOperationException($"No observed {branch} in 1024 action-start seeds.");
    }

    [TestMethod]
    public void NaturalLearnersExecuteOrderedActionsAtEveryPositionAndSelectedAlly()
    {
        foreach(var move in Moves)for(int actor=0;actor<4;actor++)foreach(int target in new[]{actor^1,actor^2})
        {
            var result=Ordered(Frame(Bodies(move.Id,actor),31),actor,target);
            var pp=result.Events.OfType<SourcePpCommandEvent>().Single();Assert.AreEqual(move.Pp,pp.Before);Assert.AreEqual(move.Pp-1,pp.After);
            string state=JsonSerializer.Serialize(result.State),trace=JsonSerializer.Serialize(result.Trace);var rng=result.State.Rng;
            AssertDispatches(result.Events,move.Id,actor,target);
            Assert.AreEqual(state,JsonSerializer.Serialize(result.State));Assert.AreEqual(trace,JsonSerializer.Serialize(result.Trace));Assert.AreEqual(rng,result.State.Rng);
            if(move.Id==49)Assert.AreEqual(20,result.Events.OfType<SourceHpChangedEvent>().Single().Damage,"Sonic Boom's source fixed20 damage survives renderer admission.");
            if(move.Id==155){Assert.AreEqual(2,result.Events.OfType<SourceMultiHitEvent>().Single().PlannedHits);Assert.IsFalse(result.Trace.Any(t=>t.Purpose.Contains("multi-hit-count",StringComparison.Ordinal)));}
        }
    }
    [TestMethod][DataRow(49)][DataRow(63)][DataRow(127)][DataRow(155)]
    public void ProtectAccuracyMissAndForeignPpOwnerNeverBorrowAHitInvocation(int id)
    {
        for(int actor=0;actor<4;actor++)
        {
            var bodies=Bodies(id,actor);bodies[actor^1]=bodies[actor^1] with{Protected=true};var blocked=Resolve(Frame(bodies,31),actor,actor^1);
            Assert.AreEqual(0,Hits(blocked.Events).Length);AssertDenied(blocked.Events,"Protect",single:actor<2);
            var success=Resolve(Frame(Bodies(id,actor),31),actor,actor^1);
            var foreign=success.Events.Select(e=>e is SourcePpCommandEvent pp?pp with{PpOwner=new(actor^2)}:e).ToImmutableArray();AssertDenied(foreign,"foreign PP owner",single:false);
            if(actor<2)
            {
                var singlesForeign=success.Events.Select(e=>e is SourcePpCommandEvent pp?pp with{PpOwner=new(actor^1)}:e).ToImmutableArray();
                AssertDenied(singlesForeign,"representable foreign PP owner");
            }
        }
        var inaccurate=Bodies(id);inaccurate[0]=inaccurate[0] with{AccuracyStage=0};
        var miss=Find(inaccurate,r=>r.Events.OfType<SourceMoveMissedEvent>().Any(),"accuracy miss").Result;
        Assert.AreEqual(0,Hits(miss.Events).Length);AssertDenied(miss.Events,"accuracy miss");
    }
    [TestMethod][DataRow(49,"GASTLY","LICK")][DataRow(63,"GASTLY","LICK")][DataRow(127,"VAPOREON","TACKLE")][DataRow(155,"PIDGEY","TACKLE")]
    public void NaturalImmunityOrAbsorptionProducesNoProjectileInvocation(int id,string species,string move)
    {
        for(int actor=0;actor<4;actor++)
        {
            var bodies=Bodies(id,actor);bodies[actor^1]=NaturalMon(species,100,move);var result=Resolve(Frame(bodies,31),actor,actor^1);
            Assert.IsTrue(result.Events.OfType<SourceAbilityPreventedEvent>().Any()||result.Events.OfType<SourceEffectivenessEvent>().Any(e=>e.Effectiveness==WildBattleEffectiveness.NoEffect));
            AssertDenied(result.Events,"natural immunity/absorption",single:actor<2);
        }
    }
    [TestMethod][DataRow(49)][DataRow(63)][DataRow(127)][DataRow(155)]
    public void SubstituteAndEarlyKoRetainOnlyActualCommittedReceiptOwnership(int id)
    {
        for(int actor=0;actor<4;actor++)foreach(bool ko in new[]{false,true})
        {
            var bodies=Bodies(id,actor);int target=actor^1;
            bodies[target]=ko?bodies[target] with{Pokemon=bodies[target].Pokemon with{CurrentHp=1}}:bodies[target] with{CalledVolatile=new(){SubstituteHp=1}};
            var result=Resolve(Frame(bodies,31),actor,target);
            if(ko){Assert.AreEqual(0,result.Events.OfType<SourceHpChangedEvent>().Single().HpAfter);if(id==155)Assert.AreEqual(1,result.Events.OfType<SourceMultiHitEvent>().Single().ActualHits);}
            else{Assert.AreEqual(0,result.Events.OfType<SourceSubstituteDamagedEvent>().Single().HpAfter);if(id==155)Assert.AreEqual(2,result.Events.OfType<SourceMultiHitEvent>().Single().ActualHits);}
            AssertDispatches(result.Events,id,actor,target);
        }
    }
    [TestMethod]
    public void HyperBeamRechargeKeepsOriginalLockPpAndNoSecondAnimation()
    {
        for(int actor=0;actor<4;actor++)
        {
            var hit=Resolve(Frame(Bodies(63,actor),31),actor,actor^1);AssertDispatches(hit.Events,63,actor,actor^1);
            Assert.AreEqual(WildBattleCalledLockKind.Recharge,hit.State.Slots[actor].Registers.Turn!.Lock!.Kind);
            int pp=hit.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp;var rng=hit.State.Rng;
            var recharge=Resolve(hit.State,actor,actor^1);
            Assert.IsTrue(recharge.Events.OfType<SourceMoveFailedEvent>().Any(e=>e.Reason=="must-recharge"));Assert.AreEqual(0,recharge.Events.OfType<SourceMoveUsedEvent>().Count());Assert.AreEqual(0,recharge.Events.OfType<SourcePpCommandEvent>().Count());
            Assert.AreEqual(pp,recharge.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);Assert.AreEqual(rng,recharge.State.Rng);Assert.AreEqual(0,recharge.Trace.Length);AssertDenied(recharge.Events,"recharge",single:actor<2);
        }
    }
    [TestMethod]
    public void InvalidBonemerangSummaryCannotReplayEitherHit()
    {
        var result=Resolve(Frame(Bodies(155),31));var summary=result.Events.OfType<SourceMultiHitEvent>().Single();
        foreach(var invalid in new[]{summary with{PlannedHits=3},summary with{ActualHits=1},summary with{Actor=new(1)},summary with{Actor=new(2)},summary with{Move=Id("DOUBLE_KICK")}})
            AssertDenied(result.Events.Select(e=>e==summary?invalid:e).ToImmutableArray(),"unmatched source double-hit summary",single:invalid.Actor.Value<2);
    }
}
