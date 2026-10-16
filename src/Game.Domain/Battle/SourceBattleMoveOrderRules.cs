using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>SELECT in the live FIGHT picker. This menu transaction never chooses an action,
/// consumes PP, advances a turn or draws randomness. Presentation and owned-instance binding
/// are checked by the application before submitting the current occupant and revision.</summary>
public static class SourceBattleMoveOrderRules
{
    public static SourceIdentityResult Swap(PokemonCatalog catalog, SourceBattleFrame frame,
        long expectedRevision, OccupantToken actor, int firstSlot, int secondSlot)
    {
        SourceIdentityResult Fail(string error) => new(frame, error);
        if (SourceFourActionTurns.ValidateCapability(catalog, frame) is { } invalid) return Fail(invalid);
        if (frame.Revision != expectedRevision) return Fail("source_move_order_stale_revision");
        if (frame.Outcome != WildBattleOutcome.Ongoing || frame.FourTurn is not null || !frame.Queue.IsEmpty ||
            !frame.Reservations.IsEmpty || !frame.PendingSwitches.IsEmpty || frame.DeferredKoVacancyMask != 0 ||
            frame.Rewards?.Pending is not null) return Fail("source_move_order_selection_phase");
        if (!frame.IsCurrent(actor) || actor.Slot.Side != BattleSide.Player || !SourceFourMedicineRules.CanSelect(frame, actor.Slot))
            return Fail("source_move_order_actor");
        var cell = frame.Get(actor.Slot);
        var body = cell.Body!;
        if (cell.Registers.Turn?.Lock is not null || body.EncoreTurnsRemaining > 0 ||
            SourceFourActionTurns.RequiresStruggle(catalog, frame, actor.Slot) ||
            (frame.PreparedMedicines ?? []).Any(p => p.Actor.Slot == actor.Slot))
            return Fail("source_move_order_picker_unavailable");
        var moves = body.Pokemon.Moves;
        if (moves.Length < 2 || firstSlot < 0 || firstSlot >= moves.Length || secondSlot < 0 || secondSlot >= moves.Length)
            return Fail("source_move_order_slot");
        if (firstSlot == secondSlot) return new(frame, null);
        if (frame.Revision == long.MaxValue) return Fail("source_move_order_revision_exhausted");

        var changed = body with { Pokemon = body.Pokemon with { Moves = SwapSlots(moves, firstSlot, secondSlot) } };
        if (body.Transform is { } transform)
        {
            var order = transform.LiveMoveOrder ?? Enumerable.Range(0, moves.Length).ToImmutableArray();
            order = SwapSlots(order, firstSlot, secondSlot);
            changed = changed with { Transform = transform with {
                LiveMoveOrder = order.Where((value, index) => value != index).Any() ? order : null } };
        }
        else if (body.CopyMoves is { } copies)
        {
            // Deliberate improvement over the pinned source's one-way mimickedMoves update:
            // move the temporary slot and its independent persistent backup together in either direction.
            changed = changed with { CopyMoves = copies with { Slots = copies.Slots.Select(copy => copy with {
                Slot = copy.Slot == firstSlot ? secondSlot : copy.Slot == secondSlot ? firstSlot : copy.Slot
            }).ToImmutableArray() } };
        }
        var party = frame.Party(actor.Member.Side);
        var member = party.Members[actor.Member.Index];
        var persistent = body.Transform is null
            ? member.Pokemon with { Moves = SwapSlots(member.Pokemon.Moves, firstSlot, secondSlot) }
            : member.Pokemon;
        var next = frame with {
            Revision = frame.Revision + 1,
            Slots = frame.Slots.SetItem(actor.Slot.Value, cell with { Body = changed }),
            Parties = frame.Parties.SetItem((int)actor.Member.Side,
                party with { Members = party.Members.SetItem(actor.Member.Index, member with { Pokemon = persistent }) })
        };
        return SourceFourActionTurns.ValidateCapability(catalog, next) is { } error ? Fail(error) : new(next, null);
    }

    /// <summary>Callers validate occupied indices before transposing complete immutable values.</summary>
    internal static ImmutableArray<T> SwapSlots<T>(ImmutableArray<T> slots, int first, int second) =>
        first == second ? slots : slots.SetItem(first, slots[second]).SetItem(second, slots[first]);
}
