using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;
using Game.Domain.Rules;

namespace Game.Domain.Battle;

public static class BattleRules
{
    public static BattleTransition Apply(BattleSnapshot state, UseMove command, IBattleContent content)
    {
        ArgumentNullException.ThrowIfNull(state);
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(content);
        var invariant = ValidateState(state, content);
        if (invariant is not null) return Failure(TransitionStatus.Fault, state, invariant.Code, invariant.Message);
        if (!StableId.IsValid(command.CommandId.Value)) return Reject("invalid-command-id", "Command ID is required.");
        if (command.ExpectedRevision != state.Revision) return Reject("stale-revision", "Expected revision does not match.");
        if (state.Phase != BattlePhase.AwaitCommands) return Reject("battle-ended", "Battle no longer accepts moves.");
        var actor = state.Battlers.FirstOrDefault(x => x.Id == command.ActorId);
        var target = state.Battlers.FirstOrDefault(x => x.Id == command.TargetId);
        if (actor is null) return Reject("unknown-actor", "Actor is not active.");
        if (target is null || actor.Side == target.Side) return Reject("invalid-target", "Target must be an opposing active combatant.");
        if (actor.Hp <= 0 || target.Hp <= 0) return Reject("fainted-combatant", "A fainted combatant cannot act or be targeted.");
        if (command.MoveSlot < 0 || command.MoveSlot >= actor.Moves.Length) return Reject("invalid-move-slot", "Move slot is out of range.");
        if (state.PendingMoves.Any(x => x.ActorId == actor.Id)) return Reject("already-selected", "Actor has already chosen this turn.");
        if (state.PendingMoves.Any(x => x.CommandId == command.CommandId)) return Reject("duplicate-command-id", "Command ID already selected this turn.");
        var slot = actor.Moves[command.MoveSlot];
        if (slot.Pp <= 0) return Reject("no-pp", "Move has no PP.");
        var move = content.FindMove(slot.MoveId);
        var moveError = ValidateMove(move, slot);
        if (moveError is not null) return Failure(TransitionStatus.Fault, state, moveError.Code, moveError.Message);

        // Validate queued input before committing the second selection. Content faults never leave partial mutation.
        MoveDefinition? queuedDefinition = null;
        if (state.PendingMoves.Length == 1)
        {
            var queued = state.PendingMoves[0];
            var queuedActor = state.Battlers.Single(x => x.Id == queued.ActorId);
            var queuedSlot = queuedActor.Moves[queued.MoveSlot];
            queuedDefinition = content.FindMove(queued.MoveId);
            var queuedError = ValidateMove(queuedDefinition, queuedSlot);
            if (queuedError is not null) return Failure(TransitionStatus.Fault, state, queuedError.Code, queuedError.Message);
        }

        var events = ImmutableArray.CreateBuilder<BattleEvent>();
        long sequence = state.EventSequence;
        EventMetadata Metadata(CommandId cause) => new(state.BattleId, ++sequence, cause);
        events.Add(new MoveChosenEvent(Metadata(command.CommandId), actor.Id, move!.Id));
        var selection = new PendingMove(command.CommandId, actor.Id, command.MoveSlot, target.Id, move.Id);
        var selected = state.PendingMoves.Add(selection);
        if (selected.Length == 1)
        {
            return new(TransitionStatus.Accepted,
                state with { Revision = state.Revision + 1, EventSequence = sequence, PendingMoves = selected },
                events.ToImmutable(), [], null);
        }

        // Canonical player/opponent ordering makes selection arrival order irrelevant to same-speed tie mapping.
        var player = state.Battlers.Single(x => x.Side == BattleSide.Player);
        var opponent = state.Battlers.Single(x => x.Side == BattleSide.Opponent);
        var playerSelection = selected.Single(x => x.ActorId == player.Id);
        var opponentSelection = selected.Single(x => x.ActorId == opponent.Id);
        MoveDefinition ResolveDefinition(PendingMove p) => p == selection ? move! : queuedDefinition!;
        var playerMove = ResolveDefinition(playerSelection);
        var opponentMove = ResolveDefinition(opponentSelection);
        var rng = state.Rng;
        var trace = ImmutableArray.CreateBuilder<RandomTrace>();
        int Draw(string purpose, int bound)
        {
            var draw = Gen3Rng.Next(rng, purpose, bound);
            rng = draw.State; trace.Add(draw.Trace); return draw.Trace.Result;
        }
        bool opponentFirst = opponentMove.Priority > playerMove.Priority ||
            (opponentMove.Priority == playerMove.Priority && opponent.Stats.Speed > player.Stats.Speed);
        if (opponentMove.Priority == playerMove.Priority && opponent.Stats.Speed == player.Stats.Speed)
            opponentFirst = Draw("turn-order:speed-tie", 2) == 1;
        PendingMove[] order = opponentFirst ? [opponentSelection, playerSelection] : [playerSelection, opponentSelection];
        events.Add(new TurnOrderEvent(Metadata(command.CommandId), order.Select(x => x.ActorId).ToImmutableArray()));
        var battlers = state.Battlers.ToBuilder();
        BattleOutcome outcome = BattleOutcome.Ongoing;
        foreach (var action in order)
        {
            int actorIndex = FindIndex(battlers, action.ActorId), targetIndex = FindIndex(battlers, action.TargetId);
            var user = battlers[actorIndex]; var defender = battlers[targetIndex];
            if (user.Hp == 0 || defender.Hp == 0) continue;
            var definition = ResolveDefinition(action);
            var oldSlot = user.Moves[action.MoveSlot];
            user = user with { Moves = user.Moves.SetItem(action.MoveSlot, oldSlot with { Pp = oldSlot.Pp - 1 }) };
            battlers[actorIndex] = user;
            events.Add(new MoveUsedEvent(Metadata(action.CommandId), user.Id, defender.Id, definition.Id, oldSlot.Pp, oldSlot.Pp - 1));
            // Always-hit is effect-driven in FireRed, not inferred from zero accuracy. This profile accepts 1–100 only.
            if (Draw($"move:{user.Id}:accuracy", 100) + 1 > definition.Accuracy)
            {
                events.Add(new MoveMissedEvent(Metadata(action.CommandId), user.Id, defender.Id, definition.Id));
                continue;
            }
            bool critical = Draw($"move:{user.Id}:critical", 16) == 0;
            int randomPercent = 100 - Draw($"move:{user.Id}:damage-drop", 16);
            var category = TypeRules.Category(definition.Type);
            var damage = DamageRules.Calculate(new(user.Level, definition.Power, definition.Type,
                category == DamageCategory.Physical ? user.Stats.Attack : user.Stats.SpecialAttack,
                category == DamageCategory.Physical ? defender.Stats.Defense : defender.Stats.SpecialDefense,
                user.Type1, user.Type2, defender.Type1, defender.Type2, critical, randomPercent));
            int hp = Math.Max(0, defender.Hp - damage.Damage);
            battlers[targetIndex] = defender with { Hp = hp };
            events.Add(new HpChangedEvent(Metadata(action.CommandId), user.Id, defender.Id, definition.Id, defender.Hp, hp, damage));
            if (hp == 0)
            {
                events.Add(new FaintedEvent(Metadata(action.CommandId), defender.Id));
                outcome = user.Side == BattleSide.Player ? BattleOutcome.PlayerWon : BattleOutcome.OpponentWon;
                events.Add(new BattleEndedEvent(Metadata(action.CommandId), outcome));
                break; // Fainted opponent never spends PP or RNG on an unexecuted action.
            }
        }
        events.Add(new TurnEndedEvent(Metadata(command.CommandId), state.Turn));
        var next = state with
        {
            Revision = state.Revision + 1, Turn = state.Turn + 1, EventSequence = sequence,
            Battlers = battlers.ToImmutable(), PendingMoves = [], Rng = rng, Outcome = outcome,
            Phase = outcome == BattleOutcome.Ongoing ? BattlePhase.AwaitCommands : BattlePhase.Ended
        };
        return new(TransitionStatus.Accepted, next, events.ToImmutable(), trace.ToImmutable(), null);

        BattleTransition Reject(string code, string message) => Failure(TransitionStatus.Rejected, state, code, message);
    }

