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

// Authenticated source-natural learners, with explicitly controlled battle state.
// Tests never bypass the permanent Pokemon validator or invent learned duplicate slots.
internal static class SupportStatusAnimationKernelFixture
{
    internal static MoveId Move(int id) => Kernel.Id(id switch {
        270 => "HELPING_HAND", 286 => "IMPRISON", 298 => "TEETER_DANCE", _ => throw new ArgumentOutOfRangeException(nameof(id)) });
    internal static WildBattleCombatant NaturalMoves(WildBattleCombatant body, params string[] names)
    {
        foreach (var name in names)
            Assert.IsTrue(Kernel.Mechanics.Catalog.Species[body.Pokemon.Species].LevelUpMoves.Any(m =>
                m.Move == Kernel.Id(name) && m.Level <= body.Pokemon.Level), "Every controlled slot must be source-learnable: " + name);
        return body with { Pokemon = body.Pokemon with { Moves = names.Select(name => {
            var move = Kernel.Id(name); int pp = Kernel.Mechanics.Catalog.Moves[move].Pp;
            return new Game.Domain.Pokemon.PokemonMoveSlot(move, pp, pp);
        }).ToImmutableArray() } };
    }
    internal static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "GROWL", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = (id switch {
            270 => Kernel.Mon("PLUSLE", "HELPING_HAND"),
            286 => NaturalMoves(Kernel.Mon("GARDEVOIR", "IMPRISON"), "IMPRISON", "GROWL", "PSYCHIC"),
            298 => Kernel.Mon("SPINDA", "TEETER_DANCE"),
            _ => throw new ArgumentOutOfRangeException(nameof(id)) }) with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    internal static WildBattleCombatant Pressure() => Kernel.Mon("MEWTWO", "PSYCHIC", 100) with { SpeedStage = 0 };
    internal static SourceKernelResult ResolveSlot(SourceBattleFrame frame, int actor, int target, int slot)
    {
        string before = JsonSerializer.Serialize(frame);
        var result = SourceBattleKernel.ResolveAction(Kernel.Mechanics.Catalog, frame,
            new(frame.Get(new(actor)).Occupant!.Value, frame.Revision, slot, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.AreEqual(before, JsonSerializer.Serialize(frame)); return result;
    }
    internal static SourceMtMoonVolatileEvent? Setup(ImmutableArray<SourceBattleEvent> events, int id) =>
        events.OfType<SourceMtMoonVolatileEvent>().SingleOrDefault(e => e.Volatile == (id == 270 ? "helping-hand" : "imprison") && e.Reason == "move");
    internal static SourceConfusionChangedEvent[] Confusions(ImmutableArray<SourceBattleEvent> events) =>
        events.OfType<SourceConfusionChangedEvent>().Where(e => e.Reason == "teeter-dance").ToArray();
    internal static ImmutableArray<SourceBattleEvent> Number(IEnumerable<SourceBattleEvent> events) =>
        events.Select((e, i) => e with { Sequence = i + 1 }).ToImmutableArray();
    internal static CampaignMoveAnimationDecision Result(ImmutableArray<SourceBattleEvent> events, int index, bool single,
        SourceBattleScene scene = SourceBattleScene.On) => single
        ? AnimationDispatchFixture.AtResult(Envelope.Single(events, index), true, scene)
        : AnimationDispatchFixture.AtResult(Envelope.Double(events, index), scene);
    internal static void Deny(ImmutableArray<SourceBattleEvent> events, bool single)
    {
        for (int i = 0; i < events.Length; i++)
        {
            Assert.IsNull(Kernel.Project(events, i, single).Dispatch, "Malformed support/status attack envelope must fail closed.");
            Assert.IsNull(Result(events, i, single).Dispatch, "Malformed support/status result envelope must fail closed.");
        }
    }
    internal static void Check(SourceKernelResult result, int id, bool single, SourceBattleEvent? owner) =>
        Check(result.Events, result, id, single, owner);
    internal static void Check(SourceFourActionResult result, int id, bool single, SourceBattleEvent? owner) =>
        Check(result.Events, result, id, single, owner);
    internal static void Check(ImmutableArray<SourceBattleEvent> events, object committed, int id, bool single, SourceBattleEvent? owner)
    {
        string before = JsonSerializer.Serialize(committed);
        var used = events.OfType<SourceMoveUsedEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().First();
        Assert.AreEqual(Move(id), used.Move); Assert.AreEqual(used.Move, attack.ChosenMove); Assert.AreEqual(used.Move, attack.CurrentMove);
        Assert.AreEqual(used.Actor, attack.Actor); Assert.AreEqual(used.Actor, pp.Actor); Assert.AreEqual(used.Actor, pp.PpOwner);
        Assert.AreEqual(used.Target, pp.Target); Assert.AreEqual(used.Slot, pp.PpSlot);
        Assert.AreEqual(used.PpBefore, pp.Before); Assert.AreEqual(used.PpAfter, pp.After);
        Assert.AreEqual(Math.Max(0, pp.Before - pp.CommandCost), pp.After);
        bool late = id == 298; int cues = 0;
        int target = owner switch { SourceMtMoonVolatileEvent x => x.Side.Value, SourceConfusionChangedEvent x => x.Side.Value, _ => used.Target.Value };
        for (int i = 0; i < events.Length; i++)
        {
            var text = Kernel.Project(events, i, single); var result = Result(events, i, single);
            Assert.AreEqual(text, Kernel.Project(events, i, single)); Assert.AreEqual(result, Result(events, i, single));
            Assert.IsNull(Kernel.Project(events, i, single, SourceBattleScene.Off).Dispatch);
            Assert.IsNull(Result(events, i, single, SourceBattleScene.Off).Dispatch);
            var decision = late ? result : text; Assert.IsNull((late ? text : result).Dispatch);
            if (owner is null || events[i] != (late ? owner : used)) Assert.IsNull(decision.Dispatch);
            else
            {
                var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); cues++;
                Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(target, cue.Target.Value);
                Assert.AreEqual((late ? owner : used).Sequence, cue.Sequence); Assert.AreEqual(owner.Sequence, cue.ResultSequence);
                Assert.AreEqual(attack.Sequence, cue.AttackSequence); Assert.AreEqual(pp.Sequence, cue.PpSequence);
                Assert.AreEqual(late ? CampaignMoveAnimationBoundary.BeforeStatusResult : CampaignMoveAnimationBoundary.AfterAttackText, cue.Boundary);
                Assert.AreEqual(!late, cue.AcknowledgesCurrentReceipt); Assert.IsFalse(cue.HitsSubstitute);
                Assert.IsNull(cue.MoveTurn); Assert.IsNull(cue.MovePower); Assert.IsNull(cue.MoveDamage);
            }
            Assert.IsNull(single ? AnimationDispatchFixture.StatAtResult(Envelope.Single(events, i), true)
                : AnimationDispatchFixture.StatAtResult(Envelope.Double(events, i)));
            if (single)
            {
                Assert.IsNull(AnimationDispatchFixture.AtAttackText(Envelope.Single(events, i), false, SourceBattleScene.On).Dispatch);
                Assert.IsNull(AnimationDispatchFixture.AtResult(Envelope.Single(events, i), false, SourceBattleScene.On).Dispatch);
            }
        }
        Assert.AreEqual(owner is null ? 0 : 1, cues);
        Assert.AreEqual(before, JsonSerializer.Serialize(committed), "Projection must preserve events, committed state, RNG and trace.");
    }
    internal static void RejectCommonEnvelopeMutations(ImmutableArray<SourceBattleEvent> events, bool single)
    {
        var used = events.OfType<SourceMoveUsedEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().First(); int ui = events.IndexOf(used), ai = events.IndexOf(attack), pi = events.IndexOf(pp);
        Deny(events.RemoveAt(ai), single); Deny(events.RemoveAt(pi), single);
        foreach (var wrong in new[] { attack with { Actor = used.Actor.Opposite }, attack with { ChosenMove = Kernel.Id("METRONOME") },
            attack with { Turn = attack.Turn + 1 }, attack with { Sequence = used.Sequence } }) Deny(events.SetItem(ai, wrong), single);
        foreach (var wrong in new[] { pp with { Actor = used.Actor.Opposite }, pp with { PpOwner = used.Actor.Opposite },
            pp with { Target = used.Target.Opposite }, pp with { ChosenMove = Kernel.Id("METRONOME") },
            pp with { CurrentMove = Kernel.Id("SPLASH") }, pp with { PpSlot = 3 }, pp with { CommandCost = 0 },
            pp with { After = pp.Before }, pp with { Turn = pp.Turn + 1 }, pp with { Sequence = used.Sequence } }) Deny(events.SetItem(pi, wrong), single);
        Deny(events.SetItem(ui, used with { Actor = used.Actor.Opposite }), single);
        Deny(Number(events.Insert(pi + 1, pp)), single);
    }
}
