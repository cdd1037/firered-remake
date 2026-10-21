using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class SilverWindAnimationKernelTests
{
    private static readonly MoveId SilverWind = Kernel.Id("SILVER_WIND");
    private static readonly WildBattleStat[] StatOrder = [WildBattleStat.Attack, WildBattleStat.Defense,
        WildBattleStat.Speed, WildBattleStat.SpecialAttack, WildBattleStat.SpecialDefense];

    // Natural source learners with controlled HP, stages and bounded real RNG
    // search. These are public-kernel integration fixtures, not route evidence.
    private static WildBattleCombatant[] Bodies(int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = Kernel.Mon("BEAUTIFLY", "SILVER_WIND", 50) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }

    private static WildBattleCombatant Stages(WildBattleCombatant body, params int[] stages) => body with {
        AttackStage = stages[0], DefenseStage = stages[1], SpeedStage = stages[2],
        SpecialAttackStageDelta = stages[3] - 6, SpecialDefenseStageDelta = stages[4] - 6 };
    private static int[] Stages(WildBattleCombatant body) => [body.AttackStage, body.DefenseStage, body.SpeedStage,
        6 + body.SpecialAttackStageDelta, 6 + body.SpecialDefenseStageDelta];
    private static SourceBattleEvent[] Group(ImmutableArray<SourceBattleEvent> events) =>
        events.Where(e => e is SourceStatChangedEvent or SourceStatAtMaximumEvent).ToArray();
    private static bool QualifyingRoll(SourceFourActionResult result) => result.Trace.Any(t =>
        t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal) && t.Raw % 100 <= 10);
    private static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        Envelope.Single(events, index);
    private static CampaignMoveAnimationDecision Text(ImmutableArray<SourceBattleEvent> events, int index, bool single,
        SourceBattleScene scene = SourceBattleScene.On) => single
        ? AnimationDispatchFixture.AtAttackText(Single(events, index), true, scene)
        : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, index), scene);
    private static CampaignMoveAnimationDecision Result(ImmutableArray<SourceBattleEvent> events, int index, bool single,
        SourceBattleScene scene = SourceBattleScene.On) => single
        ? AnimationDispatchFixture.AtResult(Single(events, index), true, scene)
        : AnimationDispatchFixture.AtResult(Envelope.Double(events, index), scene);
    private static CampaignStatAnimationDispatch? Wave(ImmutableArray<SourceBattleEvent> events, int index, bool single,
        bool behindSubstitute = false) => single
        ? AnimationDispatchFixture.StatAtResult(Single(events, index), true, behindSubstitute)
        : AnimationDispatchFixture.StatAtResult(Envelope.Double(events, index), behindSubstitute);

    private static void CheckHit(SourceFourActionResult committed, bool single, SourceBattleEvent? hit) =>
        CheckHit(committed.Events, committed, single, hit);
    private static void CheckHit(ImmutableArray<SourceBattleEvent> events, object committed, bool single, SourceBattleEvent? hit)
    {
        string before = JsonSerializer.Serialize(committed);
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(SilverWind, used.Move); Assert.AreEqual(SilverWind, attack.CurrentMove); Assert.AreEqual(SilverWind, attack.ChosenMove);
        Assert.AreEqual(SilverWind, pp.CurrentMove); Assert.AreEqual(SilverWind, pp.ChosenMove);
        Assert.AreEqual(used.Actor, attack.Actor); Assert.AreEqual(used.Actor, pp.Actor); Assert.AreEqual(used.Actor, pp.PpOwner);
        Assert.AreEqual(used.Target, pp.Target); Assert.AreEqual(used.Slot, pp.PpSlot);
        Assert.AreEqual(used.PpBefore, pp.Before); Assert.AreEqual(used.PpAfter, pp.After);
        Assert.AreEqual(Math.Max(0, pp.Before - pp.CommandCost), pp.After);
        Assert.IsTrue(attack.Sequence < used.Sequence && used.Sequence < pp.Sequence);
        int roots = 0;
        for (int i = 0; i < events.Length; i++)
        {
            var decision = Text(events, i, single); Assert.AreEqual(decision, Text(events, i, single));
            Assert.IsNull(Text(events, i, single, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(Result(events, i, single).Dispatch);
            Assert.IsNull(Result(events, i, single, SourceBattleScene.Off).Dispatch);
            if (single)
            {
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(Single(events, i), false, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtResult(Single(events, i), false, SourceBattleScene.On).Dispatch);
            }
            if (hit is null || events[i] != used) { Assert.IsNull(decision.Dispatch); continue; }
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); roots++;
            Assert.AreEqual(318, cue.SourceMoveId); Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Target, cue.Target);
            Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
            Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(pp.Sequence, cue.SnapshotSequence);
            Assert.AreEqual(hit.Sequence, cue.ResultSequence); Assert.IsTrue(pp.Sequence < hit.Sequence);
            Assert.AreEqual(hit is SourceSubstituteDamagedEvent, cue.HitsSubstitute);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText, cue.Boundary); Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
            Assert.IsNull(cue.MoveTurn); Assert.IsNull(cue.MovePower); Assert.IsNull(cue.MoveDamage);
            Assert.IsNull(cue.FuryCutterCounter); Assert.IsNull(cue.RolloutCounter);
        }
        Assert.AreEqual(hit is null ? 0 : 1, roots);
        Assert.AreEqual(before, JsonSerializer.Serialize(committed), "Projection must preserve state, receipts, RNG and trace.");
    }

    private static void CheckWave(ImmutableArray<SourceBattleEvent> events, bool single, int? expectedArgument)
    {
        string before = JsonSerializer.Serialize(events); var group = Group(events);
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single();
        int count = 0;
        for (int i = 0; i < events.Length; i++)
        {
            var wave = Wave(events, i, single); Assert.AreEqual(wave, Wave(events, i, single));
            Assert.IsNull(Wave(events, i, single, true), "The affected actor's display Substitute masks its stat wave.");
            if (single) Assert.IsNull(AnimationDispatchFixture.StatAtResult(Single(events, i), false));
            if (expectedArgument is null || events[i] != group[0]) { Assert.IsNull(wave); continue; }
            Assert.IsNotNull(wave); count++;
            Assert.AreEqual(318, wave.SourceMoveId); Assert.AreEqual(expectedArgument.Value, wave.AnimationArgument);
            Assert.AreEqual(used.Actor, wave.Affected); Assert.AreEqual(group[0].Sequence, wave.Sequence);
            Assert.AreEqual(attack.Sequence, wave.AttackSequence); Assert.AreEqual(pp.Sequence, wave.PpSequence);
        }
        Assert.AreEqual(expectedArgument.HasValue ? 1 : 0, count); Assert.AreEqual(before, JsonSerializer.Serialize(events));
    }

    private static void Deny(ImmutableArray<SourceBattleEvent> events, bool single)
    {
        for (int i = 0; i < events.Length; i++)
        {
            Assert.IsNull(Text(events, i, single).Dispatch); Assert.IsNull(Result(events, i, single).Dispatch);
            Assert.IsNull(Wave(events, i, single));
        }
    }

    [TestMethod]
    [DataRow(true, 0), DataRow(true, 1), DataRow(false, 0), DataRow(false, 1), DataRow(false, 2), DataRow(false, 3)]
    public void NaturalBeautiflyOwnsBodyAndSubstituteHitsAtEveryPositionAndSelectedAlly(bool single, int actor)
    {
        var definition = Kernel.Mechanics.Catalog.Moves[SilverWind];
        Assert.AreEqual(318, definition.SourceMoveId); Assert.AreEqual("EFFECT_ALL_STATS_UP_HIT", definition.Effect);
        Assert.AreEqual("MOVE_TARGET_SELECTED", definition.Target); Assert.AreEqual("TYPE_BUG", definition.Type);
        Assert.AreEqual(60, definition.Power); Assert.AreEqual(10, definition.SecondaryEffectChance);
        Assert.IsTrue(AnimationDispatchFixture.Supports(318, SilverWind.Value), "The canonical generated script must own Move_SILVER_WIND.");
        foreach (int target in single ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 2 })
        foreach (int substituteHp in new[] { 0, 1, 100 })
        {
            var bodies = Bodies(actor);
            if (substituteHp != 0) bodies[target] = bodies[target] with { CalledVolatile = new() { SubstituteHp = substituteHp } };
            var result = Kernel.Ordered(Kernel.Frame(bodies, single), actor, target);
            var hit = Envelope.Hits(result.Events).Single(); CheckHit(result, single, hit);
            Assert.AreEqual(substituteHp != 0, hit is SourceSubstituteDamagedEvent);
            if (substituteHp != 0)
            {
                Assert.AreEqual(bodies[target].Pokemon.CurrentHp, result.State.Slots[target].Body!.Pokemon.CurrentHp);
                var sub = (SourceSubstituteDamagedEvent)hit; Assert.AreEqual(substituteHp, sub.HpBefore);
                Assert.AreEqual(Math.Max(0, sub.HpBefore - sub.Damage), sub.HpAfter);
                if (substituteHp == 1) Assert.AreEqual(0, sub.HpAfter);
            }
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void RealSecondaryRollBranchesPreserveOneHitAndOnlySuccessfulFiveStatGroupOwnsWave(bool boost)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var bodies = Bodies(actor); bodies[actor] = Stages(bodies[actor], 6, 6, 6, 6, 6);
            var result = Kernel.Find(Kernel.Frame(bodies, single), actor, actor ^ 1,
                r => QualifyingRoll(r) == boost, "Silver Wind secondary boost=" + boost);
            var draw = result.Trace.Single(t => t.Purpose.EndsWith(".secondary-effect", StringComparison.Ordinal));
            Assert.AreEqual(100, draw.Bound); Assert.AreEqual(draw.Raw % 100, draw.Result);
            Assert.AreEqual(boost, draw.Raw % 100 <= 10, "Source seteffectwithchance uses <=, not <.");
            CheckHit(result, single, Envelope.Hits(result.Events).Single());
            Assert.AreEqual(boost ? 5 : 0, Group(result.Events).Length);
            Assert.AreEqual(boost ? 5 : 0, result.Events.OfType<SourceStatChangedEvent>().Count());
            CollectionAssert.AreEqual(Enumerable.Repeat(boost ? 7 : 6, 5).ToArray(), Stages(result.State.Slots[actor].Body!));
            CheckWave(result.Events, single, boost ? 55 : null);
        }
    }

    [TestMethod, DataRow("all"), DataRow("partial"), DataRow("one"), DataRow("capped")]
    public void ActualQualifyingRollHonorsCapsAndFirstOrderedOutcomeOwnsTheOnlyWave(string branch)
    {
        int[] stages = branch switch {
            "all" => [6, 6, 6, 6, 6], "partial" => [12, 11, 12, 6, 12],
            "one" => [12, 12, 12, 12, 11], "capped" => [12, 12, 12, 12, 12],
            _ => throw new InvalidOperationException() };
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var bodies = Bodies(actor); bodies[actor] = Stages(bodies[actor], stages);
            var result = Kernel.Find(Kernel.Frame(bodies, single), actor, actor ^ 1, QualifyingRoll, "Silver Wind caps=" + branch);
            CheckHit(result, single, Envelope.Hits(result.Events).Single());
            var group = Group(result.Events); Assert.AreEqual(branch == "capped" ? 0 : 5, group.Length);
            CollectionAssert.AreEqual(stages.Select(s => Math.Min(12, s + 1)).ToArray(), Stages(result.State.Slots[actor].Body!));
            for (int j = 0; j < group.Length; j++)
            {
                if (j > 0) Assert.AreEqual(group[j - 1].Sequence + 1, group[j].Sequence);
                if (stages[j] == 12)
                {
                    Assert.IsInstanceOfType<SourceStatAtMaximumEvent>(group[j]); var cap = (SourceStatAtMaximumEvent)group[j];
                    Assert.AreEqual(StatOrder[j], cap.Stat); Assert.AreEqual(actor, cap.Actor.Value); Assert.AreEqual(cap.Actor, cap.Target);
                    int index = result.Events.IndexOf(cap);
                    Assert.IsFalse(single
                        ? CampaignStatAnimationPresentation.IsSilentResultReceipt(Single(result.Events, index), true, moves: Kernel.Mechanics.Catalog.Moves)
                        : CampaignStatAnimationPresentation.IsSilentResultReceipt(Envelope.Double(result.Events, index), moves: Kernel.Mechanics.Catalog.Moves),
                        "An individual ALLOW_PTR cap keeps its actual source message.");
                }
                else
                {
                    Assert.IsInstanceOfType<SourceStatChangedEvent>(group[j]); var stat = (SourceStatChangedEvent)group[j];
                    Assert.AreEqual(StatOrder[j], stat.Stat); Assert.AreEqual(actor, stat.Actor.Value); Assert.AreEqual(stat.Actor, stat.Target);
                    Assert.AreEqual(stages[j], stat.Before); Assert.AreEqual(stages[j] + 1, stat.After);
                }
            }
            CheckWave(result.Events, single, branch == "capped" ? null : branch == "one" ? 19 : 55);
        }
    }

    [TestMethod, DataRow("substitute"), DataRow("ko"), DataRow("endure"), DataRow("shield-dust")]
    public void TargetSubstituteKoEndureAndShieldDustDoNotTurnSelfBoostIntoTargetOwnedWave(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(); bodies[0] = Stages(bodies[0], 6, 6, 6, 6, 6);
            if (branch == "substitute") bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } };
            if (branch is "ko" or "endure") bodies[1] = bodies[1] with { Enduring = branch == "endure", Pokemon = bodies[1].Pokemon with { CurrentHp = 1 } };
            if (branch == "shield-dust")
            {
                bodies[1] = Kernel.Mon("CATERPIE", "STRING_SHOT", 100) with { SpeedStage = 0 };
                Assert.AreEqual("ABILITY_SHIELD_DUST", WildBattleEffective.Ability(bodies[1]));
            }
            var result = Kernel.Find(Kernel.Frame(bodies, single), 0, 1, QualifyingRoll, "Silver Wind self boost through " + branch);
            var hit = Envelope.Hits(result.Events).Single(); CheckHit(result, single, hit);
            Assert.AreEqual(5, result.Events.OfType<SourceStatChangedEvent>().Count()); CheckWave(result.Events, single, 55);
            if (branch == "substitute") Assert.AreEqual(0, ((SourceSubstituteDamagedEvent)hit).HpAfter);
            if (branch == "ko")
            {
                Assert.AreEqual(0, ((SourceHpChangedEvent)hit).HpAfter);
                Assert.IsTrue(result.Events.OfType<SourceFaintedEvent>().Any(e => e.Side.Value == 1));
            }
            if (branch == "endure")
            {
                var hp = (SourceHpChangedEvent)hit; Assert.AreEqual(1, hp.HpBefore); Assert.AreEqual(1, hp.HpAfter); Assert.AreEqual(0, hp.Damage);
                var adjusted = result.Events.OfType<SourceDamageAdjustedEvent>().Single();
                Assert.IsTrue(adjusted.Endured); Assert.IsTrue(adjusted.DamageBefore > 0); Assert.AreEqual(0, adjusted.DamageAfter);
            }
        }
    }

    [TestMethod, DataRow("protect"), DataRow("miss"), DataRow("wonder-guard")]
    public void ActualPreventionNeverBorrowsHitOrSelfBoostAnimation(string branch)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            int target = actor ^ 1; var bodies = Bodies(actor);
            if (branch == "protect") bodies[target] = bodies[target] with { Protected = true };
            if (branch == "miss") { bodies[actor] = bodies[actor] with { AccuracyStage = 0 }; bodies[target] = bodies[target] with { EvasionStage = 12 }; }
            if (branch == "wonder-guard")
            {
                bodies[target] = Kernel.Mon("SHEDINJA", "HARDEN", 100) with { SpeedStage = 0 };
                Assert.AreEqual("ABILITY_WONDER_GUARD", WildBattleEffective.Ability(bodies[target]));
            }
            var frame = Kernel.Frame(bodies, single);
            var result = branch == "miss" ? Kernel.Find(frame, actor, target, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "Silver Wind miss")
                : Kernel.Ordered(frame, actor, target);
            if (branch == "protect") Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "protected"));
            if (branch == "wonder-guard") Assert.IsTrue(result.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_WONDER_GUARD"));
            Assert.AreEqual(bodies[target].Pokemon.CurrentHp, result.State.Slots[target].Body!.Pokemon.CurrentHp);
            Assert.AreEqual(0, Group(result.Events).Length); CheckHit(result, single, null); CheckWave(result.Events, single, null);
        }
    }

    [TestMethod]
    public void MissingForeignAndUnpaidPrefixReceiptsCannotBorrowHitOrStatGroup()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(); bodies[0] = Stages(bodies[0], 6, 6, 6, 6, 6);
            var result = Kernel.Find(Kernel.Frame(bodies, single), 0, 1, QualifyingRoll, "Silver Wind prefix ownership");
            var events = result.Events; var pp = events.OfType<SourcePpCommandEvent>().Single();
            var attack = events.OfType<SourceAttackStringEvent>().Single(); var hit = events.OfType<SourceHpChangedEvent>().Single();
            foreach (var wrong in new[] { pp with { Actor = new(1) }, pp with { PpOwner = new(1) }, pp with { Target = new(0) },
                pp with { ChosenMove = Kernel.Id("METRONOME") }, pp with { CurrentMove = Kernel.Id("ANCIENT_POWER") }, pp with { PpSlot = 3 },
                pp with { Before = pp.Before + 1 }, pp with { After = pp.Before }, pp with { CommandCost = 0 }, pp with { Turn = pp.Turn + 1 } })
                Deny(events.Replace(pp, wrong), single);
            foreach (var wrong in new[] { attack with { Actor = new(1) }, attack with { ChosenMove = Kernel.Id("METRONOME") },
                attack with { CurrentMove = Kernel.Id("ANCIENT_POWER") }, attack with { Turn = attack.Turn + 1 } })
                Deny(events.Replace(attack, wrong), single);
            foreach (var wrong in new[] { hit with { Actor = new(1) }, hit with { Target = new(0) }, hit with { Move = Kernel.Id("ANCIENT_POWER") },
                hit with { Turn = hit.Turn + 1 } }) Deny(events.Replace(hit, wrong), single);
            Deny(events.Remove(pp), single); Deny(events.Remove(attack), single); Deny(events.Remove(hit), single);
        }
    }

    [TestMethod]
    public void IncompleteReorderedForeignOrExtraStatGroupsCannotReplayOrStealTheCombinedWave()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(); bodies[0] = Stages(bodies[0], 6, 6, 6, 6, 6);
            var result = Kernel.Find(Kernel.Frame(bodies, single), 0, 1, QualifyingRoll, "Silver Wind ordered group");
            var events = result.Events; var stats = events.OfType<SourceStatChangedEvent>().ToArray(); Assert.AreEqual(5, stats.Length);
            int first = events.IndexOf(stats[0]); int second = events.IndexOf(stats[1]);
            var malformed = new[] {
                events.Remove(stats[1]),
                events.SetItem(first, stats[1] with { Sequence = stats[0].Sequence }).SetItem(second, stats[0] with { Sequence = stats[1].Sequence }),
                events.Replace(stats[0], stats[0] with { Actor = new(1) }), events.Replace(stats[0], stats[0] with { Target = new(1) }),
                events.Replace(stats[0], stats[0] with { After = 8 }), events.Replace(stats[0], stats[0] with { Turn = stats[0].Turn + 1 }),
                events.Replace(stats[1], stats[1] with { Sequence = stats[0].Sequence }),
                RockStatusAnimationKernelFixture.Number(events.Insert(events.IndexOf(stats[^1]) + 1, stats[^1])) };
            foreach (var invalid in malformed)
            {
                for (int i = 0; i < invalid.Length; i++) Assert.IsNull(Wave(invalid, i, single));
            }
        }
    }

    [TestMethod]
    public void CanonicalGeneratedScriptAndMetadataAreAuthoritiesAndExportedSinglesCannotBorrowSourceSidecar()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(); bodies[0] = Stages(bodies[0], 6, 6, 6, 6, 6);
            var result = Kernel.Find(Kernel.Frame(bodies, single), 0, 1, QualifyingRoll, "Silver Wind metadata and export");
            var events = result.Events; int usedAt = events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single());
            var first = events.OfType<SourceStatChangedEvent>().First(); int statAt = events.IndexOf(first);
            var moves = Kernel.Mechanics.Catalog.Moves; var definition = moves[SilverWind];
            foreach (IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? metadata in new IReadOnlyDictionary<MoveId, PokemonMoveDefinition>?[] {
                null, moves.Remove(SilverWind), moves.SetItem(SilverWind, definition with { SourceMoveId = 246 }) })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Single(events, usedAt), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, usedAt), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(Single(events, usedAt), true, SourceBattleScene.On, moves, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events, usedAt), SourceBattleScene.On, moves, (_, _) => false).Dispatch);
            foreach (IReadOnlyDictionary<MoveId, PokemonMoveDefinition>? metadata in new IReadOnlyDictionary<MoveId, PokemonMoveDefinition>?[] {
                null, moves.Remove(SilverWind), moves.SetItem(SilverWind, definition with { Effect = "EFFECT_HIT" }) })
                Assert.IsNull(single ? AnimationDispatchFixture.StatAtResult(Single(events, statAt), true, metadata)
                    : AnimationDispatchFixture.StatAtResult(Envelope.Double(events, statAt), metadata));
            CheckHit(result, single, Envelope.Hits(events).Single()); CheckWave(events, single, 55);
            if (!single) continue;
            var battle = Envelope.Single(events, usedAt); var pp = events.OfType<SourcePpCommandEvent>().Single(); int ppAt = events.IndexOf(pp);
            var exportedPp = (SourcePpCommandEvent)battle.Events[ppAt];
            Assert.AreEqual(new BattlerSlot(pp.PpOwner.Value), exportedPp.PpOwner);
            var badPp = battle with { Events = battle.Events.SetItem(ppAt, exportedPp with { PpOwner = new BattlerSlot(1) }) };
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(badPp, true, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(badPp with { EventIndex = statAt }, true));
            var exportedStat = (SourceStatChangedEvent)battle.Events[statAt];
            Assert.AreEqual(first.Stat, exportedStat.Stat); Assert.AreEqual(first.Before, exportedStat.Before); Assert.AreEqual(first.After, exportedStat.After);
            var badStat = battle with { EventIndex = statAt, Events = battle.Events.SetItem(statAt, exportedStat with { Target = new BattlerSlot(1) }) };
            Assert.IsNull(AnimationDispatchFixture.StatAtResult(badStat, true), "Malformed canonical stat results must fail closed in the single envelope.");
        }
    }

    [TestMethod]
    public void ActualMetronomeSilverWindWithRealBoostOwnsOneRootAndOneStatWave()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(); bodies[0] = Kernel.Mon("TOGETIC", "METRONOME", 29) with { AccuracyStage = 12 };
            var frame = Kernel.Frame(bodies, single); SourceKernelResult? result = null;
            for (uint seed = 0; seed < 65536; seed++)
            {
                if (WildBattleSourceCallers.Metronome(new(seed)).SourceMoveId != 318) continue;
                var candidate = Kernel.Direct(frame with { Rng = new(seed) });
                if (candidate.Events.OfType<SourceStatChangedEvent>().Count() != 5) continue;
                Console.WriteLine($"Actual Metronome -> Silver Wind boost: action-start seed={seed}"); result = candidate; break;
            }
            Assert.IsNotNull(result, "The bounded source caller search must execute an actual Silver Wind with a boost.");
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Caller == Kernel.Id("METRONOME") && e.Result == SilverWind));
            Assert.IsTrue(result.Events.OfType<SourceAttackStringEvent>().Any(e => e.CurrentMove == SilverWind && e.ChosenMove == Kernel.Id("METRONOME")));
            Assert.AreEqual(1, Envelope.Hits(result.Events).Length); Assert.AreEqual(5, result.Events.OfType<SourceStatChangedEvent>().Count());
            CalledLeafAnimationKernelFixture.Check(result.Events, result, single, Kernel.Id("METRONOME"), 318, Envelope.Hits(result.Events).Single());
            var first = Group(result.Events)[0]; var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            var attack = result.Events.OfType<SourceAttackStringEvent>().Single(e => e.CurrentMove == SilverWind); int waves = 0;
            for (int i = 0; i < result.Events.Length; i++)
            {
                var wave = Wave(result.Events, i, single);
                if (result.Events[i] != first) { Assert.IsNull(wave); continue; }
                Assert.IsNotNull(wave); waves++; Assert.AreEqual(318, wave.SourceMoveId);
                Assert.AreEqual(55, wave.AnimationArgument); Assert.AreEqual(pp.Actor, wave.Affected);
                Assert.AreEqual(first.Sequence, wave.Sequence); Assert.AreEqual(attack.Sequence, wave.AttackSequence);
                Assert.AreEqual(pp.Sequence, wave.PpSequence);
            }
            Assert.AreEqual(1, waves);
        }
    }
}
