using Game.Domain.Ids;
using Game.Domain.Pokemon;

namespace Game.Domain.WildBattle;

/// <summary>Cmd_recoverbasedonsunlight arithmetic, independent of weather storage.</summary>
public static class WildBattleSourceRecovery
{
    public static int SunlightAmount(int maximumHp, bool weatherPresent, bool sunny, bool weatherSuppressed)
    {
        if (maximumHp <= 0) throw new ArgumentOutOfRangeException(nameof(maximumHp));
        return Math.Max(1, !weatherPresent || weatherSuppressed ? maximumHp / 2 : sunny ? 20 * maximumHp / 30 : maximumHp / 4);
    }

    /// <summary>Cmd_maxattackhalvehp returns no HP debit on either failed condition.</summary>
    public static int BellyDrumCost(int maximumHp, int currentHp, int attackStage)
    {
        if (maximumHp <= 0 || currentHp < 0 || currentHp > maximumHp || attackStage is < 0 or > 12)
            throw new ArgumentOutOfRangeException(nameof(maximumHp));
        int half = Math.Max(1, maximumHp / 2);
        return attackStage < 12 && currentHp > half ? half : 0;
    }
}

public static partial class WildBattleRules
{
    private static bool HasStatRecoveryRecipe(string effect) => effect is
        "EFFECT_DEFENSE_UP_2" or "EFFECT_SPECIAL_ATTACK_UP_2" or "EFFECT_SPECIAL_DEFENSE_UP_2" or
        "EFFECT_HAZE" or "EFFECT_PSYCH_UP" or "EFFECT_SOFTBOILED" or "EFFECT_BELLY_DRUM" or
        "EFFECT_SWAGGER" or "EFFECT_FLATTER" or "EFFECT_MORNING_SUN" or "EFFECT_SYNTHESIS" or "EFFECT_MOONLIGHT" or
        "EFFECT_REFRESH" or "EFFECT_TICKLE" or "EFFECT_BULK_UP" or "EFFECT_CALM_MIND" or
        "EFFECT_DRAGON_DANCE" or "EFFECT_ATTACK_DOWN_HIT" or "EFFECT_DEFENSE_UP_HIT" or
        "EFFECT_ALL_STATS_UP_HIT" or "EFFECT_SPECIAL_ATTACK_DOWN_HIT" or "EFFECT_SUPERPOWER" or "EFFECT_OVERHEAT";

    static partial void AddCalledStatRecoverySupport(PokemonMoveDefinition move, ref bool supported)
    {
        if (MetronomeSourceMoveDefinition.Matches(move) && HasStatRecoveryRecipe(move.Effect)) supported = true;
    }

    internal sealed partial class Execution
    {
        private static readonly WildBattleStat[] SourceStatOrder = [WildBattleStat.Attack, WildBattleStat.Defense,
            WildBattleStat.Speed, WildBattleStat.SpecialAttack, WildBattleStat.SpecialDefense, WildBattleStat.Accuracy, WildBattleStat.Evasion];

