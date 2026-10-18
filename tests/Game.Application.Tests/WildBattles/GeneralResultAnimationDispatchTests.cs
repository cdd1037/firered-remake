using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using static FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed partial class GeneralResultAnimationDispatchTests
{
    private static CampaignGeneralAnimationDispatch? At(ImmutableArray<SourceBattleEvent> events, int index, bool single,
        MoveId?[]? entry = null, bool[]? substitutes = null, SourceBattleScene scene = SourceBattleScene.On) => single
        ? CampaignGeneralAnimationPresentation.AtResult(Single(events, index) with { SourceEvents = [] }, true, scene,
            entry ?? new MoveId?[2], substitutes ?? new bool[2])
        : CampaignGeneralAnimationPresentation.AtResult(Double(events, index), scene,
            entry ?? new MoveId?[4], substitutes ?? new bool[4]);
    private static void Same(CampaignGeneralAnimationDispatch? first, CampaignGeneralAnimationDispatch? second)
    {
        if (first is null) { Assert.IsNull(second); return; }
        Assert.IsNotNull(second);
        Assert.AreEqual(first, second with { SpriteVisibility = first.SpriteVisibility });
        CollectionAssert.AreEqual(first.SpriteVisibility.ToArray(), second.SpriteVisibility.ToArray());
    }
    private static CampaignGeneralAnimationDispatch Check(ImmutableArray<SourceBattleEvent> events, bool single,
        SourceBattleEvent owner, SourceBattleEvent outcome, int actor, int target, string label,
        CampaignGeneralAnimationBoundary boundary, MoveId?[]? entry = null, bool[]? substitutes = null)
    {
        string before = JsonSerializer.Serialize(events); CampaignGeneralAnimationDispatch? found = null; int count = 0;
        for (int i = 0; i < events.Length; i++)
        {
            var dispatch = At(events, i, single, entry, substitutes); Same(dispatch, At(events, i, single, entry, substitutes));
            Assert.IsNull(At(events, i, single, entry, substitutes, SourceBattleScene.Off));
            if (events[i] != owner) { Assert.IsNull(dispatch); continue; }
            Assert.IsNotNull(dispatch); found = dispatch; count++;
            Assert.AreEqual(owner.Sequence, dispatch.Sequence); Assert.AreEqual(outcome.Sequence, dispatch.ReceiptSequence);
            Assert.AreEqual(actor, dispatch.Actor.Value); Assert.AreEqual(target, dispatch.Target.Value);
            Assert.AreEqual(label, dispatch.GeneralLabel); Assert.AreEqual(boundary, dispatch.Boundary);
            Assert.AreEqual(single ? 2 : 4, dispatch.SpriteVisibility.Length);
        }
        Assert.AreEqual(1, count); Assert.AreEqual(before, JsonSerializer.Serialize(events)); return found!;
    }
    private static void Deny(ImmutableArray<SourceBattleEvent> events, bool single)
    { for (int i = 0; i < events.Length; i++) Assert.IsNull(At(events, i, single)); }
    private static SourceCalledDelayedEvent Resolved(string move = "FUTURE_SIGHT", int target = 1, long sequence = 10) =>
        new(sequence, 2, new(target), Id(move), 1, 0, "resolved");
    private static SourceHpChangedEvent Impact(string move = "FUTURE_SIGHT", int actor = 0, int target = 1, long sequence = 11) =>
        new(sequence, 2, new(actor), new(target), Id(move), 20, 0, 35, false, 91);
    private static SourceFocusPunchPreparedEvent Focus(int actor = 0, long sequence = 10) => new(sequence, 2, new(actor), 0, actor / 2);
    private static SourceMtMoonVolatileEvent Charge(int actor, long sequence, int after, string reason) =>
        new(sequence, 2, new(actor), "called-charge", after == 0 ? 1 : 0, after, reason);
    private static SourcePpCommandEvent Pp(int actor, string move, long sequence, int turn = 2) =>
        new(sequence, turn, new(actor), new(actor ^ 1), 0, Id(move), Id(move), 10, 9, 1, 0);

    [TestMethod]
    public void FutureImpactAuthenticatesOnlyItsAdjacentSuccessAndRetainsTargetSubstitute()
    {
        foreach (bool single in new[] { true, false }) foreach (string move in new[] { "FUTURE_SIGHT", "DOOM_DESIRE" })
        for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            int target = actor ^ 1; var delayed = Resolved(move, target); var hit = Impact(move, actor, target);
            string label = move == "FUTURE_SIGHT" ? "General_FutureSightHit" : "General_DoomDesireHit";
            Check([delayed, hit], single, delayed, hit, actor, target, label, CampaignGeneralAnimationBoundary.AfterText);
            var sub = new SourceSubstituteDamagedEvent(hit.Sequence, hit.Turn, hit.Actor, hit.Target, hit.Move,
                hit.HpBefore, hit.HpAfter, hit.Damage, hit.Critical, hit.RandomPercent);
            var flags = new bool[single ? 2 : 4]; flags[target] = true;
            Check([delayed, sub], single, delayed, sub, actor, target, label, CampaignGeneralAnimationBoundary.AfterText, substitutes: flags);
            flags[actor] = true; Assert.IsNull(At([delayed, sub], 0, single, substitutes: flags));
        }
    }

    [TestMethod]
    public void FailedBareCountdownSetupAndUnrelatedLaterHitsCannotAuthorizeDelayedImpact()
    {
        foreach (bool single in new[] { true, false })
        {
            var delayed = Resolved(); var hit = Impact();
            foreach (var events in new ImmutableArray<SourceBattleEvent>[] {
                [delayed], [delayed, new SourceMoveFailedEvent(11, 2, new(0), new(1), Id("FUTURE_SIGHT"), "future-accuracy")],
                [delayed with { Reason = "scheduled", Before = 0, After = 3 }, hit],
                [delayed with { Reason = "countdown", Before = 2, After = 1 }, hit],
                [delayed with { Move = Id("WISH") }, hit], [delayed, new SourceTurnReadyEvent(11, 2, 31), hit with { Sequence = 12 }],
                [delayed, new SourceAttackStringEvent(11, 2, new(0), hit.Move, hit.Move), hit with { Sequence = 12 }] }) Deny(events, single);
        }
    }

    [TestMethod]
    public void MalformedOwnershipSequenceTurnAndNumericHitEvidenceFailClosed()
    {
        foreach (bool single in new[] { true, false })
        {
            var delayed = Resolved(); var hit = Impact();
            foreach (var invalid in new[] { delayed with { Before = 2 }, delayed with { After = 1 },
                delayed with { SourcePartyIndex = 0 }, delayed with { Target = new(0) }, delayed with { Turn = 3 },
                delayed with { Sequence = -1 } }) Deny([invalid, hit], single);
            foreach (var invalid in new[] { hit with { Sequence = 10 }, hit with { Sequence = 12 }, hit with { Turn = 3 },
                hit with { Target = new(0) }, hit with { Move = Id("DOOM_DESIRE") }, hit with { HpBefore = 0 },
                hit with { HpAfter = 1 }, hit with { Damage = -1 }, hit with { Damage = 0, HpAfter = 20 },
                hit with { Critical = true }, hit with { RandomPercent = 84 }, hit with { RandomPercent = 101 },
                hit with { PreparedBasePower = 80 } }) Deny([delayed, invalid], single);
            ImmutableArray<SourceBattleEvent> duplicate = [delayed, hit, delayed with { Sequence = 12 }, hit with { Sequence = 13 }];
            Assert.IsNotNull(At(duplicate, 0, single)); Assert.IsNull(At(duplicate, 2, single));
            Assert.IsNull(At([new SourceTurnReadyEvent(10, 2, 31), delayed, hit], 1, single));
        }
    }

    [TestMethod]
    public void FocusBandZeroHpLossAuthenticatesSuccessfulDelayedImpact()
    {
        foreach (bool single in new[] { true, false }) foreach (string move in new[] { "FUTURE_SIGHT", "DOOM_DESIRE" })
        {
            var delayed = Resolved(move); var hit = Impact(move, sequence: 12) with { HpBefore = 1, HpAfter = 1, Damage = 0 };
            var adjusted = new SourceDamageAdjustedEvent(11, 2, hit.Target, hit.Move, "ITEM_FOCUS_BAND", 35, 0, false, true);
            Check([delayed, adjusted, hit], single, delayed, hit, 0, 1,
                move == "FUTURE_SIGHT" ? "General_FutureSightHit" : "General_DoomDesireHit", CampaignGeneralAnimationBoundary.AfterText);
            foreach (var invalid in new[] { adjusted with { Sequence = 10 }, adjusted with { Sequence = 12 }, adjusted with { Turn = 3 },
                adjusted with { Target = new(0) }, adjusted with { Move = Id("WISH") }, adjusted with { DamageBefore = 0 },
                adjusted with { DamageAfter = 1 }, adjusted with { Item = "ITEM_NONE" }, adjusted with { HungOn = false },
                adjusted with { Endured = true } }) Deny([delayed, invalid, hit], single);
        }
    }

    [TestMethod]
    public void FocusPreparationIsBeforeTextAndIndependentOfLaterFocusLoss()
    {
        foreach (bool single in new[] { true, false }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var focus = Focus(actor); var lost = new SourceFocusPunchLostEvent(11, 2, new(actor), 10, 0);
            Check([focus, lost], single, focus, focus, actor, actor, "General_FocusPunchSetUp", CampaignGeneralAnimationBoundary.BeforeText);
            foreach (var invalid in new[] { focus with { ChosenSlot = -1 }, focus with { ChosenSlot = 4 },
                focus with { PartyIndex = -1 }, focus with { PartyIndex = 6 } }) Deny([invalid], single);
            Assert.IsNull(At([focus, focus with { Sequence = 11 }], 1, single));
        }
    }

    [TestMethod]
    public void CurrentControllerVisibilityUsesSharedChargeReplayAndDetachedSnapshots()
    {
        foreach (bool single in new[] { true, false }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        foreach (string move in new[] { "FLY", "BOUNCE", "DIG", "DIVE" })
        {
            int count = single ? 2 : 4, target = actor ^ 1; var delayed = Resolved(target: target); var hit = Impact(actor: actor, target: target);
            MoveId?[] entry = new MoveId?[count]; entry[actor] = Id(move); var original = entry.ToArray();
            Assert.IsNull(At([delayed, hit], 0, single, entry)); Assert.IsNull(At([Focus(actor)], 0, single, entry));
            var released = At([Charge(actor, 9, 0, "released"), delayed, hit], 1, single, entry); Assert.IsNotNull(released);
            Assert.IsNull(At([delayed, hit, Charge(actor, 12, 0, "released")], 0, single, entry));
            Assert.IsNull(At([Pp(actor, move, 8), Charge(actor, 9, 1, "move"), delayed, hit], 2, single));
            Assert.IsNotNull(At([delayed, hit, Pp(actor, move, 12), Charge(actor, 13, 1, "move")], 0, single));
            Assert.IsNotNull(At([new SourceCalledPartySwitchedEvent(9, 2, new(actor), 0, 2, WildBattleCalledSwitchKind.Voluntary), delayed, hit], 1, single, entry));
            Assert.IsNotNull(At([new SourceStartedEvent(9, 2), delayed, hit], 1, single, entry));
            Assert.IsNull(At([Charge(actor, 9, 1, "move"), delayed, hit], 1, single));
            CollectionAssert.AreEqual(original, entry);
            entry[actor] = null; entry[target] = Id(move);
            var hiddenTarget = At([delayed, hit], 0, single, entry); Assert.IsNotNull(hiddenTarget);
            Assert.IsTrue(hiddenTarget.SpriteVisibility[actor]); Assert.IsFalse(hiddenTarget.SpriteVisibility[target]);
            entry[target] = null; Assert.IsFalse(hiddenTarget.SpriteVisibility[target]);
        }
    }

    [TestMethod]
    public void StrictLayoutAndPhaseGatesDoNotReadFinalFrameOrSourceShadow()
    {
        ImmutableArray<SourceBattleEvent> events = [Focus()];
        Assert.Throws<ArgumentNullException>(() => CampaignGeneralAnimationPresentation.AtResult(Double(events, 0), SourceBattleScene.On, null!, new bool[4]));
        Assert.Throws<ArgumentException>(() => CampaignGeneralAnimationPresentation.AtResult(Double(events, 0), SourceBattleScene.On, new MoveId?[2], new bool[4]));
        Assert.Throws<ArgumentException>(() => CampaignGeneralAnimationPresentation.AtResult(Double(events, 0), SourceBattleScene.On, new MoveId?[4], new bool[2]));
        Assert.Throws<ArgumentException>(() => CampaignGeneralAnimationPresentation.AtResult(Single(events, 0), true, SourceBattleScene.On, new MoveId?[4], new bool[2]));
        Assert.IsNull(CampaignGeneralAnimationPresentation.AtResult(Single(events, 0), false, SourceBattleScene.On, new MoveId?[2], new bool[2]));
        Assert.IsNull(CampaignGeneralAnimationPresentation.AtResult(Double(events, 0) with { Stage = CampaignDoubleBattleStage.Finished }, SourceBattleScene.On, new MoveId?[4], new bool[4]));
        Assert.IsNull(At(events, -1, true)); Assert.IsNull(At(events, events.Length, false));
    }

    private sealed record Batch(SourceBattleFrame Before, SourceFourActionResult Result);
    private static List<Batch> Turn(SourceBattleFrame frame, int actor, int target, int? interrupter = null,
        bool stopAtFuture = false)
    {
        var selections = frame.Slots.Take(frame.BattlerCount).Where(s => frame.CanAct(s.Slot)).Select(s => {
            var move = Mechanics.Catalog.Moves[s.Body!.Pokemon.Moves[0].Move];
            int ordinary = move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? s.Slot.Value : s.Slot.Value ^ 1;
            return new SourceActionSelection(s.Occupant!.Value, SourceActionKind.Move, 0,
                new(s.Slot.Value == actor ? target : s.Slot.Value == interrupter ? actor : ordinary));
        }).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(Mechanics.Catalog, frame, frame.Revision, selections);
        List<Batch> batches = [];
        for (int step = 0; step < 32; step++)
        {
            Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            batches.Add(new(frame, result));
            if (result.CompletedCleanTurn || stopAtFuture && result.Events.OfType<SourceCalledDelayedEvent>().Any(e => e.Reason == "resolved")) return batches;
            Assert.IsNotNull(result.Continuation); frame = result.State;
            result = SourceFourActionTurns.Advance(Mechanics.Catalog, frame, result.Continuation.Value);
        }
        Assert.Fail("The public action/end-turn lifecycle did not reach its bounded completion."); return batches;
    }
    private static WildBattleCombatant[] Bodies(string move, int actor)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Mon("SNORLAX", "DEFENSE_CURL", 100)).ToArray();
        bodies[actor] = move switch {
            "FOCUS_PUNCH" => Mon("VIGOROTH", move) with { AccuracyStage = 12 },
            "FUTURE_SIGHT" => Mon("KADABRA", move, 30) with { AccuracyStage = 12 },
            "DOOM_DESIRE" => Mon("JIRACHI", move, 50) with { AccuracyStage = 12 },
            _ => throw new ArgumentOutOfRangeException(nameof(move))
        };
        return bodies;
    }
    private static MoveId?[] Entry(Batch batch) => batch.Before.Slots.Take(batch.Before.BattlerCount).Select(s =>
        s.Registers.Turn?.Lock is { Kind: WildBattleCalledLockKind.Charge } charge ? (MoveId?)charge.Move : null).ToArray();
    private static bool[] Substitutes(Batch batch) => batch.Before.Slots.Take(batch.Before.BattlerCount)
        .Select(s => s.Body!.CalledVolatile?.SubstituteHp > 0).ToArray();
    private static Batch FutureBatch(SourceBattleFrame frame, int actor, int target)
    {
        for (int turn = 0; turn < 3; turn++)
        {
            var batches = Turn(frame, actor, target, stopAtFuture: true);
            var resolved = batches.SingleOrDefault(b => b.Result.Events.OfType<SourceCalledDelayedEvent>().Any(e => e.Reason == "resolved"));
            if (resolved is not null) return resolved;
            frame = batches[^1].Result.State;
        }
        throw new InvalidOperationException("The actual scheduled future attack did not resolve after three turns.");
    }
    private static CampaignGeneralAnimationDispatch CheckFutureBatch(Batch batch, string move, bool single, int actor, int target)
    {
        string before = JsonSerializer.Serialize(batch); var events = batch.Result.Events;
        var delayed = events.OfType<SourceCalledDelayedEvent>().Single(e => e.Reason == "resolved" && e.Move == Id(move));
        var hit = events.Single(e => e is SourceHpChangedEvent h && h.Move == Id(move) || e is SourceSubstituteDamagedEvent s && s.Move == Id(move));
        var cue = Check(events, single, delayed, hit, actor, target,
            move == "FUTURE_SIGHT" ? "General_FutureSightHit" : "General_DoomDesireHit", CampaignGeneralAnimationBoundary.AfterText,
            Entry(batch), Substitutes(batch));
        Assert.AreEqual(before, JsonSerializer.Serialize(batch)); return cue;
    }

    [TestMethod]
    public void ActualPublicPreparationOwnsItsCueEvenWhenAnEarlierHitLaterLosesFocus()
    {
        foreach (bool single in new[] { true, false }) foreach (bool interrupted in new[] { false, true })
        for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var bodies = Bodies("FOCUS_PUNCH", actor); int target = actor ^ 1;
            if (interrupted) bodies[target] = Mon("RATTATA", "TACKLE", 20) with { AccuracyStage = 12 };
            var batches = Turn(Frame(bodies, single), actor, target, interrupted ? target : null);
            var prepared = batches.Single(b => b.Result.Events.OfType<SourceFocusPunchPreparedEvent>().Any());
            var focus = prepared.Result.Events.OfType<SourceFocusPunchPreparedEvent>().Single();
            Check(prepared.Result.Events, single, focus, focus, actor, actor, "General_FocusPunchSetUp",
                CampaignGeneralAnimationBoundary.BeforeText, Entry(prepared), Substitutes(prepared));
            Assert.AreEqual(interrupted ? 1 : 0, batches.SelectMany(b => b.Result.Events).OfType<SourceFocusPunchLostEvent>().Count());
            foreach (var batch in batches.Where(b => b != prepared)) Deny(batch.Result.Events, single);
        }
    }

    [TestMethod]
    public void ActualThreeTurnFutureAndDoomImpactOwnOnePostTextCueAcrossAllLayouts()
    {
        foreach (string move in new[] { "FUTURE_SIGHT", "DOOM_DESIRE" }) foreach (bool single in new[] { true, false })
        for (int actor = 0; actor < (single ? 2 : 4); actor++)
            CheckFutureBatch(FutureBatch(Frame(Bodies(move, actor), single), actor, actor ^ 1), move, single, actor, actor ^ 1);
    }

    [TestMethod]
    public void ActualTargetSubstituteImpactKeepsTheStoredAttackerRatherThanTheTargetController()
    {
        foreach (string move in new[] { "FUTURE_SIGHT", "DOOM_DESIRE" }) foreach (bool single in new[] { true, false })
        {
            int actor = single ? 1 : 3, target = actor ^ 1; var bodies = Bodies(move, actor);
            bodies[target] = bodies[target] with { CalledVolatile = new() { SubstituteHp = 1 } };
            var batch = FutureBatch(Frame(bodies, single), actor, target);
            Assert.IsTrue(batch.Result.Events.OfType<SourceSubstituteDamagedEvent>().Any());
            CheckFutureBatch(batch, move, single, actor, target);
        }
    }

    [TestMethod]
    public void ActualFutureAccuracyFailureHasNoImpactCueAndPreservesCommittedRandomTrace()
    {
        foreach (string move in new[] { "FUTURE_SIGHT", "DOOM_DESIRE" }) foreach (bool single in new[] { true, false })
        {
            var bodies = Bodies(move, 0); bodies[0] = bodies[0] with { AccuracyStage = 0 };
            bodies[1] = bodies[1] with { EvasionStage = 12 }; Batch? found = null;
            for (uint seed = 0; seed < 64 && found is null; seed++)
            {
                var batch = FutureBatch(Frame(bodies, single) with { Rng = new(seed) }, 0, 1);
                if (batch.Result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "future-accuracy")) found = batch;
            }
            Assert.IsNotNull(found, "No actual future-accuracy miss found in 64 bounded seeds.");
            string before = JsonSerializer.Serialize(found); Deny(found.Result.Events, single);
            Assert.AreEqual(before, JsonSerializer.Serialize(found));
        }
    }

    [TestMethod]
    public void ActualFocusBandAtOneHpStillOwnsTheDelayedAnimation()
    {
        foreach (string move in new[] { "FUTURE_SIGHT", "DOOM_DESIRE" }) foreach (bool single in new[] { true, false })
        {
            var bodies = Bodies(move, 0); bodies[1] = bodies[1] with { HeldItem = "ITEM_FOCUS_BAND",
                Pokemon = bodies[1].Pokemon with { CurrentHp = 1 } }; Batch? found = null;
            for (uint seed = 0; seed < 256 && found is null; seed++)
            {
                var batch = FutureBatch(Frame(bodies, single) with { Rng = new(seed) }, 0, 1);
                if (batch.Result.Events.OfType<SourceDamageAdjustedEvent>().Any(e => e.HungOn)) found = batch;
            }
            Assert.IsNotNull(found, "No actual Focus Band delayed hit found in 256 bounded seeds.");
            var hp = found.Result.Events.OfType<SourceHpChangedEvent>().Single(e => e.Move == Id(move));
            Assert.AreEqual(1, hp.HpBefore); Assert.AreEqual(1, hp.HpAfter); Assert.AreEqual(0, hp.Damage);
            CheckFutureBatch(found, move, single, 0, 1);
        }
    }
}
