using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

internal sealed partial class FrameEndTurnContext
{
    internal bool IngrainEnabled => true;
    internal bool LiquidOozeEnabled => true;

    internal void IngrainResidual(BattlerSlot slot)
    {
        var mon = Get(slot);
        if (!IngrainEnabled || !mon.Rooted || mon.Pokemon.CurrentHp == 0 || mon.Pokemon.CurrentHp == mon.Pokemon.Stats.MaxHp) return;
        int before = mon.Pokemon.CurrentHp, after = Math.Min(mon.Pokemon.Stats.MaxHp, before + Math.Max(1, mon.Pokemon.Stats.MaxHp / 16));
        Set(slot, mon with { Pokemon = mon.Pokemon with { CurrentHp = after } });
        Add(n => new SourceIngrainHealEvent(n, Turn, slot, before, after, after - before));
    }

    internal void LeechSeedResidual(BattlerSlot slot)
    {
        var donor = Get(slot);
        if (donor.Pokemon.CurrentHp == 0 || LeechSeedSource(slot) is not { } source) return;
        var receiver = Get(source);
        // The stored source is a position, preserved across departure. The original case
        // checks receiver HP only, including a living source-absent receiver.
        if (receiver.Pokemon.CurrentHp == 0) return;
        int before = donor.Pokemon.CurrentHp, damage = Math.Min(before, Math.Max(1, donor.Pokemon.Stats.MaxHp / 8));
        int after = before - damage, receiverBefore = receiver.Pokemon.CurrentHp;
        bool ooze = LiquidOozeEnabled && WildBattleEffective.Ability(donor) == "ABILITY_LIQUID_OOZE";
        int receiverAfter = ooze ? Math.Max(0, receiverBefore - damage) : Math.Min(receiver.Pokemon.Stats.MaxHp, receiverBefore + damage);
        if (ooze) { RecordAbility(slot, "ABILITY_LIQUID_OOZE"); Add(n => new SourceAbilityActivatedEvent(n, Turn, slot, "ABILITY_LIQUID_OOZE")); }
        Set(slot, donor with { Pokemon = donor.Pokemon with { CurrentHp = after } });
        Set(source, receiver with { Pokemon = receiver.Pokemon with { CurrentHp = receiverAfter } });
        Add(n => new SourceLeechSeedDrainEvent(n, Turn, slot, source, before, after, receiverBefore, receiverAfter, damage, receiverAfter - receiverBefore));
        // gHpDealt clips the transfer to donor HP. Both updates finish before either faint;
        // the source script then runs donor tryfaintmon before receiver tryfaintmon.
        if (after == 0 && !IsAbsent(slot)) Faint(slot);
        if (receiverAfter == 0 && !IsAbsent(source)) FaintAs(source, slot);
        if (after == 0 || receiverAfter == 0) DamageOutcome();
    }
}