        partial void ExecuteCalledStatRecoveryEffect(PokemonCatalog catalog, WildBattleSide side, int slot,
            PokemonMoveDefinition move, string purpose, ref bool handled)
        {
            // Swords Dance already has the authenticated Fuchsia v1 executor. Do not
            // duplicate or supersede that delegate merely because it is in batch B03.
            if (!HasStatRecoveryRecipe(move.Effect)) return;
            if (activeInvocation is null || !MetronomeSourceMoveDefinition.Matches(move))
                throw new UnsupportedAction("called_stat_recovery_source_definition");
            handled = true;
            var targetSide = activeInvocation.Target;
            if (move.Power > 0) { SourceStatRecoveryHit(catalog, side, targetSide, slot, move, purpose); return; }
            if (move.Effect is "EFFECT_SWAGGER" or "EFFECT_FLATTER")
            { SourceStatRecoveryConfuse(side, targetSide, slot, move, purpose); return; }

            SpendEarlyPartyPp(side, targetSide, slot, move);
            switch (move.Effect)
            {
                case "EFFECT_DEFENSE_UP_2": SourceChangeStat(side, side, WildBattleStat.Defense, 2, move.Id); return;
                case "EFFECT_SPECIAL_ATTACK_UP_2": SourceChangeStat(side, side, WildBattleStat.SpecialAttack, 2, move.Id); return;
                case "EFFECT_SPECIAL_DEFENSE_UP_2": SourceChangeStat(side, side, WildBattleStat.SpecialDefense, 2, move.Id); return;
                case "EFFECT_HAZE":
                    foreach (var battler in new[] { WildBattleSide.Player, WildBattleSide.Opponent })
                        foreach (var stat in SourceStatOrder) SourceAssignStat(side, battler, stat, 6);
                    Add(n => new WildBattleMtMoonVolatileEvent(n, State.Turn, side, "haze", 0, 0, "all-stat-stages-reset"));
                    return;
                case "EFFECT_PSYCH_UP":
                    var copied = Get(targetSide);
                    foreach (var stat in SourceStatOrder) SourceAssignStat(side, side, stat, SourceStage(copied, stat));
                    Add(n => new WildBattleMtMoonVolatileEvent(n, State.Turn, side, "psych-up", 0, 0, "all-stat-stages-copied"));
                    return;
                case "EFFECT_SOFTBOILED": SourceStatRecoveryHeal(targetSide, move, Math.Max(1, Get(targetSide).Pokemon.Stats.MaxHp / 2)); return;
                case "EFFECT_MORNING_SUN":
                case "EFFECT_SYNTHESIS":
                case "EFFECT_MOONLIGHT":
                    SourceStatRecoveryHeal(side, move, WildBattleSourceRecovery.SunlightAmount(Get(side).Pokemon.Stats.MaxHp,
                        SourceCalledWeatherPresent, SourceCalledSun, SourceCalledWeatherSuppressed));
                    return;
                case "EFFECT_BELLY_DRUM":
                    var actor = Get(side);
                    int half = WildBattleSourceRecovery.BellyDrumCost(actor.Pokemon.Stats.MaxHp, actor.Pokemon.CurrentHp, actor.AttackStage);
                    if (half == 0)
                    { SourceStatRecoveryFail(side, side, move, "belly-drum-condition"); return; }
                    SourceAssignStat(side, side, WildBattleStat.Attack, 12);
                    int after = actor.Pokemon.CurrentHp - half;
                    Set(side, Get(side) with { Pokemon = Get(side).Pokemon with { CurrentHp = after } });
                    Add(n => new WildBattleHpChangedEvent(n, State.Turn, side, side, move.Id, actor.Pokemon.CurrentHp, after, half, false, 100));
                    return;
                case "EFFECT_REFRESH":
                    if (Get(side).Status is WildBattleStatus.Poison or WildBattleStatus.Burn or WildBattleStatus.Paralysis ||
                        WildBattleSourceFieldEffects.IsToxic(Get(side).Status))
                        ChangeStatus(side, WildBattleStatus.None, "refresh");
                    else SourceStatRecoveryFail(side, side, move, "no-refreshable-status");
                    return;
                case "EFFECT_TICKLE":
                    if (Get(targetSide).AttackStage == 0 && Get(targetSide).DefenseStage == 0)
                    { SourceStatRecoveryFail(side, targetSide, move, "stats-at-minimum"); return; }
                    // Tickle's cap test and PP command precede accuracy, unlike Swagger.
                    if (!SourceGenericAccuracy(side, targetSide, move, purpose))
                    { SourceStatRecoveryFail(side, targetSide, move, "accuracy"); return; }
                    bool shown = false;
                    SourceChangeStat(side, targetSide, WildBattleStat.Attack, -1, move.Id, ref shown);
                    SourceChangeStat(side, targetSide, WildBattleStat.Defense, -1, move.Id, ref shown);
                    return;
                case "EFFECT_BULK_UP": SourceStatRecoveryPair(side, move, WildBattleStat.Attack, WildBattleStat.Defense); return;
                case "EFFECT_CALM_MIND": SourceStatRecoveryPair(side, move, WildBattleStat.SpecialAttack, WildBattleStat.SpecialDefense); return;
                case "EFFECT_DRAGON_DANCE": SourceStatRecoveryPair(side, move, WildBattleStat.Attack, WildBattleStat.Speed); return;
            }
        }

        private void SourceStatRecoveryFail(WildBattleSide side, WildBattleSide target, PokemonMoveDefinition move, string reason) =>
            Add(n => new WildBattleMoveFailedEvent(n, State.Turn, side, target, move.Id, reason));

        private void SourceStatRecoveryHeal(WildBattleSide side, PokemonMoveDefinition move, int amount)
        {
            var actor = Get(side); int before = actor.Pokemon.CurrentHp, after = Math.Min(actor.Pokemon.Stats.MaxHp, before + amount);
            if (before == after) { SourceStatRecoveryFail(side, side, move, "already-full-hp"); return; }
            Set(side, actor with { Pokemon = actor.Pokemon with { CurrentHp = after } });
            Add(n => new WildBattleRecoveredEvent(n, State.Turn, side, move.Id, before, after, after - before));
        }

