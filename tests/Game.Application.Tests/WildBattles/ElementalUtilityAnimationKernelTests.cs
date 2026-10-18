using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;
using Charge = FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Legal = FireRed.Tests.Application.WildBattles.ConditionalRecoveryAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class ElementalUtilityAnimationKernelTests
{
    private static string Name(int id) => id switch {
        268 => "CHARGE", 351 => "SHOCK_WAVE", 261 => "WILL_O_WISP", _ => throw new ArgumentOutOfRangeException(nameof(id)) };
    private static MoveId Move(int id) => Kernel.Id(Name(id));

    // Source-natural learners and an authenticated TM34-compatible Pikachu.
    // Shock Wave has no level-up learner in the pinned Gen III catalog. These
    // controlled battle inputs do not claim a campaign acquisition/teaching replay.
    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => id == 261
            ? Kernel.Mon("SNORLAX", "DEFENSE_CURL", 100) with { SpeedStage = 0 }
            : Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = (id switch {
            268 => Kernel.Mon(actor % 2 == 0 ? "VOLTORB" : "ELECTRODE", "CHARGE"),
            351 => Legal.MachineMon("PIKACHU", "TM34", "SHOCK_WAVE", "THUNDER_SHOCK"),
            261 => Kernel.Mon("DUSKULL", "WILL_O_WISP"),
            _ => throw new ArgumentOutOfRangeException(nameof(id)) }) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    private static SourceMtMoonVolatileEvent Charged(ImmutableArray<SourceBattleEvent> events) =>
        events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "charge" && e.Reason == "move");
    private static SourceStatusChangedEvent Burned(ImmutableArray<SourceBattleEvent> events) =>
        events.OfType<SourceStatusChangedEvent>().Single(e => e.Reason == "move");
    private static SourceBattleEvent Owner(ImmutableArray<SourceBattleEvent> events, int id) => id switch {
        268 => Charged(events), 261 => Burned(events), _ => Kernel.Hits(events).Single() };
    private static void Deny(ImmutableArray<SourceBattleEvent> events, bool single) =>
        Envelope.Deny(events, single, "Only a direct owned elemental command/result can authorize this root.");
    private static void Check(SourceKernelResult result, int id, bool single, SourceBattleEvent? owner)
    { Shared.Check(result, id, single, owner); CheckExtra(result.Events, id, single); }
    private static void Check(SourceFourActionResult result, int id, bool single, SourceBattleEvent? owner)
    { Shared.Check(result, id, single, owner); CheckExtra(result.Events, id, single); }
    private static void CheckExtra(ImmutableArray<SourceBattleEvent> events, int id, bool single)
    {
        Assert.IsTrue(AnimationDispatchFixture.Supports(id, Move(id).Value), "Require the actual generated source ID and label.");
        for (int i = 0; i < events.Length; i++)
        {
            Assert.IsNull(Shared.Wave(events, i, single), "Gen III Charge has no Special Defense boost; these roots own no stat wave.");
            var cue = Kernel.Project(events, i, single).Dispatch;
            if (cue is not null)
            {
                Assert.IsNull(cue.MoveTurn); Assert.IsNull(cue.MovePower); Assert.IsNull(cue.MoveDamage);
                Assert.IsNull(cue.FuryCutterCounter); Assert.IsNull(cue.RolloutCounter);
                Assert.AreEqual(cue.PpSequence, cue.SnapshotSequence);
            }
            if (!single) continue;
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(Envelope.Single(events, i), false, SourceBattleScene.On).Dispatch);
            Assert.IsNull(AnimationDispatchFixture.AtResult(Envelope.Single(events, i), false, SourceBattleScene.On).Dispatch);
        }
    }

    [TestMethod]
    public void NaturalConsecutiveChargeTurnsOwnZeroAndOneToTwoAtEverySourceSlot()
    {
        var definition = Kernel.Mechanics.Catalog.Moves[Move(268)];
        Assert.AreEqual(268, definition.SourceMoveId); Assert.AreEqual("EFFECT_CHARGE", definition.Effect);
        Assert.AreEqual(0, definition.Power); Assert.AreEqual("MOVE_TARGET_USER", definition.Target);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var frame = Kernel.Frame(Bodies(268, actor), single);
            int initialPp = frame.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp;
            for (int turn = 0; turn < 2; turn++)
            {
                var result = Kernel.Ordered(frame, actor, actor); var marker = Charged(result.Events);
                Assert.AreEqual(turn, marker.Before); Assert.AreEqual(2, marker.After); Assert.AreEqual(actor, marker.Side.Value);
                var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
                Assert.AreEqual(pp.Sequence + 1, marker.Sequence); Assert.AreEqual(result.Events.IndexOf(pp) + 1, result.Events.IndexOf(marker));
                Assert.AreEqual(2, result.State.Slots[actor].Body!.ChargeTurnsRemaining);
                Check(result, 268, single, marker);
                var remaining = new List<SourceBattleEvent>(); int steps = 0;
                while (!result.CompletedCleanTurn)
                {
                    Assert.IsTrue(++steps <= 32); Assert.IsNotNull(result.Continuation);
                    result = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, result.State, result.Continuation.Value);
                    Assert.IsTrue(result.Succeeded, result.Fault); remaining.AddRange(result.Events);
                }
                frame = result.State; Assert.IsNull(SourceBattleIdentityRules.Validate(frame));
                var countdown = remaining.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Side.Value == actor && e.Volatile == "charge");
                Assert.AreEqual("countdown", countdown.Reason); Assert.AreEqual(2, countdown.Before); Assert.AreEqual(1, countdown.After);
                Assert.AreEqual(1, frame.Slots[actor].Body!.ChargeTurnsRemaining);
                Assert.AreEqual(initialPp - turn - 1, frame.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
                CallerPreludeAnimationKernelFixture.DenyLeaf(remaining.ToImmutableArray(), single, 268);
            }
        }
    }

    [TestMethod]
    public void AlreadyTwoChargeStillCommitsItsSuccessfulTwoToTwoCommand()
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var frame = Charge.Body(Kernel.Frame(Bodies(268, actor), single), actor, b => b with { ChargeTurnsRemaining = 2 });
            var result = Envelope.Resolve(frame, actor, actor); var marker = Charged(result.Events);
            Assert.AreEqual(2, marker.Before); Assert.AreEqual(2, marker.After);
            Assert.IsFalse(result.Events.OfType<SourceMoveFailedEvent>().Any()); Check(result, 268, single, marker);
        }
    }

    [TestMethod]
    public void ChargeRejectsMissingDuplicateForeignReorderedAndUnsupportedCounterReceipts()
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Kernel.Frame(Bodies(268), single), 0, 0).Events;
            var marker = Charged(events); var pp = events.OfType<SourcePpCommandEvent>().Single();
            int mi = events.IndexOf(marker), pi = events.IndexOf(pp);
            Deny(events.RemoveAt(mi), single); Deny(Shared.Number(events.Insert(mi + 1, marker)), single);
            Deny(events.SetItem(pi, marker with { Sequence = pp.Sequence }).SetItem(mi, pp with { Sequence = marker.Sequence }), single);
            foreach (var wrong in new[] { marker with { Side = new(1) }, marker with { Volatile = "stockpile" },
                marker with { Reason = "countdown" }, marker with { Before = -1 }, marker with { Before = 3 },
                marker with { After = 0 }, marker with { After = 1 }, marker with { After = 3 },
                marker with { Turn = marker.Turn + 1 }, marker with { Sequence = marker.Sequence + 1 } })
                Deny(events.SetItem(mi, wrong), single);
            foreach (SourceBattleEvent unrelated in new SourceBattleEvent[] {
                new SourceMtMoonVolatileEvent(0, marker.Turn, marker.Side, "imprison", 0, 1, "move"),
                new SourceStatusChangedEvent(0, marker.Turn, new(1), 0, WildBattleStatus.Burn, "move"),
                new SourcePayDayEvent(0, marker.Turn, marker.Side, 0, 100) })
            {
                Deny(Shared.Number(events.Insert(mi, unrelated)), single);
                Deny(Shared.Number(events.Insert(mi + 1, unrelated)), single);
            }
            SupportStatusAnimationKernelFixture.RejectCommonEnvelopeMutations(events, single);
        }
    }

    [TestMethod]
    public void ChargeAcceptsAnAuthenticMoveEndCureButRejectsItsMissingOrForeignItemOwner()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Charge.Body(Kernel.Frame(Bodies(268), single), 1,
                b => b with { HeldItem = "ITEM_LUM_BERRY", Status = WildBattleStatus.Burn, ConfusionTurnsRemaining = 3 });
            var result = Envelope.Resolve(frame, 0, 0); var item = result.Events.OfType<SourceHeldItemConsumedEvent>().Single();
            Assert.AreEqual("ITEM_LUM_BERRY", item.Item); Assert.AreEqual(0u, result.State.Slots[1].Body!.Status);
            Check(result, 268, single, Charged(result.Events));
            Deny(Shared.Number(result.Events.Remove(item)), single);
            Deny(result.Events.SetItem(result.Events.IndexOf(item), item with { Side = new(0) }), single);
        }
    }

    [TestMethod, DataRow(351), DataRow(261)]
    public void SelectedElementalRootsOwnActualResultsAtEveryActorAndOpponentOrAllyTarget(int id)
    {
        var definition = Kernel.Mechanics.Catalog.Moves[Move(id)]; Assert.AreEqual(id, definition.SourceMoveId);
        Assert.AreEqual(id == 351 ? "EFFECT_ALWAYS_HIT" : "EFFECT_WILL_O_WISP", definition.Effect);
        Assert.AreEqual(id == 351 ? 60 : 0, definition.Power); Assert.AreEqual("MOVE_TARGET_SELECTED", definition.Target);
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (int target in single ? new[] { actor ^ 1 } : new[] { actor ^ 1, actor ^ 2, actor ^ 3 })
        {
            var result = Envelope.Resolve(Kernel.Frame(Bodies(id, actor), single), actor, target);
            Assert.IsFalse(result.Events.OfType<SourceMoveFailedEvent>().Any()); Check(result, id, single, Owner(result.Events, id));
            if (id == 261)
            {
                var burn = Burned(result.Events); Assert.AreEqual(target, burn.Side.Value); Assert.AreEqual(0u, burn.Before);
                Assert.AreEqual(WildBattleStatus.Burn, burn.After); Assert.AreEqual(WildBattleStatus.Burn, result.State.Slots[target].Body!.Status);
            }
        }
    }

    [TestMethod, DataRow(1), DataRow(255)]
    public void ShockWaveHitsAndBreaksOrRetainsSubstituteWithTheCommittedTargetRoute(int substituteHp)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(351); bodies[1] = Kernel.Mon("SNORLAX", "DEFENSE_CURL", 100) with { CalledVolatile = new() { SubstituteHp = substituteHp } };
            var result = Envelope.Resolve(Kernel.Frame(bodies, single)); var hit = result.Events.OfType<SourceSubstituteDamagedEvent>().Single();
            Assert.IsTrue(hit.Damage > 0); Assert.AreEqual(substituteHp, hit.HpBefore);
            Assert.AreEqual(substituteHp == 1, hit.HpAfter == 0); Check(result, 351, single, hit);
        }
    }

    [TestMethod]
    public void ShockWaveIgnoresAccuracyStagesWithoutDrawingAccuracyRng()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(351); bodies[0] = bodies[0] with { AccuracyStage = 0 }; bodies[1] = bodies[1] with { EvasionStage = 12 };
            var result = Kernel.Ordered(Kernel.Frame(bodies, single));
            Assert.IsFalse(result.Events.OfType<SourceMoveMissedEvent>().Any());
            Assert.IsFalse(result.Trace.Any(t => t.Purpose.EndsWith(".accuracy", StringComparison.Ordinal)));
            Check(result, 351, single, Owner(result.Events, 351));
        }
    }

    [TestMethod, DataRow("ground"), DataRow("volt-absorb"), DataRow("wonder-guard"), DataRow("protect")]
    public void ShockWavePreventionNeverBorrowsAZeroDamageOrExistingHitReceipt(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(351); bodies[1] = branch switch {
                "ground" => Kernel.Mon("DIGLETT", "SCRATCH", 100),
                "volt-absorb" => Kernel.Mon("CHINCHOU", "WATER_GUN", 100),
                "wonder-guard" => Kernel.Mon("SHEDINJA", "HARDEN", 100),
                _ => bodies[1] with { Protected = true } };
            if (branch == "volt-absorb") Assert.AreEqual("ABILITY_VOLT_ABSORB", WildBattleEffective.Ability(bodies[1]));
            var result = Envelope.Resolve(Kernel.Frame(bodies, single));
            Assert.IsTrue(result.Events.Any(e => e is SourceMoveMissedEvent or SourceMoveFailedEvent or SourceAbilityPreventedEvent ||
                e is SourceEffectivenessEvent { Effectiveness: WildBattleEffectiveness.NoEffect }));
            Check(result, 351, single, null);
        }
    }

    [TestMethod, DataRow("FLY", "RAYQUAZA"), DataRow("BOUNCE", "PONYTA"), DataRow("DIG", "DIGLETT")]
    public void ShockWaveStillMissesActualSemiInvulnerableChargeStates(string move, string species)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(351); bodies[1] = Kernel.Mon(species, move, 100);
            var prepared = Envelope.Resolve(Kernel.Frame(bodies, single), 1, 0);
            Assert.AreEqual(Kernel.Id(move), prepared.State.Slots[1].Registers.Turn!.Lock!.Move);
            var result = Envelope.Resolve(prepared.State);
            Assert.IsTrue(result.Events.OfType<SourceMoveMissedEvent>().Any()); Check(result, 351, single, null);
        }
    }

    [TestMethod]
    public void ShockWaveRequiresOneOwnedHitAndRejectsAConflictingPreventionReceipt()
    {
        foreach (bool single in new[] { false, true }) foreach (bool substitute in new[] { false, true })
        {
            var bodies = Bodies(351);
            if (substitute) bodies[1] = bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } };
            var events = Envelope.Resolve(Kernel.Frame(bodies, single)).Events; var hit = Owner(events, 351); int at = events.IndexOf(hit);
            var used = events.OfType<SourceMoveUsedEvent>().Single();
            Deny(events.RemoveAt(at), single); Deny(Shared.Number(events.Insert(at + 1, hit)), single);
            SourceBattleEvent foreign = hit switch {
                SourceHpChangedEvent hp => hp with { Target = used.Actor },
                SourceSubstituteDamagedEvent doll => doll with { Target = used.Actor },
                _ => throw new InvalidOperationException() };
            Deny(events.SetItem(at, foreign), single);
            Deny(Shared.Number(events.Insert(at, new SourceMoveMissedEvent(0, used.Turn, used.Actor, used.Target, used.Move))), single);
            SupportStatusAnimationKernelFixture.RejectCommonEnvelopeMutations(events, single);
        }
    }

    [TestMethod, DataRow("water-veil"), DataRow("fire-type"), DataRow("already-burned"), DataRow("existing-status"),
        DataRow("substitute"), DataRow("safeguard"), DataRow("protect"), DataRow("miss")]
    public void WillOWispPreventionRequiresAnActualPrimaryBurnBeforeAnimation(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(261); bodies[1] = branch switch {
                "water-veil" => Kernel.Mon("WAILORD", "SPLASH", 100),
                "fire-type" => Kernel.Mon("CHARMANDER", "SCRATCH", 100),
                "already-burned" => bodies[1] with { Status = WildBattleStatus.Burn },
                "existing-status" => bodies[1] with { Status = WildBattleStatus.Paralysis },
                "substitute" => bodies[1] with { CalledVolatile = new() { SubstituteHp = 1 } },
                "protect" => bodies[1] with { Protected = true }, _ => bodies[1] };
            if (branch == "water-veil") Assert.AreEqual("ABILITY_WATER_VEIL", WildBattleEffective.Ability(bodies[1]));
            if (branch == "miss") bodies[0] = bodies[0] with { AccuracyStage = 0 };
            var frame = Kernel.Frame(bodies, single);
            if (branch == "safeguard") frame = frame with { Sides = frame.Sides.SetItem(1, frame.Sides[1] with { SafeguardTurnsRemaining = 5 }) };
            var result = branch == "miss" ? Envelope.Find(frame, 0, 1, r => r.Events.OfType<SourceMoveMissedEvent>().Any() ||
                r.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "accuracy"), "Will-O-Wisp accuracy miss") : Envelope.Resolve(frame);
            Assert.IsFalse(result.Events.OfType<SourceStatusChangedEvent>().Any(e => e.Reason == "move")); Check(result, 261, single, null);
        }
    }

    [TestMethod]
    public void WillOWispReachesFlashFireOnANonFireTargetThroughAuthenticEntryTrace()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(261);
            foreach (int slot in single ? new[] { 0 } : new[] { 0, 2 })
                bodies[slot] = Kernel.Mon("VULPIX", "WILL_O_WISP") with { AccuracyStage = 12, SpeedStage = slot == 0 ? 12 : 0 };
            bodies[1] = Kernel.Mon("RALTS", "GROWL", 100, personality: 1);
            Assert.AreEqual("ABILITY_TRACE", bodies[1].Pokemon.AbilityId);
            var frame = Kernel.Frame(bodies, single); Assert.AreEqual("ABILITY_FLASH_FIRE", WildBattleEffective.Ability(frame.Slots[1].Body!));
            var result = Envelope.Resolve(frame);
            Assert.IsTrue(result.Events.OfType<SourceAbilityPreventedEvent>().Any(e => e.Ability == "ABILITY_FLASH_FIRE" && e.Move == Move(261)));
            Assert.IsTrue(result.State.Slots[1].Body!.FlashFireBoost);
            Assert.IsFalse(result.Events.OfType<SourceStatusChangedEvent>().Any(e => e.Reason == "move")); Check(result, 261, single, null);
        }
    }

    [TestMethod, DataRow("ITEM_RAWST_BERRY"), DataRow("ITEM_LUM_BERRY"), DataRow("synchronize")]
    public void WillOWispKeepsItsPrimaryBurnAuthorityAcrossLaterCureAndSynchronizeResults(string suffix)
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(261); bodies[1] = suffix == "synchronize"
                ? Kernel.Mon("ABRA", "TELEPORT", 100) : bodies[1] with { HeldItem = suffix };
            var result = Envelope.Resolve(Kernel.Frame(bodies, single)); var burn = Burned(result.Events);
            Check(result, 261, single, burn);
            var later = result.Events.OfType<SourceStatusChangedEvent>().Single(e => e.Reason != "move");
            Assert.IsTrue(later.Sequence > burn.Sequence); Assert.AreEqual(suffix == "synchronize" ? "synchronize" : "held-berry", later.Reason);
            Assert.AreEqual(suffix == "synchronize" ? 0 : 1, later.Side.Value);
            Assert.AreEqual(suffix == "synchronize" ? WildBattleStatus.Burn : 0u, later.After);
            Assert.AreEqual(suffix == "synchronize" ? WildBattleStatus.Burn : 0u, result.State.Slots[1].Body!.Status);
            Deny(result.Events.Remove(burn), single);
            Deny(result.Events.SetItem(result.Events.IndexOf(burn), burn with { Reason = later.Reason }), single);
        }
    }

    [TestMethod]
    public void WillOWispRequiresOneOwnedExactBurnTransitionRatherThanAnyStatusSuffix()
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Kernel.Frame(Bodies(261), single)).Events; var burn = Burned(events); int at = events.IndexOf(burn);
            Deny(events.RemoveAt(at), single); Deny(Shared.Number(events.Insert(at + 1, burn)), single);
            foreach (var wrong in new[] { burn with { Side = new(0) }, burn with { Before = WildBattleStatus.Paralysis },
                burn with { After = 0 }, burn with { After = WildBattleStatus.Paralysis },
                burn with { After = WildBattleStatus.Burn | WildBattleStatus.Poison }, burn with { Reason = "synchronize" },
                burn with { Reason = "held-berry" }, burn with { Turn = burn.Turn + 1 } }) Deny(events.SetItem(at, wrong), single);
            SupportStatusAnimationKernelFixture.RejectCommonEnvelopeMutations(events, single);
        }
    }

    [TestMethod, DataRow(268), DataRow(351), DataRow(261)]
    public void ActualMetronomeLeavesCannotBorrowDirectElementalRootAdmission(int id)
    {
        uint seed = CallerPreludeAnimationKernelFixture.MetronomeSeed(id);
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(id); bodies[0] = Kernel.Mon("TOGETIC", "METRONOME", 29) with { AccuracyStage = 12 };
            var result = Envelope.Resolve(Kernel.Frame(bodies, single) with { Rng = new(seed) });
            Assert.IsTrue(result.Events.OfType<SourceCallerSelectionEvent>().Any(e => e.Result == Move(id)));
            Assert.IsNotNull(Owner(result.Events, id)); CallerPreludeAnimationKernelFixture.DenyLeaf(result.Events, single, id);
        }
    }

    [TestMethod, DataRow(268), DataRow(351), DataRow(261)]
    public void CanonicalMetadataScriptSupportAndExportedSinglesResultsRemainAuthoritative(int id)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(Kernel.Frame(Bodies(id), single), 0, id == 268 ? 0 : 1).Events;
            var used = events.OfType<SourceMoveUsedEvent>().Single(); int ui = events.IndexOf(used);
            var definitions = Kernel.Mechanics.Catalog.Moves; var definition = definitions[Move(id)];
            foreach (var metadata in new[] { null, definitions.Remove(Move(id)), definitions.SetItem(Move(id), definition with { SourceMoveId = 33 }) })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Envelope.Single(events, ui), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, ui), SourceBattleScene.On, metadata).Dispatch);
            if (id != 351) foreach (var wrong in new[] { definition with { Power = 1 },
                definition with { Target = id == 268 ? "MOVE_TARGET_SELECTED" : "MOVE_TARGET_USER" } })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Envelope.Single(events, ui), true, SourceBattleScene.On, definitions.SetItem(Move(id), wrong)).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, ui), SourceBattleScene.On, definitions.SetItem(Move(id), wrong)).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(Envelope.Single(events, ui), true, SourceBattleScene.On, definitions, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events, ui), SourceBattleScene.On, definitions, (_, _) => false).Dispatch);
            if (!single) continue;
            var battle = Envelope.Single(events, ui) with { SourceEvents = [] };
            Assert.IsNotNull(AnimationDispatchFixture.AtAttackText(battle, true, SourceBattleScene.On).Dispatch);
            int oi = events.IndexOf(Owner(events, id)); WildBattleEvent corrupted = battle.Events[oi] switch {
                WildBattleMtMoonVolatileEvent marker => marker with { Before = 3 },
                WildBattleStatusChangedEvent burn => burn with { After = WildBattleStatus.Poison },
                WildBattleHpChangedEvent hit => hit with { Target = WildBattleSide.Player },
                _ => throw new InvalidOperationException("Expected the actual exported owner.") };
            Assert.IsNull(AnimationDispatchFixture.AtAttackText(battle with { Events = battle.Events.SetItem(oi, corrupted) }, true, SourceBattleScene.On).Dispatch);
        }
    }
}
