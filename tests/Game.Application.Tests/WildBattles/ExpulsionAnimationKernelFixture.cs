using System.Collections.Immutable;
using System.Text.Json;
using Game.Application;
using Game.Content;
using Game.Domain.Battle;
using Game.Domain.Ids;
using Game.Domain.Options;
using Game.Domain.Pokemon;
using Game.Domain.Trainers;
using Game.Domain.WildBattle;
using Kernel = FireRed.Tests.Application.WildBattles.WeatherSelectedAnimationKernelFixture;
using Envelope = FireRed.Tests.Application.WildBattles.OverlayHitAnimationKernelFixture;

namespace FireRed.Tests.Application.WildBattles;

// Authentic natural learners, public action/turn commands and generated trainer rosters.
// Controlled stages, status, substitute and RNG are integration fixtures, not route-play evidence.
internal static class ExpulsionAnimationKernelFixture
{
    internal static (string Name, string Species, string Effect) Spec(int id) => id switch {
        18 => ("WHIRLWIND", "PIDGEOT", "EFFECT_ROAR"),
        46 => ("ROAR", "GROWLITHE", "EFFECT_ROAR"),
        100 => ("TELEPORT", "ABRA", "EFFECT_TELEPORT"),
        _ => throw new ArgumentOutOfRangeException(nameof(id)) };
    internal static MoveId Move(int id) => Kernel.Id(Spec(id).Name);
    internal static WildBattleCombatant[] Bodies(int id, int actor = 0)
    {
        var bodies = Enumerable.Range(0, 4).Select(_ => Kernel.Mon("WAILORD", "SPLASH", 100) with { SpeedStage = 0 }).ToArray();
        var spec = Spec(id); bodies[actor] = Kernel.Mon(spec.Species, spec.Name, 100) with { SpeedStage = 12, AccuracyStage = 12 };
        return bodies;
    }
    internal static SourceBattleFrame Frame(WildBattleCombatant[] bodies, bool single, bool reserves = true)
    {
        var frame = Kernel.Frame(bodies, single);
        if (single || !reserves) return frame;
        foreach (BattleSide side in new[] { BattleSide.Player, BattleSide.Opponent })
        {
            var reserve = Kernel.Mon("MAGIKARP", "SPLASH", 100, personality: 17 + (uint)side);
            var party = frame.Party(side);
            frame = frame with { Parties = frame.Parties.SetItem((int)side,
                party with { Members = party.Members.Add(new(reserve.Pokemon, reserve.HeldItem, reserve.Status)) }) };
        }
        Assert.IsNull(SourceFourActionTurns.ValidateCapability(Kernel.Mechanics.Catalog, frame)); return frame;
    }
    internal static SourceBattleFrame TrainerSingle(int id)
    {
        var setup = SourceTrainerEncounterEntry.Prepare(new(42), "expulsion-animation");
        var trainers = TestAssets.Cached(() => PretVsSeekerParties.Load(TestAssets.Root));
        var made = VsSeekerTrainerPartyFactory.Create(trainers, Kernel.Mechanics, new("pret:TRAINER_YOUNGSTER_BEN"), setup.Rng);
        var spec = Spec(id);
        var entry = SourceTrainerEncounterEntry.Begin(Kernel.Mechanics.Catalog, setup, made,
            [Kernel.Mon(spec.Species, spec.Name, 100), Kernel.Mon("MAGIKARP", "SPLASH", 100, personality: 19)], 0);
        Assert.IsTrue(entry.Succeeded, entry.Fault); Assert.IsNull(SourceTrainerEncounterPolicy.Validate(Kernel.Mechanics.Catalog, entry.State!));
        return entry.State!;
    }
    internal static SourceFourActionResult Ordered(SourceBattleFrame frame, int actor = 0, int? target = null, uint? actionSeed = null, Action<SourceBattleFrame>? beforeAction = null)
    {
        string before = JsonSerializer.Serialize(frame);
        var selections = frame.Slots.Take(frame.BattlerCount).Select(cell => {
            var move = Kernel.Mechanics.Catalog.Moves[cell.Body!.Pokemon.Moves[0].Move];
            int recipient = cell.Slot.Value == actor && target is {} chosen ? chosen
                : move.Target is "MOVE_TARGET_USER" or "MOVE_TARGET_USER_OR_SELECTED" ? cell.Slot.Value : cell.Slot.Value ^ 1;
            return new SourceActionSelection(cell.Occupant!.Value, SourceActionKind.Move, 0, new(recipient));
        }).ToImmutableArray();
        var result = SourceFourActionTurns.BeginActions(Kernel.Mechanics.Catalog, frame, frame.Revision, selections);
        Assert.IsTrue(result.Succeeded, result.Fault);
        // Roar/Whirlwind have priority -6: advance other real actions instead of
        // pretending controlled Speed can move them ahead of priority-zero Splash.
        for (int i = 0; i < frame.BattlerCount; i++)
        {
            Assert.IsNotNull(result.Continuation);
            bool own = result.State.Queue[result.State.NextAction].Slot.Value == actor;
            if (own && actionSeed is {} seed) result = result with { State = result.State with { Rng = new(seed) } };
            if (own) beforeAction?.Invoke(result.State);
            result = SourceFourActionTurns.Advance(Kernel.Mechanics.Catalog, result.State, result.Continuation.Value);
            Assert.IsTrue(result.Succeeded, result.Fault); Assert.IsNull(SourceBattleIdentityRules.Validate(result.State));
            if (!own) continue;
            Assert.AreEqual(before, JsonSerializer.Serialize(frame)); return result;
        }
        throw new InvalidOperationException("The real queued actor was not reached.");
    }
    internal static SourceKernelResult Direct(SourceBattleFrame frame, int actor = 0, int? target = null) =>
        Envelope.Resolve(frame, actor, target ?? (Kernel.Mechanics.Catalog.Moves[frame.Slots[actor].Body!.Pokemon.Moves[0].Move].Target == "MOVE_TARGET_USER" ? actor : actor ^ 1));
    internal static CampaignWildBattle Single(ImmutableArray<SourceBattleEvent> events, int index) => Envelope.Single(events, index);
    internal static CampaignMoveAnimationDecision Project(ImmutableArray<SourceBattleEvent> events, int index, bool single, bool result = false,
        SourceBattleScene scene = SourceBattleScene.On) => single
        ? result ? AnimationDispatchFixture.AtResult(Single(events, index), true, scene) : AnimationDispatchFixture.AtAttackText(Single(events, index), true, scene)
        : result ? AnimationDispatchFixture.AtResult(Envelope.Double(events, index), scene) : AnimationDispatchFixture.AtAttackText(Envelope.Double(events, index), scene);
    internal static SourceBattleEvent Marker(ImmutableArray<SourceBattleEvent> events, int id) => id == 100
        ? events.OfType<SourceEndedEvent>().Single(e => e.Outcome == WildBattleOutcome.Escaped)
        : events.OfType<SourceForcedOutEvent>().Single(e => e.Move == Move(id));
    internal static void Deny(ImmutableArray<SourceBattleEvent> events, bool single)
    { for (int i = 0; i < events.Length; i++) foreach (bool result in new[] { false, true }) Assert.IsNull(Project(events, i, single, result).Dispatch); }
    internal static CampaignMoveAnimationDispatch Check(ImmutableArray<SourceBattleEvent> events, int id, bool single)
    {
        string before = JsonSerializer.Serialize(events); var marker = Marker(events, id);
        var used = events.OfType<SourceMoveUsedEvent>().Single(); var attack = events.OfType<SourceAttackStringEvent>().Single();
        var pp = events.OfType<SourcePpCommandEvent>().Single();
        Assert.AreEqual(Move(id), used.Move); Assert.AreEqual(used.Move, attack.CurrentMove); Assert.AreEqual(used.Move, attack.ChosenMove);
        Assert.AreEqual(used.Actor, attack.Actor); Assert.AreEqual(used.Actor, pp.Actor); Assert.AreEqual(used.Actor, pp.PpOwner);
        Assert.AreEqual(used.Target, pp.Target); Assert.AreEqual(used.Move, pp.CurrentMove); Assert.AreEqual(used.Move, pp.ChosenMove);
        Assert.AreEqual(used.Slot, pp.PpSlot); Assert.AreEqual(used.PpBefore, pp.Before); Assert.AreEqual(used.PpAfter, pp.After);
        Assert.AreEqual(Math.Max(0, pp.Before - pp.CommandCost), pp.After);
        Assert.AreEqual(pp.Sequence + 1, marker.Sequence); Assert.AreEqual(events.IndexOf(pp) + 1, events.IndexOf(marker));
        Assert.IsTrue(attack.Sequence < used.Sequence && used.Sequence < pp.Sequence);
        CampaignMoveAnimationDispatch? found = null;
        for (int i = 0; i < events.Length; i++) foreach (bool result in new[] { false, true })
        {
            var decision = Project(events, i, single, result); Assert.AreEqual(decision, Project(events, i, single, result));
            Assert.IsNull(Project(events, i, single, result, SourceBattleScene.Off).Dispatch);
            if (result || events[i] != used) { Assert.IsNull(decision.Dispatch, decision.Reason); continue; }
            found = decision.Dispatch; Assert.IsNotNull(found, decision.Reason);
            Assert.AreEqual(id, found.SourceMoveId); Assert.AreEqual(used.Actor, found.Actor); Assert.AreEqual(used.Target, found.Target);
            Assert.AreEqual(used.Sequence, found.Sequence); Assert.AreEqual(attack.Sequence, found.AttackSequence);
            Assert.AreEqual(pp.Sequence, found.PpSequence); Assert.AreEqual(pp.Sequence, found.SnapshotSequence); Assert.AreEqual(marker.Sequence, found.ResultSequence);
            Assert.AreEqual(CampaignMoveAnimationBoundary.AfterAttackText, found.Boundary); Assert.IsTrue(found.AcknowledgesCurrentReceipt);
            Assert.IsFalse(found.HitsSubstitute); Assert.IsNull(found.MoveTurn); Assert.IsNull(found.MoveDamage); Assert.IsNull(found.MovePower);
        }
        Assert.IsNotNull(found); Assert.AreEqual(before, JsonSerializer.Serialize(events)); return found;
    }
    internal static ImmutableArray<SourceBattleEvent> Number(ImmutableArray<SourceBattleEvent> events) =>
        events.Select((e, i) => e with { Sequence = i + 1 }).ToImmutableArray();
}
