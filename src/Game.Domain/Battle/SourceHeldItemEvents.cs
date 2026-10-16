using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal static class SourceHeldItemEvents
{
    internal static void Publish(WildBattleCombatant before, SourceHeldEffect effect, BattlerSlot slot,
        BattlerSlot? infatuation, int turn, Action<Func<long, SourceBattleEvent>> add)
    {
        var after = effect.Body;
        if (before.ConfusionTurnsRemaining != after.ConfusionTurnsRemaining)
            add(n => new SourceConfusionChangedEvent(n, turn, slot, before.ConfusionTurnsRemaining,
                after.ConfusionTurnsRemaining, effect.CureConfusion ? before.HeldItem == "ITEM_PERSIM_BERRY" ? "persim-berry" : "held-berry" : "flavor-berry"));
        if (effect.CureStatus) add(n => new SourceStatusChangedEvent(n, turn, slot, before.Status, after.Status, "held-berry"));
        if (effect.CureInfatuation) add(n => new SourceInfatuationChangedEvent(n, turn, slot, infatuation, null, "mental-herb"));
        if (!effect.StatChanges.IsDefaultOrEmpty)
            foreach (var stat in effect.StatChanges) add(n => new SourceStatChangedEvent(n, turn, slot, slot, stat.Stat, stat.Before, stat.After));
        if (!before.FocusEnergy && after.FocusEnergy) add(n => new SourceFocusEnergyEvent(n, turn, slot));
        if (effect.Consume)
            add(n => new SourceHeldItemConsumedEvent(n, turn, slot, before.HeldItem, before.Pokemon.CurrentHp, after.Pokemon.CurrentHp,
                after.Pokemon.CurrentHp - before.Pokemon.CurrentHp) { PpRecovery = effect.PpRecovery,
                StatChanges = effect.StatChanges.IsDefaultOrEmpty ? null : effect.StatChanges,
                FocusEnergyRaised = !before.FocusEnergy && after.FocusEnergy ? true : null, Flavor = effect.Flavor });
        else if (after.Pokemon.CurrentHp != before.Pokemon.CurrentHp)
            add(n => new SourceHeldItemHealEvent(n, turn, slot, slot, new("pret:MOVE_NONE"), before.HeldItem, 0,
                before.Pokemon.CurrentHp, after.Pokemon.CurrentHp, after.Pokemon.CurrentHp - before.Pokemon.CurrentHp));
    }
}
