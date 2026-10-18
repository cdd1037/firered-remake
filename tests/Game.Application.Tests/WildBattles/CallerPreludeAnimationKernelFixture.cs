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

// Source-natural caller/leaf learners; controlled sleep and bounded source selection
// are integration fixtures. No caller execution, selection, or PP rules are copied here.
internal static class CallerPreludeAnimationKernelFixture
{
    internal static MoveId Move(int id) => Kernel.Id(id switch { 118 => "METRONOME", 214 => "SLEEP_TALK", _ => throw new ArgumentOutOfRangeException(nameof(id)) });
    internal static WildBattleCombatant[] Bodies(int id, int actor = 0, string? sleepingLeaf = "TACKLE")
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100)).ToArray();
        var caller = id == 118 ? Kernel.Mon("TOGETIC", "METRONOME", 29) : Kernel.Mon("SNORLAX", "SLEEP_TALK");
        if (id == 214 && sleepingLeaf is not null)
        {
            var learned = Kernel.Mon("SNORLAX", sleepingLeaf).Pokemon.Moves[0];
            caller = caller with { Pokemon = caller.Pokemon with { Moves = caller.Pokemon.Moves.Add(learned) } };
        }
        bodies[actor] = caller with { Status = id == 214 ? 3u : 0u, AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    internal static uint MetronomeSeed(int leaf = 33)
    {
        for (uint seed = 0; seed < 65536; seed++)
            if (WildBattleSourceCallers.Metronome(new(seed)).SourceMoveId == leaf) return seed;
        throw new InvalidOperationException($"No source Metronome selection for {leaf} in the bounded seed set.");
    }
    internal static SourceBattleFrame Frame(int id, bool single, int actor = 0, string? sleepingLeaf = "TACKLE") =>
        Prepare(Kernel.Frame(Bodies(id, actor, sleepingLeaf), single), id);
    internal static SourceBattleFrame Prepare(SourceBattleFrame frame, int id) => id == 118 ? frame with { Rng = new(MetronomeSeed()) } : frame;
    internal static CampaignMoveAnimationDecision Result(ImmutableArray<SourceBattleEvent> events, int i, bool single) => single
        ? AnimationDispatchFixture.AtResult(Envelope.Single(events, i), true, SourceBattleScene.On)
        : AnimationDispatchFixture.AtResult(Envelope.Double(events, i), SourceBattleScene.On);
    internal static void DenyLeaf(ImmutableArray<SourceBattleEvent> events, bool single, int leaf)
    {
        string before = JsonSerializer.Serialize(events);
        for (int i = 0; i < events.Length; i++)
        {
            Assert.IsFalse(Kernel.Project(events, i, single).Dispatch?.SourceMoveId == leaf, "Called leaf remains outside direct-root admission.");
            Assert.IsFalse(Result(events, i, single).Dispatch?.SourceMoveId == leaf, "Called result remains outside direct-root admission.");
        }
        Assert.AreEqual(before, JsonSerializer.Serialize(events));
    }
    internal static void DenyPrelude(ImmutableArray<SourceBattleEvent> events, bool single, int id) => DenyLeaf(events, single, id);
    internal static CampaignMoveAnimationDispatch Check(SourceKernelResult result, int id, bool single) => Check(result.Events, result, id, single);
    internal static CampaignMoveAnimationDispatch Check(ImmutableArray<SourceBattleEvent> events, object committed, int id, bool single)
    {
        string before = JsonSerializer.Serialize(committed);
        var caller = Move(id); var attack = events.OfType<SourceAttackStringEvent>().Single(e => e.CurrentMove == caller);
        var selection = events.OfType<SourceCallerSelectionEvent>().Single(e => e.Caller == caller);
        var transition = events.OfType<SourceCalledMoveTransitionEvent>().Single();
        var used = events.OfType<SourceMoveUsedEvent>().SingleOrDefault(e => e.Move == caller);
        var pp = events.OfType<SourcePpCommandEvent>().SingleOrDefault(e => e.CurrentMove == caller);
        Assert.AreEqual(caller, attack.ChosenMove); Assert.AreEqual(attack.Actor, selection.Actor);
        Assert.AreEqual(selection.Result, transition.Invocation.CurrentMove); Assert.AreEqual(attack.Actor, transition.Invocation.PpOwner);
        Assert.AreEqual(caller, transition.Invocation.OriginalChosenMove); Assert.AreEqual(caller, transition.Invocation.ChosenMove);
        Assert.IsTrue(attack.Sequence < selection.Sequence && selection.Sequence < transition.Sequence);
        long cursor = id == 118 ? attack.Sequence : used!.Sequence;
        if (id == 118) { Assert.IsNull(used); Assert.IsNull(pp); }
        else
        {
            Assert.IsNotNull(used); Assert.IsNotNull(pp); Assert.AreEqual(used.Actor, pp.PpOwner);
            Assert.AreEqual(used.Target, pp.Target); Assert.AreEqual(used.Slot, pp.PpSlot);
            Assert.AreEqual(used.PpBefore, pp.Before); Assert.AreEqual(used.PpAfter, pp.After);
            Assert.AreEqual(Math.Max(0, pp.Before - pp.CommandCost), pp.After);
            Assert.IsTrue(used.Sequence < pp.Sequence && pp.Sequence < selection.Sequence);
        }
        CampaignMoveAnimationDispatch? found = null;
        for (int i = 0; i < events.Length; i++)
        {
            var decision = Kernel.Project(events, i, single);
            Assert.AreEqual(decision, Kernel.Project(events, i, single));
            Assert.IsNull(Kernel.Project(events, i, single, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(Result(events, i, single).Dispatch);
            if (single) Assert.IsNull(AnimationDispatchFixture.AtAttackText(Envelope.Single(events, i), false, SourceBattleScene.On).Dispatch);
            if (events[i].Sequence != cursor)
            {
                CalledLeafAnimationKernelFixture.AssertSelectedTextCue(events, i, selection, transition, decision.Dispatch);
                continue;
            }
            Assert.IsNull(found); var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); found = cue;
            Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(attack.Actor, cue.Actor);
            Assert.AreEqual(id == 118 ? attack.Actor : transition.Invocation.Target, cue.Target);
            Assert.AreEqual(cursor, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
            Assert.AreEqual(pp?.Sequence, cue.PpSequence); Assert.AreEqual(id == 118 ? cursor : selection.Sequence, cue.ResultSequence);
            Assert.AreEqual(pp?.Sequence ?? cursor, cue.SnapshotSequence);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterCallerText, cue.Boundary); Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
            Assert.IsFalse(cue.HitsSubstitute); Assert.IsNull(cue.MoveTurn); Assert.IsNull(cue.MovePower);
            Assert.IsNull(cue.MoveDamage); Assert.IsNull(cue.FuryCutterCounter);
        }
        Assert.IsNotNull(found);
        if (selection.Result == Kernel.Id("TACKLE") && CalledLeafAnimationKernelFixture.HasSimpleLeafResult(events, selection.Result.Value))
            CalledLeafAnimationKernelFixture.Check(events, committed, single, caller, 33);
        Assert.AreEqual(before, JsonSerializer.Serialize(committed), "Projection preserves committed state, RNG and trace.");
        return found;
    }
}
