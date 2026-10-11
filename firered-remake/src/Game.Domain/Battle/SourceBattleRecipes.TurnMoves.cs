using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasTurnMoveRecipe(string effect) => effect is "EFFECT_DISABLE" or "EFFECT_ROLLOUT";
    internal void ExecuteTurnMove(PokemonCatalog catalog, BattlerSlot actor, int slot,
        PokemonMoveDefinition move, string purpose, ref bool handled)
    {
        if (!HasTurnMoveRecipe(move.Effect)) return;
        handled = true;
        var target = Invocation!.Target;
        if (move.Effect == "EFFECT_ROLLOUT") { SourceRollout(catalog, actor, target, move, purpose); return; }
        SpendEarlyPartyPp(actor, target, slot, move);
        if (!SourceGenericAccuracy(actor, target, move, purpose))
        { Add(n => new SourceMoveFailedEvent(n, Turn, actor, target, move.Id, "accuracy")); return; }
        ApplyDisable(actor, target, move, purpose);
    }
    internal void ApplyMinimize(BattlerSlot slot)
    {
        if (Get(slot).Minimized) return;
        Set(slot, Get(slot) with { Minimized = true });
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, slot, "minimized", 0, 1, "move"));
    }
    internal void ApplyDefenseCurl(BattlerSlot slot)
    {
        if (Get(slot).DefenseCurled) return;
        Set(slot, Get(slot) with { DefenseCurled = true });
        Add(n => new SourceDefenseCurledEvent(n, Turn, slot));
    }
    internal void ApplyDisable(BattlerSlot side, BattlerSlot target, PokemonMoveDefinition move, string purpose)
    {
        var body = Get(target);
        // Cmd_disablelastusedattack stops at the FIRST matching move position.
        var learned = body.Pokemon.Moves.FirstOrDefault(m => m.Move == body.LastUsedMove);
        if (body.DisabledMove is not null || learned is null || learned.CurrentPp == 0)
        { Add(n => new SourceMoveFailedEvent(n, Turn, side, target, move.Id, "no-disable-target")); return; }
        ChangeDisable(target, learned.Move, (Draw($"{purpose}.disable-duration", 4) & 3) + 2, "move");
    }
    internal void ChangeDisable(BattlerSlot slot, MoveId? move, int turns, string reason)
    {
        var body = Get(slot);
        if (body.DisableTurnsRemaining == turns && body.DisabledMove == move) return;
        Set(slot, body with { DisabledMove = turns == 0 ? null : move, DisableTurnsRemaining = turns });
        Add(n => new SourceDisableChangedEvent(n, Turn, slot, move ?? body.DisabledMove, body.DisableTurnsRemaining, turns, reason));
    }
    internal bool DisabledPreventsMove(BattlerSlot slot, MoveId move)
    {
        if (Get(slot).DisableTurnsRemaining == 0 || Get(slot).DisabledMove != move) return false;
        CancellerCancelMultiTurn(slot, "disabled");
        Add(n => new SourceMoveFailedEvent(n, Turn, slot, CancellerFailureTarget(slot), move, "disabled"));
        return true;
    }

    // Only storage/legacy scalar adapters vary. Cmd_rolloutdamagecalculation is shared.
    internal virtual int RolloutRemaining(BattlerSlot slot) => throw new SourceRecipeUnsupported("source_rollout_storage");
    internal virtual void ChangeRollout(BattlerSlot slot, PokemonMoveDefinition move, int remaining, string reason) => throw new SourceRecipeUnsupported("source_rollout_storage");
    internal virtual void RolloutAttackString(BattlerSlot slot) => throw new SourceRecipeUnsupported("source_rollout_storage");
    internal virtual bool RolloutAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose) => SourceGenericAccuracy(actor, target, move, purpose);
    internal virtual bool RolloutImmune(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) =>
        SourceTypeResult(catalog, target, move, command: SourceTypeCommand.TypeCalc2, apply: true).Immune;
    internal virtual void RolloutReportType(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move)
    {
        if (!SourceTypeReport(catalog, actor, target, move, command: SourceTypeCommand.TypeCalc2))
            SourceGenericEffectiveness(catalog, actor, target, move);
    }
    internal virtual int RolloutDamage(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, bool critical, int percent, int power, string purpose) =>
        AdjustHeldDamage(target, move.Id, SourceGenericDamage(catalog, Get(actor), Get(target), target, move, critical, percent, power), purpose);
    internal virtual void RolloutFinish(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose, int lost)
    {
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        if (lost > 0) MtMoonRageOnDamage(actor, target, move.Id);
        SourceGenericContact(catalog, actor, target, move, purpose, lost);
        ForestOutcome(true);
    }
    internal void SourceRollout(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose)
    {
        int before = RolloutRemaining(actor);
        if (before == 0) SpendEarlyPartyPp(actor, target, Invocation!.PpSlot, move); else RolloutAttackString(actor);
        if (!RolloutAccuracy(actor, target, move, purpose))
        {
            if (before != 0) ChangeRollout(actor, move, 0, "missed");
            if (SourceTypeResultsEnabled && WildBattleEffective.Ability(Get(target)) == "ABILITY_WONDER_GUARD")
            {
                SourceTypeNoEffect(target, clearHitType: true); // failed accuracy's JumpIfMoveFailed
                if (SourceTypeReport(catalog, actor, target, move, command: SourceTypeCommand.TypeCalc2)) return;
            }
            SourceGenericMiss(actor, target, move); return;
        }
        if (RolloutImmune(catalog, actor, target, move))
        { if (before != 0) ChangeRollout(actor, move, 0, "no-effect"); RolloutReportType(catalog, actor, target, move); return; }
        int remaining = (before == 0 ? 5 : before) - 1;
        int power = (move.Power << (4 - remaining)) * (Get(actor).DefenseCurled ? 2 : 1);
        ChangeRollout(actor, move, remaining, before == 0 ? "move" : remaining == 0 ? "finished" : "countdown");
        bool critical = SourceGenericCritical(Get(actor), Get(target), move, purpose);
        int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
        int damage = RolloutDamage(catalog, actor, target, move, critical, percent, power, purpose);
        int lost = SourceGenericApplyDamage(actor, target, move, damage, critical, percent);
        RolloutReportType(catalog, actor, target, move);
        Draw($"{purpose}.secondary-effect", 100);
        RolloutFinish(catalog, actor, target, move, purpose, lost);
    }
}
