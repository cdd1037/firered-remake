using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;
using Shared = FireRed.Tests.Application.WildBattles.RockStatusAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// Natural learners, actual public-kernel receipt sequences. Controlled HP,
// status and Substitute are integration fixtures, not natural-route evidence.
[TestClass, TestCategory("Game")]
public sealed class LockedPairAnimationKernelTests
{
    private static MoveId Move(int id) => Kernel.Id(id == 37 ? "THRASH" : id == 80 ? "PETAL_DANCE" : "BIDE");
    private static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        bodies[actor] = Kernel.Mon(id == 37 ? "NIDOKING" : id == 80 ? "ODDISH" : "PINECO", id == 37 ? "THRASH" : id == 80 ? "PETAL_DANCE" : "BIDE", id == 80 ? 39 : 50) with { AccuracyStage = 12, SpeedStage = 12 };
        if (id == 117) bodies[actor ^ 1] = Kernel.Mon("RATTATA", "TACKLE", 20) with { AccuracyStage = 12, SpeedStage = 0 };
        return bodies;
    }
    private static SourceBattleFrame Body(SourceBattleFrame frame, int slot, Func<WildBattleCombatant, WildBattleCombatant> change)
    {
        var cell = frame.Slots[slot]; var body = change(cell.Body!); var key = cell.Member!.Value; var party = frame.Parties[(int)key.Side];
        var member = party.Members[key.Index] with { Pokemon = WildBattleTransformationRules.PersistentPokemon(body), Status = WildBattleTransformationRules.PersistentStatus(body), HeldItem = body.HeldItem, KnockedOffItem = body.KnockedOffItem };
        return frame with { Slots = frame.Slots.SetItem(slot, cell with { Body = body }), Parties = frame.Parties.SetItem((int)key.Side, party with { Members = party.Members.SetItem(key.Index, member) }) };
    }
    private static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events, int index, bool single, bool hit, SourceBattleScene scene = SourceBattleScene.On) =>
        single ? hit ? AnimationDispatchFixture.AtResult(Envelope.Single(events, index), true, scene) : AnimationDispatchFixture.AtAttackText(Envelope.Single(events, index), true, scene)
            : hit ? AnimationDispatchFixture.AtResult(Envelope.Double(events, index), scene) : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, index), scene);
    private static CampaignMoveAnimationDispatch Check(ImmutableArray<SourceBattleEvent> events, bool single, int id, SourceBattleEvent owner, SourceBattleEvent result,
        CampaignMoveAnimationBoundary boundary, byte? moveTurn)
    {
        string before = JsonSerializer.Serialize(events); CampaignMoveAnimationDispatch? found = null; int count = 0;
        for (int i = 0; i < events.Length; i++) foreach (bool hit in new[] { false, true })
        {
            var decision = Project(events, i, single, hit); Assert.AreEqual(decision, Project(events, i, single, hit));
            Assert.IsNull(Project(events, i, single, hit, SourceBattleScene.Off).Dispatch);
            if (events[i] != owner || hit != (boundary == CampaignMoveAnimationBoundary.BeforeHitResult)) { Assert.IsNull(decision.Dispatch, decision.Reason); continue; }
            found = decision.Dispatch; Assert.IsNotNull(found, decision.Reason); count++;
            Assert.AreEqual(id, found.SourceMoveId); Assert.AreEqual(owner.Sequence, found.Sequence); Assert.AreEqual(result.Sequence, found.ResultSequence);
            Assert.AreEqual(boundary, found.Boundary); Assert.AreEqual(moveTurn, found.MoveTurn);
            Assert.AreEqual(boundary != CampaignMoveAnimationBoundary.BeforeHitResult, found.AcknowledgesCurrentReceipt);
            Assert.AreEqual(result is SourceSubstituteDamagedEvent, found.HitsSubstitute);
            var attack = events.OfType<SourceAttackStringEvent>().SingleOrDefault(); var pp = events.OfType<SourcePpCommandEvent>().SingleOrDefault();
            Assert.AreEqual(attack?.Sequence, found.AttackSequence); Assert.AreEqual(pp?.Sequence, found.PpSequence);
            if (result is SourceHpChangedEvent h) { Assert.AreEqual(h.Actor, found.Actor); Assert.AreEqual(h.Target, found.Target); }
            if (result is SourceSubstituteDamagedEvent s) { Assert.AreEqual(s.Actor, found.Actor); Assert.AreEqual(s.Target, found.Target); }
        }
        Assert.AreEqual(1, count); Assert.AreEqual(before, JsonSerializer.Serialize(events)); return found!;
    }
    private static void Deny(ImmutableArray<SourceBattleEvent> events, bool single)
    { for (int i = 0; i < events.Length; i++) foreach (bool hit in new[] { false, true }) Assert.IsNull(Project(events, i, single, hit).Dispatch); }
    private static SourceBattleFrame StoredBide(bool single, int actor = 0, bool ghost = false, bool empty = false)
    {
        var bodies = Bodies(117, actor); if (ghost) bodies[actor ^ 1] = Kernel.Mon("GASTLY", "NIGHT_SHADE", 50) with { AccuracyStage = 12 };
        var setup = Envelope.Resolve(Kernel.Frame(bodies, single), actor, actor);
        Assert.AreEqual(WildBattleCalledLockKind.Bide, setup.State.Slots[actor].Registers.Turn!.Lock!.Kind);
        var damaged = empty ? setup : Envelope.Resolve(setup.State, actor ^ 1, actor);
        if (!empty) Assert.IsTrue(damaged.State.Slots[actor].Registers.Turn!.Lock!.StoredDamage > 0);
        var storing = Envelope.Resolve(damaged.State, actor, actor);
        var marker = storing.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "called-bide" && e.Reason == "storing-energy");
        Assert.AreEqual(2, marker.Before); Assert.AreEqual(1, marker.After); Assert.AreEqual(0, Envelope.Hits(storing.Events).Length);
        Assert.IsFalse(storing.Events.Any(e => e is SourceMoveUsedEvent or SourcePpCommandEvent or SourceAttackStringEvent)); Deny(storing.Events, single);
        return storing.State;
    }
    private static (SourceKernelResult First, SourceKernelResult Repeat) Thrash(bool single, bool substitute = false, int actor = 0)
    {
        var bodies = Bodies(37, actor); var first = Envelope.Resolve(Kernel.Frame(bodies, single), actor, actor ^ 1);
        Assert.AreEqual(WildBattleCalledLockKind.Rampage, first.State.Slots[actor].Registers.Turn!.Lock!.Kind);
        var frame = substitute ? Body(first.State, actor ^ 1, b => b with { CalledVolatile = new() { SubstituteHp = 100 } }) : first.State;
        return (first, Envelope.Resolve(frame, actor, actor ^ 1));
    }
    [TestMethod, DataRow(37), DataRow(80), DataRow(117)]
    public void InitialPpOwnedBranchesUseTheirRealResultAcrossAllPresentPositions(int id)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var result = Envelope.Resolve(Kernel.Frame(Bodies(id, actor), single), actor, id == 117 ? actor : actor ^ 1);
            var used = result.Events.OfType<SourceMoveUsedEvent>().Single(); var pp = result.Events.OfType<SourcePpCommandEvent>().Single();
            Assert.AreEqual(Move(id), used.Move); Assert.AreEqual(pp.Before - pp.CommandCost, pp.After);
            SourceBattleEvent outcome = id != 117 ? Envelope.Hits(result.Events).Single() : result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "called-bide" && e.Reason == "move");
            Check(result.Events, single, id, used, outcome, CampaignMoveAnimationBoundary.AfterAttackText, id == 117 ? (byte)0 : null);
            if (id == 117) { Assert.AreEqual(used.Actor, used.Target); Assert.AreEqual(pp.Sequence + 1, outcome.Sequence); }
        }
    }
    [TestMethod, DataRow(false), DataRow(true)]
    public void PetalDanceRetainedRandomTargetUsesItsActualNoPpHitAndRejectsBorrowedReceipts(bool substitute)
    {
        foreach(bool single in new[]{false,true})for(int actor=0;actor<(single?2:4);actor++)
        {
            var first=Envelope.Resolve(Kernel.Frame(Bodies(80,actor),single),actor,actor^1);
            Assert.AreEqual(WildBattleCalledLockKind.Rampage,first.State.Slots[actor].Registers.Turn!.Lock!.Kind);
            var frame=first.State;
            if(substitute)foreach(int target in single?new[]{actor^1}:new[]{actor^1,actor^3})frame=Body(frame,target,b=>b with{CalledVolatile=new(){SubstituteHp=100}});
            var repeat=Envelope.Resolve(frame,actor,actor^1);var hit=Envelope.Hits(repeat.Events).Single();var attack=repeat.Events.OfType<SourceAttackStringEvent>().Single();
            Assert.IsFalse(repeat.Events.Any(e=>e is SourceMoveUsedEvent or SourcePpCommandEvent));Assert.AreEqual(first.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp,repeat.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
            var cue=Check(repeat.Events,single,80,attack,hit,CampaignMoveAnimationBoundary.AfterRetainedAttackText,null);Assert.AreEqual(substitute,cue.HitsSubstitute);Assert.AreNotEqual(cue.Actor.Side,cue.Target.Side);
            int at=repeat.Events.IndexOf(hit);Deny(repeat.Events.RemoveAt(at),single);Deny(Shared.Number(repeat.Events.Insert(at,first.Events.OfType<SourcePpCommandEvent>().Single())),single);
        }
    }
    [TestMethod, DataRow(false), DataRow(true)]
    public void RetainedThrashOwnsTheActualUnpairedAttackStringWithoutPp(bool substitute)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var (first, repeat) = Thrash(single, substitute, actor); var attack = repeat.Events.OfType<SourceAttackStringEvent>().Single();
            Assert.IsFalse(repeat.Events.Any(e => e is SourceMoveUsedEvent or SourcePpCommandEvent));
            Assert.AreEqual(first.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp, repeat.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
            Check(repeat.Events, single, 37, attack, Envelope.Hits(repeat.Events).Single(), CampaignMoveAnimationBoundary.AfterRetainedAttackText, null);
        }
    }
    [TestMethod, DataRow(false), DataRow(true)]
    public void StoredBideReleaseOwnsItsCurrentHitWithNoAttackOrPp(bool substitute)
    {
        foreach (bool single in new[] { false, true }) for (int actor = 0; actor < (single ? 2 : 4); actor++)
        {
            var frame = StoredBide(single, actor); int energy = frame.Slots[actor].Registers.Turn!.Lock!.StoredDamage;
            if (substitute) frame = Body(frame, actor ^ 1, b => b with { CalledVolatile = new() { SubstituteHp = 100 } });
            var result = Envelope.Resolve(frame, actor, actor); var hit = Envelope.Hits(result.Events).Single();
            var released = result.Events.OfType<SourceMtMoonVolatileEvent>().Single(e => e.Volatile == "called-bide" && e.Reason == "released");
            Assert.AreEqual(1, released.Before); Assert.AreEqual(0, released.After); Assert.IsTrue(released.Sequence < hit.Sequence);
            Assert.IsFalse(result.Events.Any(e => e is SourceAttackStringEvent or SourceMoveUsedEvent or SourcePpCommandEvent));
            Assert.AreEqual(frame.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp, result.State.Slots[actor].Body!.Pokemon.Moves[0].CurrentPp);
            Assert.IsTrue(energy > 0); Check(result.Events, single, 117, hit, hit, CampaignMoveAnimationBoundary.BeforeHitResult, 1);
        }
    }
    [TestMethod]
    public void BideRetaliatesAgainstItsActualFriendlyDamageSourceInDoubles()
    {
        for (int actor = 0; actor < 4; actor++)
        {
            int partner = actor ^ 2; var bodies = Bodies(117, actor); bodies[partner] = Kernel.Mon("RATTATA", "TACKLE", 20) with { AccuracyStage = 12 };
            var setup = Envelope.Resolve(Kernel.Frame(bodies), actor, actor);
            var damaged = Envelope.Resolve(setup.State, partner, actor);
            var stored = damaged.State.Slots[actor].Registers.Turn!.Lock!; Assert.IsTrue(stored.StoredDamage > 0); Assert.AreEqual(new BattlerSlot(partner), stored.StoredDamageSource);
            var storing = Envelope.Resolve(damaged.State, actor, actor); Deny(storing.Events, false);
            var result = Envelope.Resolve(storing.State, actor, actor); var hit = result.Events.OfType<SourceHpChangedEvent>().Single();
            Assert.AreEqual(new BattlerSlot(partner), hit.Target); Assert.AreEqual(hit.Actor.Side, hit.Target.Side);
            Assert.IsFalse(result.Events.Any(e => e is SourceAttackStringEvent or SourceMoveUsedEvent or SourcePpCommandEvent));
            Check(result.Events, false, 117, hit, hit, CampaignMoveAnimationBoundary.BeforeHitResult, 1);
        }
    }
    [TestMethod, DataRow("empty"), DataRow("protected"), DataRow("immune"), DataRow("sleep")]
    public void BideNoEnergyMissImmunityAndCancellerNeverBorrowTheReleaseRoot(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = StoredBide(single, ghost: branch == "immune", empty: branch == "empty");
            if (branch == "protected") frame = Body(frame, 1, b => b with { Protected = true });
            if (branch == "sleep") frame = Body(frame, 0, b => b with { Status = 3 });
            var result = Envelope.Resolve(frame, 0, 0); Deny(result.Events, single);
            Assert.IsFalse(result.Events.Any(e => e is SourcePpCommandEvent or SourceMoveUsedEvent));
            if (branch == "empty") Assert.IsTrue(result.Events.OfType<SourceMoveFailedEvent>().Any(e => e.Reason == "bide-no-energy"));
            if (branch == "protected") { var failure=result.Events.OfType<SourceMoveFailedEvent>().Single(); Assert.AreEqual("protected",failure.Reason); Assert.AreEqual(Move(117),failure.Move); Assert.AreEqual(new BattlerSlot(0),failure.Actor); Assert.AreEqual(new BattlerSlot(1),failure.Target); Assert.AreEqual(0,Envelope.Hits(result.Events).Length); }
            if (branch == "immune") Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
        }
    }
    [TestMethod, DataRow("protected"), DataRow("immune"), DataRow("sleep")]
    public void RetainedThrashFailureCannotClaimAnUnpairedSuccessfulInvocation(string branch)
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = Thrash(single).First.State;
            if (branch == "protected") frame = Body(frame, 1, b => b with { Protected = true });
            if (branch == "immune") frame = Body(frame, 1, _ => Kernel.Mon("GASTLY", "LICK", 100));
            if (branch == "sleep") frame = Body(frame, 0, b => b with { Status = 3 });
            var result = Envelope.Resolve(frame); Deny(result.Events, single);
            if (branch == "protected") { var failure=result.Events.OfType<SourceMoveFailedEvent>().Single(); Assert.AreEqual("protected",failure.Reason); Assert.AreEqual(Move(37),failure.Move); Assert.AreEqual(new BattlerSlot(0),failure.Actor); Assert.AreEqual(new BattlerSlot(1),failure.Target); Assert.AreEqual(0,Envelope.Hits(result.Events).Length); Assert.AreEqual(Move(37),result.Events.OfType<SourcePpCommandEvent>().Single().CurrentMove); }
            if (branch == "immune") Assert.IsTrue(result.Events.OfType<SourceEffectivenessEvent>().Any(e => e.Effectiveness == WildBattleEffectiveness.NoEffect));
        }
    }
    [TestMethod, DataRow("fake-pp"), DataRow("duplicate-hit"), DataRow("wrong-move"), DataRow("wrong-actor"), DataRow("allied-target"), DataRow("nonmonotonic")]
    public void RetainedThrashRequiresItsOwnUniqueNoPpHit(string mutation)
    {
        foreach (bool single in new[] { false, true })
        {
            var (first, repeat) = Thrash(single); var events = repeat.Events; int at = events.IndexOf(Envelope.Hits(events).Single());
            if (mutation == "fake-pp") events = Shared.Number(events.Insert(at, first.Events.OfType<SourcePpCommandEvent>().Single()));
            if (mutation == "duplicate-hit") events = Shared.Number(events.Insert(at, events[at]));
            if (mutation == "wrong-move") events = events.SetItem(at, ((SourceHpChangedEvent)events[at]) with { Move = Kernel.Id("TACKLE") });
            if (mutation == "allied-target") events = events.SetItem(at, ((SourceHpChangedEvent)events[at]) with { Target = new(single ? 0 : 2) });
            if (mutation == "wrong-actor") events = events.SetItem(at, ((SourceHpChangedEvent)events[at]) with { Actor = new(1) });
            if (mutation == "nonmonotonic") events = events.SetItem(at, events[at] with { Sequence = events[0].Sequence });
            Deny(events, single);
        }
    }
    [TestMethod, DataRow("fake-pp"), DataRow("missing-release"), DataRow("duplicate-hit"), DataRow("wrong-actor"), DataRow("wrong-count"), DataRow("wrong-turn"), DataRow("nonmonotonic")]
    public void BideReleaseRequiresTheMatchingOneToZeroPrefixAndUniqueHit(string mutation)
    {
        foreach (bool single in new[] { false, true })
        {
            var result = Envelope.Resolve(StoredBide(single), 0, 0); var events = result.Events;
            int at = Array.FindIndex(events.ToArray(), e => e is SourceMtMoonVolatileEvent { Volatile: "called-bide", Reason: "released" });
            var released = (SourceMtMoonVolatileEvent)events[at]; var hit = Envelope.Hits(events).Single();
            if (mutation == "fake-pp") events = Shared.Number(events.Insert(at + 1, new SourcePpCommandEvent(0, released.Turn, new(0), new(1), 0, Move(117), Move(117), 9, 8, 1, 0)));
            if (mutation == "missing-release") events = events.RemoveAt(at);
            if (mutation == "duplicate-hit") events = Shared.Number(events.Add(hit));
            if (mutation == "wrong-actor") events = events.SetItem(at, released with { Side = new(1) });
            if (mutation == "wrong-count") events = events.SetItem(at, released with { Before = 2 });
            if (mutation == "wrong-turn") events = events.SetItem(at, released with { Turn = released.Turn + 1 });
            if (mutation == "nonmonotonic") events = events.SetItem(events.IndexOf(hit), hit with { Sequence = released.Sequence });
            Deny(events, single);
        }
    }
    [TestMethod]
    public void RetainedThrashSurvivesLaterRoughSkinFaintWithoutInventingPp()
    {
        foreach (bool single in new[] { false, true })
        {
            var bodies = Bodies(37); bodies[1] = Kernel.Mon("CARVANHA", "BITE", 100); if (!single) bodies[3] = bodies[1];
            Assert.AreEqual("ABILITY_ROUGH_SKIN", WildBattleEffective.Ability(bodies[1]));
            var first = Envelope.Resolve(Kernel.Frame(bodies, single)); var frame = Body(first.State, 0, b => b with { Pokemon = b.Pokemon with { CurrentHp = 1 } });
            // The queued path owns the real singles FaintClearSetData lifecycle.
            var choices = frame.Slots.Take(frame.BattlerCount).Select(s => new SourceActionSelection(s.Occupant!.Value, SourceActionKind.Move, 0, s.Registers.Turn?.Lock?.Target ?? (Kernel.Mechanics.Catalog.Moves[s.Body!.Pokemon.Moves[0].Move].Target == "MOVE_TARGET_USER" ? s.Slot : new(s.Slot.Value ^ 1)))).ToImmutableArray();
            var start = SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog, frame, frame.Revision, choices); Assert.IsTrue(start.Succeeded, start.Fault); Assert.AreEqual(0, start.State.Queue[0].Slot.Value); Assert.IsNotNull(start.Continuation);
            var result = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, start.State, start.Continuation.Value); Assert.IsTrue(result.Succeeded, result.Fault);
            var hit = Envelope.Hits(result.Events).Single(); Assert.AreEqual(0, result.State.Slots[0].Body!.Pokemon.CurrentHp);
            Assert.IsFalse(result.Events.Any(e => e is SourceMoveUsedEvent or SourcePpCommandEvent));
            Check(result.Events, single, 37, result.Events.OfType<SourceAttackStringEvent>().Single(), hit, CampaignMoveAnimationBoundary.AfterRetainedAttackText, null);
        }
    }
    [TestMethod]
    public void BideReleaseKeepsItsHitBoundaryWhenLaterContactDamageFaintsTheActor()
    {
        foreach (bool single in new[] { false, true })
        {
            var frame = StoredBide(single); frame = Body(frame, 1, _ => Kernel.Mon("CARVANHA", "BITE", 100) with { SpeedStage = 0 });
            frame = Body(frame, 0, b => b with { Pokemon = b.Pokemon with { CurrentHp = 1 } });
            var choices = frame.Slots.Take(frame.BattlerCount).Select(s => new SourceActionSelection(s.Occupant!.Value, SourceActionKind.Move, 0, s.Registers.Turn?.Lock?.Target ?? (Kernel.Mechanics.Catalog.Moves[s.Body!.Pokemon.Moves[0].Move].Target == "MOVE_TARGET_USER" ? s.Slot : new(s.Slot.Value ^ 1)))).ToImmutableArray();
            var start = SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog, frame, frame.Revision, choices); Assert.IsTrue(start.Succeeded, start.Fault); Assert.AreEqual(0, start.State.Queue[0].Slot.Value); Assert.IsNotNull(start.Continuation);
            var result = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, start.State, start.Continuation.Value); Assert.IsTrue(result.Succeeded, result.Fault);
            var hit = Envelope.Hits(result.Events).Single(); Assert.AreEqual(0, result.State.Slots[0].Body!.Pokemon.CurrentHp);
            Assert.IsFalse(result.Events.Any(e => e is SourceAttackStringEvent or SourceMoveUsedEvent or SourcePpCommandEvent));
            Check(result.Events, single, 117, hit, hit, CampaignMoveAnimationBoundary.BeforeHitResult, 1);
        }
    }
}
