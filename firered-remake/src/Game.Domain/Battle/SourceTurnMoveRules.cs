using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal static class SourceTurnMoveRules
{
    internal static SourceBattleSlot ProjectPokemon(SourceBattleSlot cell, PokemonInstance pokemon)
    {
        var body = WildBattleTransformationRules.ApplyPersistentPokemon(cell.Body!, pokemon);
        var turn = cell.Registers.Turn;
        // gLockedMoves survives learning. Only the adapter's PP-position identity follows
        // the learned replacement; its forced move, retained target and counter stay intact.
        if (turn?.Lock is { } locked && locked.PpSlot < body.Pokemon.Moves.Length)
            turn = turn with { Lock = locked with { PpSlotMove = body.Pokemon.Moves[locked.PpSlot].Move } };
        return cell with { Body = body, Registers = cell.Registers with { Turn = turn } };
    }

    internal static string? Validate(WildBattleCombatant body, PokemonCatalog catalog)
    {
        // A just-overwritten disabled move remains until ENDTURN_DISABLE removes it.
        if (body.DisableTurnsRemaining is < 0 or > 5 ||
            (body.DisableTurnsRemaining == 0) != (body.DisabledMove is null) ||
            body.DisabledMove is { } move && !catalog.Moves.ContainsKey(move) ||
            body.Pokemon.CurrentHp == 0 && body.DisabledMove is not null)
            return "source_kernel_disable_state";
        if (body.RolloutTurnsRemaining != 0 || body.RolloutMoveSlot is not null)
            return "source_kernel_legacy_rollout_state";
        return null;
    }
}
