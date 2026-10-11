using System.Collections.Immutable;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed record SourceHeldEffect(WildBattleCombatant Body, bool Activated = false, bool Consume = false,
    bool CureStatus = false, bool CureConfusion = false, bool CureInfatuation = false,
    SourceItemPpRecovery? PpRecovery = null, ImmutableArray<SourceItemStatChange> StatChanges = default, string? Flavor = null);

internal static partial class SourceHeldBerryRules
{
    internal static bool IsConsumableEffect(string effect) => effect is
        "HOLD_EFFECT_RESTORE_HP" or "HOLD_EFFECT_RESTORE_PP" or "HOLD_EFFECT_RESTORE_STATS" or
        "HOLD_EFFECT_CURE_PAR" or "HOLD_EFFECT_CURE_SLP" or "HOLD_EFFECT_CURE_PSN" or "HOLD_EFFECT_CURE_BRN" or
        "HOLD_EFFECT_CURE_FRZ" or "HOLD_EFFECT_CURE_CONFUSION" or "HOLD_EFFECT_CURE_STATUS" or "HOLD_EFFECT_CURE_ATTRACT" or
        "HOLD_EFFECT_CONFUSE_SPICY" or "HOLD_EFFECT_CONFUSE_DRY" or "HOLD_EFFECT_CONFUSE_SWEET" or
        "HOLD_EFFECT_CONFUSE_BITTER" or "HOLD_EFFECT_CONFUSE_SOUR" or "HOLD_EFFECT_ATTACK_UP" or
        "HOLD_EFFECT_DEFENSE_UP" or "HOLD_EFFECT_SPEED_UP" or "HOLD_EFFECT_SP_ATTACK_UP" or
        "HOLD_EFFECT_SP_DEFENSE_UP" or "HOLD_EFFECT_CRITICAL_UP" or "HOLD_EFFECT_RANDOM_STAT_UP";
    internal static readonly WildBattleStat[] SourceStats = [WildBattleStat.Attack, WildBattleStat.Defense,
        WildBattleStat.Speed, WildBattleStat.SpecialAttack, WildBattleStat.SpecialDefense, WildBattleStat.Accuracy, WildBattleStat.Evasion];
    internal static WildBattleCombatant SetStage(WildBattleCombatant body, WildBattleStat stat, int stage) => stat switch {
        WildBattleStat.Attack => body with { AttackStage = stage }, WildBattleStat.Defense => body with { DefenseStage = stage },
        WildBattleStat.Speed => body with { SpeedStage = stage }, WildBattleStat.SpecialAttack => body with { SpecialAttackStageDelta = stage - 6 },
        WildBattleStat.SpecialDefense => body with { SpecialDefenseStageDelta = stage - 6 },
        WildBattleStat.Accuracy => body with { AccuracyStage = stage }, _ => body with { EvasionStage = stage } };

