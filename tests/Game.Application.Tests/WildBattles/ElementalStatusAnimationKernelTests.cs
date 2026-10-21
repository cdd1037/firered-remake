using System.Collections.Immutable;
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
public sealed class ElementalStatusAnimationKernelTests
{
    private static (string Name, string Species, string Effect) Spec(int id) => id switch {
        109 => ("CONFUSE_RAY", "ZUBAT", "EFFECT_CONFUSE"), 142 => ("LOVELY_KISS", "JYNX", "EFFECT_SLEEP"),
        186 => ("SWEET_KISS", "PICHU", "EFFECT_CONFUSE"), 193 => ("FORESIGHT", "VENONAT", "EFFECT_FORESIGHT"),
        _ => throw new ArgumentOutOfRangeException(nameof(id)) };
    private static MoveId Move(int id) => Kernel.Id(Spec(id).Name);
    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        var spec = Spec(id); bodies[actor] = Kernel.Mon(spec.Species, spec.Name) with { AccuracyStage = 12, SpeedStage = 12 }; return bodies;
    }
    private static SourceBattleEvent Marker(ImmutableArray<SourceBattleEvent> events, int id) => id switch {
        142 => events.OfType<SourceStatusChangedEvent>().Single(e => e.Reason == "move"),
        193 => events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "identified"),
        _ => events.OfType<SourceConfusionChangedEvent>().Single(e => e.Reason == "move") };

    [TestMethod, DataRow(109), DataRow(142), DataRow(186), DataRow(193)]
    public void NaturalSelectedStatusRootsOwnExactlyOneCueAcrossAllSlotsSinglesAndAllies(int id)
    {
        var definition = Kernel.Mechanics.Catalog.Moves[Move(id)]; Assert.AreEqual(id, definition.SourceMoveId);
        Assert.AreEqual(Spec(id).Effect, definition.Effect); Assert.AreEqual("MOVE_TARGET_SELECTED", definition.Target);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 2 })
        {
            var result = Envelope.Resolve(Kernel.Frame(Bodies(id, actor), single), actor, target); var marker = Marker(result.Events, id);
            Assert.IsFalse(result.Events.OfType<SourceMoveFailedEvent>().Any()); Shared.Check(result, id, single, marker);
            for (int i = 0; i < result.Events.Length; i++) Assert.IsNull(Shared.Wave(result.Events, i, single));
            if (marker is SourceConfusionChangedEvent confusion)
            { Assert.AreEqual(target, confusion.Side.Value); Assert.AreEqual(0, confusion.Before); Assert.IsTrue(confusion.After is >= 2 and <= 5); }
            if (marker is SourceStatusChangedEvent sleep)
            { Assert.AreEqual(target, sleep.Side.Value); Assert.AreEqual(0u, sleep.Before); Assert.IsTrue(WildBattleStatus.IsSleeping(sleep.After)); }
            if (marker is SourceMtMoonVolatileEvent identified)
            { Assert.AreEqual(target, identified.Side.Value); Assert.AreEqual(0, identified.Before); Assert.AreEqual(1, identified.After); }
        }
    }

    [TestMethod, DataRow(109), DataRow(142), DataRow(186), DataRow(193)]
    public void SelectedStatusRejectsMissingDuplicateForeignAndCalledSetupReceipts(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Kernel.Frame(Bodies(id), single)).Events; var marker = Marker(events, id); int at = events.IndexOf(marker);
            var used = events.OfType<SourceMoveUsedEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single();
            var attack = events.OfType<SourceAttackStringEvent>().Single(); int usedAt = events.IndexOf(used), ppAt = events.IndexOf(pp);
            Shared.Deny(events.RemoveAt(at), single); Shared.Deny(Shared.Number(events.Insert(at + 1, marker)), single);
            Shared.Deny(events.SetItem(at, marker with { Turn = marker.Turn + 1 }), single);
            Shared.Deny(events.SetItem(at, marker with { Sequence = pp.Sequence }), single);
            Shared.Deny(events.SetItem(ppAt, pp with { PpOwner = new(1) }), single);
            Shared.Deny(events.SetItem(ppAt, pp with { PpSlot = 1 }), single);
            Shared.Deny(events.SetItem(ppAt, pp with { ChosenMove = Kernel.Id("METRONOME") }), single);
            Shared.Deny(events.SetItem(events.IndexOf(attack), attack with { ChosenMove = Kernel.Id("METRONOME") }), single);
            Shared.Deny(events.SetItem(usedAt, used with { Target = used.Actor }).SetItem(ppAt, pp with { Target = used.Actor }), single);
            if (id != 142)
            {
                Assert.AreEqual(ppAt + 1, at); Assert.AreEqual(pp.Sequence + 1, marker.Sequence);
                Shared.Deny(events.SetItem(ppAt, marker with { Sequence = pp.Sequence }).SetItem(at, pp with { Sequence = marker.Sequence }), single);
                Shared.Deny(Shared.Number(events.Insert(at, new SourceMtMoonVolatileEvent(0, marker.Turn, used.Target, "unrelated", 0, 1, "move"))), single);
            }
            if (marker is SourceConfusionChangedEvent confusion)
                foreach (var wrong in new[] { confusion with { Side = used.Actor }, confusion with { Before = 1 }, confusion with { After = 1 },
                    confusion with { After = 6 }, confusion with { Reason = "countdown" } }) Shared.Deny(events.SetItem(at, wrong), single);
            if (marker is SourceStatusChangedEvent sleep)
                foreach (var wrong in new[] { sleep with { Side = used.Actor }, sleep with { Before = WildBattleStatus.Burn },
                    sleep with { After = WildBattleStatus.Paralysis }, sleep with { Reason = "rest" } }) Shared.Deny(events.SetItem(at, wrong), single);
            if (marker is SourceMtMoonVolatileEvent identified)
                foreach (var wrong in new[] { identified with { Side = used.Actor }, identified with { Before = -1 }, identified with { Before = 2 },
                    identified with { After = 0 }, identified with { Volatile = "sure-hit" }, identified with { Reason = "countdown" } }) Shared.Deny(events.SetItem(at, wrong), single);
            if (single)
            {
                var battle = Envelope.Single(events, usedAt); Assert.IsNotNull(AnimationDispatchFixture.AtAttackText(battle, true, SourceBattleScene.On).Dispatch);
                var exported = battle.Events[at];
                SourceBattleEvent changed = exported switch {
                    SourceConfusionChangedEvent e => e with { Before = 1 }, SourceStatusChangedEvent e => e with { Before = WildBattleStatus.Burn },
                    SourceMtMoonVolatileEvent e => e with { After = 0 }, _ => throw new InvalidOperationException() };
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle with { Events = battle.Events.SetItem(at, changed) }, true, SourceBattleScene.On).Dispatch);
            }
            var definitions = Kernel.Mechanics.Catalog.Moves; var definition = definitions[Move(id)];
            foreach (var wrong in new[] { definition with { Power = 1 }, definition with { Target = "MOVE_TARGET_USER" } })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Envelope.Single(events, usedAt), true, SourceBattleScene.On, definitions.SetItem(Move(id), wrong)).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, usedAt), SourceBattleScene.On, definitions.SetItem(Move(id), wrong)).Dispatch);
        }
    }

    [TestMethod, DataRow(109), DataRow(186)]
    public void PrimaryConfusionUsesActualOwnTempoSafeguardSubstituteAndAccuracyPrevention(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (string branch in new[] { "own-tempo", "safeguard", "substitute", "already-confused", "protect", "miss" })
        {
            var bodies = Bodies(id); bodies[1] = branch switch {
                "own-tempo" => Kernel.Mon("SPINDA", "TACKLE", 100),
                "substitute" => bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } },
                "already-confused" => bodies[1] with { ConfusionTurnsRemaining = 3 },
                "protect" => bodies[1] with { Protected = true }, _ => bodies[1] };
            if (branch == "own-tempo") Assert.AreEqual("ABILITY_OWN_TEMPO", WildBattleEffective.Ability(bodies[1]));
            if (branch == "miss") bodies[0] = bodies[0] with { AccuracyStage = 0 };
            var frame = Kernel.Frame(bodies, single);
            if (branch == "safeguard") frame = frame with { Sides = frame.Sides.SetItem(1, frame.Sides[1] with { SafeguardTurnsRemaining = 5 }) };
            var result = branch == "miss" ? Envelope.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "primary confusion miss") : Envelope.Resolve(frame);
            Assert.IsFalse(result.Events.OfType<SourceConfusionChangedEvent>().Any(e => e.Reason == "move")); Shared.Check(result, id, single, null);
        }
    }

    [TestMethod, DataRow("insomnia"), DataRow("safeguard"), DataRow("substitute"), DataRow("already-asleep"), DataRow("existing-status"), DataRow("protect"), DataRow("miss")]
    public void LovelyKissUsesActualPrimarySleepPrevention(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(142); bodies[1] = branch switch {
                "insomnia" => Kernel.Mon("DROWZEE", "HYPNOSIS", 100),
                "substitute" => bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } },
                "already-asleep" => bodies[1] with { Status = 3 }, "existing-status" => bodies[1] with { Status = WildBattleStatus.Burn },
                "protect" => bodies[1] with { Protected = true }, _ => bodies[1] };
            if (branch == "insomnia") Assert.AreEqual("ABILITY_INSOMNIA", WildBattleEffective.Ability(bodies[1]));
            if (branch == "miss") bodies[0] = bodies[0] with { AccuracyStage = 0 };
            var frame = Kernel.Frame(bodies, single);
            if (branch == "safeguard") frame = frame with { Sides = frame.Sides.SetItem(1, frame.Sides[1] with { SafeguardTurnsRemaining = 5 }) };
            var result = branch == "miss" ? Envelope.Find(frame, 0, 1,
                r => r.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "accuracy") || r.Events.OfType<SourceMoveMissedEvent>().Any(), "Lovely Kiss miss") : Envelope.Resolve(frame);
            Assert.IsFalse(result.Events.OfType<SourceStatusChangedEvent>().Any(e => e.Reason == "move")); Shared.Check(result, 142, single, null);
        }
    }

    [TestMethod, DataRow("repeat"), DataRow("substitute"), DataRow("ghost"), DataRow("safeguard")]
    public void ForesightCommitsReidentificationAndBypassesSubstituteTypeAndSafeguard(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(193); bodies[1] = branch switch {
                "repeat" => bodies[1] with { Identified = true },
                "substitute" => bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } },
                "ghost" => Kernel.Mon("GASTLY", "LICK", 100), _ => bodies[1] };
            var frame = Kernel.Frame(bodies, single);
            if (branch == "safeguard") frame = frame with { Sides = frame.Sides.SetItem(1, frame.Sides[1] with { SafeguardTurnsRemaining = 5 }) };
            var result = Envelope.Resolve(frame); var marker = (SourceMtMoonVolatileEvent)Marker(result.Events, 193);
            Assert.AreEqual(branch == "repeat" ? 1 : 0, marker.Before); Assert.AreEqual(1, marker.After);
            Assert.IsTrue(result.State.Slots[1].Body!.Identified); Shared.Check(result, 193, single, marker);
        }
    }

    [TestMethod, DataRow("protect"), DataRow("miss")]
    public void ForesightFailedAccuracyNeverBorrowsExistingIdentifiedState(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(193); bodies[1] = bodies[1] with { Identified = true, Protected = branch == "protect" };
            if (branch == "miss") bodies[0] = bodies[0] with { AccuracyStage = 0 };
            var frame = Kernel.Frame(bodies, single); var result = branch == "miss"
                ? Envelope.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "Foresight accuracy failure") : Envelope.Resolve(frame);
            Assert.IsTrue(result.State.Slots[1].Body!.Identified); Assert.IsFalse(result.Events.OfType<SourceMtMoonVolatileEvent>().Any(e => e.Volatile == "identified"));
            Shared.Check(result, 193, single, null);
        }
    }

    [TestMethod, DataRow(109), DataRow(186)]
    public void ConfusionDurationUsesEveryCommittedSourceCounterWithoutReplayingRng(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (int duration in new[] { 2, 3, 4, 5 })
        {
            var result = Envelope.Find(Kernel.Frame(Bodies(id), single), 0, 1,
                r => r.Events.OfType<SourceConfusionChangedEvent>().Any(e => e.After == duration && e.Reason == "move"), "primary confusion duration " + duration);
            Shared.Check(result, id, single, Marker(result.Events, id));
        }
    }

    [TestMethod, DataRow(109), DataRow(142), DataRow(186), DataRow(193)]
    public void ActualMetronomeStatusLeavesOwnTheirCommittedAttackTextBoundary(int id)
    {
        uint? selected = null; for (uint seed = 0; seed < 65536; seed++)
            if (WildBattleSourceCallers.Metronome(new(seed)).SourceMoveId == id) { selected = seed; break; }
        Assert.IsNotNull(selected);
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[0] = Kernel.Mon("TOGETIC", "METRONOME", 29) with { AccuracyStage = 12 };
            var result = Envelope.Resolve(Kernel.Frame(bodies, single) with { Rng = new(selected.Value) });
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Caller == Kernel.Id("METRONOME") && e.Result == Move(id)));
            Assert.IsNotNull(Marker(result.Events, id)); CalledLeafAnimationKernelFixture.Check(result.Events, result, single, Kernel.Id("METRONOME"), id, Marker(result.Events, id));
        }
    }
}
