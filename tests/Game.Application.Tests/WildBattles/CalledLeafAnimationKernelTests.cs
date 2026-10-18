using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using F = FireRed.Tests.Application.WildBattles.CalledLeafAnimationKernelFixture;
using Body = FireRed.Tests.Application.WildBattles.ChargeCopyAnimationKernelFixture;
using Numbering = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

[TestClass, TestCategory("Game")]
public sealed class CalledLeafAnimationKernelTests
{
    [TestMethod]
    [DataRow(WildBattleTerrain.Grass, 78)]
    [DataRow(WildBattleTerrain.LongGrass, 75)]
    [DataRow(WildBattleTerrain.Sand, 89)]
    [DataRow(WildBattleTerrain.Underwater, 56)]
    [DataRow(WildBattleTerrain.Water, 57)]
    [DataRow(WildBattleTerrain.Pond, 61)]
    [DataRow(WildBattleTerrain.Mountain, 157)]
    [DataRow(WildBattleTerrain.Cave, 247)]
    [DataRow(WildBattleTerrain.Building, 129)]
    [DataRow(WildBattleTerrain.Plain, 129)]
    public void NaturalNaturePowerAuthenticatesEverySourceTerrainLeafAtEveryBattlePosition(WildBattleTerrain terrain, int leaf)
    {
        Assert.AreEqual(leaf, WildBattleSourceCallers.NaturePower(terrain));
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var frame = F.Nature(single, terrain, actor); string before = JsonSerializer.Serialize(frame);
            var result = Kernel.Ordered(frame, actor, actor ^ 1);
            var cue = F.Check(result, single, Kernel.Id("NATURE_POWER"), leaf);
            Assert.AreEqual(actor, cue.Actor.Value);
            var selection = result.Events.OfType<SourceCallerSelectionEvent>().Single();
            Assert.IsTrue(selection.CandidateSourceMoveIds.IsEmpty);
            Assert.IsFalse(result.Trace.Any(t => t.Purpose == "wild-battle.nature-power-select"),
                "Terrain chooses the source leaf without an extra selection RNG draw.");
            var paid = result.Events.OfType<SourcePpCommandEvent>().First();
            Assert.AreEqual(frame.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp, paid.Before);
            Assert.AreEqual(paid.After, result.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
            Assert.AreEqual(Kernel.Id("NATURE_POWER"), result.State.Slots[actor].Body!.Pokemon.Moves[0].Move);
            Assert.AreEqual(before, JsonSerializer.Serialize(frame));
        }
    }

