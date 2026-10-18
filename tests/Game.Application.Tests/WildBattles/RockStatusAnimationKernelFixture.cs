using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Legal = FireRed.Tests.Application.WildBattles.ConditionalRecoveryAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

internal static class RockStatusAnimationKernelFixture
{
    internal static (string Name, string Effect) Spec(int id) => id switch {
        246 => ("ANCIENT_POWER", "EFFECT_ALL_STATS_UP_HIT"), 317 => ("ROCK_TOMB", "EFFECT_SPEED_DOWN_HIT"),
        197 => ("DETECT", "EFFECT_PROTECT"), 203 => ("ENDURE", "EFFECT_ENDURE"), 50 => ("DISABLE", "EFFECT_DISABLE"),
        _ => throw new ArgumentOutOfRangeException(nameof(id)) };
    internal static MoveId Move(int id) => Kernel.Id(Spec(id).Name);
    internal static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with {
            SpeedStage = 0, LastUsedMove = Kernel.Id("SPLASH") }).ToArray();
        bodies[actor] = (id switch {
            246 => Kernel.Mon("OMANYTE", "ANCIENT_POWER"),
            317 => Legal.MachineMon("GEODUDE", "TM39", "ROCK_TOMB"),
            197 => Kernel.Mon("HITMONCHAN", "DETECT"),
            203 => Kernel.Mon("HITMONLEE", "ENDURE"),
            50 => Kernel.Mon("PSYDUCK", "DISABLE"),
            _ => throw new ArgumentOutOfRangeException(nameof(id)) }) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    internal static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> events) =>
        events.Select((e, i) => e with { Sequence = i + 1 }).ToImmutableArray();
    internal static CampaignStatAnimationDispatch? Wave(ImmutableArray<SourceBattleEvent> events, int i, bool single) => single
        ? AnimationDispatchFixture.StatAtResult(Envelope.Single(events, i), true)
        : AnimationDispatchFixture.StatAtResult(Envelope.Double(events, i));
    internal static void Deny(ImmutableArray<SourceBattleEvent> events, bool single) =>
        Envelope.Deny(events, single, "A malformed rock/status receipt cannot own the selected root.");
    internal static void Check(SourceKernelResult result, int id, bool single, SourceBattleEvent? owner) => Check(result.Events, result, id, single, owner);
    internal static void Check(SourceFourActionResult result, int id, bool single, SourceBattleEvent? owner) => Check(result.Events, result, id, single, owner);
    private static void Check(ImmutableArray<SourceBattleEvent> events, object result, int id, bool single, SourceBattleEvent? owner)
    {
        string before = JsonSerializer.Serialize(result); var used = events.OfType<SourceMoveUsedEvent>().Single();
        var attack = events.OfType<SourceAttackStringEvent>().Single(); var pp = events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(id, Kernel.Mechanics.Catalog.Moves[used.Move].SourceMoveId); Assert.AreEqual(used.Actor, pp.Actor); Assert.AreEqual(used.Actor, pp.PpOwner);
        Assert.AreEqual(used.Target, pp.Target); Assert.AreEqual(used.Move, pp.CurrentMove); Assert.AreEqual(used.Move, pp.ChosenMove);
        Assert.AreEqual(used.Slot, pp.PpSlot); Assert.AreEqual(used.PpBefore, pp.Before); Assert.AreEqual(used.PpAfter, pp.After);
        Assert.AreEqual(Math.Max(0, pp.Before - pp.CommandCost), pp.After); int count = 0;
        for (int i = 0; i < events.Length; i++)
        {
            var decision = Kernel.Project(events, i, single); Assert.AreEqual(decision, Kernel.Project(events, i, single));
            Assert.IsNull(Kernel.Project(events, i, single, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(single ? AnimationDispatchFixture.AtResult(Envelope.Single(events, i), true, SourceBattleScene.On).Dispatch
                : AnimationDispatchFixture.AtResult(Envelope.Double(events, i), SourceBattleScene.On).Dispatch);
            if (owner is null || events[i] != used) { Assert.IsNull(decision.Dispatch); continue; }
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); count++;
            Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Target, cue.Target);
            Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
            Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(owner.Sequence, cue.ResultSequence);
            Assert.AreEqual(owner is SourceSubstituteDamagedEvent, cue.HitsSubstitute);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText, cue.Boundary); Assert.IsTrue(cue.AcknowledgesCurrentReceipt);
        }
        Assert.AreEqual(owner is null ? 0 : 1, count); Assert.AreEqual(before, JsonSerializer.Serialize(result));
    }
    internal static SourceFourActionResult LastProtection(int id, bool single)
    {
        // All choices have the same source priority; only actual speed orders the tested actor last.
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("HITMONCHAN", "DETECT") with { SpeedStage = 12 }).ToArray();
        bodies[0] = Bodies(id)[0] with { SpeedStage = 0 }; var frame = Kernel.Frame(bodies, single);
        var choices = frame.Slots.Take(frame.BattlerCount).Select(s =>
            new SourceActionSelection(s.Occupant!.Value, SourceActionKind.Move, 0, s.Slot)).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog, frame, frame.Revision, choices);
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.AreEqual(0, result.State.Queue[^1].Slot.Value);
        for (int step = 0; !result.Events.OfType<SourceMoveUsedEvent>().Any(e => e.Actor.Value == 0); step++)
        {
            Assert.IsTrue(step < 16); Assert.IsNotNull(result.Continuation);
            result = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, result.State, result.Continuation.Value);
            Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        }
        return result;
    }
}
