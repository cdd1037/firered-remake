using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    // AtkCanceller_UnableToUseMove: one ordered recipe used by both storage contexts.
    // Historical profiles retain their original enablement; unported locks remain compatibility hooks.
    internal virtual bool CancellerSleepEnabled => true;
    internal virtual bool CancellerFreezeEnabled => true;
    internal virtual bool CancellerEarlyPartyEnabled => true;
    internal virtual bool CancellerEarlyBirdEnabled => true;
    internal virtual bool CancellerSleepingMovesEnabled => true;
    internal virtual bool CancellerTruantEnabled => true;
    internal virtual bool CancellerInfatuationEnabled => true;
    internal virtual bool ConfusionTowerRules => true;
    internal virtual bool ConfusionExpandedRules(PokemonCatalog catalog) => true;
    internal virtual bool ConfusionCanonicalRules(PokemonCatalog catalog) => true;
    internal virtual void CancellerUproarWake(BattlerSlot slot) { }
    internal virtual void CancellerCancelMultiTurn(BattlerSlot slot, string reason) { }
    internal virtual void CancellerParalysisBlocked(BattlerSlot slot) { }
    internal virtual bool CancellerCompatibilityLocks(PokemonCatalog catalog, BattlerSlot slot, MoveId move) => false;
    internal abstract bool CancellerHasRecharge(BattlerSlot slot);
    internal abstract void CancellerClearRecharge(BattlerSlot slot);
    internal virtual BattlerSlot CancellerFailureTarget(BattlerSlot slot) => Invocation!.Target;

    internal virtual bool CancellerMoveThawEnabled => true;
    internal virtual bool CancellerLateMoveThawEnabled => true;
    internal bool ActionPreventsMove(PokemonCatalog catalog, BattlerSlot actor, MoveId move, string purpose)
    {
        bool blocked = CancellerSleepEnabled && SleepPreventsMove(actor, move) ||
        CancellerFreezeEnabled && FreezePreventsMove(actor, move, purpose) ||
        TruantPreventsMove(actor, move) || RechargePreventsMove(actor, move) ||
        CancellerEarlyPartyEnabled && FlinchLocksConfusionPreventMove(catalog, actor, move, purpose) ||
        ParalysisPreventsMove(actor, move, purpose) ||
        CancellerInfatuationEnabled && InfatuationPreventsMove(actor, move, purpose);
        if (!blocked && CancellerLateMoveThawEnabled) ThawByMove(actor, move);
        return blocked;
    }

    internal bool SleepPreventsMove(BattlerSlot actor, MoveId move)
    {
        CancellerUproarWake(actor);
        uint before = Get(actor).Status;
        if (!WildBattleStatus.IsSleeping(before)) return false;
        uint decrement = CancellerEarlyBirdEnabled && WildBattleEffective.Ability(Get(actor)) == "ABILITY_EARLY_BIRD" ? 2u : 1u;
        uint after = before < decrement ? 0 : before - decrement;
        ChangeStatus(actor, after, after == 0 ? "woke-up" : "sleep-countdown");
        if (after == 0 || CancellerSleepingMovesEnabled && move.Value is "pret:MOVE_SNORE" or "pret:MOVE_SLEEP_TALK") return false;
        Add(n => new SourceActionBlockedEvent(n, Turn, actor, move, after));
        return true;
    }
    internal bool FreezePreventsMove(BattlerSlot actor, MoveId move, string purpose)
    {
        if (Get(actor).Status != WildBattleStatus.Freeze) return false;
        if (Draw($"{purpose}.freeze-thaw", 5) % 5 == 0)
        { ChangeStatus(actor, WildBattleStatus.None, "thawed-out"); return false; }
        if (CancellerMoveThawEnabled && IsThawHitMove(move)) return false;
        Add(n => new SourceActionBlockedEvent(n, Turn, actor, move, WildBattleStatus.Freeze));
        return true;
    }
    internal bool TruantPreventsMove(BattlerSlot actor, MoveId move)
    {
        var mon = Get(actor);
        if (!CancellerTruantEnabled || !mon.TruantLoafing || WildBattleEffective.Ability(mon) != "ABILITY_TRUANT") return false;
        CancellerCancelMultiTurn(actor, "truant");
        Add(n => new SourceMoveFailedEvent(n, Turn, actor, CancellerFailureTarget(actor), move, "truant"));
        return true;
    }
    internal bool RechargePreventsMove(BattlerSlot actor, MoveId move)
    {
        if (!CancellerHasRecharge(actor)) return false;
        CancellerClearRecharge(actor);
        CancellerCancelMultiTurn(actor, "recharged");
        Add(n => new SourceMoveFailedEvent(n, Turn, actor, CancellerFailureTarget(actor), move, "must-recharge"));
        return true;
    }
    internal bool FlinchLocksConfusionPreventMove(PokemonCatalog catalog, BattlerSlot actorSlot, MoveId move, string purpose)
    {
        if (Get(actorSlot).Flinched)
        {
            CancellerCancelMultiTurn(actorSlot, "flinch");
            Set(actorSlot, Get(actorSlot) with { Flinched = false });
            Add(n => new SourceFlinchEvent(n, Turn, actorSlot, false));
            return true;
        }
        if (CancellerCompatibilityLocks(catalog, actorSlot, move)) return true;
        int remaining = Get(actorSlot).ConfusionTurnsRemaining;
        if (remaining == 0) return false;
        Set(actorSlot, Get(actorSlot) with { ConfusionTurnsRemaining = remaining - 1 });
        Add(n => new SourceConfusionChangedEvent(n, Turn, actorSlot, remaining, remaining - 1, remaining == 1 ? "snapped-out" : "countdown"));
        if (remaining == 1 || (Draw($"{purpose}.confusion-action", 2) & 1) != 0) return false;
        CancellerCancelMultiTurn(actorSlot, "confusion");
        var actor = Get(actorSlot);
        int percent = 100 - Draw($"{purpose}.confusion-damage", 16) % 16;
        int damage = SourceConfusionDamage(actor, percent, ConfusionTowerRules, ConfusionExpandedRules(catalog),
            catalog.Moves.TryGetValue(move, out var selected) && selected.Effect == "EFFECT_EXPLOSION", ConfusionCanonicalRules(catalog));
        if (SourceHeldDamageEnabled) damage = AdjustHeldDamage(actorSlot, move, damage, purpose, normal2: true);
        HitItemsConfusionDamage(actorSlot, damage);
        int before = actor.Pokemon.CurrentHp, after = Math.Max(0, before - damage);
        Set(actorSlot, actor with { Pokemon = actor.Pokemon with { CurrentHp = after } });
        Add(n => new SourceConfusionSelfHitEvent(n, Turn, actorSlot, move, before, after, damage, percent));
        SourceDamageResultMessage(actorSlot, actorSlot, catalog.Moves[move]);
        if (after == 0) ForestFaint(actorSlot);
        ForestOutcome(true);
        return true;
    }
    internal bool ParalysisPreventsMove(BattlerSlot actor, MoveId move, string purpose)
    {
        if (Get(actor).Status != WildBattleStatus.Paralysis || Draw($"{purpose}.paralysis", 4) % 4 != 0) return false;
        CancellerParalysisBlocked(actor);
        Add(n => new SourceActionBlockedEvent(n, Turn, actor, move, Get(actor).Status));
        return true;
    }
        // CalculateBaseDamage(self,self,MOVE_POUND,0,40,0,self,self), then adjustnormaldamage2.
        // No STAB, type calculation, critical draw, PP consumption or contact effects.
        internal static int SourceConfusionDamage(WildBattleCombatant actor, int percent, bool tower = false, bool fuchsia = false, bool explosion = false, bool canonicalSource = false)
        {
            int attack = actor.Pokemon.Stats.Attack;
            if (canonicalSource && WildBattleEffective.Ability(actor) is "ABILITY_HUGE_POWER" or "ABILITY_PURE_POWER") attack *= 2;
            if (actor.AttackBadgeBoost) attack = attack * 110 / 100;
            if (canonicalSource) attack = SourceBattleHeldItems.PhysicalAttack(attack, actor.HeldItem);
            if (tower && actor.HeldItem == "ITEM_THICK_CLUB" && actor.Pokemon.SourceSpeciesId is 104 or 105) attack *= 2;
            if (canonicalSource && WildBattleEffective.Ability(actor) == "ABILITY_HUSTLE") attack = attack * 150 / 100;
            if (WildBattleEffective.Ability(actor) == "ABILITY_GUTS" && actor.Status != 0) attack = attack * 150 / 100;
            attack = Math.Max(1, WildBattleDamage.Stat(attack, actor.AttackStage));
            int defense = actor.Pokemon.Stats.Defense;
            if (fuchsia && actor.DefenseBadgeBoost) defense = defense * 110 / 100;
            if (fuchsia && actor.HeldItem == "ITEM_METAL_POWDER" && actor.Pokemon.SourceSpeciesId == 132) defense *= 2;
            // CalculateBaseDamage still reads gCurrentMove's effect during a Pound self-hit.
            if (fuchsia && explosion) defense = Math.Max(1, defense / 2);
            defense = Math.Max(1, WildBattleDamage.Stat(defense, actor.DefenseStage));
            int damage = checked(attack * 40 * (2 * actor.Pokemon.Level / 5 + 2)) / defense / 50;
            if (actor.Status == WildBattleStatus.Burn && WildBattleEffective.Ability(actor) != "ABILITY_GUTS") damage /= 2;
            return Math.Max(1, (Math.Max(1, damage) + 2) * percent / 100);
        }

}
