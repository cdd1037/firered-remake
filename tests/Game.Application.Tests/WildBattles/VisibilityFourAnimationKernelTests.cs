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
public sealed class VisibilityFourAnimationKernelTests
{
    private static (string Name, string Species, string Effect) Spec(int id) => id switch {
        185 => ("FAINT_ATTACK", "MEOWTH", "EFFECT_ALWAYS_HIT"), 293 => ("CAMOUFLAGE", "STARYU", "EFFECT_CAMOUFLAGE"),
        316 => ("ODOR_SLEUTH", "GROWLITHE", "EFFECT_FORESIGHT"), 347 => ("CALM_MIND", "ALAKAZAM", "EFFECT_CALM_MIND"),
        _ => throw new ArgumentOutOfRangeException(nameof(id)) };
    private static MoveId Move(int id) => Kernel.Id(Spec(id).Name);
    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100)).ToArray();
        var spec = Spec(id); bodies[actor] = Kernel.Mon(spec.Species, spec.Name) with { AccuracyStage = 12, SpeedStage = 12 }; return bodies;
    }
    private static SourceBattleFrame Frame(WildBattleCombatant[] bodies, bool single)
    {
        var frame = Kernel.Frame(bodies, single); return frame with { Field = frame.Field with { Terrain = WildBattleTerrain.Grass } };
    }
    private static int Target(int id, int actor = 0) => id is 293 or 347 ? actor : actor ^ 1;
    private static SourceBattleEvent Marker(ImmutableArray<SourceBattleEvent> events, int id) => id switch {
        185 => Envelope.Hits(events).Single(),
        293 => events.OfType<SourceCalledOverlayChangedEvent>().Single(e => e.Move == Move(id)),
        316 => events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "identified"),
        347 => events.First(e => e is SourceStatChangedEvent or SourceStatAtMaximumEvent),
        _ => throw new ArgumentOutOfRangeException(nameof(id)) };
    private static bool Silent(ImmutableArray<SourceBattleEvent> events, int at, bool single) => single
        ? CampaignStatAnimationPresentation.IsSilentResultReceipt(Envelope.Single(events, at), true, Kernel.Mechanics.Catalog.Moves)
        : CampaignStatAnimationPresentation.IsSilentResultReceipt(Envelope.Double(events, at), Kernel.Mechanics.Catalog.Moves);
    private static void Deny(ImmutableArray<SourceBattleEvent> events, bool single)
    {
        Shared.Deny(events, single);
        for (int i = 0; i < events.Length; i++) Assert.IsNull(Shared.Wave(events, i, single));
    }
    private static void CheckCalm(SourceKernelResult result, bool single, int attack, int defense)
    {
        var events = result.Events; bool succeeds = attack < 12 || defense < 12;
        var outcomes = events.Where(e => e is SourceStatChangedEvent or SourceStatAtMaximumEvent).ToArray();
        Assert.AreEqual(succeeds ? 2 : 0, outcomes.Length);
        if (!succeeds)
        {
            Assert.IsTrue(events.OfType<SourceMoveFailedEvent>().Any(e => e.Move == Move(347) && e.Reason == "stats-at-maximum"));
            Shared.Check(result, 347, single, null); Deny(events, single); return;
        }
        AssertOutcome(outcomes[0], WildBattleStat.SpecialAttack, attack);
        AssertOutcome(outcomes[1], WildBattleStat.SpecialDefense, defense);
        Shared.Check(result, 347, single, outcomes[0]);
        var used = events.OfType<SourceMoveUsedEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(events.IndexOf(pp) + 1, events.IndexOf(outcomes[0]));
        Assert.AreEqual(events.IndexOf(outcomes[0]) + 1, events.IndexOf(outcomes[1]));
        int argument = attack < 12 && defense < 12 ? 55 : attack < 12 ? 18 : 19;
        for (int i = 0; i < events.Length; i++)
        {
            var wave = Shared.Wave(events, i, single);
            if (events[i] == outcomes[0])
            {
                Assert.IsNotNull(wave); Assert.AreEqual(347, wave.SourceMoveId); Assert.AreEqual(argument, wave.AnimationArgument);
                Assert.AreEqual(used.Actor, wave.Affected); Assert.AreEqual(outcomes[0].Sequence, wave.Sequence); Assert.AreEqual(pp.Sequence, wave.PpSequence);
            }
            else Assert.IsNull(wave);
            // Calm Mind has explicit chooser jumps after BOTH statbuffchange commands
            // (battle_scripts_1.s2745/2751), so individual cap receipts are silent.
            Assert.AreEqual(events[i] is SourceStatAtMaximumEvent, Silent(events, i, single));
        }
        static void AssertOutcome(SourceBattleEvent row, WildBattleStat stat, int before)
        {
            if (before == 12) { Assert.IsInstanceOfType<SourceStatAtMaximumEvent>(row); Assert.AreEqual(stat, ((SourceStatAtMaximumEvent)row).Stat); }
            else { Assert.IsInstanceOfType<SourceStatChangedEvent>(row); var changed = (SourceStatChangedEvent)row; Assert.AreEqual(stat, changed.Stat); Assert.AreEqual(before, changed.Before); Assert.AreEqual(before + 1, changed.After); }
        }
    }

    [TestMethod, DataRow(185), DataRow(293), DataRow(316), DataRow(347)]
    public void NaturalVisibilityRootsUseExistingOwnedReceiptsAcrossAllPositionsAndSelectedAllies(int id)
    {
        var definition = Kernel.Mechanics.Catalog.Moves[Move(id)]; Assert.AreEqual(id, definition.SourceMoveId); Assert.AreEqual(Spec(id).Effect, definition.Effect);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in !single && (id is 185 or 316) ? new[] { actor ^ 1, actor ^ 2 } : new[] { Target(id, actor) })
        {
            var result = Envelope.Resolve(Frame(Bodies(id, actor), single), actor, target);
            if (id == 347) CheckCalm(result, single, 6, 6); else Shared.Check(result, id, single, Marker(result.Events, id));
        }
    }

    [TestMethod]
    public void FaintAttackIgnoresAccuracyAndEvasionButStillOwnsItsActualSubstituteHit()
    {
        foreach (bool single in new[] { false, true }) foreach (bool substitute in new[] { false, true })
        {
            var bodies = Bodies(185); bodies[0] = bodies[0] with { AccuracyStage = 0 };
            bodies[1] = bodies[1] with { EvasionStage = 12, CalledVolatile = substitute ? new() { SubstituteHp = 1 } : null };
            var result = Envelope.Resolve(Frame(bodies, single));
            Assert.IsFalse(result.Events.OfType<SourceMoveMissedEvent>().Any());
            Assert.IsFalse(result.Trace.Any(t => t.Purpose.Contains("accuracy", StringComparison.Ordinal)));
            Assert.AreEqual(substitute, Marker(result.Events, 185) is SourceSubstituteDamagedEvent); Shared.Check(result, 185, single, Marker(result.Events, 185));
        }
    }

    [TestMethod, DataRow("protect"), DataRow("dig"), DataRow("fly")]
    public void FaintAttackRetainsProtectAndActualSemiInvulnerableFailures(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(185); var frame = Frame(bodies, single);
            if (branch == "protect") { bodies[1] = bodies[1] with { Protected = true }; frame = Frame(bodies, single); }
            else
            {
                string move = branch == "dig" ? "DIG" : "FLY"; bodies[1] = Kernel.Mon(branch == "dig" ? "DIGLETT" : "RAYQUAZA", move, 100);
                frame = Envelope.Resolve(Frame(bodies, single), 1, 0).State;
                Assert.AreEqual(Kernel.Id(move), frame.Slots[1].Registers.Turn!.Lock!.Move);
            }
            var result = Envelope.Resolve(frame); Assert.IsTrue(result.Events.Any(e => e is SourceMoveMissedEvent or SourceMoveFailedEvent));
            Shared.Check(result, 185, single, null);
        }
    }

    [TestMethod, DataRow(0, "TYPE_GRASS"), DataRow(1, "TYPE_GRASS"), DataRow(2, "TYPE_GROUND"),
        DataRow(3, "TYPE_WATER"), DataRow(4, "TYPE_WATER"), DataRow(5, "TYPE_WATER"), DataRow(6, "TYPE_ROCK"),
        DataRow(7, "TYPE_ROCK"), DataRow(8, "TYPE_NORMAL"), DataRow(9, "TYPE_NORMAL")]
    public void CamouflageUsesTheCommittedNativeTerrainTypeAndRejectsAlreadyHeldTypes(int terrain, string type)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Frame(Bodies(293), single); frame = frame with { Field = frame.Field with { Terrain = (WildBattleTerrain)terrain } };
            var result = Envelope.Resolve(frame, 0, 0);
            Assert.AreEqual(frame.Rng, result.State.Rng); Assert.AreEqual(0, result.Trace.Length);
            if (type == "TYPE_WATER")
            {
                Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "already-type"));
                Assert.IsFalse(result.Events.OfType<SourceCalledOverlayChangedEvent>().Any()); Shared.Check(result, 293, single, null); continue;
            }
            var overlay = (SourceCalledOverlayChangedEvent)Marker(result.Events, 293);
            Assert.AreEqual(type, overlay.After!.Type1); Assert.AreEqual(type, overlay.After.Type2); Assert.IsNull(overlay.Source);
            Shared.Check(result, 293, single, overlay);
            var repeated = Envelope.Resolve(result.State, 0, 0);
            Assert.IsTrue(repeated.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "already-type"));
            Assert.IsFalse(repeated.Events.OfType<SourceCalledOverlayChangedEvent>().Any()); Shared.Check(repeated, 293, single, null);
        }
    }

    [TestMethod]
    public void CamouflageMissingOrNonNativeTerrainFailsTransactionallyWithoutInventingAType()
    {
        foreach (WildBattleTerrain? terrain in new WildBattleTerrain?[] { null, WildBattleTerrain.Link, WildBattleTerrain.Champion })
        {
            var frame = Frame(Bodies(293), false); frame = frame with { Field = frame.Field with { Terrain = terrain } };
            string before = JsonSerializer.Serialize(frame);
            var result = SourceBattleKernel.ResolveAction(Kernel.Mechanics.Catalog, frame, new(frame.Slots[0].Occupant!.Value, frame.Revision, 0, new(0)));
            Assert.IsFalse(result.Succeeded); Assert.AreEqual(0, result.Events.Length); Assert.AreEqual(0, result.Trace.Length);
            Assert.AreSame(frame, result.State); Assert.AreEqual(before, JsonSerializer.Serialize(frame));
        }
    }

    [TestMethod, DataRow("repeat"), DataRow("substitute"), DataRow("ghost"), DataRow("safeguard")]
    public void OdorSleuthReidentifiesAndBypassesSubstituteTypeAndSafeguard(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(316); bodies[1] = branch switch {
                "repeat" => bodies[1] with { Identified = true }, "substitute" => bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } },
                "ghost" => Kernel.Mon("GASTLY", "LICK", 100), _ => bodies[1] };
            var frame = Frame(bodies, single);
            if (branch == "safeguard") frame = frame with { Sides = frame.Sides.SetItem(1, frame.Sides[1] with { SafeguardTurnsRemaining = 5 }) };
            var result = Envelope.Resolve(frame); var marker = (SourceMtMoonVolatileEvent)Marker(result.Events, 316);
            Assert.AreEqual(branch == "repeat" ? 1 : 0, marker.Before); Assert.AreEqual(1, marker.After); Assert.AreEqual("move", marker.Reason);
            Assert.IsTrue(result.State.Slots[1].Body!.Identified); Shared.Check(result, 316, single, marker);
        }
    }

    [TestMethod, DataRow("protect"), DataRow("miss")]
    public void OdorSleuthFailureCannotBorrowTheTargetsPreviouslyIdentifiedState(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(316); bodies[1] = bodies[1] with { Identified = true, Protected = branch == "protect", EvasionStage = 12 };
            bodies[0] = bodies[0] with { AccuracyStage = 0 }; var frame = Frame(bodies, single);
            var result = branch == "miss" ? Envelope.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "Odor Sleuth miss") : Envelope.Resolve(frame);
            Assert.IsTrue(result.State.Slots[1].Body!.Identified); Assert.IsFalse(result.Events.OfType<SourceMtMoonVolatileEvent>().Any(e => e.Volatile == "identified"));
            Shared.Check(result, 316, single, null);
        }
    }

    [TestMethod, DataRow(6, 6), DataRow(12, 6), DataRow(6, 12), DataRow(12, 12)]
    public void CalmMindHasOneOrderedGroupWaveWithSilentIndividualCapsAndNoBothCappedRoot(int attack, int defense)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(347); bodies[0] = bodies[0] with { SpecialAttackStageDelta = attack - 6, SpecialDefenseStageDelta = defense - 6 };
            var frame = Frame(bodies, single); var result = Envelope.Resolve(frame, 0, 0); CheckCalm(result, single, attack, defense);
            Assert.AreEqual(frame.Rng, result.State.Rng); Assert.AreEqual(0, result.Trace.Length);
            Assert.AreEqual(Math.Min(12, attack + 1) - 6, result.State.Slots[0].Body!.SpecialAttackStageDelta);
            Assert.AreEqual(Math.Min(12, defense + 1) - 6, result.State.Slots[0].Body!.SpecialDefenseStageDelta);
        }
    }

    [TestMethod, DataRow(185), DataRow(293), DataRow(316), DataRow(347)]
    public void VisibilityRootsRejectMissingForeignDuplicateOrCalledEnvelopeOwnership(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Frame(Bodies(id), single), 0, Target(id)).Events; var marker = Marker(events, id); int mi = events.IndexOf(marker);
            var used = events.OfType<SourceMoveUsedEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single(); int pi = events.IndexOf(pp);
            var attack = events.OfType<SourceAttackStringEvent>().Single();
            Deny(events.Remove(marker), single); Deny(Shared.Number(events.Insert(mi + 1, marker)), single);
            Deny(events.SetItem(mi, marker with { Turn = marker.Turn + 1 }), single);
            Deny(events.SetItem(mi, marker with { Sequence = pp.Sequence }), single);
            foreach (var bad in new[] { pp with { PpOwner = new(1) }, pp with { PpSlot = 3 }, pp with { ChosenMove = Kernel.Id("METRONOME") }, pp with { CommandCost = 0 } })
                Deny(events.SetItem(pi, bad), single);
            Deny(events.SetItem(events.IndexOf(attack), attack with { ChosenMove = Kernel.Id("METRONOME") }), single);
            if (marker is SourceHpChangedEvent hit)
                foreach (var bad in new[] { hit with { Actor = new(1) }, hit with { Target = new(0) }, hit with { Move = Kernel.Id("TACKLE") } }) Deny(events.SetItem(mi, bad), single);
            if (marker is SourceCalledOverlayChangedEvent overlay)
            {
                // Source is a canonical-only field; the historical Wild overlay
                // envelope cannot carry this mutation. Preserve its doubles negative.
                if (!single) Deny(events.SetItem(mi, overlay with { Source = new(1) }), false);
                foreach (var bad in new[] { overlay with { Side = new(1) }, overlay with { Move = Kernel.Id("CONVERSION") },
                    overlay with { After = null }, overlay with { Before = overlay.After }, overlay with { After = overlay.After! with { Type2 = "TYPE_MYSTERY" } },
                    overlay with { After = overlay.After! with { Ability = "ABILITY_FORECAST" } } }) Deny(events.SetItem(mi, bad), single);
            }
            if (marker is SourceMtMoonVolatileEvent identified)
                foreach (var bad in new[] { identified with { Side = new(0) }, identified with { Before = 2 }, identified with { After = 0 },
                    identified with { Reason = "countdown" }, identified with { Volatile = "sure-hit" } }) Deny(events.SetItem(mi, bad), single);
            if (marker is SourceStatChangedEvent stat)
            {
                foreach (var bad in new[] { stat with { Actor = new(1) }, stat with { Target = new(1) }, stat with { Stat = WildBattleStat.Attack }, stat with { After = stat.Before + 2 } }) Deny(events.SetItem(mi, bad), single);
                var second = events.OfType<SourceStatChangedEvent>().Last(); int si = events.IndexOf(second);
                Deny(events.Remove(second), single); Deny(Shared.Number(events.SetItem(mi, second).SetItem(si, stat)), single);
                Deny(Shared.Number(events.Insert(si + 1, second)), single);
            }
            if (single)
            {
                var exported = Envelope.Single(events, events.IndexOf(used)) with { SourceEvents = [] };
                Assert.IsNotNull(AnimationDispatchFixture.AtAttackText(exported, true, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(exported with { Events = exported.Events.RemoveAt(mi) }, true, SourceBattleScene.On).Dispatch);
            }
            var moves = Kernel.Mechanics.Catalog.Moves;
            foreach (var metadata in new[] { null, moves.Remove(Move(id)), moves.SetItem(Move(id), moves[Move(id)] with { SourceMoveId = 1 }) })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Envelope.Single(events, events.IndexOf(used)), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, events.IndexOf(used)), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(Envelope.Single(events, events.IndexOf(used)), true, SourceBattleScene.On, moves, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events, events.IndexOf(used)), SourceBattleScene.On, moves, (_, _) => false).Dispatch);
        }
    }

    [TestMethod, DataRow(185), DataRow(293), DataRow(316), DataRow(347)]
    public void ActualMetronomeVisibilityLeavesOwnTheirRootAndCalmMindWave(int id)
    {
        uint seed = CallerPreludeAnimationKernelFixture.MetronomeSeed(id);
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[0] = Kernel.Mon("TOGETIC", "METRONOME", 29) with { AccuracyStage = 12 };
            var result = Envelope.Resolve(Frame(bodies, single) with { Rng = new(seed) });
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Result == Move(id)));
            Assert.IsNotNull(Marker(result.Events, id)); CalledLeafAnimationKernelFixture.Check(result.Events, result, single, Kernel.Id("METRONOME"), id, Marker(result.Events, id));
            var owner = Marker(result.Events, id); var pp = result.Events.OfType<SourcePpCommandEvent>().Single(); int waves = 0;
            for (int i = 0; i < result.Events.Length; i++)
            {
                var wave = Shared.Wave(result.Events, i, single);
                if (id != 347 || result.Events[i] != owner) { Assert.IsNull(wave); continue; }
                Assert.IsNotNull(wave); waves++; Assert.AreEqual(347, wave.SourceMoveId);
                Assert.AreEqual(55, wave.AnimationArgument); Assert.AreEqual(pp.Actor, wave.Affected);
                Assert.AreEqual(owner.Sequence, wave.Sequence); Assert.AreEqual(pp.Sequence, wave.PpSequence);
            }
            Assert.AreEqual(id == 347 ? 1 : 0, waves);
        }
    }
}
