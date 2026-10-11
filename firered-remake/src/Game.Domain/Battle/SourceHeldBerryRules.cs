using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal enum SourceBerryPhase { Normal, NormalMoveTurn, MoveEnd }
internal readonly record struct SourceBerryEffect(bool CureStatus, bool CureConfusion, int HpRestored)
{
    internal bool Consumed => CureStatus || CureConfusion || HpRestored > 0;
}

/// <summary>The existing campaign berry cohort from ItemBattleEffects. Owners supply storage,
/// source phase order and compatibility-profile switches; berries never consume RNG.</summary>
internal static class SourceHeldBerryRules
{
    internal static bool Supports(string item) => item is "ITEM_ORAN_BERRY" or "ITEM_CHERI_BERRY" or
        "ITEM_CHESTO_BERRY" or "ITEM_PECHA_BERRY" or "ITEM_RAWST_BERRY" or "ITEM_ASPEAR_BERRY" or
        "ITEM_PERSIM_BERRY" or "ITEM_SPELON_BERRY" or "ITEM_PAMTRE_BERRY" or "ITEM_WATMEL_BERRY" or
        "ITEM_DURIN_BERRY" or "ITEM_BELUE_BERRY";

    internal static SourceBerryEffect Resolve(WildBattleCombatant mon, SourceBerryPhase phase,
        bool statusBerries = true, bool freeze = true, bool toxic = true)
    {
        // ITEMEFFECT_NORMAL guards HP; ITEMEFFECT_MOVE_END deliberately does not.
        if (phase != SourceBerryPhase.MoveEnd && mon.Pokemon.CurrentHp == 0) return default;
        if (phase == SourceBerryPhase.Normal && mon.HeldItem == "ITEM_ORAN_BERRY" &&
            mon.Pokemon.CurrentHp <= mon.Pokemon.Stats.MaxHp / 2)
            return new(false, false, Math.Min(10, mon.Pokemon.Stats.MaxHp - mon.Pokemon.CurrentHp));
        if (!statusBerries) return default;
        bool status = mon.HeldItem switch {
            "ITEM_CHERI_BERRY" => mon.Status == WildBattleStatus.Paralysis,
            "ITEM_CHESTO_BERRY" => WildBattleStatus.IsSleeping(mon.Status),
            "ITEM_PECHA_BERRY" => mon.Status == WildBattleStatus.Poison || toxic && WildBattleSourceFieldEffects.IsToxic(mon.Status),
            "ITEM_RAWST_BERRY" => mon.Status == WildBattleStatus.Burn,
            "ITEM_ASPEAR_BERRY" => freeze && mon.Status == WildBattleStatus.Freeze,
            _ => false };
        return new(status, mon.HeldItem == "ITEM_PERSIM_BERRY" && mon.ConfusionTurnsRemaining > 0, 0);
    }
}
