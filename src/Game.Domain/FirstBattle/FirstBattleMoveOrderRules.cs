using Game.Domain.Battle;
using Game.Domain.Pokemon;

namespace Game.Domain.FirstBattle;

/// <summary>The first-rival controller delegates SELECT to the ordinary FIGHT picker.
/// Keep its bounded tutorial state and reuse the same complete-slot transposition.</summary>
public static class FirstBattleMoveOrderRules
{
    public static FirstBattleResult Swap(PokemonCatalog catalog, FirstBattleState state,
        long expectedRevision, int firstSlot, int secondSlot)
    {
        FirstBattleResult Fail(string error) => new(TransitionStatus.Rejected, state, [], [],
            new(error, "Move ordering requires the current first-battle move picker."));
        if (FirstBattleRules.Validate(catalog, state) is { } invalid) return Fail(invalid);
        if (state.Revision != expectedRevision) return Fail("first_battle_move_order_stale_revision");
        if (state.Outcome != FirstBattleOutcome.Ongoing || FirstBattleRules.RequiresStruggle(state.Player))
            return Fail("first_battle_move_order_picker_unavailable");
        var moves = state.Player.Pokemon.Moves;
        if (moves.Length < 2 || firstSlot < 0 || firstSlot >= moves.Length || secondSlot < 0 || secondSlot >= moves.Length)
            return Fail("first_battle_move_order_slot");
        if (firstSlot == secondSlot) return new(TransitionStatus.Accepted, state, [], [], null);
        if (state.Revision == long.MaxValue) return Fail("first_battle_move_order_revision_exhausted");
        var next = state with { Revision = state.Revision + 1, Player = state.Player with {
            Pokemon = state.Player.Pokemon with { Moves = SourceBattleMoveOrderRules.SwapSlots(moves, firstSlot, secondSlot) } } };
        return FirstBattleRules.Validate(catalog, next) is { } error ? Fail(error)
            : new(TransitionStatus.Accepted, next, [], [], null);
    }
}
