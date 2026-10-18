using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// Actual public turn commands with natural source learners. Controlled speed,
// PP, status and substitute are integration fixtures, not natural-route evidence.
internal static class ChargeCopyAnimationKernelFixture
{
    internal static readonly int[] All = [13, 130, 101, 102, 104, 354];
    internal static (string Name, string Species, string Effect) Spec(int id) => id switch {
        13 => ("RAZOR_WIND", "NUZLEAF", "EFFECT_RAZOR_WIND"),
        130 => ("SKULL_BASH", "SQUIRTLE", "EFFECT_SKULL_BASH"),
        101 => ("NIGHT_SHADE", "GASTLY", "EFFECT_LEVEL_DAMAGE"),
        102 => ("MIMIC", "JIGGLYPUFF", "EFFECT_MIMIC"),
        104 => ("DOUBLE_TEAM", "PIKACHU", "EFFECT_EVASION_UP"),
        315 => ("OVERHEAT", "CHARMANDER", "EFFECT_OVERHEAT"),
        354 => ("PSYCHO_BOOST", "DEOXYS", "EFFECT_OVERHEAT"),
        _ => throw new ArgumentOutOfRangeException(nameof(id)) };
    internal static MoveId Move(int id) => Kernel.Id(Spec(id).Name);
    internal static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        var spec = Spec(id); var user = id == 315 ? ConditionalRecoveryAnimationKernelFixture.MachineMon(spec.Species, "TM50", spec.Name, "SCRATCH") : Kernel.Mon(spec.Species, spec.Name);
        bodies[actor] = user with { AccuracyStage = 12, SpeedStage = 12 };
        return bodies;
    }
    internal static SourceBattleFrame Body(SourceBattleFrame frame, int slot, Func<WildBattleCombatant, WildBattleCombatant> change)
    {
        var cell = frame.Slots[slot]; var body = change(cell.Body!); var key = cell.Member!.Value; var party = frame.Parties[(int)key.Side];
        var member = party.Members[key.Index] with { Pokemon = WildBattleTransformationRules.PersistentPokemon(body),
            Status = WildBattleTransformationRules.PersistentStatus(body), HeldItem = body.HeldItem, KnockedOffItem = body.KnockedOffItem };
        return frame with { Slots = frame.Slots.SetItem(slot, cell with { Body = body }),
            Parties = frame.Parties.SetItem((int)key.Side, party with { Members = party.Members.SetItem(key.Index, member) }) };
    }
    internal static SourceBattleFrame Initial(int id, bool single, int actor = 0)
    {
        var frame = Kernel.Frame(Bodies(id, actor), single);
        // Seed actual move history through the same public kernel, rather than
        // assigning LastUsedMove to manufacture successful Mimic evidence.
        if (id == 102) frame = Envelope.Resolve(frame, actor ^ 1, actor ^ 1).State;
        return frame;
    }
    internal static SourceFourActionResult Ordered(SourceBattleFrame frame, int actor = 0, int? target = null, int moveSlot = 0)
    {
        string before = JsonSerializer.Serialize(frame);
        var selections = frame.Slots.Take(frame.BattlerCount).Select(cell => {
            var locked = cell.Registers.Turn?.Lock; int slot = locked?.PpSlot ?? (cell.Slot.Value == actor ? moveSlot : 0);
            var move = Kernel.Mechanics.Catalog.Moves[cell.Body!.Pokemon.Moves[slot].Move];
            var recipient = locked?.Target ?? (cell.Slot.Value == actor && target is { } chosen ? new BattlerSlot(chosen)
                : move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? cell.Slot : cell.Slot.Opposite);
            return new SourceActionSelection(cell.Occupant!.Value, SourceActionKind.Move, slot, recipient);
        }).ToImmutableArray();
        var start = SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog, frame, frame.Revision, selections);
        Assert.IsTrue(start.Succeeded, start.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(start.State));
        Assert.AreEqual(actor, start.State.Queue[0].Slot.Value, "Controlled speed must put the tested actor first.");
        Assert.IsNotNull(start.Continuation);
        var result = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, start.State, start.Continuation.Value);
        Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        Assert.IsFalse(result.Events.IsEmpty); Assert.AreEqual(before, JsonSerializer.Serialize(frame));
        return result;
    }
    internal static SourceBattleFrame CompleteTurn(SourceFourActionResult result)
    {
        int steps = 0;
        while (!result.CompletedCleanTurn)
        {
            Assert.IsTrue(++steps <= 32, "One nonfainting turn must reach its public completion boundary.");
            Assert.IsNotNull(result.Continuation);
            result = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, result.State, result.Continuation.Value);
            Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
        }
        return result.State;
    }
    internal static SourceBattleFrame Charged(int id, bool single, int actor = 0, int? pp = null, bool pressure = false, int defense = 6)
    {
        var bodies = Bodies(id, actor);
        bodies[actor] = bodies[actor] with { DefenseStage = defense };
        if (pp is { } remaining) bodies[actor] = bodies[actor] with { Pokemon = bodies[actor].Pokemon with {
            Moves = [bodies[actor].Pokemon.Moves[0] with { CurrentPp = remaining }] } };
        if (pressure) foreach (int slot in Enumerable.Range(0, single ? 2 : 4).Where(s => (s & 1) != (actor & 1)))
        {
            bodies[slot] = Kernel.Mon("ARTICUNO", "MIST", 100) with { SpeedStage = 0 };
            Assert.AreEqual("ABILITY_PRESSURE", WildBattleEffective.Ability(bodies[slot]));
        }
        var result = Ordered(Kernel.Frame(bodies, single), actor, actor ^ 1);
        Assert.AreEqual(WildBattleCalledLockKind.Charge, result.State.Slots[actor].Registers.Turn!.Lock!.Kind);
        return CompleteTurn(result);
    }
    internal static SourceFourActionResult Find(SourceBattleFrame frame, int actor, int target,
        Func<SourceFourActionResult, bool> predicate, string branch)
    {
        for (uint seed = 0; seed < 1024; seed++)
        {
            var result = Ordered(frame with { Rng = new(seed) }, actor, target);
            if (!predicate(result)) continue;
            Console.WriteLine($"{branch}: actual public-turn-start seed={seed}"); return result;
        }
        throw new InvalidOperationException($"No actual {branch} in 1024 public-turn-start seeds.");
    }
    internal static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) =>
        Envelope.Single(events, index) with { SourceEvents = [] };
    internal static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events, int index,
        bool single, bool result, SourceBattleScene scene = SourceBattleScene.On) => single
        ? result ? AnimationDispatchFixture.AtResult(Single(events, index), true, scene) : AnimationDispatchFixture.AtAttackText(Single(events, index), true, scene)
        : result ? AnimationDispatchFixture.AtResult(Envelope.Double(events, index), scene) : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, index), scene);
    internal static CampaignStatAnimationDispatch? Wave(ImmutableArray<SourceBattleEvent> events, int index, bool single) => single
        ? AnimationDispatchFixture.StatAtResult(Single(events, index), true) : AnimationDispatchFixture.StatAtResult(Envelope.Double(events, index));
    internal static void Deny(ImmutableArray<SourceBattleEvent> events, bool single)
    { for (int i = 0; i < events.Length; i++) foreach (bool result in new[] { false, true }) Assert.IsNull(Project(events, i, single, result).Dispatch); }
    internal static CampaignMoveAnimationDispatch Check(ImmutableArray<SourceBattleEvent> events, int id, bool single,
        SourceBattleEvent owner, SourceBattleEvent outcome, byte? turn = null)
    {
        string before = JsonSerializer.Serialize(events); int count = 0; CampaignMoveAnimationDispatch? found = null;
        bool setup = owner is SourceMtMoonVolatileEvent;
        var attack = events.OfType<SourceAttackStringEvent>().SingleOrDefault(); var used = events.OfType<SourceMoveUsedEvent>().SingleOrDefault();
        int target = outcome switch { SourceHpChangedEvent h => h.Target.Value, SourceSubstituteDamagedEvent s => s.Target.Value,
            SourceMoveCopiedEvent c => c.Target.Value, SourceStatChangedEvent s => s.Target.Value,
            _ => events.OfType<SourcePpCommandEvent>().First().Target.Value };
        var pp = events.OfType<SourcePpCommandEvent>().Single(p => p.Target.Value == target);
        Assert.AreEqual(Move(id), pp.ChosenMove); Assert.AreEqual(pp.ChosenMove, pp.CurrentMove);
        Assert.AreEqual(pp.Actor, pp.PpOwner); Assert.IsTrue(pp.PpSlot is >= 0 and < 4);
        Assert.AreEqual(turn == 1 ? pp.Before : Math.Max(0, pp.Before - pp.CommandCost), pp.After);
        if (setup) { Assert.IsNull(used); Assert.IsNull(attack); }
        else { Assert.IsNotNull(used); Assert.IsNotNull(attack); Assert.AreEqual(used.PpBefore, events.OfType<SourcePpCommandEvent>().First().Before); }
        for (int i = 0; i < events.Length; i++) foreach (bool result in new[] { false, true })
        {
            var decision = Project(events, i, single, result); Assert.AreEqual(decision, Project(events, i, single, result));
            Assert.IsNull(Project(events, i, single, result, SourceBattleScene.Off).Dispatch);
            if (single) Assert.IsNull(result ? AnimationDispatchFixture.AtResult(Single(events, i), false, SourceBattleScene.On).Dispatch
                : AnimationDispatchFixture.AtAttackText(Single(events, i), false, SourceBattleScene.On).Dispatch);
            if (events[i] != owner || result != setup) { Assert.IsNull(decision.Dispatch, decision.Reason); continue; }
            found = decision.Dispatch; Assert.IsNotNull(found, decision.Reason); count++;
            Assert.AreEqual(id, found.SourceMoveId); Assert.AreEqual(owner.Sequence, found.Sequence); Assert.AreEqual(outcome.Sequence, found.ResultSequence);
            Assert.AreEqual(attack?.Sequence, found.AttackSequence); Assert.AreEqual(pp.Sequence, found.PpSequence);
            Assert.AreEqual(pp.Sequence, found.SnapshotSequence); Assert.AreEqual(pp.Actor, found.Actor); Assert.AreEqual(target, found.Target.Value);
            Assert.AreEqual(setup ? CampaignMoveAnimationBoundary.BeforeChargeResult : CampaignMoveAnimationBoundary.AfterAttackText, found.Boundary);
            Assert.AreEqual(!setup, found.AcknowledgesCurrentReceipt); Assert.AreEqual(turn, found.MoveTurn);
            Assert.AreEqual(outcome is SourceSubstituteDamagedEvent, found.HitsSubstitute);
        }
        Assert.AreEqual(1, count); Assert.AreEqual(before, JsonSerializer.Serialize(events)); return found!;
    }
}
