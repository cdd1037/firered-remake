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

// These are committed public-kernel receipts using natural source learners.
// Controlled HP, stages, volatile state and bounded RNG search are integration fixtures,
// not natural-route evidence. Rendering availability is always the canonical fixture.
internal static class OverlayHitAnimationKernelFixture
{
    internal static SourceMechanicsCatalog Mechanics => TestAssets.Cached(() => SourceMechanicsCatalogLoader.LoadAuthenticated(TestAssets.Root));
    internal static readonly int[] All = [184, 137, 296, 114, 65, 238, 6, 140];
    internal static (string Name, string Species, string Effect) Spec(int id) => id switch {
        184 => ("SCARY_FACE", "CHARMANDER", "EFFECT_SPEED_DOWN_2"),
        137 => ("GLARE", "DUNSPARCE", "EFFECT_PARALYZE"),
        296 => ("MIST_BALL", "LATIAS", "EFFECT_SPECIAL_ATTACK_DOWN_HIT"),
        114 => ("HAZE", "ZUBAT", "EFFECT_HAZE"),
        65 => ("DRILL_PECK", "SPEAROW", "EFFECT_HIT"),
        238 => ("CROSS_CHOP", "MANKEY", "EFFECT_HIGH_CRITICAL"),
        6 => ("PAY_DAY", "MEOWTH", "EFFECT_PAY_DAY"),
        140 => ("BARRAGE", "EXEGGCUTE", "EFFECT_MULTI_HIT"),
        _ => throw new ArgumentOutOfRangeException(nameof(id))
    };
    internal static MoveId Id(string name) => new("pret:MOVE_" + name);
    internal static MoveId Move(int id) => Id(Spec(id).Name);
    internal static CampaignDoubleBattle Double(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("overlay-hit", new("test"), "test", new(), CampaignDoubleBattleStage.Combat, events, index, [], [], 0, 0, [], [], [], [], 0);
    internal static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        new("overlay-hit", null!, new(), events.Select(SourceBattleEventAdapter.Export).ToImmutableArray(), index,
            null!, null!, CampaignBattlePhase.Events, [], 0, []) { SourceEvents = events };
    internal static WildBattleCombatant Mon(string species, string name, int level = 50)
    {
        var pokemon = SourcePokemonFactory.Create(Mechanics,
            new(new("pret:SPECIES_" + species), level, 42, IvPolicy.Fixed(31), PersonalityPolicy.Fixed(0)), new(0)).Pokemon;
        var move = Id(name);
        Assert.IsTrue(Mechanics.Catalog.Species[pokemon.Species].LevelUpMoves.Any(x => x.Move == move && x.Level <= level),
            $"Require natural source learner: {species}/{level}/{name}.");
        int pp = Mechanics.Catalog.Moves[move].Pp;
        return new(pokemon with { Moves = [new(move, pp, pp)] });
    }
    internal static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Mon("SNORLAX", "DEFENSE_CURL", 100)).ToArray();
        var spec = Spec(id);
        bodies[actor] = Mon(spec.Species, spec.Name) with { AccuracyStage = id == 114 ? 6 : 12 };
        return bodies;
    }
    internal static SourceBattleFrame Frame(WildBattleCombatant[] bodies, bool single = false)
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
        return entry.State with { Rng = new(31) };
    }
    internal static SourceKernelResult Resolve(SourceBattleFrame frame, int actor = 0, int target = 1)
    {
        string before = JsonSerializer.Serialize(frame);
        var result = SourceBattleKernel.ResolveAction(Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, 0, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty); Assert.AreEqual(before, JsonSerializer.Serialize(frame));
        return result;
    }
    internal static SourceKernelResult Find(SourceBattleFrame frame, int actor, int target,
        Func<SourceKernelResult, bool> matches, string branch)
    {
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Resolve(frame with { Rng = new(seed) }, actor, target);
            if (!matches(result)) continue;
            Console.WriteLine($"{branch}: observed action-start seed={seed}");
            return result;
        }
        throw new InvalidOperationException($"No actual {branch} in 1024 action-start seeds.");
    }
    internal static SourceFourActionResult Ordered(SourceBattleFrame frame, int actor, int target)
    {
        string before = JsonSerializer.Serialize(frame);
        var choices = frame.Slots.Take(frame.BattlerCount).Select(s => new SourceActionSelection(s.Occupant!.Value,
            SourceActionKind.Move, 0, s.Slot.Value == actor ? new(target) : s.Slot)).ToImmutableArray();
        var started = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, choices);
        Assert.IsTrue(started.Succeeded, started.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(started.State));
        Assert.AreEqual(actor, started.State.Queue[0].Slot.Value, "The controlled speed fixture must place the tested actor first.");
        Assert.IsNotNull(started.Continuation);
        var result = SourceFourActionTurns.Advance(Mechanics.Catalog, started.State, started.Continuation.Value);
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.AreEqual(actor, result.Events.OfType<SourceMoveUsedEvent>().Single().Actor.Value);
        Assert.AreEqual(before, JsonSerializer.Serialize(frame));
        return result;
    }
    internal static SourceFourActionResult FindOrdered(SourceBattleFrame frame, int actor, int target,
        Func<SourceFourActionResult, bool> matches, string branch)
    {
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Ordered(frame with { Rng = new(seed) }, actor, target);
            if (!matches(result)) continue;
            Console.WriteLine($"{branch}: observed public-turn-start seed={seed}");
            return result;
        }
        throw new InvalidOperationException($"No actual {branch} in 1024 public-turn-start seeds.");
    }
    internal static SourceBattleEvent[] Hits(ImmutableArray<SourceBattleEvent> events) =>
        events.Where(e => e is SourceHpChangedEvent or SourceSubstituteDamagedEvent).ToArray();
    internal static void Check(SourceKernelResult result, int id, bool single, bool expected = true) =>
        Check(result.Events, result, id, single, expected);
    internal static void Check(SourceFourActionResult result, int id, bool single, bool expected = true) =>
        Check(result.Events, result, id, single, expected);
    private static void Check(ImmutableArray<SourceBattleEvent> events, object result, int id, bool single, bool expected)
    {
        string before = JsonSerializer.Serialize(result);
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(Move(id), used.Move); Assert.AreEqual(used.Move, attack.CurrentMove); Assert.AreEqual(used.Move, attack.ChosenMove);
        Assert.AreEqual(used.Move, pp.CurrentMove); Assert.AreEqual(used.Move, pp.ChosenMove);
        Assert.AreEqual(used.Actor, pp.Actor); Assert.AreEqual(used.Actor, pp.PpOwner); Assert.AreEqual(used.Target, pp.Target);
        Assert.AreEqual(used.PpBefore, pp.Before); Assert.AreEqual(used.PpAfter, pp.After); Assert.AreEqual(used.Slot, pp.PpSlot);
        Assert.AreEqual(Math.Max(0, pp.Before - pp.CommandCost), pp.After);
        Assert.IsTrue(attack.Sequence < used.Sequence && used.Sequence < pp.Sequence);
        var hits = Hits(events);
        SourceBattleEvent? outcome = !expected ? null : id switch {
            184 => events.OfType<SourceStatChangedEvent>().Single(s => s.Actor == used.Actor && s.Target == used.Target && s.Stat == WildBattleStat.Speed),
            137 => events.OfType<SourceStatusChangedEvent>().Single(s => s.Side == used.Target && s.Reason == "move"),
            114 => events.OfType<SourceMtMoonVolatileEvent>().Single(s => s.Volatile == "haze"),
            140 => null,
            _ => hits.Single()
        };
        if (expected && id == 140)
        {
            var summary = events.OfType<SourceMultiHitEvent>().Single();
            Assert.AreEqual(hits.Length, summary.ActualHits); Assert.IsTrue(summary.PlannedHits is >= 2 and <= 5);
            Assert.IsTrue(summary.ActualHits is >= 1 && summary.ActualHits <= summary.PlannedHits);
            Assert.IsTrue(hits.All(h => pp.Sequence < h.Sequence && h.Sequence < summary.Sequence));
        }
        int roots = 0, waves = 0;
        for (int i = 0; i < events.Length; i++)
        {
            CampaignMoveAnimationDecision Text(SourceBattleScene scene) => single
                ? AnimationDispatchFixture.AtAttackText(Single(events, i), true, scene)
                : AnimationDispatchFixture.AtAttackText(Double(events, i), scene);
            CampaignMoveAnimationDecision Hit(SourceBattleScene scene) => single
                ? AnimationDispatchFixture.AtResult(Single(events, i), true, scene)
                : AnimationDispatchFixture.AtResult(Double(events, i), scene);
            var text = Text(SourceBattleScene.On); var hit = Hit(SourceBattleScene.On);
            Assert.AreEqual(text, Text(SourceBattleScene.On)); Assert.AreEqual(hit, Hit(SourceBattleScene.On));
            Assert.IsNull(Text(SourceBattleScene.Off).Dispatch); Assert.IsNull(Hit(SourceBattleScene.Off).Dispatch);
            int ordinal = Array.FindIndex(hits, h => h.Sequence == events[i].Sequence);
            bool owns = expected && (id == 140 ? ordinal >= 0 : events[i] == used);
            var decision = id == 140 ? hit : text;
            Assert.IsNull((id == 140 ? text : hit).Dispatch);
            if (owns)
            {
                var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); roots++;
                var resultReceipt = id == 140 ? hits[ordinal] : outcome!;
                Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Target, cue.Target);
                Assert.AreEqual(attack.Sequence, cue.AttackSequence); Assert.AreEqual(pp.Sequence, cue.PpSequence);
                Assert.AreEqual(resultReceipt.Sequence, cue.ResultSequence);
                Assert.AreEqual(id == 140 ? resultReceipt.Sequence : used.Sequence, cue.Sequence);
                Assert.AreEqual(resultReceipt is SourceSubstituteDamagedEvent, cue.HitsSubstitute);
                Assert.AreEqual(id != 140, cue.AcknowledgesCurrentReceipt);
                Assert.AreEqual(id == 140 ? (byte?)ordinal : null, cue.MoveTurn);
            }
            else Assert.IsNull(decision.Dispatch, "Only committed eligible receipts may own an animation root.");
            var wave = single ? AnimationDispatchFixture.StatAtResult(Single(events, i), true)
                : AnimationDispatchFixture.StatAtResult(Double(events, i));
            bool ownsWave = expected && id is 184 or 296 && events[i] is SourceStatChangedEvent stat &&
                stat.Actor == used.Actor && stat.Target == used.Target && stat.Before > stat.After;
            if (ownsWave)
            {
                Assert.IsNotNull(wave); waves++; Assert.AreEqual(id, wave.SourceMoveId);
                Assert.AreEqual(id == 184 ? 48 : 25, wave.AnimationArgument); Assert.AreEqual(used.Target, wave.Affected);
                Assert.AreEqual(events[i].Sequence, wave.Sequence); Assert.AreEqual(pp.Sequence, wave.PpSequence);
            }
            else Assert.IsNull(wave, "Haze resets and unrelated receipts cannot invent a generic stat wave.");
            if (single)
            {
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, i), false, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtResult(Single(events, i), false, SourceBattleScene.On).Dispatch);
            }
        }
        Assert.AreEqual(!expected ? 0 : id == 140 ? hits.Length : 1, roots);
        Assert.AreEqual(expected && id is 184 or 296 ? events.OfType<SourceStatChangedEvent>().Count(s => s.Actor == used.Actor && s.Target == used.Target && s.After < s.Before) : 0, waves);
        Assert.AreEqual(before, JsonSerializer.Serialize(result), "Projection preserves state, RNG, trace and receipts.");
    }
    internal static void Deny(ImmutableArray<SourceBattleEvent> events, bool single, string reason)
    {
        for (int i = 0; i < events.Length; i++)
        {
            Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Single(events, i), true, SourceBattleScene.On).Dispatch
                : AnimationDispatchFixture.AtAttackText(Double(events, i), SourceBattleScene.On).Dispatch, reason);
            Assert.IsNull(single ? AnimationDispatchFixture.AtResult(Single(events, i), true, SourceBattleScene.On).Dispatch
                : AnimationDispatchFixture.AtResult(Double(events, i), SourceBattleScene.On).Dispatch, reason);
        }
    }
}
