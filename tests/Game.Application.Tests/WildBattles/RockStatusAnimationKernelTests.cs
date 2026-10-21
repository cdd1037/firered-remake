using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using static FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class RockStatusAnimationKernelTests
{
    [TestMethod, DataRow(246), DataRow(317), DataRow(197), DataRow(203), DataRow(50)]
    public void NaturalLearnersAndAuthenticatedTmOwnOneRootAcrossAllSlotsAndBothSinglesSides(int id)
    {
        var definition = Kernel.Mechanics.Catalog.Moves[Move(id)]; Assert.AreEqual(id, definition.SourceMoveId);
        Assert.AreEqual(Spec(id).Effect, definition.Effect);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in id is 197 or 203 ? new[] { actor } : single ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 2 })
        {
            var frame = Kernel.Frame(Bodies(id, actor), single);
            if (id is 197 or 203)
            {
                var result = Kernel.Ordered(frame, actor, target); var attempt = result.Events.OfType<SourceProtectAttemptEvent>().Single();
                Assert.IsTrue(attempt.Succeeded); Check(result, id, single, attempt);
            }
            else
            {
                var result = Envelope.Resolve(frame, actor, target);
                SourceBattleEvent owner = id == 50 ? result.Events.OfType<SourceDisableChangedEvent>().Single() : Envelope.Hits(result.Events).Single();
                if (owner is SourceDisableChangedEvent disabled)
                {
                    Assert.AreEqual(target, disabled.Side.Value); Assert.AreEqual(Kernel.Id("SPLASH"), disabled.Move);
                    Assert.AreEqual(0, disabled.Before); Assert.IsTrue(disabled.After is >= 2 and <= 5); Assert.AreEqual("move", disabled.Reason);
                }
                Check(result, id, single, owner);
            }
        }
    }

    [TestMethod, DataRow(197), DataRow(203)]
    public void ProtectionSuccessRequiresAdjacentOwnedAttemptAndMatchingVolatile(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Kernel.Ordered(Kernel.Frame(Bodies(id), single), 0, 0); var events = result.Events;
            var used = events.OfType<SourceMoveUsedEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single();
            var attempt = events.OfType<SourceProtectAttemptEvent>().Single(); var marker = events.OfType<SourceMtMoonVolatileEvent>().Single();
            int at = events.IndexOf(attempt), mark = events.IndexOf(marker), ppAt = events.IndexOf(pp), usedAt = events.IndexOf(used);
            Assert.AreEqual(ppAt + 1, at); Assert.AreEqual(at + 1, mark); Assert.AreEqual(pp.Sequence + 1, attempt.Sequence);
            Assert.AreEqual(attempt.Sequence + 1, marker.Sequence); Assert.IsTrue(attempt.NotLastAction);
            Assert.AreEqual(0, attempt.Before); Assert.AreEqual(1, attempt.After); Assert.IsTrue(attempt.Roll <= attempt.Threshold);
            Assert.AreEqual(id == 203 ? "endure" : "protect", marker.Volatile); Assert.AreEqual(0, marker.Before); Assert.AreEqual(1, marker.After);
            Deny(events.RemoveAt(at), single); Deny(events.RemoveAt(mark), single); Deny(events.RemoveAt(ppAt), single);
            Deny(Number(events.Insert(mark + 1, marker)), single); Deny(Number(events.Insert(mark + 1, attempt)), single);
            Deny(Number(events.Insert(at, new SourceMtMoonVolatileEvent(0, used.Turn, used.Actor, "unrelated", 0, 1, "move"))), single);
            Deny(events.SetItem(at, marker with { Sequence = attempt.Sequence }).SetItem(mark, attempt with { Sequence = marker.Sequence }), single);
            foreach (var wrong in new[] { attempt with { Side = new(1) }, attempt with { Move = Move(50) }, attempt with { Succeeded = false },
                attempt with { NotLastAction = false }, attempt with { Before = -1 }, attempt with { After = 0 },
                attempt with { After = 2 }, attempt with { Threshold = 0, Roll = 1 }, attempt with { Roll = -1 },
                attempt with { Turn = attempt.Turn + 1 } }) Deny(events.SetItem(at, wrong), single);
            foreach (var wrong in new[] { marker with { Side = new(1) }, marker with { Before = 1 }, marker with { After = 0 },
                marker with { Volatile = id == 203 ? "protect" : "endure" }, marker with { Reason = "countdown" },
                marker with { Turn = marker.Turn + 1 } }) Deny(events.SetItem(mark, wrong), single);
            Deny(events.SetItem(ppAt, pp with { PpOwner = new(1) }), single);
            Deny(events.SetItem(usedAt, used with { Target = new(1) }).SetItem(ppAt, pp with { Target = new(1) }), single);
            var attack = events.OfType<SourceAttackStringEvent>().Single();
            Deny(events.SetItem(events.IndexOf(attack), attack with { ChosenMove = Kernel.Id("METRONOME") }), single);
            var definitions = Kernel.Mechanics.Catalog.Moves; var definition = definitions[Move(id)];
            foreach (var wrong in new[] { definition with { Power = 1 }, definition with { Target = "MOVE_TARGET_SELECTED" } })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Envelope.Single(events, usedAt), true, SourceBattleScene.On, definitions.SetItem(Move(id), wrong)).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, usedAt), SourceBattleScene.On, definitions.SetItem(Move(id), wrong)).Dispatch);
        }
    }

    [TestMethod, DataRow(197), DataRow(203)]
    public void ProtectionRandomFailureAndActualLastActionSpendPpWithoutRoot(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[0] = bodies[0] with { LastUsedMove = Move(id), ProtectConsecutiveUses = 1 };
            var failed = Kernel.Find(Kernel.Frame(bodies, single), 0, 0,
                r => r.Events.OfType<SourceProtectAttemptEvent>().Any(e => !e.Succeeded), "repeat protection failure");
            var attempt = failed.Events.OfType<SourceProtectAttemptEvent>().Single();
            Assert.IsTrue(attempt.NotLastAction); Assert.IsTrue(attempt.Roll > attempt.Threshold); Assert.AreEqual(0, attempt.After);
            Assert.IsFalse(failed.Events.OfType<SourceMtMoonVolatileEvent>().Any(e => e.Volatile is "endure" or "protect"));
            Check(failed, id, single, null);
            var last = LastProtection(id, single); attempt = last.Events.OfType<SourceProtectAttemptEvent>().Single();
            Assert.IsFalse(attempt.NotLastAction); Assert.IsFalse(attempt.Succeeded); Assert.AreEqual(0, attempt.Before);
            Assert.IsTrue(last.Trace.Any(t => t.Purpose.EndsWith(".protect-success", StringComparison.Ordinal)));
            Check(last, id, single, null);
        }
    }

    [TestMethod]
    public void DisableReceiptPreservesDisabledMoveAndSinglesUsesExportedReceiptWithoutFinalState()
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Kernel.Frame(Bodies(50), single)).Events;
            var disabled = events.OfType<SourceDisableChangedEvent>().Single(); int at = events.IndexOf(disabled);
            var pp = events.OfType<SourcePpCommandEvent>().Single(); int ppAt = events.IndexOf(pp);
            int usedAt = Array.FindIndex(events.ToArray(), e => e is SourceMoveUsedEvent);
            Assert.AreEqual(ppAt + 1, at); Assert.AreEqual(pp.Sequence + 1, disabled.Sequence);
            Deny(events.RemoveAt(at), single); Deny(Number(events.Insert(at + 1, disabled)), single);
            Deny(events.SetItem(ppAt, disabled with { Sequence = pp.Sequence }).SetItem(at, pp with { Sequence = disabled.Sequence }), single);
            Deny(Number(events.Insert(at, new SourceMtMoonVolatileEvent(0, disabled.Turn, new(1), "unrelated", 0, 1, "move"))), single);
            foreach (var wrong in new[] { disabled with { Side = new(0) }, disabled with { Move = null }, disabled with { Move = Kernel.Id("MISSING") }, disabled with { Before = 1 },
                disabled with { After = 1 }, disabled with { After = 6 }, disabled with { Reason = "countdown" },
                disabled with { Reason = "expired" }, disabled with { Turn = disabled.Turn + 1 } }) Deny(events.SetItem(at, wrong), single);
            Deny(events.SetItem(ppAt, pp with { PpOwner = new(1) }), single);
            var attack = events.OfType<SourceAttackStringEvent>().Single();
            Deny(events.SetItem(events.IndexOf(attack), attack with { ChosenMove = Kernel.Id("METRONOME") }), single);
            if (single)
            {
                var battle = Envelope.Single(events, usedAt); var exported = (SourceDisableChangedEvent)battle.Events[at];
                Assert.AreEqual(disabled.Move, exported.Move);
                Assert.IsNotNull(AnimationDispatchFixture.AtAttackText(battle, true, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle with { Events = battle.Events.SetItem(at, exported with { Move = null }) }, true, SourceBattleScene.On).Dispatch);
            }
        }
    }

    [TestMethod, DataRow("no-last-move"), DataRow("last-move-absent"), DataRow("zero-pp"), DataRow("already-disabled"), DataRow("protect"), DataRow("miss")]
    public void DisableActualFailureDoesNotBorrowPriorStateOrSpendDurationRandomness(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(50); var target = bodies[1];
            bodies[1] = branch switch {
                "no-last-move" => target with { LastUsedMove = null },
                "last-move-absent" => target with { LastUsedMove = Kernel.Id("TACKLE") },
                "zero-pp" => target with { Pokemon = target.Pokemon with { Moves = [target.Pokemon.Moves[0] with { CurrentPp = 0 }] } },
                "already-disabled" => target with { DisabledMove = Kernel.Id("SPLASH"), DisableTurnsRemaining = 3 },
                "protect" => target with { Protected = true }, _ => target };
            if (branch == "miss") bodies[0] = bodies[0] with { AccuracyStage = 0 };
            var frame = Kernel.Frame(bodies, single);
            var result = branch == "miss" ? Envelope.Find(frame, 0, 1,
                r => r.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "accuracy"), "Disable accuracy failure") : Envelope.Resolve(frame);
            Assert.IsFalse(result.Events.OfType<SourceDisableChangedEvent>().Any());
            Assert.IsFalse(result.Trace.Any(t => t.Purpose.EndsWith(".disable-duration", StringComparison.Ordinal)));
            Check(result, 50, single, null);
        }
    }

    [TestMethod]
    public void DisableBypassesSubstituteAndActualResidualCountdownCannotReplaySetup()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(50); bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } };
            var direct = Envelope.Resolve(Kernel.Frame(bodies, single)); Check(direct, 50, single, direct.Events.OfType<SourceDisableChangedEvent>().Single());
            var result = Kernel.Ordered(Kernel.Frame(Bodies(50), single)); int setups = 0, countdowns = 0;
            for (int step = 0; ; step++)
            {
                Assert.IsTrue(step < 32); string before = JsonSerializer.Serialize(result);
                for (int i = 0; i < result.Events.Length; i++)
                {
                    var cue = Kernel.Project(result.Events, i, single).Dispatch; if (cue?.SourceMoveId == 50) setups++;
                    if (result.Events[i] is not SourceDisableChangedEvent { Reason: "countdown" } residual) continue;
                    countdowns++; Assert.AreEqual(Kernel.Id("SPLASH"), residual.Move); Assert.AreEqual(residual.Before - 1, residual.After);
                    Assert.IsNull(cue); Assert.IsNull(Wave(result.Events, i, single));
                }
                Assert.AreEqual(before, JsonSerializer.Serialize(result)); if (result.CompletedCleanTurn) break;
                Assert.IsNotNull(result.Continuation); result = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, result.State, result.Continuation.Value);
                Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            }
            Assert.AreEqual(1, setups); Assert.AreEqual(1, countdowns);
        }
    }

    [TestMethod, DataRow(246), DataRow(317)]
    public void RockHitsRetainOrdinarySubstituteAndPreventionOwnership(int id)
    {
        foreach (bool single in new[] { false, true }) foreach (string branch in new[] { "substitute", "protect", "miss" })
        {
            var bodies = Bodies(id); if (branch == "substitute") bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 100 } };
            if (branch == "protect") bodies[1] = bodies[1] with { Protected = true };
            if (branch == "miss") bodies[0] = bodies[0] with { AccuracyStage = 0 };
            var frame = Kernel.Frame(bodies, single); var result = branch == "miss"
                ? Envelope.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Any(), "rock accuracy failure") : Envelope.Resolve(frame);
            var hit = Envelope.Hits(result.Events).SingleOrDefault(); Assert.AreEqual(branch == "substitute", hit is SourceSubstituteDamagedEvent);
            Check(result, id, single, hit);
            if (id == 317) for (int i = 0; i < result.Events.Length; i++) Assert.IsNull(Wave(result.Events, i, single));
        }
    }

    [TestMethod, DataRow("ordinary"), DataRow("minimum"), DataRow("clear-body"), DataRow("mist")]
    public void RockTombSecondaryWaveRequiresActualOwnedSpeedDrop(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(317); bodies[1] = bodies[1] with { SpeedStage = branch == "minimum" ? 0 : 6 };
            if (branch == "clear-body") bodies[1] = Kernel.Mon("TENTACOOL", "ACID", 100);
            var frame = Kernel.Frame(bodies, single);
            if (branch == "mist") frame = frame with { Sides = frame.Sides.SetItem(1, frame.Sides[1] with { MistTurnsRemaining = 5 }) };
            var result = Envelope.Resolve(frame); Check(result, 317, single, Envelope.Hits(result.Events).Single()); int waves = 0;
            for (int i = 0; i < result.Events.Length; i++)
            {
                var wave = Wave(result.Events, i, single); if (wave is null) continue; waves++;
                Assert.AreEqual("ordinary", branch); Assert.AreEqual(317, wave.SourceMoveId); Assert.AreEqual(24, wave.AnimationArgument);
                var stat = (SourceStatChangedEvent)result.Events[i]; Assert.AreEqual(WildBattleStat.Speed, stat.Stat);
                Assert.AreEqual(6, stat.Before); Assert.AreEqual(5, stat.After); Assert.AreEqual(stat.Target, wave.Affected);
            }
            Assert.AreEqual(branch == "ordinary" ? 1 : 0, waves);
        }
    }

    [TestMethod, DataRow("all"), DataRow("one"), DataRow("partial"), DataRow("capped")]
    public void AncientPowerOwnsOneSourceOrderedCombinedWaveOrNoneWhenAllStatsCapped(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(246); bodies[0] = bodies[0] with { AttackStage = branch == "all" ? 6 : 12,
                DefenseStage = branch is "all" or "partial" ? 6 : 12, SpeedStage = branch is "all" or "partial" ? 6 : 12,
                SpecialAttackStageDelta = branch == "all" ? 0 : 6, SpecialDefenseStageDelta = branch == "capped" ? 6 : 0 };
            var frame = Kernel.Frame(bodies, single);
            var result = branch == "capped" ? Envelope.Resolve(frame) : Envelope.Find(frame, 0, 1,
                r => r.Events.OfType<SourceStatChangedEvent>().Any(), "AncientPower stat group");
            Check(result, 246, single, Envelope.Hits(result.Events).Single());
            var stats = result.Events.Where(e => e is SourceStatChangedEvent or SourceStatAtMaximumEvent).ToArray();
            Assert.AreEqual(branch == "capped" ? 0 : 5, stats.Length); int waves = 0;
            for (int i = 0; i < result.Events.Length; i++)
            {
                var wave = Wave(result.Events, i, single); if (wave is null) continue; waves++;
                Assert.AreEqual(stats[0].Sequence, wave.Sequence); Assert.AreEqual(246, wave.SourceMoveId);
                Assert.AreEqual(new BattlerSlot(0), wave.Affected); Assert.AreEqual(branch == "one" ? 19 : 55, wave.AnimationArgument);
            }
            Assert.AreEqual(branch == "capped" ? 0 : 1, waves);
            if (stats.Length != 0)
            {
                var expected = new[] { WildBattleStat.Attack, WildBattleStat.Defense, WildBattleStat.Speed, WildBattleStat.SpecialAttack, WildBattleStat.SpecialDefense };
                foreach (var cap in stats.OfType<SourceStatAtMaximumEvent>())
                {
                    int capIndex = result.Events.IndexOf(cap);
                    Assert.IsFalse(single ? CampaignStatAnimationPresentation.IsSilentResultReceipt(Envelope.Single(result.Events, capIndex), true,moves:CurrentCampaignFixture.Catalog.PokemonContent!.Catalog.Moves)
                        : CampaignStatAnimationPresentation.IsSilentResultReceipt(Envelope.Double(result.Events, capIndex),moves:CurrentCampaignFixture.Catalog.PokemonContent!.Catalog.Moves),
                        "ALLOW_PTR returns WORKED at a cap, so AllStatsUp prints its cap result.");
                }
                CollectionAssert.AreEqual(expected, stats.Select(e => e is SourceStatChangedEvent s ? s.Stat : ((SourceStatAtMaximumEvent)e).Stat).ToArray());
                int first = result.Events.IndexOf(stats[0]); var malformed = result.Events.RemoveAt(first + 1);
                for (int i = first; i < malformed.Length; i++) Assert.IsNull(Wave(malformed, i, single), "An incomplete five-stat group cannot lend a combined wave.");
            }
        }
    }
}