        private void SourceStatRecoveryPair(WildBattleSide side, PokemonMoveDefinition move, WildBattleStat first, WildBattleStat second)
        {
            if (SourceStage(Get(side), first) == 12 && SourceStage(Get(side), second) == 12)
            { SourceStatRecoveryFail(side, side, move, "stats-at-maximum"); return; }
            SourceChangeStat(side, side, first, 1, move.Id);
            SourceChangeStat(side, side, second, 1, move.Id);
        }

        private void SourceStatRecoveryConfuse(WildBattleSide side, WildBattleSide targetSide, int slot,
            PokemonMoveDefinition move, string purpose)
        {
            bool hit = SourceGenericAccuracy(side, targetSide, move, purpose);
            SpendEarlyPartyPp(side, targetSide, slot, move);
            if (!hit) { SourceGenericMiss(side, targetSide, move); return; }
            var stat = move.Effect == "EFFECT_SWAGGER" ? WildBattleStat.Attack : WildBattleStat.SpecialAttack;
            if (SourceStage(Get(targetSide), stat) == 12 && Get(targetSide).ConfusionTurnsRemaining != 0)
            { SourceStatRecoveryFail(side, targetSide, move, "confused-and-stat-maxed"); return; }
            SourceChangeStat(side, targetSide, stat, move.Effect == "EFFECT_SWAGGER" ? 2 : 1, move.Id);
            if (Get(targetSide).Pokemon.AbilityId == "ABILITY_OWN_TEMPO")
            {
                RecordAbility?.Invoke(targetSide, "ABILITY_OWN_TEMPO");
                Add(n => new WildBattleAbilityPreventedEvent(n, State.Turn, side, targetSide, move.Id, "ABILITY_OWN_TEMPO"));
                return;
            }
            if (SourceCalledSafeguardBlocks(side, targetSide, move.Id)) return;
            if (Get(targetSide).ConfusionTurnsRemaining != 0)
            { SourceStatRecoveryFail(side, targetSide, move, "already-confused"); return; }
            ApplyConfusion(targetSide, purpose, false);
        }

        private void SourceStatRecoveryHit(PokemonCatalog catalog, WildBattleSide side, WildBattleSide targetSide, int slot,
            PokemonMoveDefinition move, string purpose)
        {
            bool hit = SourceGenericAccuracy(side, targetSide, move, purpose);
            SpendEarlyPartyPp(side, targetSide, slot, move);
            if (!hit) { SourceGenericMiss(side, targetSide, move); return; }
            if (Route11WaterAbsorb(side, targetSide, move) || SsAnneFlashFire(side, targetSide, move)) return;
            bool critical = SourceGenericCritical(Get(side), Get(targetSide), move, purpose);
            int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
            int damage = SourceGenericDamage(catalog, Get(side), Get(targetSide), targetSide, move, critical, percent);
            SourceGenericApplyDamage(side, targetSide, move, damage, critical, percent);
            SourceGenericEffectiveness(catalog, side, targetSide, move);
            bool certain = move.Effect is "EFFECT_SUPERPOWER" or "EFFECT_OVERHEAT";
            // The certain branch short-circuits Random only when MOVE_RESULT_NO_EFFECT
            // is clear. An immune hit still executes Random in the following else-if.
            bool apply = certain && damage > 0;
            if (!apply)
            {
                int chance = move.SecondaryEffectChance * (Get(side).Pokemon.AbilityId == "ABILITY_SERENE_GRACE" ? 2 : 1);
                apply = Draw($"{purpose}.secondary-effect", 100) % 100 <= chance && damage > 0;
            }
            if (apply)
            {
                switch (move.Effect)
                {
                    case "EFFECT_ATTACK_DOWN_HIT": SourceChangeStat(side, targetSide, WildBattleStat.Attack, -1, move.Id, secondary: true); break;
                    case "EFFECT_SPECIAL_ATTACK_DOWN_HIT": SourceChangeStat(side, targetSide, WildBattleStat.SpecialAttack, -1, move.Id, secondary: true); break;
                    case "EFFECT_DEFENSE_UP_HIT": SourceChangeStat(side, side, WildBattleStat.Defense, 1, move.Id, secondary: true); break;
                    case "EFFECT_ALL_STATS_UP_HIT":
                        // This secondary invokes a separate ALLOW_PTR script. It first
                        // skips an entirely capped user, then visits all five stats in
                        // source order, including the cap message for an individual stat.
                        if (Get(side).Pokemon.CurrentHp > 0 && SourceStatOrder.Take(5).Any(stat => SourceStage(Get(side), stat) < 12))
                            foreach (var stat in SourceStatOrder.Take(5)) SourceChangeStat(side, side, stat, 1, move.Id);
                        break;
                    case "EFFECT_SUPERPOWER":
                        SourceChangeStat(side, side, WildBattleStat.Attack, -1, move.Id, certain: true);
                        SourceChangeStat(side, side, WildBattleStat.Defense, -1, move.Id, certain: true);
                        break;
                    case "EFFECT_OVERHEAT": SourceChangeStat(side, side, WildBattleStat.SpecialAttack, -2, move.Id, certain: true); break;
                }
            }
            if (Get(targetSide).Pokemon.CurrentHp == 0) ForestFaint(targetSide);
            if (damage > 0) MtMoonRageOnDamage(side, targetSide, move.Id);
            SourceGenericContact(catalog, side, targetSide, move, purpose, damage);
            ForestOutcome(true);
        }

