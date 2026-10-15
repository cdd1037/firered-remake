using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{

        internal static readonly WildBattleStat[] SourceStatOrder = [WildBattleStat.Attack, WildBattleStat.Defense,
            WildBattleStat.Speed, WildBattleStat.SpecialAttack, WildBattleStat.SpecialDefense, WildBattleStat.Accuracy, WildBattleStat.Evasion];

        internal void ExecuteStatRecovery(PokemonCatalog catalog, BattlerSlot side, int slot,
            PokemonMoveDefinition move, string purpose, ref bool handled)
        {
            // Swords Dance is owned by the basic-stat recipe; the historical Fuchsia
            // delegate is a thin adapter to that same result script.
            if (!HasStatRecoveryRecipe(move.Effect)) return;
            if (Invocation is null || !SourceMoveDefinitionRules.IsValid(move))
                throw new SourceRecipeUnsupported("called_stat_recovery_source_definition");
            handled = true;
            var targetSide = Invocation.Target;
            if (move.Power > 0) { SourceStatRecoveryHit(catalog, side, targetSide, slot, move, purpose); return; }
            if (move.Effect is "EFFECT_SWAGGER" or "EFFECT_FLATTER")
            { SourceStatRecoveryConfuse(side, targetSide, slot, move, purpose); return; }

            if (move.Effect == "EFFECT_MINIMIZE") ApplyMinimize(side);
            SpendEarlyPartyPp(side, targetSide, slot, move);
            switch (move.Effect)
            {
                case "EFFECT_MINIMIZE": ChangeStage(side, side, WildBattleStat.Evasion, 1, move.Id); return;
                case "EFFECT_DEFENSE_CURL": ApplyDefenseCurl(side); ChangeStage(side, side, WildBattleStat.Defense, 1, move.Id); return;
                case "EFFECT_DEFENSE_UP_2": SourceChangeStat(side, side, WildBattleStat.Defense, 2, move.Id); return;
                case "EFFECT_SPECIAL_ATTACK_UP_2": SourceChangeStat(side, side, WildBattleStat.SpecialAttack, 2, move.Id); return;
                case "EFFECT_SPECIAL_DEFENSE_UP_2": SourceChangeStat(side, side, WildBattleStat.SpecialDefense, 2, move.Id); return;
                case "EFFECT_HAZE":
                    foreach (var battler in ActiveSlots)
                        foreach (var stat in SourceStatOrder) SourceAssignStat(side, battler, stat, 6);
                    Add(n => new SourceMtMoonVolatileEvent(n, Turn, side, "haze", 0, 0, "all-stat-stages-reset"));
                    return;
                case "EFFECT_PSYCH_UP":
                    var copied = Get(targetSide);
                    foreach (var stat in SourceStatOrder) SourceAssignStat(side, side, stat, SourceStage(copied, stat));
                    Add(n => new SourceMtMoonVolatileEvent(n, Turn, side, "psych-up", 0, 0, "all-stat-stages-copied"));
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
                    HitItemsCommandDamage(half);
                    RecordHitItemDamage(side, half, substitute: false, noEffect: false, physical: true);
                    int after = actor.Pokemon.CurrentHp - half;
                    Set(side, Get(side) with { Pokemon = Get(side).Pokemon with { CurrentHp = after } });
                    Add(n => new SourceHpChangedEvent(n, Turn, side, side, move.Id, actor.Pokemon.CurrentHp, after, half, false, 100));
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

        internal void SourceStatRecoveryFail(BattlerSlot side, BattlerSlot target, PokemonMoveDefinition move, string reason) =>
            Add(n => new SourceMoveFailedEvent(n, Turn, side, target, move.Id, reason));

        internal void SourceStatRecoveryHeal(BattlerSlot side, PokemonMoveDefinition move, int amount)
        {
            var actor = Get(side); int before = actor.Pokemon.CurrentHp, after = Math.Min(actor.Pokemon.Stats.MaxHp, before + amount);
            if (before != after || move.Effect is "EFFECT_RESTORE_HP" or "EFFECT_SOFTBOILED") HitItemsCommandDamage(-amount);
            if (before == after) { SourceStatRecoveryFail(side, side, move, "already-full-hp"); return; }
            Set(side, actor with { Pokemon = actor.Pokemon with { CurrentHp = after } });
            Add(n => new SourceRecoveredEvent(n, Turn, side, move.Id, before, after, after - before));
        }

        internal void SourceStatRecoveryPair(BattlerSlot side, PokemonMoveDefinition move, WildBattleStat first, WildBattleStat second)
        {
            if (SourceStage(Get(side), first) == 12 && SourceStage(Get(side), second) == 12)
            { SourceStatRecoveryFail(side, side, move, "stats-at-maximum"); return; }
            SourceChangeStat(side, side, first, 1, move.Id);
            SourceChangeStat(side, side, second, 1, move.Id);
        }

        internal void SourceStatRecoveryConfuse(BattlerSlot side, BattlerSlot targetSide, int slot,
            PokemonMoveDefinition move, string purpose)
        {
            bool hit = SourceGenericAccuracy(side, targetSide, move, purpose);
            SpendEarlyPartyPp(side, targetSide, slot, move);
            if (!hit) { SourceGenericMiss(side, targetSide, move); return; }
            var stat = move.Effect == "EFFECT_SWAGGER" ? WildBattleStat.Attack : WildBattleStat.SpecialAttack;
            if (SourceStage(Get(targetSide), stat) == 12 && Get(targetSide).ConfusionTurnsRemaining != 0)
            { SourceStatRecoveryFail(side, targetSide, move, "confused-and-stat-maxed"); return; }
            SourceChangeStat(side, targetSide, stat, move.Effect == "EFFECT_SWAGGER" ? 2 : 1, move.Id);
            if (WildBattleEffective.Ability(Get(targetSide)) == "ABILITY_OWN_TEMPO")
            {
                RecordAbility(targetSide, "ABILITY_OWN_TEMPO");
                Add(n => new SourceAbilityPreventedEvent(n, Turn, side, targetSide, move.Id, "ABILITY_OWN_TEMPO"));
                return;
            }
            if (SourceCalledSafeguardBlocks(side, targetSide, move.Id)) return;
            if (Get(targetSide).ConfusionTurnsRemaining != 0)
            { SourceStatRecoveryFail(side, targetSide, move, "already-confused"); return; }
            ApplyConfusion(targetSide, purpose, false);
        }

        internal void SourceStatRecoveryHit(PokemonCatalog catalog, BattlerSlot side, BattlerSlot targetSide, int slot,
            PokemonMoveDefinition move, string purpose)
        {
            bool hit = SourceGenericAccuracy(side, targetSide, move, purpose);
            SpendEarlyPartyPp(side, targetSide, slot, move);
            if (!hit) { SourceGenericMiss(side, targetSide, move); return; }
            if (Route11WaterAbsorb(side, targetSide, move) || SsAnneFlashFire(side, targetSide, move)) return;
            bool critical = SourceGenericCritical(Get(side), Get(targetSide), move, purpose);
            int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
            int damage = SourceGenericDamage(catalog, Get(side), Get(targetSide), targetSide, move, critical, percent);
            int effectiveDamage = damage;
            damage = AdjustHeldDamage(targetSide, move.Id, damage, purpose);
            SourceGenericApplyDamage(side, targetSide, move, damage, critical, percent);
            SourceGenericEffectiveness(catalog, side, targetSide, move);
            bool certain = move.Effect is "EFFECT_SUPERPOWER" or "EFFECT_OVERHEAT";
            var secondary = SecondaryEffect(side, move, purpose, effectiveDamage == 0, certain: certain);
            if (secondary.Apply)
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

}
