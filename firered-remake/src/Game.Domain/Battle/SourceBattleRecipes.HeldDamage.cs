using Game.Domain.Ids;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal readonly record struct SourceDamageAdjustment(int Damage, bool Endured, bool HungOn);

internal static class SourceHeldDamageRules
{
    // Cmd_adjustnormaldamage/2/setdamage: focusBanded is a sticky special-status bit.
    internal static bool FocusRoll(bool alreadySet, ushort roll) => alreadySet || roll % 100 < 10;
    internal static SourceDamageAdjustment Adjust(int hp, int damage, bool substitute, bool endure, bool falseSwipe, bool focus)
    {
        if (!substitute && hp <= damage && (falseSwipe || endure || focus))
            return new(hp - 1, endure, !endure && focus);
        return new(damage, false, false);
    }
}

internal abstract partial class SourceRecipeContext
{
    // The slot kernel remains closed to held items until its owner admits this phase.
    internal virtual bool SourceHeldDamageEnabled => false;
    internal virtual bool FocusBanded(BattlerSlot slot) => false;
    internal virtual void SetFocusBanded(BattlerSlot slot) => throw new SourceRecipeUnsupported("source_kernel_held_damage_not_migrated");
    internal virtual bool HasDamageSubstitute(BattlerSlot slot) => Get(slot).CalledVolatile?.SubstituteHp > 0;
    internal virtual void RecordEndureAdjustment(BattlerSlot slot) { }
    private readonly HashSet<BattlerSlot> heldDamageEndured = [];
    internal bool HeldDamageEndured(BattlerSlot slot) => heldDamageEndured.Contains(slot);
    private readonly HashSet<BattlerSlot> heldDamageHungOn = [];
    internal bool HeldDamageHungOn(BattlerSlot slot) => heldDamageHungOn.Contains(slot);
    private readonly HashSet<BattlerSlot> heldDamageEffectiveZeros = [];
    internal bool HeldDamageEffectiveZero(BattlerSlot slot) => heldDamageEffectiveZeros.Contains(slot);
    internal void ClearHeldActionResults() { heldDamageEndured.Clear(); heldDamageHungOn.Clear(); heldDamageEffectiveZeros.Clear(); }

    internal bool RollFocusBand(BattlerSlot target, string purpose)
    {
        if (!SourceHeldDamageEnabled) return false;
        if (Get(target).HeldItem == "ITEM_FOCUS_BAND")
        {
            // Draw even when already sticky, nonlethal, zero damage or behind Substitute.
            ushort roll = Draw($"{purpose}.focus-band", 100);
            if (SourceHeldDamageRules.FocusRoll(FocusBanded(target), roll)) SetFocusBanded(target);
        }
        return FocusBanded(target);
    }

    internal int AdjustHeldDamage(BattlerSlot target, MoveId move, int damage, string purpose,
        bool falseSwipe = false, bool normal2 = false, bool ohko = false, bool roll = true)
    {
        if (!SourceHeldDamageEnabled) return damage;
        bool focus = roll ? RollFocusBand(target, purpose) : FocusBanded(target);
        var mon = Get(target);
        var result = SourceHeldDamageRules.Adjust(mon.Pokemon.CurrentHp, damage,
            !ohko && HasDamageSubstitute(target), mon.Enduring, !normal2 && falseSwipe, focus);
        heldDamageEndured.Remove(target);
        heldDamageHungOn.Remove(target);
        heldDamageEffectiveZeros.Remove(target);
        if (damage > 0 && result.Damage == 0) heldDamageEffectiveZeros.Add(target);
        if (result.Endured) { heldDamageEndured.Add(target); RecordEndureAdjustment(target); }
        if (result.HungOn) heldDamageHungOn.Add(target);
        // Mechanical adjustment receipt, not Cmd_resultmessage or an item-consumption event.
        // Explosion can reach this command before a later accuracy failure.
        if (result.Damage != damage || result.Endured || result.HungOn)
            Add(n => new SourceDamageAdjustedEvent(n, Turn, target, move, mon.HeldItem, damage,
                result.Damage, result.Endured, result.HungOn));
        return result.Damage;
    }
}
