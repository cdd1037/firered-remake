using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Rules;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasGenericRecipe(string effect) => effect is
        "EFFECT_HIT" or "EFFECT_QUICK_ATTACK" or "EFFECT_HIGH_CRITICAL" or "EFFECT_ALWAYS_HIT" or
        "EFFECT_THAW_HIT" or "EFFECT_TRI_ATTACK" or "EFFECT_FREEZE_HIT" or "EFFECT_BURN_HIT" or "EFFECT_PARALYZE_HIT" or "EFFECT_POISON_HIT" or "EFFECT_CONFUSE_HIT" or
        "EFFECT_FLINCH_HIT" or "EFFECT_FLINCH_MINIMIZE_HIT" or "EFFECT_SPEED_DOWN_HIT" or
        "EFFECT_ACCURACY_DOWN_HIT" or "EFFECT_DEFENSE_DOWN_HIT" or "EFFECT_SPECIAL_DEFENSE_DOWN_HIT" or
        "EFFECT_ATTACK_UP_HIT" or "EFFECT_TRAP" or "EFFECT_ABSORB" or "EFFECT_MULTI_HIT" or "EFFECT_DOUBLE_HIT" or "EFFECT_TWINEEDLE" or
        "EFFECT_EXPLOSION" or "EFFECT_POISON" or "EFFECT_SLEEP" or "EFFECT_CONFUSE" or "EFFECT_ATTACK_UP" or
        "EFFECT_ATTACK_DOWN_2" or "EFFECT_SPEED_DOWN_2" or "EFFECT_SPECIAL_DEFENSE_DOWN_2" or
        "EFFECT_RESTORE_HP" or "EFFECT_MEAN_LOOK";

    internal static bool HasStatRecoveryRecipe(string effect) => effect is
        "EFFECT_DEFENSE_CURL" or "EFFECT_MINIMIZE" or "EFFECT_DEFENSE_UP_2" or "EFFECT_SPECIAL_ATTACK_UP_2" or "EFFECT_SPECIAL_DEFENSE_UP_2" or
        "EFFECT_HAZE" or "EFFECT_PSYCH_UP" or "EFFECT_SOFTBOILED" or "EFFECT_BELLY_DRUM" or
        "EFFECT_SWAGGER" or "EFFECT_FLATTER" or "EFFECT_MORNING_SUN" or "EFFECT_SYNTHESIS" or "EFFECT_MOONLIGHT" or
        "EFFECT_REFRESH" or "EFFECT_TICKLE" or "EFFECT_BULK_UP" or "EFFECT_CALM_MIND" or
        "EFFECT_DRAGON_DANCE" or "EFFECT_ATTACK_DOWN_HIT" or "EFFECT_DEFENSE_UP_HIT" or
        "EFFECT_ALL_STATS_UP_HIT" or "EFFECT_SPECIAL_ATTACK_DOWN_HIT" or "EFFECT_SUPERPOWER" or "EFFECT_OVERHEAT";


        internal void ExecuteGeneric(PokemonCatalog catalog, BattlerSlot side, int slot,
            PokemonMoveDefinition move, string purpose, ref bool handled)
        {
            if (move.Id == WildBattleRules.StruggleMove)
            {
                ExecuteStruggle(catalog, side, Invocation!.Target, slot, move, purpose); handled = true; return;
            }
            if (!HasGenericRecipe(move.Effect)) return;
            if (Invocation is null || !MetronomeSourceMoveDefinition.Matches(move))
                throw new SourceRecipeUnsupported("called_generic_source_definition");
            handled = true;
            var target = Invocation.Target;
            switch (move.Effect)
            {
                case "EFFECT_POISON":
                case "EFFECT_SLEEP": SourcePrimaryStatus(catalog, side, slot, move, purpose); return;
                case "EFFECT_CONFUSE": ExecuteVolatileTarget(side, target, slot, move, purpose); return;
                case "EFFECT_RESTORE_HP":
                    SpendEarlyPartyPp(side, side, slot, move);
                    SourceStatRecoveryHeal(side, move, Math.Max(1, Get(side).Pokemon.Stats.MaxHp / 2)); return;
                case "EFFECT_MEAN_LOOK": ExecuteVolatileTarget(side, target, slot, move, purpose); return;
                case "EFFECT_ATTACK_UP":
                    SpendEarlyPartyPp(side, side, slot, move);
                    ChangeStage(side, side, WildBattleStat.Attack, 1, move.Id); return;
                case "EFFECT_ATTACK_DOWN_2":
                case "EFFECT_SPEED_DOWN_2":
                case "EFFECT_SPECIAL_DEFENSE_DOWN_2":
                    // BattleScript_EffectStatDown runs accuracy before attackstring/ppreduce.
                    bool hit = SourceGenericAccuracy(side, target, move, purpose);
                    SpendEarlyPartyPp(side, target, slot, move);
                    if (!hit) { SourceGenericMiss(side, target, move); return; }
                    ChangeStage(side, target, move.Effect == "EFFECT_ATTACK_DOWN_2" ? WildBattleStat.Attack :
                        move.Effect == "EFFECT_SPEED_DOWN_2" ? WildBattleStat.Speed : WildBattleStat.SpecialDefense, -2, move.Id);
                    return;
                case "EFFECT_THAW_HIT": ExecuteThawHit(catalog, side, target, slot, move, purpose); return;
                case "EFFECT_TRI_ATTACK": ExecuteTriAttack(catalog, side, target, slot, move, purpose); return;
                case "EFFECT_FREEZE_HIT": ExecuteFreezeHit(catalog, side, target, slot, move, purpose); return;
                case "EFFECT_DOUBLE_HIT": SourceGenericMultiHit(catalog, side, target, slot, move, purpose, fixedTwoHits: true); return;
                case "EFFECT_TWINEEDLE": SourceGenericMultiHit(catalog, side, target, slot, move, purpose, fixedTwoHits: true, twineedle: true); return;
                case "EFFECT_MULTI_HIT": SourceGenericMultiHit(catalog, side, target, slot, move, purpose); return;
                case "EFFECT_EXPLOSION": SourceGenericExplosion(catalog, side, target, slot, move, purpose); return;
                default: SourceGenericHit(catalog, side, target, slot, move, purpose); return;
            }
        }

        internal bool SourceGenericAccuracy(BattlerSlot side, BattlerSlot target, PokemonMoveDefinition move, string purpose)
        {
            if (Get(target).Protected && move.Flags.Contains("FLAG_PROTECT_AFFECTED")) return false;
            if (CalledSureHit(side, target)) return true;
            if (move.Effect is "EFFECT_ALWAYS_HIT" or "EFFECT_VITAL_THROW") return !BillUnderground(side, target, move);
            return EarlyPartyAccuracy(side, target, move, purpose, false);
        }

        internal virtual void SourceGenericMiss(BattlerSlot side, BattlerSlot target, PokemonMoveDefinition move) =>
            Add(n => new SourceMoveMissedEvent(n, Turn, side, target, move.Id));

        internal bool SourceGenericCritical(WildBattleCombatant actor, WildBattleCombatant target, PokemonMoveDefinition move, string purpose)
        {
            // Cmd_critcalc, including the three effect-specific critical hooks and source stage 4.
            int stage = (actor.FocusEnergy ? 2 : 0) +
                (move.Effect is "EFFECT_HIGH_CRITICAL" or "EFFECT_SKY_ATTACK" or "EFFECT_BLAZE_KICK" or "EFFECT_POISON_TAIL" ? 1 : 0) +
                (actor.HeldItem == "ITEM_SCOPE_LENS" ? 1 : 0) +
                (actor.HeldItem == "ITEM_LUCKY_PUNCH" && actor.Pokemon.SourceSpeciesId == 113 ? 2 : 0) +
                (actor.HeldItem == "ITEM_STICK" && actor.Pokemon.SourceSpeciesId == 83 ? 2 : 0);
            return CeruleanCritical(actor, target, move.Effect, purpose, stage switch { 0 => 16, 1 => 8, 2 => 4, 3 => 3, _ => 2 });
        }

        internal int SourceGenericDamage(PokemonCatalog catalog, WildBattleCombatant actor, WildBattleCombatant target,
            BattlerSlot targetSide, PokemonMoveDefinition move, bool critical, int percent, int? dynamicPower = null, ElementType? effectiveType = null, int? damageMultiplier = null) =>
            ForestBattleDamage.Calculate(catalog, actor, target, move.Id, critical, percent,
                true, true, true, true, route3Rules: true, dynamicPower: dynamicPower, mtMoonRules: true, ceruleanRules: true, billRules: true,
                vermilionRules: true, ssAnneRules: true, vermilionGymRules: true, route11Rules: true,
                rain: SourceCalledRain, rockTunnelRules: true, reflect: RockTunnelReflects(targetSide),
                sourceDefinition: move, sourceEffectiveType: effectiveType, sun: SourceCalledSun, lightScreen: SourceCalledLightScreen(targetSide),
                sourceDamageMultiplier: damageMultiplier ?? SourceChargeRules.DamageMultiplier(SemiInvulnerable(targetSide),move),
                sourceSolarBeamWeakWeather: move.Effect == "EFFECT_SOLAR_BEAM" && (SourceCalledRain || StruggleSandstorm || SettingWeather.HailTurnsRemaining > 0),
                weatherSuppressed: SourceDamageWeatherSuppressed, sourceDoubleBattle: SourceDoubleBattle,
                sourceDefenderCount: SourceDefenderCount(targetSide), sourcePlusMinusBoost: SourcePlusMinusBoost(actor),
                sourceHelpingHand: Invocation is { } invocation && HelpingHand(invocation.Actor)).Damage;

        // The canonical owner emits result text here, after actual HP/critical commands.
        // Legacy singles retain their existing event timing through the empty default.
        internal virtual void SourceDamageResultMessage(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) { }

        internal void SourceGenericEffectiveness(PokemonCatalog catalog, BattlerSlot side, BattlerSlot targetSide,
            PokemonMoveDefinition move)
        {
            SourceDamageResultMessage(side, targetSide, move);
            var target = Get(targetSide);
            if (!target.Identified || move.Type is not ("TYPE_NORMAL" or "TYPE_FIGHTING"))
            {
                ReportPewterEffectiveness(catalog, side, targetSide, move.Id);
                return;
            }
            ElementType Type(string symbol) => Enum.Parse<ElementType>(symbol[5..], true);
            var species = WildBattleEffective.Types(catalog, target);
            WildBattleEffectiveness? result = null;
            foreach (var row in TypeRules.OrderedEffects(Type(move.Type), Type(species.Type1), Type(species.Type2)))
            {
                // Foresight removes the Ghost entry before result flags, just as typecalc does.
                if (row.Defense == ElementType.Ghost) continue;
                if (row.MultiplierTenths == 0) result = WildBattleEffectiveness.NoEffect;
                else if (result != WildBattleEffectiveness.NoEffect)
                    result = row.MultiplierTenths switch {
                        5 => result == WildBattleEffectiveness.SuperEffective ? null : WildBattleEffectiveness.NotVeryEffective,
                        20 => result == WildBattleEffectiveness.NotVeryEffective ? null : WildBattleEffectiveness.SuperEffective,
                        _ => result
                    };
            }
            if (result is { } effectiveness)
                Add(n => new SourceEffectivenessEvent(n, Turn, side, targetSide, move.Id, effectiveness));
        }

        internal int SourceGenericApplyDamage(BattlerSlot side, BattlerSlot targetSide, PokemonMoveDefinition move,
            int damage, bool critical, int percent)
            => SourceGenericApplyDamage(side, targetSide, move.Id, damage, critical, percent);

        internal void SourceGenericContact(PokemonCatalog catalog, BattlerSlot side, BattlerSlot targetSide,
            PokemonMoveDefinition move, string purpose, int damage)
        {
            FireHitThaw(side, targetSide, move);
            if (damage > 0 && SourceCalledHitReachedPokemon(targetSide) && WildBattleEffective.Ability(Get(targetSide)) == "ABILITY_STATIC" && Get(side).Pokemon.CurrentHp > 0 &&
                move.Flags.Contains("FLAG_MAKES_CONTACT") && Draw($"{purpose}.static", 3) % 3 == 0)
            {
                RecordAbility(targetSide, "ABILITY_STATIC");
                InflictStatus(catalog, side, WildBattleStatus.Paralysis, true);
            }
            Route3ContactAbility(catalog, side, targetSide, move, purpose, damage);
        }

        internal void SourceGenericSecondary(PokemonCatalog catalog, BattlerSlot side, BattlerSlot targetSide,
            PokemonMoveDefinition move, string purpose, int damage)
        {
            // Cmd_seteffectwithchance consumes this draw even for a zero effect byte or immunity.
            int roll = Draw($"{purpose}.secondary-effect", 100) % 100;
            int chance = move.SecondaryEffectChance * (WildBattleEffective.Ability(Get(side)) == "ABILITY_SERENE_GRACE" ? 2 : 1);
            if (roll > chance || damage == 0) return;
            switch (move.Effect)
            {
                case "EFFECT_BLAZE_KICK":
                case "EFFECT_BURN_HIT": InflictStatus(catalog, targetSide, WildBattleStatus.Burn, false); break;
                case "EFFECT_PARALYZE_HIT": InflictStatus(catalog, targetSide, WildBattleStatus.Paralysis, false); break;
                case "EFFECT_POISON_TAIL":
                case "EFFECT_POISON_HIT": InflictStatus(catalog, targetSide, WildBattleStatus.Poison, false); break;
                case "EFFECT_CONFUSE_HIT": ApplyConfusion(targetSide, purpose, true); break;
                case "EFFECT_FLINCH_HIT":
                case "EFFECT_FLINCH_MINIMIZE_HIT": ApplyFlinch(targetSide); break;
                case "EFFECT_SPEED_DOWN_HIT": ChangeStage(side, targetSide, WildBattleStat.Speed, -1, move.Id, true); break;
                case "EFFECT_ACCURACY_DOWN_HIT": ChangeStage(side, targetSide, WildBattleStat.Accuracy, -1, move.Id, true); break;
                case "EFFECT_DEFENSE_DOWN_HIT": ChangeStage(side, targetSide, WildBattleStat.Defense, -1, move.Id, true); break;
                case "EFFECT_SPECIAL_DEFENSE_DOWN_HIT": ChangeStage(side, targetSide, WildBattleStat.SpecialDefense, -1, move.Id, true); break;
                case "EFFECT_ATTACK_UP_HIT": ChangeStage(side, side, WildBattleStat.Attack, 1, move.Id, true); break;
                case "EFFECT_TRAP": ApplyBinding(targetSide, purpose, move.Id); break;
            }
        }

        internal void SourceGenericHit(PokemonCatalog catalog, BattlerSlot side, BattlerSlot targetSide, int slot,
            PokemonMoveDefinition move, string purpose)
        {
            bool hit = SourceGenericAccuracy(side, targetSide, move, purpose);
            SpendEarlyPartyPp(side, targetSide, slot, move);
            if (!hit) { SourceGenericMiss(side, targetSide, move); return; }
            if (Route11WaterAbsorb(side, targetSide, move) || SsAnneFlashFire(side, targetSide, move)) return;
            var actor = Get(side); var target = Get(targetSide);
            bool critical = SourceGenericCritical(actor, target, move, purpose);
            int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
            int damage = SourceGenericDamage(catalog, actor, target, targetSide, move, critical, percent);
            int effectiveDamage = damage;
            damage = AdjustHeldDamage(targetSide, move.Id, damage, purpose);
            int lost = SourceGenericApplyDamage(side, targetSide, move, damage, critical, percent);
            SourceGenericEffectiveness(catalog, side, targetSide, move);
            if (move.Effect == "EFFECT_ABSORB")
            {
                if (effectiveDamage > 0)
                {
                    bool ooze = WildBattleEffective.Ability(target) == "ABILITY_LIQUID_OOZE";
                    int before = Get(side).Pokemon.CurrentHp, drain = Math.Max(1, lost / 2);
                    int after = ooze ? Math.Max(0, before - drain) : Math.Min(Get(side).Pokemon.Stats.MaxHp, before + drain);
                    HitItemsCommandDamage(ooze ? drain : -drain);
                    if (ooze) RecordHitItemDamage(side, before - after, substitute: false, noEffect: false, physical: false);
                    if (ooze) { RecordAbility(targetSide, "ABILITY_LIQUID_OOZE"); Add(n => new SourceAbilityActivatedEvent(n, Turn, targetSide, "ABILITY_LIQUID_OOZE")); }
                    Set(side, Get(side) with { Pokemon = Get(side).Pokemon with { CurrentHp = after } });
                    Add(n => new SourceDrainEvent(n, Turn, side, targetSide, move.Id, lost, before, after, after - before));
                    if (after == 0) ForestFaint(side);
                }
            }
            else SourceGenericSecondary(catalog, side, targetSide, move, purpose, effectiveDamage);
            if (Get(targetSide).Pokemon.CurrentHp == 0) ForestFaint(targetSide);
            if (damage > 0) MtMoonRageOnDamage(side, targetSide, move.Id);
            SourceGenericContact(catalog, side, targetSide, move, purpose, damage);
            ForestOutcome(true);
        }

        internal void SourceGenericMultiHit(PokemonCatalog catalog, BattlerSlot side, BattlerSlot targetSide, int slot,
            PokemonMoveDefinition move, string purpose, bool fixedTwoHits = false, bool twineedle = false)
        {
            bool hit = SourceGenericAccuracy(side, targetSide, move, purpose);
            SpendEarlyPartyPp(side, targetSide, slot, move);
            if (!hit) { SourceGenericMiss(side, targetSide, move); return; }
            int planned = 2;
            if (!fixedTwoHits)
            {
                int roll = Draw($"{purpose}.multi-hit-count", 4) & 3;
                planned = roll > 1 ? (Draw($"{purpose}.multi-hit-count-extra", 4) & 3) + 2 : roll + 2;
            }
            int actual = 0, lastDamage = 0, lastEffectiveDamage = 0;
            for (int i = 0; i < planned; i++)
            {
                var actor = Get(side); var target = Get(targetSide);
                if (actor.Pokemon.CurrentHp == 0 || target.Pokemon.CurrentHp == 0 || WildBattleStatus.IsSleeping(actor.Status) &&
                    !(SourceHeldDamageEnabled && Invocation?.ChosenMove.Value == "pret:MOVE_SLEEP_TALK")) break;
                bool critical = SourceGenericCritical(actor, target, move, purpose);
                // MultiHit tests type immunity before adjustnormaldamage, which owns the damage draw.
                if (SourceGenericDamage(catalog, actor, target, targetSide, move, critical, 100) == 0) break;
                int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
                lastDamage = SourceGenericDamage(catalog, actor, target, targetSide, move, critical, percent);
                lastEffectiveDamage = lastDamage;
                lastDamage = AdjustHeldDamage(targetSide, move.Id, lastDamage, purpose);
                SourceGenericApplyDamage(side, targetSide, move, lastDamage, critical, percent);
                actual++;
                MtMoonRageOnDamage(side, targetSide, move.Id);
                SourceGenericContact(catalog, side, targetSide, move, purpose, lastDamage);
                HitItemsPerHitMoveEnd(catalog, side, targetSide, move);
                SourceMultiHitMoveEnd(catalog, side, targetSide, move);
                if (Endured(targetSide)) break;
            }
            SourceGenericEffectiveness(catalog, side, targetSide, move);
            Add(n => new SourceMultiHitEvent(n, Turn, side, targetSide, move.Id, planned, actual));
            if (twineedle)
            {
                // Preserve the user-approved Twineedle comparison from its prior source delegate.
                int roll = Draw($"{purpose}.secondary-effect", 100) % 100;
                if (lastEffectiveDamage > 0 && roll < move.SecondaryEffectChance)
                    InflictStatus(catalog, targetSide, WildBattleStatus.Poison, false);
            }
            else SourceGenericSecondary(catalog, side, targetSide, move, purpose, lastEffectiveDamage);
            if (Get(targetSide).Pokemon.CurrentHp == 0) ForestFaint(targetSide);
            ForestOutcome(true);
            SourceMultiHitFinalMoveEnd();
        }

        internal void SourceGenericExplosion(PokemonCatalog catalog, BattlerSlot side, BattlerSlot targetSide, int slot,
            PokemonMoveDefinition move, string purpose)
        {
            SpendEarlyPartyPp(side, targetSide, slot, move);
            if (Route11Damp(side, move)) return;
            int hp = Get(side).Pokemon.CurrentHp;
            Set(side, Get(side) with { Pokemon = Get(side).Pokemon with { CurrentHp = 0 } });
            Add(n => new SourceSelfDestructEvent(n, Turn, side, move.Id, hp));
            foreach (var target in SourceNumericTargets(side, targetSide))
            {
                BeginScriptTarget(target);
                var actor = Get(side); var defender = Get(target);
                bool critical = SourceGenericCritical(actor, defender, move, purpose);
                int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
                int damage = SourceGenericDamage(catalog, actor, defender, target, move, critical, percent);
                damage = AdjustHeldDamage(target, move.Id, damage, purpose);
                // Explosion computes damage and its random multiplier before accuracy.
                if (SourceGenericAccuracy(side, target, move, purpose))
                {
                    SourceGenericApplyDamage(side, target, move, damage, critical, percent);
                    SourceGenericEffectiveness(catalog, side, target, move);
                    if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
                    if (damage > 0) MtMoonRageOnDamage(side, target, move.Id);
                    SourceGenericContact(catalog, side, target, move, purpose, damage);
                }
                else SourceGenericMiss(side, target, move);
                CompleteScriptTarget();
            }
            ForestFaint(side);
            ForestOutcome(true);
        }
}
