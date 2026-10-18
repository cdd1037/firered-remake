using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Numbering = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// Every positive mirror history and damage register below is produced by the public
// kernel: Scratch/Ember -> Counter/Mirror Coat -> Mirror Move. Only natural learned
// slots and controlled stages/levels are fixture inputs; no receipt is invented.
[TestClass, TestCategory("Game")]
public sealed class CalledRetaliationTargetResolutionTests
{
    private static string Leaf(int id) => id == 68 ? "COUNTER" : "MIRROR_COAT";
    private static SourceKernelResult Resolve(SourceBattleFrame frame, int actor, int target, int slot = 0)
    {
        string before = JsonSerializer.Serialize(frame);
        var result = SourceBattleKernel.ResolveAction(Kernel.Mechanics.Catalog, frame,
            new(frame.Slots[actor].Occupant!.Value, frame.Revision, slot, new(target)));
        Assert.IsTrue(result.Succeeded, result.Fault);
        Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty);
        Assert.AreEqual(before, JsonSerializer.Serialize(frame));
        return result;
    }

    private static SourceBattleFrame Prepare(int id, bool single, int actor, int source, bool followMe = false)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100)).ToArray();
        var mirror = Kernel.Mon("TORCHIC", "MIRROR_MOVE", 100);
        var initiating = Kernel.Mon("TORCHIC", id == 68 ? "SCRATCH" : "EMBER", 100).Pokemon.Moves[0];
        bodies[actor] = mirror with { Pokemon = mirror.Pokemon with { Moves = mirror.Pokemon.Moves.Add(initiating) },
            AccuracyStage = 12, AttackStage = 0, SpecialAttackStageDelta = -6 };
        bodies[source] = Kernel.Mon("WOBBUFFET", Leaf(id), 100) with { AccuracyStage = 12 };
        if (followMe) bodies[source ^ 2] = Kernel.Mon("TOGETIC", "FOLLOW_ME", 100);
        var initial = Kernel.Frame(bodies, single);
        var first = Resolve(initial, actor, source, 1);
        Assert.IsTrue(first.Events.OfType<SourceHpChangedEvent>().Any(e => e.Actor.Value == actor && e.Target.Value == source && e.Damage > 0));
        var returned = Resolve(first.State, source, actor);
        Assert.IsTrue(returned.Events.OfType<SourceHpChangedEvent>().Any(e => e.Actor.Value == source && e.Target.Value == actor && e.Move == Kernel.Id(Leaf(id)) && e.Damage > 0));
        var history = returned.State.Slots[actor].Registers.History;
        Assert.IsNotNull(history); Assert.AreEqual(Kernel.Id(Leaf(id)), history.LastTakenMove);
        var damage = returned.State.Slots[actor].Registers.Damage;
        Assert.IsNotNull(damage); Assert.AreEqual(new BattlerSlot(source), id == 68 ? damage.PhysicalSource : damage.SpecialSource);
        Assert.IsTrue(returned.State.Slots[actor].Body!.Pokemon.CurrentHp > 0);
        if (!followMe) return returned.State;
        var redirect = Resolve(returned.State, source ^ 2, source ^ 2);
        Assert.IsTrue(redirect.Events.OfType<SourceMtMoonVolatileEvent>().Any(e => e.Volatile == "follow-me" && e.After == 1));
        Assert.AreEqual(new BattlerSlot(source ^ 2), redirect.State.Sides[source & 1].FollowMeTarget);
        Assert.AreEqual(history.LastTakenMove, redirect.State.Slots[actor].Registers.History!.LastTakenMove);
        return redirect.State;
    }

    private static object? Read(ImmutableArray<SourceBattleEvent> events, bool single)
    {
        var type = typeof(CampaignMoveAnimationPresentation).Assembly.GetType("Game.Application.CampaignAnimationInvocation", true)!;
        var method = type.GetMethod("TryRead", BindingFlags.NonPublic | BindingFlags.Static)!;
        int at = events.IndexOf(events.OfType<SourceMoveUsedEvent>().Single(e => e.Move != Kernel.Id("MIRROR_MOVE")));
        return method.Invoke(null, [events.ToArray(), at, single ? 2 : 4, Kernel.Mechanics.Catalog.Moves]);
    }

    private static CampaignMoveAnimationDispatch Check(SourceKernelResult result, int id, bool single)
    {
        string before = JsonSerializer.Serialize(result);
        var events = result.Events; var move = Kernel.Id(Leaf(id));
        var transition = events.OfType<SourceCalledMoveTransitionEvent>().Single();
        var selected = events.OfType<SourceCallerSelectionEvent>().Single();
        var used = events.OfType<SourceMoveUsedEvent>().Single(e => e.Move == move);
        var attack = events.OfType<SourceAttackStringEvent>().Single(e => e.CurrentMove == move);
        var pp = events.OfType<SourcePpCommandEvent>().Single(e => e.CurrentMove == move);
        var hit = events.OfType<SourceHpChangedEvent>().Single(e => e.Actor == used.Actor && e.Move == move);
        Assert.AreEqual(Kernel.Id("MIRROR_MOVE"), selected.Caller);
        Assert.AreEqual(move, selected.Result); Assert.AreEqual(move, transition.Invocation.CurrentMove);
        Assert.AreEqual(Kernel.Id("MIRROR_MOVE"), transition.Invocation.ChosenMove);
        Assert.AreEqual(used.Actor, transition.Invocation.PpOwner);
        Assert.AreEqual(Kernel.Id("MIRROR_MOVE"), pp.ChosenMove);
        Assert.AreEqual(0, pp.PpSlot); Assert.IsTrue(pp.After < pp.Before);
        Assert.AreEqual(used.Target, pp.Target); Assert.AreEqual(used.Target, hit.Target);
        Assert.AreEqual(used.Target, result.Invocation!.Target);
        Assert.IsNotNull(Read(events, single));
        var resolved = events.OfType<SourceMoveTargetResolvedEvent>().SingleOrDefault();
        if (resolved is null) Assert.AreEqual(transition.Invocation.Target, used.Target);
        else
        {
            Assert.AreEqual(transition.Invocation.Target, resolved.PriorTarget);
            Assert.AreEqual(used.Target, resolved.Target); Assert.AreNotEqual(resolved.PriorTarget, resolved.Target);
            Assert.AreEqual(used.Actor, resolved.Actor); Assert.AreEqual(used.Move, resolved.CurrentMove);
            Assert.AreEqual(SourceMoveTargetResolutionReason.Retaliation, resolved.Reason);
            Assert.AreEqual(transition.Sequence + 1, resolved.Sequence);
            Assert.AreEqual(resolved.Sequence + 1, attack.Sequence);
        }
        int count = 0; CampaignMoveAnimationDispatch? found = null;
        for (int i = 0; i < events.Length; i++)
        {
            var decision = Kernel.Project(events, i, single);
            Assert.AreEqual(decision, Kernel.Project(events, i, single));
            Assert.IsNull(Kernel.Project(events, i, single, SourceBattleScene.Off).Dispatch);
            Assert.IsFalse(CallerPreludeAnimationKernelFixture.Result(events, i, single).Dispatch?.SourceMoveId == id);
            if (events[i] != used) { Assert.IsFalse(decision.Dispatch?.SourceMoveId == id); continue; }
            var cue = decision.Dispatch; Assert.IsNotNull(cue, decision.Reason); found = cue; count++;
            Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(used.Actor, cue.Actor); Assert.AreEqual(used.Target, cue.Target);
            Assert.AreEqual(used.Sequence, cue.Sequence); Assert.AreEqual(attack.Sequence, cue.AttackSequence);
            Assert.AreEqual(pp.Sequence, cue.PpSequence); Assert.AreEqual(hit.Sequence, cue.ResultSequence);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText, cue.Boundary);
        }
        Assert.AreEqual(1, count); Assert.IsNotNull(found);
        Assert.AreEqual(before, JsonSerializer.Serialize(result), "Projection preserves gameplay state, PP, damage, RNG and trace.");
        return found;
    }

    private static void Deny(ImmutableArray<SourceBattleEvent> events, int id, bool single = false)
    {
        Assert.IsNull(Read(events, single), "Malformed target proof must fail the shared invocation descriptor.");
        for (int i = 0; i < events.Length; i++)
        {
            Assert.IsFalse(Kernel.Project(events, i, single).Dispatch?.SourceMoveId == id);
            Assert.IsFalse(CallerPreludeAnimationKernelFixture.Result(events, i, single).Dispatch?.SourceMoveId == id);
        }
    }

    [TestMethod, DataRow(68), DataRow(243)]
    public void ActualMirrorRetaliationUsesMatchingSinglesAndBothDoublesDamageSources(int id)
    {
        foreach (bool single in new[] { true, false })
            for (int actor = 0; actor < (single ? 2 : 4); actor++)
                foreach (int source in single ? new[] { actor ^ 1 } : new[] { (actor & 1) ^ 1, ((actor & 1) ^ 1) + 2 })
                {
                    var result = Resolve(Prepare(id, single, actor, source), actor, (actor & 1) ^ 1);
                    var cue = Check(result, id, single);
                    Assert.AreEqual(source, cue.Target.Value);
                    Assert.AreEqual(!single && source >= 2, result.Events.OfType<SourceMoveTargetResolvedEvent>().Any());
                }
    }

    [TestMethod, DataRow(68), DataRow(243)]
    public void ActualFollowMeOverridesRecordedDamageSourceAfterTheUnchangedCallerTransition(int id)
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int source = (actor & 1) ^ 1;
            var frame = Prepare(id, false, actor, source, followMe: true);
            var result = Resolve(frame, actor, source); var cue = Check(result, id, false);
            var transition = result.Events.OfType<SourceCalledMoveTransitionEvent>().Single();
            var resolved = result.Events.OfType<SourceMoveTargetResolvedEvent>().Single();
            Assert.AreEqual(source, transition.Invocation.Target.Value);
            Assert.AreEqual(source ^ 2, resolved.Target.Value); Assert.AreEqual(source ^ 2, cue.Target.Value);
            var damage = frame.Slots[actor].Registers.Damage!;
            Assert.AreEqual(source, (id == 68 ? damage.PhysicalSource : damage.SpecialSource)!.Value.Value);
        }
    }

    [TestMethod, DataRow(68), DataRow(243)]
    public void MissingForeignWrongDuplicateReorderedAndUnchangedTargetProofsFailClosed(int id)
    {
        var result = Resolve(Prepare(id, false, 0, 3), 0, 1); Check(result, id, false);
        var events = result.Events; var receipt = events.OfType<SourceMoveTargetResolvedEvent>().Single();
        var transition = events.OfType<SourceCalledMoveTransitionEvent>().Single();
        int at = events.IndexOf(receipt), transitionAt = events.IndexOf(transition);
        Deny(events.RemoveAt(at), id);
        Deny(Numbering.Number(events.Insert(at + 1, receipt)), id);
        foreach (var wrong in new[] { receipt with { Actor = new(2) }, receipt with { CurrentMove = Kernel.Id("TACKLE") },
            receipt with { PriorTarget = new(0) }, receipt with { Target = new(1) }, receipt with { Target = new(0) },
            receipt with { PriorTarget = receipt.Target }, receipt with { Turn = receipt.Turn + 1 },
            receipt with { Sequence = receipt.Sequence - 1 }, receipt with { Reason = (SourceMoveTargetResolutionReason)99 } })
            Deny(events.SetItem(at, wrong), id);
        Deny(events.SetItem(transitionAt, transition with { Invocation = transition.Invocation with { Target = new(0) } }), id);
        // Moving the receipt before selection/transition cannot prove the later target change.
        Deny(Numbering.Number(events.RemoveAt(at).Insert(transitionAt, receipt)), id);
        // Relabeling both the proof and final page cannot make a same-side resolution valid.
        var used = events.OfType<SourceMoveUsedEvent>().Single();
        Deny(events.SetItem(at, receipt with { Target = new(2) }).SetItem(events.IndexOf(used), used with { Target = new(2) }), id);
        var unrelated = Kernel.Id("TACKLE");
        var forgedHit = events.Select(e => e switch {
            SourceMoveTargetResolvedEvent x => x with { CurrentMove = unrelated },
            SourceCallerSelectionEvent x => x with { Result = unrelated },
            SourceCalledMoveTransitionEvent x => x with { Invocation = x.Invocation with { CurrentMove = unrelated } },
            SourceAttackStringEvent x when x.CurrentMove == used.Move => x with { CurrentMove = unrelated },
            SourceMoveUsedEvent x => x with { Move = unrelated },
            SourcePpCommandEvent x => x with { CurrentMove = unrelated },
            SourceHpChangedEvent x => x with { Move = unrelated },
            _ => e }).ToImmutableArray();
        Deny(forgedHit, 33);
        Assert.AreEqual(receipt, result.Events[at], "Negative projection edits stay in detached event copies.");
    }

    [TestMethod, DataRow(68), DataRow(243)]
    public void RealDirectSinglesRetargetExportsNormalizesAndKeepsDamageStateAndRngIdentical(int id)
    {
        // Wobbuffet owns a real receipt from Torchic's initiating move. Comparing
        // preselected targets proves the new event adds no damage or RNG behavior.
        var mirrorFrame = Prepare(id, true, 0, 1);
        var selected = Resolve(mirrorFrame, 1, 0);
        var retargeted = Resolve(mirrorFrame, 1, 1);
        Assert.IsFalse(selected.Events.OfType<SourceMoveTargetResolvedEvent>().Any());
        var receipt = retargeted.Events.OfType<SourceMoveTargetResolvedEvent>().Single();
        Deny(Numbering.Number(retargeted.Events.Insert(retargeted.Events.IndexOf(receipt) + 1, receipt)), id, single: true);
        Assert.AreEqual(new BattlerSlot(1), receipt.PriorTarget); Assert.AreEqual(new BattlerSlot(0), receipt.Target);
        Assert.AreEqual(JsonSerializer.Serialize(selected.State with { EventSequence = 0 }),
            JsonSerializer.Serialize(retargeted.State with { EventSequence = 0 }));
        Assert.AreEqual(JsonSerializer.Serialize(selected.Trace), JsonSerializer.Serialize(retargeted.Trace));
        Assert.AreEqual(selected.State.Rng, retargeted.State.Rng);
        Assert.AreEqual(selected.Invocation, retargeted.Invocation);
        var withoutProof = retargeted.Events.Where(e => e is not SourceMoveTargetResolvedEvent).ToImmutableArray();
        Assert.AreEqual(JsonSerializer.Serialize(Numbering.Number(selected.Events)), JsonSerializer.Serialize(Numbering.Number(withoutProof)));
        SourceBattleEvent source = receipt;
        Assert.AreEqual(receipt, JsonSerializer.Deserialize<SourceBattleEvent>(JsonSerializer.Serialize(source)));
        WildBattleEvent exported = SourceBattleEventAdapter.Export(receipt);
        Assert.IsInstanceOfType<WildBattleMoveTargetResolvedEvent>(exported);
        Assert.AreEqual(exported, JsonSerializer.Deserialize<WildBattleEvent>(JsonSerializer.Serialize(exported)));
        var normalize = typeof(CampaignMoveAnimationPresentation).GetMethod("AnimationReceipt", BindingFlags.NonPublic | BindingFlags.Static)!;
        Assert.AreEqual(receipt, normalize.Invoke(null, [exported]));
        int index = retargeted.Events.IndexOf(retargeted.Events.OfType<SourceMoveUsedEvent>().Single());
        var cue = Kernel.Project(retargeted.Events, index, true).Dispatch; Assert.IsNotNull(cue);
        Assert.AreEqual(id, cue.SourceMoveId); Assert.AreEqual(receipt.Target, cue.Target);
        Assert.IsNull(SourceBattleEventHooks.ValidateRouting(retargeted.State, receipt));
        Assert.IsNotNull(SourceBattleEventHooks.ValidateRouting(retargeted.State, receipt with { Reason = (SourceMoveTargetResolutionReason)99 }));
        Assert.IsNotNull(SourceBattleEventHooks.ValidateRouting(retargeted.State, receipt with { Target = new(3) }));
    }
}