    private static int FindIndex(ImmutableArray<CombatantSnapshot>.Builder battlers, CombatantId id)
    {
        for (int i = 0; i < battlers.Count; i++) if (battlers[i].Id == id) return i;
        throw new InvalidOperationException("Validated actor vanished.");
    }
    private static BattleTransition Failure(TransitionStatus status, BattleSnapshot state, string code, string message) =>
        new(status, state, [], [], new(code, message));

    private static Diagnostic? ValidateMove(MoveDefinition? move, MoveSlot slot)
    {
        if (move is null || move.Id != slot.MoveId) return new("missing-move-content", $"Missing or mismatched definition for {slot.MoveId}.");
        if (move.Effect != MoveEffect.OrdinaryDamage || move.Type == ElementType.Mystery || move.Accuracy == 0)
            return new("unsupported-move", $"Move {move.Id} is outside the ordinary-damage profile.");
        if (!Enum.IsDefined(move.Type) || move.Power is < 1 or > 255 || move.Accuracy is < 1 or > 100 ||
            move.MaxPp is < 1 or > 64 || move.Priority is < -7 or > 7 || slot.MaxPp != move.MaxPp)
            return new("invalid-move-content", $"Invalid definition or PP bounds for {move.Id} (PP Ups unsupported).");
        return null;
    }

    /// <summary>Pure semantic validation for load/restore boundaries; validates every represented move reference.</summary>
    public static Diagnostic? ValidateState(BattleSnapshot state, IBattleContent content)
    {
        ArgumentNullException.ThrowIfNull(state); ArgumentNullException.ThrowIfNull(content);
        var error = ValidateSnapshot(state);
        if (error is not null) return error;
        foreach (var battler in state.Battlers)
        foreach (var slot in battler.Moves)
        {
            var moveError = ValidateMove(content.FindMove(slot.MoveId), slot);
            if (moveError is not null) return moveError;
        }
        return null;
    }