    [TestMethod]
    public void MirrorMoveUsesActualPriorHitHistoryAndOwnsOnlyTheCalledLeafMoveUsedPage()
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var frame = F.Mirror(single, actor); string before = JsonSerializer.Serialize(frame);
            var result = Envelope.Resolve(frame, actor, actor ^ 1);
            F.Check(result, single, Kernel.Id("MIRROR_MOVE"), 33);
            var selection = result.Events.OfType<SourceCallerSelectionEvent>().Single();
            Assert.AreEqual(Kernel.Id("TACKLE"), selection.Result);
            Assert.IsFalse(result.Trace.Any(t => t.Purpose == "wild-battle.mirror-move-select"),
                "The actual preferred LastTakenMove wins without fallback selection RNG.");
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(frame.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp - pp.CommandCost,
                result.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
            Assert.AreEqual(Kernel.Id("MIRROR_MOVE"), result.State.Slots[actor].Body!.Pokemon.Moves[0].Move);
            Assert.AreEqual(before, JsonSerializer.Serialize(frame));
        }
    }

    [TestMethod]
    public void CalledNaturePowerPressureChargesTheLearnedCallerSlotAndKeepsTheSelectedTarget()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Nature(single, WildBattleTerrain.Cave);
            foreach (int target in single ? new[] { 1 } : new[] { 1, 3 })
                frame = Body.Body(frame, target, _ => Kernel.Mon("ARTICUNO", "MIST", 100));
            var result = Envelope.Resolve(frame); var cue = F.Check(result, single, Kernel.Id("NATURE_POWER"), 247);
            var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(2, pp.CommandCost); Assert.AreEqual(pp.Before - 2, pp.After);
            Assert.AreEqual(pp.Target, cue.Target); Assert.AreEqual(0, pp.PpSlot);
            Assert.AreEqual(pp.After, result.State.Slots[0].Body!.Pokemon.Moves[0].CurrentPp);
        }
    }

    [TestMethod]
    public void CalledLeafFailuresKeepTheRealCallerSelectionButDoNotInventAnAnimation()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = F.Mirror(single);
            foreach (int target in single ? new[] { 1 } : new[] { 1, 3 })
                frame = Body.Body(frame, target, b => b with { Protected = true });
            var result = Envelope.Resolve(frame);
            Assert.AreEqual(Kernel.Id("TACKLE"), result.Events.OfType<SourceCallerSelectionEvent>().Single().Result);
            Assert.AreEqual(1, result.Events.OfType<SourceCalledMoveTransitionEvent>().Count());
            Assert.IsTrue(result.Events.Any(e => e is SourceMoveFailedEvent f && f.Move == Kernel.Id("TACKLE") ||
                e is SourceMoveMissedEvent m && m.Move == Kernel.Id("TACKLE")));
            F.Deny(result.Events, single, 33);
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void MissingDuplicateAndReorderedCallerProofCannotAuthorizeAnOtherwiseRealLeaf(bool mirror)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(mirror ? F.Mirror(single) : F.Nature(single, WildBattleTerrain.Cave)).Events;
            int leaf = mirror ? 33 : 247;
            var selection = events.OfType<SourceCallerSelectionEvent>().Single();
            var transition = events.OfType<SourceCalledMoveTransitionEvent>().Single();
            var header = events.OfType<SourceAttackStringEvent>().First();
            F.Check(events, events, single, selection.Caller, leaf);
            foreach (var receipt in new SourceBattleEvent[] { header, selection, transition })
            {
                F.Deny(events.Remove(receipt), single, leaf);
                F.Deny(Numbering.Number(events.Insert(events.IndexOf(receipt) + 1, receipt)), single, leaf);
            }
            F.Deny(events.Remove(header).Remove(selection).Remove(transition), single, leaf);
            var swapped = events.SetItem(events.IndexOf(selection), transition).SetItem(events.IndexOf(transition), selection);
            F.Deny(Numbering.Number(swapped), single, leaf);
            F.Deny(events.SetItem(events.IndexOf(selection), selection with { Sequence = transition.Sequence }), single, leaf);
            F.Deny(events.SetItem(events.IndexOf(transition), transition with { Turn = transition.Turn + 1 }), single, leaf);
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void ForeignOrMalformedSelectionAndTransitionCannotBlessTheRealLeafResult(bool mirror)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(mirror ? F.Mirror(single) : F.Nature(single, WildBattleTerrain.Cave)).Events;
            int leaf = mirror ? 33 : 247;
            var selection = events.OfType<SourceCallerSelectionEvent>().Single();
            var transition = events.OfType<SourceCalledMoveTransitionEvent>().Single();
            int si = events.IndexOf(selection), ti = events.IndexOf(transition); var call = transition.Invocation;
            foreach (var wrong in new[] { selection with { Actor = new(1) }, selection with { Caller = Kernel.Id("GROWL") },
                selection with { Result = Kernel.Id("SPLASH") }, selection with { Result = null },
                selection with { Turn = selection.Turn + 1 }, selection with { CandidateSourceMoveIds = [-1] } })
                F.Deny(events.SetItem(si, wrong), single, leaf);
            foreach (var wrong in new[] { call with { Actor = new(1) }, call with { PpOwner = new(1) },
                call with { OriginalChosenMove = Kernel.Id("METRONOME") }, call with { ChosenMove = call.CurrentMove },
                call with { CurrentMove = Kernel.Id("SPLASH") }, call with { CalledMove = call.CurrentMove },
                call with { ChosenSlot = 1 }, call with { PpSlot = 1 }, call with { Target = call.Actor },
                call with { CancellerPhase = WildBattleAttackCancellerPhase.Start },
                call with { Flags = call.Flags | WildBattleInvocationFlags.NoPpDeduct },
                call with { Flags = call.Flags | WildBattleInvocationFlags.AttackStringPrinted } })
                F.Deny(events.SetItem(ti, transition with { Invocation = wrong }), single, leaf);
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void CallerAuthenticationDoesNotRelaxLeafAttackAndPpOwnership(bool mirror)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(mirror ? F.Mirror(single) : F.Nature(single, WildBattleTerrain.Cave)).Events;
            int leaf = mirror ? 33 : 247; var pp = events.OfType<SourcePpCommandEvent>().Single();
            var attack = events.OfType<SourceAttackStringEvent>().Last(); int pi = events.IndexOf(pp), ai = events.IndexOf(attack);
            foreach (var wrong in new[] { pp with { Actor = new(1) }, pp with { PpOwner = new(1) },
                pp with { Target = pp.Actor }, pp with { ChosenMove = pp.CurrentMove }, pp with { CurrentMove = pp.ChosenMove },
                pp with { PpSlot = 1 }, pp with { CommandCost = 0 }, pp with { After = pp.Before }, pp with { Turn = pp.Turn + 1 } })
                F.Deny(events.SetItem(pi, wrong), single, leaf);
            foreach (var wrong in new[] { attack with { Actor = new(1) }, attack with { ChosenMove = attack.CurrentMove },
                attack with { CurrentMove = attack.ChosenMove }, attack with { Turn = attack.Turn + 1 } })
                F.Deny(events.SetItem(ai, wrong), single, leaf);
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void CalledLeafStillRequiresValidCallerLeafDefinitionsAndTheActualGeneratedRoot(bool mirror)
    {
        foreach (bool single in new[] { false, true })
        {
            var events = Envelope.Resolve(mirror ? F.Mirror(single) : F.Nature(single, WildBattleTerrain.Cave)).Events;
            var selection = events.OfType<SourceCallerSelectionEvent>().Single();
            var used = events.OfType<SourceMoveUsedEvent>().Single(); int index = events.IndexOf(used);
            var definitions = Kernel.Mechanics.Catalog.Moves; var caller = definitions[selection.Caller];
            foreach (var metadata in new[] { null, definitions.Remove(caller.Id), definitions.Remove(used.Move),
                definitions.SetItem(caller.Id, caller with { Power = 1 }),
                definitions.SetItem(caller.Id, caller with { Target = "MOVE_TARGET_USER" }),
                definitions.SetItem(caller.Id, caller with { Effect = "EFFECT_HIT" }) })
                Assert.IsNull(single ? AnimationDispatchFixture.AtAttackText(Envelope.Single(events, index), true, SourceBattleScene.On, metadata).Dispatch
                    : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, index), SourceBattleScene.On, metadata).Dispatch);
            Assert.IsNull(single ? CampaignMoveAnimationPresentation.AtAttackText(Envelope.Single(events, index), true, SourceBattleScene.On, definitions, (_, _) => false).Dispatch
                : CampaignMoveAnimationPresentation.AtAttackText(Envelope.Double(events, index), SourceBattleScene.On, definitions, (_, _) => false).Dispatch);
        }
    }
}