    // ItemBattleEffects owns the phase gates. Data remains in the authenticated C15 item pack.
    internal static SourceHeldEffect ResolveCanonical(PokemonCatalog? catalog, WildBattleCombatant mon,
        SourceBerryPhase phase, bool infatuated, Func<string, int, ushort> draw, string purpose)
    {
        if (phase is SourceBerryPhase.Normal or SourceBerryPhase.NormalMoveTurn && mon.Pokemon.CurrentHp == 0) return new(mon);
        var definition = SourceHeldItemCatalog.Find(catalog, mon.HeldItem);
        if (definition is null)
        {
            var old = phase == SourceBerryPhase.SwitchIn ? default : Resolve(mon, phase);
            return Finish(mon, old.CureStatus, old.CureConfusion, false, old.HpRestored, old.Consumed);
        }
        if (mon.HeldItem == "ITEM_ENIGMA_BERRY") throw new SourceRecipeUnsupported("source_held_enigma_dynamic_input_required");
        string effect = definition.HoldEffect; int parameter = definition.HoldEffectParam;
        var after = mon; var changes = ImmutableArray.CreateBuilder<SourceItemStatChange>();
        void Stage(WildBattleStat stat, int value)
        {
            int before = SourceStatRecipeContext.SourceStage(after, stat);
            if (before == value) return;
            changes.Add(new(stat, before, value)); after = SetStage(after, stat, value);
        }
        // Amulet Coin updates the battle-local money latch in the storage owner; this body recipe owns White Herb.
        if (effect == "HOLD_EFFECT_RESTORE_STATS")
        {
            foreach (var stat in SourceStats)
                if (SourceStatRecipeContext.SourceStage(after, stat) < 6) Stage(stat, 6);
            return changes.Count == 0 ? new(mon) : new(after with { HeldItem = "ITEM_NONE" }, true, true, StatChanges: changes.ToImmutable());
        }
        if (phase == SourceBerryPhase.SwitchIn) return new(mon);
        bool cure = effect switch {
            "HOLD_EFFECT_CURE_PAR" => mon.Status == WildBattleStatus.Paralysis,
            "HOLD_EFFECT_CURE_SLP" => WildBattleStatus.IsSleeping(mon.Status),
            "HOLD_EFFECT_CURE_PSN" => mon.Status == WildBattleStatus.Poison || WildBattleSourceFieldEffects.IsToxic(mon.Status),
            "HOLD_EFFECT_CURE_BRN" => mon.Status == WildBattleStatus.Burn,
            "HOLD_EFFECT_CURE_FRZ" => mon.Status == WildBattleStatus.Freeze,
            "HOLD_EFFECT_CURE_STATUS" => mon.Status != 0, _ => false };
        bool confusion = effect is "HOLD_EFFECT_CURE_CONFUSION" or "HOLD_EFFECT_CURE_STATUS" && mon.ConfusionTurnsRemaining != 0;
        bool attract = effect == "HOLD_EFFECT_CURE_ATTRACT" && infatuated;
        if (cure || confusion || attract) return Finish(mon, cure, confusion, attract, 0, true,
            writeStatus: effect == "HOLD_EFFECT_CURE_STATUS" || phase == SourceBerryPhase.MoveEnd);
        if (phase != SourceBerryPhase.Normal) return new(mon);
        int hp = mon.Pokemon.CurrentHp, max = mon.Pokemon.Stats.MaxHp;
        if (effect == "HOLD_EFFECT_RESTORE_HP" && hp <= max / 2)
            return Finish(mon, false, false, false, Math.Min(parameter, max - hp), true);
        if (effect == "HOLD_EFFECT_LEFTOVERS" && hp < max)
            return Finish(mon, false, false, false, Math.Min(Math.Max(1, max / 16), max - hp), false);
        if (effect == "HOLD_EFFECT_RESTORE_PP")
        {
            // Source scans PARTY moves, not copied battle moves, and restores the first zero slot.
            var persistent = WildBattleTransformationRules.PersistentPokemon(mon);
            for (int i = 0; i < persistent.Moves.Length; i++)
            {
                var slot = persistent.Moves[i];
                if (slot.CurrentPp != 0 || slot.Move.Value == "pret:MOVE_NONE") continue;
                if (catalog is null || !catalog.Moves.TryGetValue(slot.Move, out var move)) throw new SourceRecipeUnsupported("source_held_leppa_move_catalog");
                int maximum = move.Pp + move.Pp * 20 * slot.PpUps / 100;
                int pp = Math.Min(parameter, maximum);
                persistent = persistent with { Moves = persistent.Moves.SetItem(i, slot with { CurrentPp = pp }) };
                after = WildBattleTransformationRules.ApplyPersistentPokemon(mon, persistent) with { HeldItem = "ITEM_NONE" };
                return new(after, true, true, PpRecovery: new(slot.Move, i, 0, pp));
            }
            return new(mon);
        }
        string? flavor = effect switch {
            "HOLD_EFFECT_CONFUSE_SPICY" => "spicy", "HOLD_EFFECT_CONFUSE_DRY" => "dry", "HOLD_EFFECT_CONFUSE_SWEET" => "sweet",
            "HOLD_EFFECT_CONFUSE_BITTER" => "bitter", "HOLD_EFFECT_CONFUSE_SOUR" => "sour", _ => null };
        if (flavor is not null && hp <= max / 2)
        {
            int heal = Math.Min(Math.Max(1, max / parameter), max - hp);
            after = mon with { Pokemon = mon.Pokemon with { CurrentHp = hp + heal }, HeldItem = "ITEM_NONE" };
            bool disliked = DislikesFlavor(mon.Pokemon.Personality, flavor);
            if (disliked && mon.ConfusionTurnsRemaining == 0 && WildBattleEffective.Ability(mon) != "ABILITY_OWN_TEMPO")
                after = after with { ConfusionTurnsRemaining = draw(purpose + ".flavor-confusion", 4) % 4 + 2 };
            return new(after, true, true, Flavor: disliked ? flavor : null);
        }
        if (parameter <= 0 || hp > max / parameter) return new(mon);
        WildBattleStat? raised = effect switch {
            "HOLD_EFFECT_ATTACK_UP" => WildBattleStat.Attack, "HOLD_EFFECT_DEFENSE_UP" => WildBattleStat.Defense,
            "HOLD_EFFECT_SPEED_UP" => WildBattleStat.Speed, "HOLD_EFFECT_SP_ATTACK_UP" => WildBattleStat.SpecialAttack,
            "HOLD_EFFECT_SP_DEFENSE_UP" => WildBattleStat.SpecialDefense, _ => null };
        if (raised is { } statUp && SourceStatRecipeContext.SourceStage(mon, statUp) < 12)
            Stage(statUp, SourceStatRecipeContext.SourceStage(mon, statUp) + 1);
        if (effect == "HOLD_EFFECT_RANDOM_STAT_UP" && SourceStats.Take(5).Any(s => SourceStatRecipeContext.SourceStage(mon, s) < 12))
        {
            int choice;
            do { choice = draw(purpose + ".random-stat", 5) % 5; }
            while (SourceStatRecipeContext.SourceStage(mon, SourceStats[choice]) == 12);
            Stage(SourceStats[choice], Math.Min(12, SourceStatRecipeContext.SourceStage(mon, SourceStats[choice]) + 2));
        }
        if (changes.Count > 0) return new(after with { HeldItem = "ITEM_NONE" }, true, true, StatChanges: changes.ToImmutable());
        if (effect == "HOLD_EFFECT_CRITICAL_UP" && !mon.FocusEnergy)
            return new(mon with { FocusEnergy = true, HeldItem = "ITEM_NONE" }, true, true);
        return new(mon);
    }
    internal static bool DislikesFlavor(uint personality, string flavor)
    {
        int nature = (int)(personality % 25), up = nature / 5, down = nature % 5;
        int stat = flavor switch { "spicy" => 0, "sour" => 1, "sweet" => 2, "dry" => 3, "bitter" => 4, _ => -1 };
        return up != down && stat == down;
    }
    private static SourceHeldEffect Finish(WildBattleCombatant mon, bool status, bool confusion, bool attract, int heal, bool consume, bool writeStatus = false)
    {
        var after = mon with { HeldItem = consume ? "ITEM_NONE" : mon.HeldItem,
            Pokemon = mon.Pokemon with { CurrentHp = mon.Pokemon.CurrentHp + heal } };
        if (status || writeStatus) after = after with { Status = status ? WildBattleStatus.None : mon.Status, SourcePartyStatusOverride = null,
            Transform = after.Transform is { PartyStatusOverride: not null } tr ? tr with { PartyStatusOverride = null } : after.Transform,
            Nightmare = WildBattleStatus.IsSleeping(mon.Status) ? false : mon.Nightmare };
        if (confusion) after = after with { ConfusionTurnsRemaining = 0 };
        return new(after, consume || heal > 0, consume, status, confusion, attract);
    }
}
