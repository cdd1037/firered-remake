using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasSleepResidualRecipe(string effect) => effect is "EFFECT_NIGHTMARE" or "EFFECT_CURSE" or "EFFECT_YAWN";
    internal virtual bool SleepResidualNoAccuracy(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) =>
        CalledSureHit(actor, target) || SemiInvulnerable(target) == WildBattleSemiInvulnerable.None &&
            !(Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED"));
    internal virtual void SleepResidualPrepare(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) { }
    internal virtual void CurseStat(BattlerSlot actor, WildBattleStat stat, int amount, PokemonMoveDefinition move) =>
        SourceChangeStat(actor, actor, stat, amount, move.Id, certain: true);
    internal virtual void CurseCostReceipt(BattlerSlot actor, PokemonMoveDefinition move, int damage, int lost)
    {
        HitItemsCommandDamage(damage);
        RecordHitItemDamage(actor, lost, false, false, physical: false); // TYPE_MYSTERY is special in Gen III.
    }
    internal void SetSleepResidualFlag(BattlerSlot slot, bool nightmare, bool value, string reason)
    {
        var body = Get(slot);
        if ((nightmare ? body.Nightmare : body.Cursed) == value) return;
        Set(slot, nightmare ? body with { Nightmare = value } : body with { Cursed = value });
        Add(n => new SourceTowerVolatileEvent(n, Turn, slot, nightmare ? "nightmare" : "curse", value, reason));
    }
    internal void ExecuteSleepResidual(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        int slot, PokemonMoveDefinition move)
    {
        bool statCurse = move.Effect == "EFFECT_CURSE" && !WildBattleEffective.HasType(catalog, Get(actor), "TYPE_GHOST");
        bool disobedientStatCurse = statCurse && Invocation!.Flags.HasFlag(WildBattleInvocationFlags.DisobedientMove);
        var selectedTarget = target;
        if (statCurse) target = actor;
        // Disobedience calls raw GetMoveTarget. Non-Ghost Curse retargets only after
        // attackstring/ppreduce and its stat-limit guard; Pressure sees the original target.
        SleepResidualPrepare(actor, disobedientStatCurse ? selectedTarget : target, move);
        SpendEarlyPartyPp(actor, disobedientStatCurse ? selectedTarget : target, slot, move);
        // These branches end through BattleScript_ButItFailed/resultmessage. The
        // attackcanceler Protect miss has text priority, after script-specific checks.
        // Direct ability/Safeguard messages below deliberately bypass this projection.
        void Fail(string reason) => Add(n => new SourceMoveFailedEvent(n, Turn, actor, disobedientStatCurse ? Invocation!.Target : target, move.Id,
            Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED") ? "protected" : reason));
        var body = Get(target);
        if (statCurse)
        {
            if (body.SpeedStage == 0 && body.AttackStage == 12 && body.DefenseStage == 12) { Fail("stat-limits"); return; }
            if (disobedientStatCurse) SetPreparedInvocation(Invocation! with { Target = actor });
            CurseStat(actor, WildBattleStat.Speed, -1, move);
            CurseStat(actor, WildBattleStat.Attack, 1, move);
            CurseStat(actor, WildBattleStat.Defense, 1, move);
            return;
        }
        if (move.Effect == "EFFECT_YAWN" && WildBattleEffective.Ability(body) is "ABILITY_VITAL_SPIRIT" or "ABILITY_INSOMNIA")
        {
            string ability = WildBattleEffective.Ability(body); RecordAbility(target, ability);
            Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, target, move.Id, ability)); return;
        }
        if (SourceCalledSecondaryBlocked(target)) { Fail("substitute"); return; }
        if (move.Effect == "EFFECT_NIGHTMARE")
        {
            // No accuracycheck in the source script: Protect's miss marker does not
            // prevent SetMoveEffect(primary), nor does semi-invulnerability.
            if (body.Nightmare || !WildBattleStatus.IsSleeping(body.Status)) { Fail(body.Nightmare ? "already-nightmare" : "target-awake"); return; }
            SetSleepResidualFlag(target, true, true, "move"); return;
        }
        if (move.Effect == "EFFECT_YAWN" && SourceCalledSafeguardBlocks(actor, target, move.Id)) return;
        if (!SleepResidualNoAccuracy(actor, target, move))
        {
            Fail("semi-invulnerable");
            return;
        }
        if (move.Effect == "EFFECT_CURSE")
        {
            if (body.Cursed) { Fail("already-cursed"); return; }
            SetSleepResidualFlag(target, false, true, "move");
            var attacker = Get(actor); int before = attacker.Pokemon.CurrentHp, damage = Math.Max(1, attacker.Pokemon.Stats.MaxHp / 2), after = Math.Max(0, before - damage);
            CurseCostReceipt(actor, move, damage, before - after);
            Set(actor, attacker with { Pokemon = attacker.Pokemon with { CurrentHp = after } });
            Add(n => new SourceTowerHpEvent(n, Turn, actor, "curse-cost", before, after, damage));
            if (after == 0) ForestFaint(actor);
            ForestOutcome(true); return;
        }
        if (UproarPreventsSleep(target)) { Fail("uproar"); return; }
        if (body.YawnTurnsRemaining != 0 || body.Status != 0) { Fail(body.YawnTurnsRemaining != 0 ? "already-drowsy" : "existing-status"); return; }
        Set(target, body with { YawnTurnsRemaining = 2 });
        Add(n => new SourceYawnEvent(n, Turn, target, 0, 2, "move"));
    }
}
