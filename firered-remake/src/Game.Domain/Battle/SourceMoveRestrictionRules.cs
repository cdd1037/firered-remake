using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>CheckMoveLimitations / GetImprisonedMovesCount. Recipe admission is not a move
/// limitation: an unsupported but usable move never creates permission to use Struggle.</summary>
internal static class SourceMoveRestrictionRules
{
    internal static bool Imprisoned(MoveId move, IEnumerable<WildBattleCombatant> opponents) =>
        move != WildBattleRules.StruggleMove && opponents.Any(m => m.Imprison && m.Pokemon.CurrentHp > 0 &&
            m.Pokemon.Moves.Any(learned => learned.Move == move));

    internal static bool Selectable(PokemonCatalog catalog, WildBattleCombatant actor, int slot,
        IEnumerable<WildBattleCombatant> opponents)
    {
        if (slot < 0 || slot >= actor.Pokemon.Moves.Length) return false;
        var learned = actor.Pokemon.Moves[slot];
        return learned.CurrentPp > 0 && (actor.DisabledMove != learned.Move || actor.DisableTurnsRemaining == 0) &&
            (actor.EncoreTurnsRemaining == 0 || actor.EncoredMove == learned.Move) &&
            SourceBattleHeldItems.ChoiceAllows(actor, learned.Move) && !Imprisoned(learned.Move, opponents) &&
            !(actor.CalledVolatile is { Tormented: true } && actor.LastUsedMove == learned.Move && learned.Move != WildBattleRules.StruggleMove) &&
            !(actor.CalledVolatile is { TauntTurnsRemaining: > 0 } && catalog.Moves[learned.Move].Power == 0);
    }

    internal static IEnumerable<WildBattleCombatant> Opponents(SourceBattleFrame frame, BattlerSlot actor) =>
        frame.Slots.Take(frame.BattlerCount).Where(c => c.Slot.Side != actor.Side && c.Body is not null).Select(c => c.Body!);

    internal static string? Validate(PokemonCatalog catalog, WildBattleCombatant body)
    {
        // Learning, Sketch and Transform can overwrite the retained position. The action
        // selector or ENDTURN_ENCORE owns that expiry; validation must not erase its history.
        if (body.EncoreTurnsRemaining is < 0 or > 6 || (body.EncoreTurnsRemaining == 0) != (body.EncoreMoveSlot is null) ||
            (body.EncoreTurnsRemaining == 0) != (body.EncoredMove is null) || body.EncoreMoveSlot is < 0 or > 3 ||
            body.EncoredMove is { } move && (move.Value is "pret:MOVE_ENCORE" or "pret:MOVE_MIRROR_MOVE" or "pret:MOVE_STRUGGLE" ||
                !catalog.Moves.ContainsKey(move)) || body.Pokemon.CurrentHp == 0 && (body.Imprison || body.EncoreTurnsRemaining != 0))
            return "source_kernel_move_restriction_state";
        if (body.LastUsedMove is { } last && last.Value is not ("pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE") && !catalog.Moves.ContainsKey(last))
            return "source_kernel_last_move_definition";
        return null;
    }

    internal static WildBattleCombatant ClearEncore(WildBattleCombatant body) => body with {
        EncoreTurnsRemaining = 0, EncoreMoveSlot = null, EncoredMove = null };
    internal static bool EncoreOverwritten(WildBattleCombatant body) => body.EncoreMoveSlot is { } slot &&
        (slot >= body.Pokemon.Moves.Length || body.Pokemon.Moves[slot].Move != body.EncoredMove);
}