        private static int SourceStage(WildBattleCombatant mon, WildBattleStat stat) => stat switch
        {
            WildBattleStat.Attack => mon.AttackStage, WildBattleStat.Defense => mon.DefenseStage,
            WildBattleStat.Speed => mon.SpeedStage, WildBattleStat.SpecialAttack => 6 + mon.SpecialAttackStageDelta,
            WildBattleStat.SpecialDefense => 6 + mon.SpecialDefenseStageDelta,
            WildBattleStat.Accuracy => mon.AccuracyStage, _ => mon.EvasionStage
        };

        private void SourceAssignStat(WildBattleSide side, WildBattleSide targetSide, WildBattleStat stat, int after)
        {
            var mon = Get(targetSide); int before = SourceStage(mon, stat);
            if (before == after) return;
            Set(targetSide, stat switch {
                WildBattleStat.Attack => mon with { AttackStage = after }, WildBattleStat.Defense => mon with { DefenseStage = after },
                WildBattleStat.Speed => mon with { SpeedStage = after }, WildBattleStat.SpecialAttack => mon with { SpecialAttackStageDelta = after - 6 },
                WildBattleStat.SpecialDefense => mon with { SpecialDefenseStageDelta = after - 6 },
                WildBattleStat.Accuracy => mon with { AccuracyStage = after }, _ => mon with { EvasionStage = after }
            });
            Add(n => new WildBattleStatChangedEvent(n, State.Turn, side, targetSide, stat, before, after));
        }

        private void SourceChangeStat(WildBattleSide side, WildBattleSide target, WildBattleStat stat, int amount, MoveId move,
            bool secondary = false, bool certain = false)
        { bool shown = false; SourceChangeStat(side, target, stat, amount, move, ref shown, secondary, certain); }

        private void SourceChangeStat(WildBattleSide side, WildBattleSide target, WildBattleStat stat, int amount, MoveId move,
            ref bool preventionShown, bool secondary = false, bool certain = false)
        {
            var mon = Get(target);
            if (secondary && mon.Pokemon.CurrentHp == 0) return;
            if (amount < 0 && !certain && move.Value != "pret:MOVE_CURSE" && SourceCalledMist(target))
            {
                if (!secondary && !preventionShown)
                {
                    SourceCalledProtectionFailure(side, target, move, "mist");
                    preventionShown = true;
                }
                return;
            }
            string ability = mon.Pokemon.AbilityId;
            if (amount < 0 && !certain && (ability is "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE" ||
                ability == "ABILITY_KEEN_EYE" && stat == WildBattleStat.Accuracy ||
                ability == "ABILITY_HYPER_CUTTER" && stat == WildBattleStat.Attack))
            {
                if (!secondary && !preventionShown)
                {
                    RecordAbility?.Invoke(target, ability);
                    Add(n => new WildBattleAbilityPreventedEvent(n, State.Turn, side, target, move, ability));
                    // Clear Body and White Smoke report once for a multiple-stat script.
                    if (ability is "ABILITY_CLEAR_BODY" or "ABILITY_WHITE_SMOKE") preventionShown = true;
                }
                return;
            }
            if (amount < 0 && secondary && ability == "ABILITY_SHIELD_DUST") return;
            int before = SourceStage(mon, stat), after = Math.Clamp(before + amount, 0, 12);
            if (before == after)
            {
                if (!secondary)
                {
                    if (amount > 0) Add(n => new WildBattleStatAtMaximumEvent(n, State.Turn, side, target, stat));
                    else Add(n => new WildBattleStatAtMinimumEvent(n, State.Turn, side, target, stat));
                }
                return;
            }
            SourceAssignStat(side, target, stat, after);
        }
    }
}
