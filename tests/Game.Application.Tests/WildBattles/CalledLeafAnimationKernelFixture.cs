using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// Authentic kernel receipts remain untouched, including the selected caller,
// current leaf, original sequence, persistent learned slot and PP owner.
internal static class CalledLeafAnimationKernelFixture
{
    internal static SourceBattleFrame Nature(bool single, WildBattleTerrain terrain, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = Kernel.Mon("LOTAD", "NATURE_POWER", 50) with { AccuracyStage = 12, SpeedStage = 12 };
        var frame = Kernel.Frame(bodies, single);
        return frame with { Field = frame.Field with { Terrain = terrain } };
    }

    internal static SourceBattleFrame Mirror(bool single, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = Kernel.Mon("PIDGEY", "MIRROR_MOVE", 100) with { AccuracyStage = 12, SpeedStage = 12 };
        bodies[actor ^ 1] = Kernel.Mon("SNORLAX", "TACKLE", 100) with { AccuracyStage = 12, SpeedStage = 0 };
        var first = Envelope.Resolve(Kernel.Frame(bodies, single), actor ^ 1, actor);
        Assert.IsTrue(first.Events.OfType<SourceHpChangedEvent>().Any(e => e.Target.Value == actor && e.Move == Kernel.Id("TACKLE")));
        Assert.AreEqual(Kernel.Id("TACKLE"), first.State.Slots[actor].Registers.History!.LastTakenMove);
        Assert.IsTrue(first.State.Slots[actor].Body!.Pokemon.CurrentHp > 0);
        return first.State;
    }

