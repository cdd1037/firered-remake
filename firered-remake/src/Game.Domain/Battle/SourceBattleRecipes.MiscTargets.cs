using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasMiscTargetRecipe(string effect) => effect is "EFFECT_PRESENT" or "EFFECT_SECRET_POWER" or "EFFECT_TEETER_DANCE";

    internal void ExecuteMiscTarget(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, int slot,
        PokemonMoveDefinition move, string purpose)
    {
        if (!HasMiscTargetRecipe(move.Effect) || !SourceMoveDefinitionRules.IsValid(move))
            throw new SourceRecipeUnsupported("source_misc_target_definition");
        if (move.Effect == "EFFECT_TEETER_DANCE") { ExecuteTeeterDance(catalog, actor, target, slot, move, purpose); return; }
        if (move.Effect == "EFFECT_SECRET_POWER")
        {
            // getsecretpowereffect is deterministic. The ordinary hit's one shared
            // seteffectwithchance command owns its RNG, Serene Grace and no-effect gate.
            if (SettingWeather.Terrain is null) throw new SourceRecipeUnsupported("source_secret_power_terrain");
            SourceGenericHit(catalog, actor, target, slot, move, purpose); return;
        }
        bool hit = FixedDamageAccuracy(actor, target, move, purpose);
        SpendEarlyPartyPp(actor, target, slot, move);
        if (!hit) { SourceGenericMiss(actor, target, move); return; }
        // Present runs typecalc before its random branch. Healing clears ordinary
        // DOESNT_AFFECT_FOE only; Wonder Guard's MISSED survives the HP command.
        bool guard = SourceTypeResult(catalog, target, move, apply: true).WonderGuard;
        int roll = Draw($"{purpose}.present", 256) & 255;
        if (roll >= 204)
        {
            var mon = Get(target); int before = mon.Pokemon.CurrentHp, max = mon.Pokemon.Stats.MaxHp;
            int amount = Math.Max(1, max / 4);
            HitItemsCommandDamage(-amount, guard || before == max && SourceTypeResult(catalog, target, move).Immune);
            if (before == max)
            {
                // AlreadyAtFullHp does not take the healing branch that clears
                // DOESNT_AFFECT_FOE; retain that flag through move-end history.
                if (SourceTypeResult(catalog, target, move).Immune) SourceTypeNoEffect(target, clearHitType: guard);
                SourceStatRecoveryFail(actor, target, move, "already-full-hp");
            }
            else
            {
                int after = guard ? before : Math.Min(max, before + amount);
                Set(target, mon with { Pokemon = mon.Pokemon with { CurrentHp = after } });
                Add(n => new SourceRecoveredEvent(n, Turn, target, move.Id, before, after, after - before));
            }
            return;
        }
        int power = roll < 102 ? 40 : roll < 178 ? 80 : 120;
        bool critical = SourceGenericCritical(Get(actor), Get(target), move, purpose);
        int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
        int raw = SourceGenericDamage(catalog, Get(actor), Get(target), target, move, critical, percent, power);
        int damage = FixedDamageAdjust(target, move, raw, purpose);
        if (SourceTypeResult(catalog, target, move).Immune) raw = damage = 0;
        int lost = PresentApplyDamage(catalog, actor, target, move, damage, critical, percent);
        PresentReportType(catalog, actor, target, move);
        SourceGenericSecondary(catalog, actor, target, move, purpose, raw);
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        if (lost > 0) MtMoonRageOnDamage(actor, target, move.Id);
        SourceGenericContact(catalog, actor, target, move, purpose, lost);
        ForestOutcome(true);
    }

    internal virtual int PresentApplyDamage(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        PokemonMoveDefinition move, int damage, bool critical, int percent)
    {
        int lost = SourceGenericApplyDamage(actor, target, move, damage, critical, percent);
        if (critical && lost == 0 && !SourceTypeResult(catalog, target, move).Immune && !FixedDamageZeroCriticalPrinted(target))
            Add(n => new SourceCriticalHitEvent(n, Turn, actor, target));
        return lost;
    }
    internal virtual void PresentReportType(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) =>
        SourceGenericEffectiveness(catalog, actor, target, move);

    internal void ApplySecretPowerSecondary(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target,
        PokemonMoveDefinition move, string purpose, bool certain)
    {
        var mon = Get(target);
        // Shared SetMoveEffect guards precede status-duration draws, including on
        // the breaking hit while the Substitute status bit still exists.
        if (mon.Pokemon.CurrentHp == 0 || SourceCalledSecondaryBlocked(target)) return;
        switch (SettingWeather.Terrain)
        {
            case WildBattleTerrain.Grass: InflictStatus(catalog, target, WildBattleStatus.Poison, false, certain: certain); break;
            case WildBattleTerrain.LongGrass:
                if (mon.Status == 0 && !Safeguard(target) && !UproarPreventsSleep(target) &&
                    WildBattleEffective.Ability(mon) is not ("ABILITY_SHIELD_DUST" or "ABILITY_INSOMNIA" or "ABILITY_VITAL_SPIRIT"))
                    ChangeStatus(target, (uint)((Draw($"{purpose}.sleep-duration", 4) & 3) + 2), "move");
                break;
            case WildBattleTerrain.Sand: ChangeStage(actor, target, WildBattleStat.Accuracy, -1, move.Id, true); break;
            case WildBattleTerrain.Underwater: ChangeStage(actor, target, WildBattleStat.Defense, -1, move.Id, true); break;
            case WildBattleTerrain.Water: ChangeStage(actor, target, WildBattleStat.Attack, -1, move.Id, true); break;
            case WildBattleTerrain.Pond: ChangeStage(actor, target, WildBattleStat.Speed, -1, move.Id, true); break;
            case WildBattleTerrain.Mountain: ApplyConfusion(target, purpose, true); break;
            case WildBattleTerrain.Cave: ApplySecondaryFlinch(target, certain); break;
            default: InflictStatus(catalog, target, WildBattleStatus.Paralysis, false, certain: certain); break;
        }
    }

    internal virtual void BeginTeeterDanceTarget(BattlerSlot target) => BeginScriptTarget(target);
    internal virtual IEnumerable<BattlerSlot> TeeterDanceSlots(BattlerSlot initialTarget) => ActiveSlots.OrderBy(s => s.Value);
    internal virtual void CompleteTeeterDanceTarget(PokemonCatalog catalog) => CompleteScriptTarget();
    private void ExecuteTeeterDance(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot initialTarget, int slot, PokemonMoveDefinition move, string purpose)
    {
        SpendEarlyPartyPp(actor, initialTarget, slot, move);
        // Teeter Dance owns a literal numeric loop, unlike the damaging spread
        // scripts' source-absence filter. Even the user reaches moveendto.
        foreach (var target in TeeterDanceSlots(initialTarget))
        {
            BeginTeeterDanceTarget(target);
            if (target != actor)
            {
                var mon = Get(target);
                if (WildBattleEffective.Ability(mon) == "ABILITY_OWN_TEMPO")
                {
                    RecordAbility(target, "ABILITY_OWN_TEMPO");
                    Add(n => new SourceAbilityPreventedEvent(n, Turn, actor, target, move.Id, "ABILITY_OWN_TEMPO"));
                }
                else if (mon.Pokemon.CurrentHp > 0 && SourceCalledSecondaryBlocked(target)) SourceStatRecoveryFail(actor, target, move, "substitute");
                else if (mon.Pokemon.CurrentHp > 0 && mon.ConfusionTurnsRemaining > 0) SourceStatRecoveryFail(actor, target, move, "already-confused");
                else if (!FixedDamageAccuracy(actor, target, move, purpose)) SourceGenericMiss(actor, target, move);
                // This script's Safeguard label rejoins its loop, not end2.
                else if (Safeguard(target)) SourceStatRecoveryFail(actor, target, move, "safeguard");
                else if (mon.Pokemon.CurrentHp > 0)
                {
                    int turns = 2 + (Draw($"{purpose}.confusion-duration", 4) & 3);
                    Set(target, mon with { ConfusionTurnsRemaining = turns });
                    Add(n => new SourceConfusionChangedEvent(n, Turn, target, mon.ConfusionTurnsRemaining, turns, "teeter-dance"));
                }
            }
            CompleteTeeterDanceTarget(catalog);
        }
    }
}
