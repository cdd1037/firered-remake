using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameRecipeContext
{
    internal static bool HasMoveRestrictionRecipe(string effect) => effect is "EFFECT_ENCORE" or "EFFECT_IMPRISON" || HasSelectedVolatileRecipe(effect);
    internal bool EncoreAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose) =>
        SourceGenericAccuracy(actor, target, move, purpose);

    internal void ExecuteMoveRestriction(BattlerSlot actor, BattlerSlot target, int slot, PokemonMoveDefinition move, string purpose)
    {
        if (HasSelectedVolatileRecipe(move.Effect))
        { ExecuteSelectedVolatile(actor, target, slot, move, purpose); return; }
        // Encore's accuracycheck precedes attackstring/ppreduce, including failure.
        bool hit = move.Effect != "EFFECT_ENCORE" || EncoreAccuracy(actor, target, move, purpose);
        SpendEarlyPartyPp(actor, target, slot, move);
        if (!hit) { SourceGenericMiss(actor, target, move); return; }
        void Fail(string reason) => Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, reason));
        if (move.Effect == "EFFECT_ENCORE")
        {
            var body = Get(target); int previous = -1;
            if (body.LastUsedMove is { } last && last.Value is not ("pret:MOVE_NONE" or "pret:MOVE_UNAVAILABLE" or "pret:MOVE_STRUGGLE" or "pret:MOVE_ENCORE" or "pret:MOVE_MIRROR_MOVE"))
                for (int i = 0; i < body.Pokemon.Moves.Length; i++) if (body.Pokemon.Moves[i].Move == last) { previous = i; break; }
            if (body.EncoredMove is not null || previous < 0 || body.Pokemon.Moves[previous].CurrentPp == 0)
            { Fail("no-encore-target"); return; }
            int duration = (Draw($"{purpose}.encore-duration", 4) & 3) + 3;
            Set(target, body with { EncoreMoveSlot = previous, EncoredMove = body.Pokemon.Moves[previous].Move, EncoreTurnsRemaining = duration });
            Add(n => new SourceEncoreChangedEvent(n, Turn, target, previous, 0, duration, "move"));
            return;
        }
        if (Get(actor).Imprison) { Fail("already-imprisoning"); return; }
        // PressurePPLoseOnUsingImprison scans both opposing positions and the FIRST
        // learned Imprison slot, independently of the invocation's PP owner/caller.
        var opponents = ActiveSlots.Where(s => s.Side != actor.Side).ToArray();
        int pressure = opponents.Count(s => WildBattleEffective.Ability(Get(s)) == "ABILITY_PRESSURE");
        var user = Get(actor); int position = -1;
        for (int i = 0; i < user.Pokemon.Moves.Length; i++) if (user.Pokemon.Moves[i].Move.Value == "pret:MOVE_IMPRISON") { position = i; break; }
        if (pressure != 0 && position >= 0)
        {
            var learned = user.Pokemon.Moves[position]; int after = Math.Max(0, learned.CurrentPp - pressure);
            Set(actor, user with { Pokemon = user.Pokemon with { Moves = user.Pokemon.Moves.SetItem(position, learned with { CurrentPp = after }) } });
            Add(n => new SourcePpCommandEvent(n, Turn, actor, actor, position, Invocation!.ChosenMove, move.Id,
                learned.CurrentPp, after, pressure, MoveRestrictionRngCursor) { PpOwner = actor });
        }
        if (!opponents.Any(s => Get(actor).Pokemon.Moves.Any(m => Get(s).Pokemon.Moves.Any(o => o.Move == m.Move))))
        { Fail("no-shared-move"); return; }
        Set(actor, Get(actor) with { Imprison = true });
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, actor, "imprison", 0, 1, "move"));
    }

    internal bool ImprisonPreventsMove(BattlerSlot actor, MoveId move)
    {
        if (!SourceMoveRestrictionRules.Imprisoned(move, ActiveSlots.Where(s => s.Side != actor.Side).Select(Get))) return false;
        CancellerCancelMultiTurn(actor, "imprison");
        Add(n => new SourceMoveFailedEvent(n, Turn, actor, CancellerFailureTarget(actor), move, "imprisoned"));
        return true;
    }
}

internal sealed partial class FrameEndTurnContext
{
    internal void EncoreResidual(BattlerSlot slot)
    {
        var body = Get(slot); if (body.EncoreMoveSlot is not { } position) return;
        bool overwritten = SourceMoveRestrictionRules.EncoreOverwritten(body);
        int after = overwritten || body.Pokemon.Moves[position].CurrentPp == 0 ? 0 : body.EncoreTurnsRemaining - 1;
        Set(slot, after == 0 ? SourceMoveRestrictionRules.ClearEncore(body) : body with { EncoreTurnsRemaining = after });
        Add(n => new SourceEncoreChangedEvent(n, Turn, slot, position, body.EncoreTurnsRemaining, after,
            overwritten ? "move-changed" : after == 0 ? "expired" : "countdown"));
    }
}