    private static Diagnostic? ValidateSnapshot(BattleSnapshot state)
    {
        if (state.RulesVersion != RuleVersions.SingleBattleV0 || state.Mode != BattleMode.Singles ||
            state.ActiveMechanics != UnsupportedBattleMechanics.None)
            return new("unsupported-rules-profile", "Only fr-single-v0 singles without unimplemented modifiers are enabled.");
        if (!StableId.IsValid(state.BattleId.Value) || string.IsNullOrWhiteSpace(state.ContentVersion) || state.Revision < 0 ||
            state.Revision == long.MaxValue || state.Turn < 1 || state.Turn == int.MaxValue || state.EventSequence < 0 ||
            state.EventSequence > long.MaxValue - 32 || state.Rng.Cursor > ulong.MaxValue - 8 || state.Rng.AlgorithmId != Gen3Rng.AlgorithmId ||
            !Enum.IsDefined(state.Phase) || !Enum.IsDefined(state.Outcome) || state.Battlers.IsDefault || state.Battlers.Length != 2 ||
            state.PendingMoves.IsDefault || state.PendingMoves.Length > 1)
            return new("invalid-battle-state", "Invalid battle identifiers, counters, roster, or pending selection array.");
        foreach (var b in state.Battlers)
        {
            if (b is null || b.Stats is null || !StableId.IsValid(b.Id.Value) || !StableId.IsValid(b.SpeciesId.Value) ||
                (b.OriginalTrainerId.HasValue && !StableId.IsValid(b.OriginalTrainerId.Value.Value)) || !Enum.IsDefined(b.Side) ||
                b.Level is < 1 or > 100 || !Enum.IsDefined(b.Type1) || (b.Type2.HasValue && !Enum.IsDefined(b.Type2.Value)) ||
                b.Stats.MaxHp is < 1 or > 999 || b.Hp < 0 || b.Hp > b.Stats.MaxHp ||
                b.Stats.Attack is < 1 or > 999 || b.Stats.Defense is < 1 or > 999 ||
                b.Stats.SpecialAttack is < 1 or > 999 || b.Stats.SpecialDefense is < 1 or > 999 || b.Stats.Speed is < 1 or > 999 ||
                b.Moves.IsDefault || b.Moves.Length is < 1 or > 4 || b.Moves.Any(m => m is null || !StableId.IsValid(m.MoveId.Value) ||
                    m.MaxPp is < 1 or > 64 || m.Pp < 0 || m.Pp > m.MaxPp))
                return new("invalid-combatant", "Invalid combatant identity, stats, HP, types, or move slots.");
        }
        if (state.Battlers[0].Id == state.Battlers[1].Id || state.Battlers[0].Side == state.Battlers[1].Side)
            return new("invalid-roster", "Singles profile requires one distinct active combatant per side.");
        if ((state.Phase == BattlePhase.Ended) != (state.Outcome != BattleOutcome.Ongoing))
            return new("invalid-battle-outcome", "Phase and outcome disagree.");
        if (state.Phase == BattlePhase.AwaitCommands && state.Battlers.Any(b => b.Hp == 0))
            return new("invalid-active-combatant", "Ongoing singles battle cannot contain fainted active combatants.");
        if (state.Phase == BattlePhase.Ended)
        {
            int playerHp = state.Battlers.Single(b => b.Side == BattleSide.Player).Hp;
            int opponentHp = state.Battlers.Single(b => b.Side == BattleSide.Opponent).Hp;
            bool valid = state.Outcome switch
            {
                BattleOutcome.PlayerWon => playerHp > 0 && opponentHp == 0,
                BattleOutcome.OpponentWon => opponentHp > 0 && playerHp == 0,
                BattleOutcome.Draw => playerHp == 0 && opponentHp == 0,
                _ => false
            };
            if (!valid || !state.PendingMoves.IsEmpty) return new("invalid-battle-outcome", "Winner, HP, and pending moves disagree.");
        }
        foreach (var p in state.PendingMoves)
        {
            if (p is null || !StableId.IsValid(p.CommandId.Value)) return new("invalid-pending-move", "Pending command is invalid.");
            var actor = state.Battlers.FirstOrDefault(b => b.Id == p.ActorId);
            var target = state.Battlers.FirstOrDefault(b => b.Id == p.TargetId);
            if (actor is null || target is null || actor.Side == target.Side || p.MoveSlot < 0 || p.MoveSlot >= actor.Moves.Length ||
                actor.Moves[p.MoveSlot].Pp == 0 || actor.Moves[p.MoveSlot].MoveId != p.MoveId)
                return new("invalid-pending-move", "Pending command no longer matches state.");
        }
        return null;
    }
}