    internal static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events, int index, bool single,
        bool result = false, SourceBattleScene scene = SourceBattleScene.On) => result
        ? single ? AnimationDispatchFixture.AtResult(Envelope.Single(events, index), true, scene)
            : AnimationDispatchFixture.AtResult(Envelope.Double(events, index), scene)
        : Kernel.Project(events, index, single, scene);

    internal static CampaignMoveAnimationDispatch Check(SourceKernelResult result, bool single, MoveId caller, int leaf) =>
        Check(result.Events, result, single, caller, leaf);
    internal static CampaignMoveAnimationDispatch Check(SourceFourActionResult result, bool single, MoveId caller, int leaf) =>
        Check(result.Events, result, single, caller, leaf);

    internal static CampaignMoveAnimationDispatch Check(ImmutableArray<SourceBattleEvent> events, object committed,
        bool single, MoveId caller, int leaf)
    {
        var definition = Kernel.Mechanics.Catalog.Moves.Values.Single(m => m.SourceMoveId == leaf);
        var used = events.OfType<SourceMoveUsedEvent>().Single(e => e.Move == definition.Id);
        bool Prevented(BattlerSlot target) => events.Any(e =>
            e is SourceMoveFailedEvent f && f.Actor == used.Actor && f.Target == target && f.Move == used.Move ||
            e is SourceMoveMissedEvent m && m.Actor == used.Actor && m.Target == target && m.Move == used.Move ||
            e is SourceAbilityPreventedEvent a && a.Actor == used.Actor && a.Target == target && a.Move == used.Move ||
            e is SourceEffectivenessEvent { Effectiveness: WildBattleEffectiveness.NoEffect } n && n.Actor == used.Actor && n.Target == target && n.Move == used.Move);
        var outcome = events.First(e => e.Sequence > used.Sequence && (e switch {
            SourceHpChangedEvent h => h.Actor == used.Actor && h.Move == used.Move && !Prevented(h.Target),
            SourceSubstituteDamagedEvent h => h.Actor == used.Actor && h.Move == used.Move && !Prevented(h.Target),
            SourceStatusChangedEvent status => status.Side == used.Target && status.Reason == "move",
            SourceHeldItemsSwappedEvent item => item.Actor == used.Actor && item.Target == used.Target && item.Move == used.Move,
            SourceHeldItemRecycledEvent item => item.Actor == used.Actor && item.Move == used.Move,
            SourceMoveFailedEvent failure => definition.Effect == "EFFECT_SPLASH" && failure.Actor == used.Actor &&
                failure.Target == used.Actor && failure.Move == used.Move && failure.Reason == "nothing-happened",
            _ => false
        }));
        var target = outcome switch { SourceHpChangedEvent h => h.Target, SourceSubstituteDamagedEvent h => h.Target, _ => used.Target };
        return Check(events, committed, single, caller, leaf, outcome, target: target);
    }

    internal static CampaignMoveAnimationDispatch Check(ImmutableArray<SourceBattleEvent> events, object committed,
        bool single, MoveId caller, int leaf, SourceBattleEvent outcome,
        CampaignMoveAnimationBoundary boundary = CampaignMoveAnimationBoundary.AfterAttackText, BattlerSlot? target = null) =>
        CheckMany(events, committed, single, caller, leaf, [(outcome, boundary, target)]).Single();

    internal static IReadOnlyList<CampaignMoveAnimationDispatch> CheckMany(ImmutableArray<SourceBattleEvent> events, object committed,
        bool single, MoveId caller, int leaf,
        IReadOnlyList<(SourceBattleEvent Outcome, CampaignMoveAnimationBoundary Boundary, BattlerSlot? Target)> expected)
    {
        string before = JsonSerializer.Serialize(committed);
        Assert.IsTrue(expected.Count > 0, "A positive leaf check must name at least one real expected outcome.");
        var definition = Kernel.Mechanics.Catalog.Moves.Values.Single(m => m.SourceMoveId == leaf);
        Assert.IsTrue(AnimationDispatchFixture.Supports(leaf, definition.Id.Value), "Require the real generated ID/label pair.");
        var used = events.OfType<SourceMoveUsedEvent>().Single(e => e.Move == definition.Id);
        var attack = events.OfType<SourceAttackStringEvent>().Single(e => e.CurrentMove == definition.Id);
        var callerAttack = events.OfType<SourceAttackStringEvent>().Single(e => e.CurrentMove == caller);
        var selection = events.OfType<SourceCallerSelectionEvent>().Single(e => e.Caller == caller);
        var transition = events.OfType<SourceCalledMoveTransitionEvent>().Single();
        var invocation = transition.Invocation;
        var commands = events.OfType<SourcePpCommandEvent>().Where(e => e.CurrentMove == definition.Id).ToArray();
        Assert.IsTrue(commands.Length > 0);
        Assert.AreEqual(caller, callerAttack.ChosenMove); Assert.AreEqual(caller, attack.ChosenMove);
        Assert.AreEqual(caller, invocation.OriginalChosenMove); Assert.AreEqual(caller, invocation.ChosenMove);
        Assert.AreEqual(definition.Id, selection.Result); Assert.AreEqual(definition.Id, invocation.CurrentMove);
        Assert.AreEqual(used.Actor, selection.Actor); Assert.AreEqual(used.Actor, invocation.Actor);
        Assert.AreEqual(used.Actor, invocation.PpOwner); Assert.AreEqual(used.Target, invocation.Target);
        Assert.AreEqual(used.Slot, invocation.PpSlot); Assert.AreEqual(used.Actor, callerAttack.Actor);
        Assert.AreEqual(WildBattleAttackCancellerPhase.ConditionsPassed, invocation.CancellerPhase);
        bool sleep = caller == Kernel.Id("SLEEP_TALK");
        Assert.AreEqual(WildBattleInvocationFlags.Obeys | (sleep ? WildBattleInvocationFlags.NoPpDeduct : WildBattleInvocationFlags.None), invocation.Flags);
        Assert.IsTrue(callerAttack.Sequence < selection.Sequence && selection.Sequence < transition.Sequence &&
            transition.Sequence < attack.Sequence && attack.Sequence < used.Sequence);
        Assert.AreEqual(attack.Sequence + 1, used.Sequence);
        Assert.AreEqual(used.Sequence + 1, commands[0].Sequence);
        Assert.AreEqual(used.PpBefore, commands[0].Before); Assert.AreEqual(used.PpAfter, commands[0].After);
        Assert.AreEqual(sleep ? commands[0].Before : Math.Max(0, commands[0].Before - commands[0].CommandCost), commands[0].After);
        Assert.AreEqual(invocation.PpSlot, commands[0].PpSlot);
        foreach (var pp in commands)
        {
            Assert.AreEqual(caller, pp.ChosenMove); Assert.AreEqual(definition.Id, pp.CurrentMove);
            Assert.AreEqual(used.Actor, pp.Actor); Assert.AreEqual(invocation.PpOwner, pp.PpOwner);
            Assert.IsTrue(pp.CommandCost > 0);
        }
        foreach (var item in expected)
        {
            Assert.IsTrue(events.Contains(item.Outcome), "Expect the unchanged committed outcome object.");
            Assert.AreEqual(used.Turn, item.Outcome.Turn);
            Assert.IsTrue(item.Outcome.Sequence >= commands[0].Sequence);
            Assert.IsTrue(item.Boundary is CampaignMoveAnimationBoundary.AfterAttackText or CampaignMoveAnimationBoundary.BeforeHitResult or
                CampaignMoveAnimationBoundary.BeforeRecoveryResult or CampaignMoveAnimationBoundary.BeforeStatusResult);
        }
        var found = new List<CampaignMoveAnimationDispatch>();
        for (int i = 0; i < events.Length; i++) foreach (bool atResult in new[] { false, true })
        {
            var decision = Project(events, i, single, atResult);
            Assert.AreEqual(decision, Project(events, i, single, atResult));
            Assert.IsNull(Project(events, i, single, atResult, SourceBattleScene.Off).Dispatch);
            if (single)
            {
                var legacy = Envelope.Single(events, i) with { SourceEvents = [] };
                Assert.AreEqual(decision, atResult ? AnimationDispatchFixture.AtResult(legacy, true, SourceBattleScene.On)
                    : AnimationDispatchFixture.AtAttackText(legacy, true, SourceBattleScene.On));
                Assert.IsNull(atResult ? AnimationDispatchFixture.AtResult(legacy, false, SourceBattleScene.On).Dispatch
                    : AnimationDispatchFixture.AtAttackText(legacy, false, SourceBattleScene.On).Dispatch);
            }
            var owns = expected.Where(x => atResult == (x.Boundary != CampaignMoveAnimationBoundary.AfterAttackText) &&
                events[i].Sequence == (atResult ? x.Outcome.Sequence : used.Sequence)).ToArray();
            Assert.IsTrue(owns.Length <= 1, "Expected roots must have distinct actual presentation cursors.");
            if (owns.Length == 0)
            {
                Assert.IsFalse(decision.Dispatch?.SourceMoveId == leaf, "Caller text, transition and unrelated later results must not replay the leaf.");
                if (decision.Dispatch is {} prelude)
                {
                    Assert.IsFalse(atResult, "A caller prelude cannot be replayed from a result cursor.");
                    var callerDefinition = Kernel.Mechanics.Catalog.Moves[caller];
                    Assert.IsTrue(callerDefinition.Effect is "EFFECT_METRONOME" or "EFFECT_ASSIST" or "EFFECT_SLEEP_TALK");
                    var callerUsed = events.OfType<SourceMoveUsedEvent>().SingleOrDefault(u => u.Move == caller);
                    var callerPp = events.OfType<SourcePpCommandEvent>().SingleOrDefault(p => p.CurrentMove == caller);
                    long cursor = sleep ? callerUsed!.Sequence : callerAttack.Sequence;
                    Assert.AreEqual(cursor, events[i].Sequence); Assert.AreEqual(cursor, prelude.Sequence);
                    Assert.AreEqual(callerDefinition.SourceMoveId, prelude.SourceMoveId);
                    Assert.AreEqual(CampaignMoveAnimationBoundary.AfterCallerText, prelude.Boundary);
                    Assert.IsTrue(prelude.AcknowledgesCurrentReceipt); Assert.IsFalse(prelude.HitsSubstitute);
                    Assert.AreEqual(callerAttack.Sequence, prelude.AttackSequence); Assert.AreEqual(callerPp?.Sequence, prelude.PpSequence);
                    Assert.AreEqual(callerPp?.Sequence ?? cursor, prelude.SnapshotSequence);
                    Assert.AreEqual(callerDefinition.Effect == "EFFECT_METRONOME" ? cursor : selection.Sequence, prelude.ResultSequence);
                    Assert.AreEqual(used.Actor, prelude.Actor);
                    Assert.AreEqual(callerDefinition.Effect == "EFFECT_METRONOME" ? used.Actor : invocation.Target, prelude.Target);
                }
                continue;
            }
            var expectation = owns[0]; var target = expectation.Target ?? used.Target;
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); found.Add(cue);
            Assert.AreEqual(leaf, cue.SourceMoveId); Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(target, cue.Target);
            Assert.AreEqual(events[i].Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
            Assert.AreEqual(expectation.Boundary, cue.Boundary);
            Assert.AreEqual(!atResult, cue.AcknowledgesCurrentReceipt); Assert.AreEqual(expectation.Outcome.Sequence, cue.ResultSequence);
            var ownedPp = commands.FirstOrDefault(p => p.Target == target && p.Sequence <= expectation.Outcome.Sequence) ?? commands[0];
            Assert.AreEqual(ownedPp.Sequence, cue.PpSequence); Assert.AreEqual(ownedPp.Sequence, cue.SnapshotSequence);
            Assert.AreEqual(expectation.Outcome is SourceSubstituteDamagedEvent, cue.HitsSubstitute);
        }
        Assert.AreEqual(expected.Count, found.Count);
        Assert.AreEqual(before, JsonSerializer.Serialize(committed), "Projection must preserve state, RNG, trace and all receipts.");
        return found;
    }

    internal static bool HasSimpleLeafResult(ImmutableArray<SourceBattleEvent> events, MoveId leaf) =>
        events.OfType<SourceMoveUsedEvent>().Any(u => u.Move == leaf && (
            events.OfType<SourceHpChangedEvent>().Any(h => h.Actor == u.Actor && h.Move == leaf && h.Sequence > u.Sequence &&
                !events.Any(e => e is SourceMoveFailedEvent f && f.Actor == u.Actor && f.Target == h.Target && f.Move == leaf ||
                    e is SourceMoveMissedEvent m && m.Actor == u.Actor && m.Target == h.Target && m.Move == leaf ||
                    e is SourceAbilityPreventedEvent a && a.Actor == u.Actor && a.Target == h.Target && a.Move == leaf)) ||
            events.OfType<SourceMoveFailedEvent>().Any(f => leaf == Kernel.Id("SPLASH") && f.Actor == u.Actor &&
                f.Target == u.Actor && f.Move == leaf && f.Reason == "nothing-happened" && f.Sequence > u.Sequence)));

    internal static void AssertSelectedTextCue(ImmutableArray<SourceBattleEvent> events, int index,
        SourceCallerSelectionEvent selection, SourceCalledMoveTransitionEvent transition, CampaignMoveAnimationDispatch? cue)
    {
        if (cue is null) return;
        Assert.IsInstanceOfType<SourceMoveUsedEvent>(events[index]); var used = (SourceMoveUsedEvent)events[index];
        var definition = Kernel.Mechanics.Catalog.Moves[selection.Result!.Value];
        Assert.AreEqual(definition.Id, used.Move); Assert.AreEqual(definition.SourceMoveId, cue.SourceMoveId);
        Assert.AreEqual(selection.Actor, used.Actor); Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Sequence, cue.Sequence);
        Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText, cue.Boundary); Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
        Assert.IsTrue(index > 0); Assert.IsInstanceOfType<SourceAttackStringEvent>(events[index - 1]);
        var attack = (SourceAttackStringEvent)events[index - 1];
        Assert.AreEqual(selection.Caller, attack.ChosenMove); Assert.AreEqual(used.Move, attack.CurrentMove);
        Assert.AreEqual(used.Actor, attack.Actor); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
        var pp = events.OfType<SourcePpCommandEvent>().Single(p => p.Sequence == cue.PpSequence);
        Assert.AreEqual(used.Actor, pp.Actor); Assert.AreEqual(transition.Invocation.PpOwner, pp.PpOwner);
        Assert.AreEqual(selection.Caller, pp.ChosenMove); Assert.AreEqual(used.Move, pp.CurrentMove);
        Assert.AreEqual(transition.Invocation.PpSlot, pp.PpSlot); Assert.AreEqual(pp.Sequence, cue.SnapshotSequence);
        Assert.IsTrue(used.Sequence < pp.Sequence && pp.Sequence < cue.ResultSequence);
        var outcome = events.Single(e => e.Sequence == cue.ResultSequence);
        bool owned = outcome switch {
            SourceHpChangedEvent h => h.Actor == used.Actor && h.Target == cue.Target && h.Move == used.Move,
            SourceSubstituteDamagedEvent h => h.Actor == used.Actor && h.Target == cue.Target && h.Move == used.Move,
            SourceStatChangedEvent s => s.Actor == used.Actor && s.Target == cue.Target,
            SourceStatAtMinimumEvent s => s.Actor == used.Actor && s.Target == cue.Target,
            SourceStatAtMaximumEvent s => s.Actor == used.Actor && s.Target == cue.Target,
            SourceMtMoonVolatileEvent v => definition.Effect == "EFFECT_PERISH_SONG" && v.Volatile == "perish-song" && v.Reason == "move",
            SourceMoveFailedEvent f => definition.Effect == "EFFECT_SPLASH" && f.Actor == used.Actor && f.Target == used.Actor &&
                f.Move == used.Move && f.Reason == "nothing-happened" && cue.Target == used.Actor,
            _ => false
        };
        Assert.IsTrue(owned, "Only the actual selected leaf's committed outcome may accompany the caller prelude.");
        Assert.AreEqual(outcome is SourceSubstituteDamagedEvent, cue.HitsSubstitute);
    }

    internal static void Deny(ImmutableArray<SourceBattleEvent> events, bool single, int leaf)
    {
        string before = JsonSerializer.Serialize(events);
        for (int i = 0; i < events.Length; i++)
        {
            Assert.IsFalse(Project(events, i, single).Dispatch?.SourceMoveId == leaf, "Unproved caller provenance cannot authorize the leaf text root.");
            Assert.IsFalse(Project(events, i, single, result: true).Dispatch?.SourceMoveId == leaf, "Unproved caller provenance cannot authorize a result root.");
        }
        Assert.AreEqual(before, JsonSerializer.Serialize(events));
    }
}
