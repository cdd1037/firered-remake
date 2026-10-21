using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Caller = FireRed.Tests.Application.WildBattles.CallerPreludeAnimationKernelFixture;
using Shared = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// Public source-kernel calls retain their chosen/leaf/PP identities. Source-natural
// learners and bounded actual RNG branches are fixtures, not route-play evidence.
[TestClass, TestCategory("Game")]
public sealed class CalledStatAnimationKernelTests
{
    private static CampaignStatAnimationDispatch? Wave(ImmutableArray<SourceBattleEvent> events, int index, bool single) =>
        Shared.Wave(events, index, single);
    private static WildBattleCombatant[] Bodies() =>
        Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100)).ToArray();
    private static SourceKernelResult Metronome(string name, bool single, int actor = 0,
        Func<WildBattleCombatant, WildBattleCombatant>? prepare = null, bool requireSecondary = false)
    {
        var id = Kernel.Id(name); int sourceId = Kernel.Mechanics.Catalog.Moves[id].SourceMoveId;
        var bodies = Bodies(); var caller = Kernel.Mon("TOGETIC", "METRONOME", 29) with { AccuracyStage = 12 };
        bodies[actor] = prepare is null ? caller : prepare(caller); var frame = Kernel.Frame(bodies, single);
        // Reuse the real source selector only to bound candidates. The public kernel
        // still selects, executes, consumes PP and commits every tested receipt.
        for (uint seed = 0; seed < 65536; seed++)
        {
            if (WildBattleSourceCallers.Metronome(new(seed)).SourceMoveId != sourceId) continue;
            var result = Envelope.Resolve(frame with { Rng = new(seed) }, actor, actor ^ 1);
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Caller == Kernel.Id("METRONOME") && e.Result == id));
            if (!requireSecondary || result.Events.OfType<SourceStatChangedEvent>().Any()) return result;
        }
        throw new InvalidOperationException($"No actual Metronome/{name} stat branch in the bounded source seed set.");
    }
    private static SourceKernelResult Nature(bool single, WildBattleTerrain terrain, bool secondary)
    {
        var bodies = Bodies(); bodies[0] = Kernel.Mon("LOTAD", "NATURE_POWER", 13) with { AccuracyStage = 12 };
        var frame = Kernel.Frame(bodies, single); frame = frame with { Field = frame.Field with { Terrain = terrain } };
        return secondary ? Envelope.Find(frame, 0, 1, r => r.Events.OfType<SourceStatChangedEvent>().Any(), "Nature Power stat secondary")
            : Envelope.Resolve(frame);
    }
    private static void Check(SourceKernelResult result, bool single, string name,
        params (SourceBattleEvent Result, int Affected, int Argument)[] expected)
    {
        string before = JsonSerializer.Serialize(result); var id = Kernel.Id(name);
        var used = result.Events.OfType<SourceMoveUsedEvent>().Single(e => e.Move == id);
        var attack = result.Events.OfType<SourceAttackStringEvent>().Single(e => e.CurrentMove == id);
        var commands = result.Events.OfType<SourcePpCommandEvent>().Where(e => e.CurrentMove == id).ToArray();
        Assert.AreNotEqual(id, attack.ChosenMove); Assert.IsTrue(commands.Length > 0);
        foreach (var command in commands)
        {
            Assert.AreEqual(attack.ChosenMove, command.ChosenMove); Assert.AreEqual(used.Actor, command.PpOwner);
            Assert.AreEqual(used.Slot, command.PpSlot); Assert.IsTrue(command.CommandCost > 0);
        }
        int found = 0;
        for (int i = 0; i < result.Events.Length; i++)
        {
            var wave = Wave(result.Events, i, single); Assert.AreEqual(wave, Wave(result.Events, i, single));
            var owner = expected.SingleOrDefault(e => e.Result.Sequence == result.Events[i].Sequence);
            if (owner.Result is null) { Assert.IsNull(wave); continue; }
            found++; Assert.IsNotNull(wave); var pp = commands.Last(p => p.Sequence < owner.Result.Sequence);
            Assert.AreEqual(Kernel.Mechanics.Catalog.Moves[id].SourceMoveId, wave.SourceMoveId);
            Assert.AreEqual(owner.Result.Sequence, wave.Sequence); Assert.AreEqual(attack.Sequence, wave.AttackSequence);
            Assert.AreEqual(pp.Sequence, wave.PpSequence); Assert.AreEqual(owner.Affected, wave.Affected.Value);
            Assert.AreEqual(owner.Argument, wave.AnimationArgument);
            if (single)
            {
                var exported = Envelope.Single(result.Events, i);
                Assert.AreEqual(wave, AnimationDispatchFixture.StatAtResult(exported, true), "Canonical receipts carry the caller proof unchanged through the single envelope.");
                Assert.IsNull(AnimationDispatchFixture.StatAtResult(exported, false));
                Assert.IsNull(AnimationDispatchFixture.StatAtResult(exported, true, true));
            }
            else Assert.IsNull(AnimationDispatchFixture.StatAtResult(Envelope.Double(result.Events, i), true));
        }
        Assert.AreEqual(expected.Length, found); Assert.AreEqual(before, JsonSerializer.Serialize(result), "Stat projection preserves receipts, state, RNG and trace.");
    }
    private static void Deny(ImmutableArray<SourceBattleEvent> events, bool single, string reason)
    {
        for (int i = 0; i < events.Length; i++) Assert.IsNull(Wave(events, i, single), reason);
    }

    [TestMethod]
    public void RealMetronomeAccuracyReductionOwnsOnlyTheLeafStatReceiptInEveryPosition()
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var result = Metronome("SMOKESCREEN", single, actor); var stat = result.Events.OfType<SourceStatChangedEvent>().Single();
            Assert.AreEqual(WildBattleStat.Accuracy, stat.Stat); Assert.AreEqual(stat.Before - 1, stat.After);
            Check(result, single, "SMOKESCREEN", (stat, stat.Target.Value, 27));
        }
    }

    [TestMethod]
    public void RealMirrorHistoryOwnsAccuracyWaveWithoutACallerWaveOrLearnedLeafSlot()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(); bodies[0] = Kernel.Mon("TORCHIC", "MIRROR_MOVE", 37) with { AccuracyStage = 12 };
            bodies[1] = Kernel.Mon("CHARMANDER", "SMOKESCREEN", 19) with { AccuracyStage = 12 };
            var previous = Envelope.Resolve(Kernel.Frame(bodies, single), 1, 0);
            Assert.AreEqual(WildBattleStat.Accuracy, previous.Events.OfType<SourceStatChangedEvent>().Single().Stat);
            Assert.AreEqual(Kernel.Id("SMOKESCREEN"), previous.State.Slots[0].Registers.History!.LastTakenMove);
            var result = Envelope.Resolve(previous.State); var stat = result.Events.OfType<SourceStatChangedEvent>().Single();
            Assert.AreEqual(Kernel.Id("MIRROR_MOVE"), result.State.Slots[0].Body!.Pokemon.Moves[0].Move);
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Caller == Kernel.Id("MIRROR_MOVE") && e.Result == Kernel.Id("SMOKESCREEN")));
            Check(result, single, "SMOKESCREEN", (stat, stat.Target.Value, 27));
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void SleepTalkSelfDefenseWaveKeepsCallerSpendAndRejectsExhaustedLeaf(bool exhaustedLeaf)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Caller.Bodies(214, sleepingLeaf: "DEFENSE_CURL");
            foreach (int slot in new[] { 1, 3 }) bodies[slot] = Kernel.Mon("ARTICUNO", "MIST", 100);
            if (exhaustedLeaf)
            {
                var body = bodies[0]; bodies[0] = body with { Pokemon = body.Pokemon with {
                    Moves = body.Pokemon.Moves.SetItem(1, body.Pokemon.Moves[1] with { CurrentPp = 0 }) } };
            }
            var frame = Kernel.Frame(bodies, single); var result = Envelope.Resolve(frame);
            var caller = result.Events.OfType<SourcePpCommandEvent>().Single(e => e.CurrentMove == Kernel.Id("SLEEP_TALK"));
            Assert.AreEqual(2, caller.CommandCost); Assert.AreEqual(caller.Before - 2, caller.After);
            if (exhaustedLeaf)
            {
                // Selection permits zero PP, but the real late attackcanceler
                // stops this leaf before MoveUsed/ppreduce or a stat result.
                Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Result == Kernel.Id("DEFENSE_CURL")));
                Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Move == Kernel.Id("DEFENSE_CURL") && e.Reason == "no-pp"));
                Assert.IsFalse(result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Move == Kernel.Id("DEFENSE_CURL")));
                Assert.IsFalse(result.Events.OfType<SourceStatChangedEvent>().Any());
                Assert.AreEqual(1, result.Events.OfType<SourcePpCommandEvent>().Count());
                Assert.AreEqual(0, result.State.Slots[0].Body!.Pokemon.Moves[1].CurrentPp);
                Deny(result.Events, single, "A selected but cancelled leaf cannot own a stat wave.");
                continue;
            }
            var stat = result.Events.OfType<SourceStatChangedEvent>().Single();
            var leaf = result.Events.OfType<SourcePpCommandEvent>().Single(e => e.CurrentMove == Kernel.Id("DEFENSE_CURL"));
            Assert.AreEqual(1, leaf.PpSlot); Assert.AreEqual(1, leaf.CommandCost); Assert.AreEqual(leaf.Before, leaf.After);
            Assert.AreEqual(frame.Slots[0].Body!.Pokemon.Moves[1], result.State.Slots[0].Body!.Pokemon.Moves[1]);
            Assert.AreEqual(frame.Slots[0].Body!.Pokemon.Moves[0].CurrentPp - 2, result.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp);
            Check(result, single, "DEFENSE_CURL", (stat, 0, 16));
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void CalledCalmMindKeepsFirstResultOwnershipAndSilentIndividualCap(bool firstCapped)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Metronome("CALM_MIND", single, prepare: b => b with { SpecialAttackStageDelta = firstCapped ? 6 : 0 });
            var outcomes = result.Events.Where(e => e is SourceStatChangedEvent or SourceStatAtMaximumEvent).ToArray();
            Assert.AreEqual(2, outcomes.Length); Assert.AreEqual(firstCapped, outcomes[0] is SourceStatAtMaximumEvent);
            Check(result, single, "CALM_MIND", (outcomes[0], 0, firstCapped ? 19 : 55));
            int index = result.Events.IndexOf(outcomes[0]);
            Assert.AreEqual(firstCapped, single ? CampaignStatAnimationPresentation.IsSilentResultReceipt(Envelope.Single(result.Events, index), true, Kernel.Mechanics.Catalog.Moves)
                : CampaignStatAnimationPresentation.IsSilentResultReceipt(Envelope.Double(result.Events, index), Kernel.Mechanics.Catalog.Moves));
            Assert.AreEqual(outcomes[0].Sequence, single ? CampaignStatAnimationPresentation.GroupedMoveResultSequence(Envelope.Single(result.Events, index), true, Kernel.Mechanics.Catalog.Moves)
                : CampaignStatAnimationPresentation.GroupedMoveResultSequence(Envelope.Double(result.Events, index), Kernel.Mechanics.Catalog.Moves));
            Deny(result.Events.Remove(outcomes[1]), single, "An incomplete caller-owned grouped result cannot borrow a wave.");
        }
    }

    [TestMethod, DataRow(WildBattleTerrain.Cave, "SHADOW_BALL", WildBattleStat.SpecialDefense, 26),
        DataRow(WildBattleTerrain.Pond, "BUBBLE_BEAM", WildBattleStat.Speed, 24)]
    public void NaturePowerDamageSecondaryKeepsTheSuccessfulLeafHitPrefix(WildBattleTerrain terrain, string name, WildBattleStat expected, int argument)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Nature(single, terrain, true); var stat = result.Events.OfType<SourceStatChangedEvent>().Single();
            Assert.AreEqual(expected, stat.Stat); Assert.AreEqual(Kernel.Id("NATURE_POWER"), result.State.Slots[0].Body!.Pokemon.Moves[0].Move);
            Check(result, single, name, (stat, stat.Target.Value, argument));
            var hit = result.Events.OfType<SourceHpChangedEvent>().Single();
            Deny(result.Events.Remove(hit), single, "The authentic caller chain cannot substitute for a missing successful hit.");
        }
    }

    [TestMethod]
    public void NaturePowerPrimaryStatusDoesNotInventAStatWave()
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Nature(single, WildBattleTerrain.Grass, false);
            Assert.AreEqual(WildBattleStatus.Paralysis, result.Events.OfType<SourceStatusChangedEvent>().Single().After);
            Check(result, single, "STUN_SPORE");
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void CalledAncientPowerStillRequiresAllFiveOrderedOutcomesForOneWave(bool firstCapped)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Metronome("ANCIENT_POWER", single, prepare: b => b with { AttackStage = firstCapped ? 12 : 6 }, requireSecondary: true);
            var outcomes = result.Events.Where(e => e is SourceStatChangedEvent or SourceStatAtMaximumEvent).ToArray();
            Assert.AreEqual(5, outcomes.Length); Assert.AreEqual(firstCapped, outcomes[0] is SourceStatAtMaximumEvent);
            Check(result, single, "ANCIENT_POWER", (outcomes[0], 0, 55));
            Deny(result.Events.Remove(outcomes[^1]), single, "A called secondary still needs the entire source all-stat group.");
        }
    }

    [TestMethod, DataRow("SMOKESCREEN"), DataRow("CALM_MIND"), DataRow("SHADOW_BALL")]
    public void ForgedCallerProofCannotAuthorizePrimaryGroupedOrSecondaryWaves(string name)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = name == "SHADOW_BALL" ? Nature(single, WildBattleTerrain.Cave, true) : Metronome(name, single);
            var events = result.Events; var selection = events.OfType<SourceCallerSelectionEvent>().Single();
            var transition = events.OfType<SourceCalledMoveTransitionEvent>().Single(); var call = transition.Invocation;
            int si = events.IndexOf(selection), ti = events.IndexOf(transition);
            var variants = new List<ImmutableArray<SourceBattleEvent>> {
                events.Remove(selection), events.Remove(transition), Shared.Number(events.Insert(si + 1, selection)),
                Shared.Number(events.Insert(ti + 1, transition)), Shared.Number(events.SetItem(si, transition).SetItem(ti, selection)),
                events.SetItem(si, selection with { Actor = new(1) }), events.SetItem(si, selection with { Caller = Kernel.Id("TACKLE") }),
                events.SetItem(si, selection with { Result = Kernel.Id("TACKLE") }), events.SetItem(si, selection with { Turn = selection.Turn + 1 })
            };
            foreach (var wrong in new[] {
                call with { Actor = new(1) }, call with { PpOwner = new(1) }, call with { Target = new(call.Target.Value ^ 1) },
                call with { ChosenMove = Kernel.Id("TACKLE") }, call with { OriginalChosenMove = Kernel.Id("TACKLE") },
                call with { CurrentMove = Kernel.Id("TACKLE") }, call with { ChosenSlot = 3 }, call with { PpSlot = 3 },
                call with { CalledMove = Kernel.Id("TACKLE") }, call with { Flags = call.Flags | WildBattleInvocationFlags.NoPpDeduct }
            }) variants.Add(events.SetItem(ti, transition with { Invocation = wrong }));
            foreach (var malformed in variants) Deny(malformed, single, $"{name}: unauthenticated caller identity cannot authorize a stat wave.");
        }
    }

    [TestMethod]
    public void BareCalledHeaderAndPpIdentityCannotImpersonateAnAuthenticatedChain()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(); bodies[0] = Kernel.Mon("CHARMANDER", "SMOKESCREEN", 19) with { AccuracyStage = 12 };
            var events = Envelope.Resolve(Kernel.Frame(bodies, single)).Events;
            var attack = events.OfType<SourceAttackStringEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single();
            Deny(events.Replace(attack, attack with { ChosenMove = Kernel.Id("METRONOME") })
                .Replace(pp, pp with { ChosenMove = Kernel.Id("METRONOME") }), single, "Chosen/current disagreement needs real source caller evidence.");
        }
    }
}
