using Game.Domain.Ids;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>One slot-typed ON_SWITCHIN/Intimidate/Trace/Forecast recipe. Owners supply only
/// storage, phase lifetime and event adapters; copied abilities never call ON_SWITCHIN.</summary>
internal abstract class SourceEntryAbilityContext : SourceStatRecipeContext
{
    internal abstract int BattlerCount { get; }
    internal abstract SourceFieldRegisters Field { get; set; }
    internal abstract ushort Draw(string purpose, int bound);
    internal abstract bool SuppressIntimidateTrace { get; }
    internal abstract bool IsAbsent(BattlerSlot slot);
    internal abstract void ArmIntimidate(BattlerSlot slot);
    internal virtual bool SinglesLivingTargets => false;
    internal virtual void LowerAttack(BattlerSlot actor, BattlerSlot target) =>
        SourceChangeStat(actor, target, WildBattleStat.Attack, -1, new("pret:MOVE_NONE"));
    private IEnumerable<BattlerSlot> Slots => Enumerable.Range(0, BattlerCount).Select(i => new BattlerSlot(i));

    internal void WeatherEntry(BattlerSlot slot)
    {
        var mon = Get(slot);
        if (mon.Pokemon.CurrentHp == 0) return;
        string ability = WildBattleEffective.Ability(mon);
        var before = Field; var after = SourceWeatherRules.ApplyEntry(before, ability);
        if (ReferenceEquals(before, after)) return;
        Field = after;
        RecordAbility(slot, ability);
        Add(n => new SourceAbilityActivatedEvent(n, Turn, slot, ability));
        Add(n => new SourcePermanentWeatherChangedEvent(n, Turn, slot, ability, before.PermanentWeather?.Kind, after.PermanentWeather!.Kind));
        ForecastAll();
    }

    internal void SwitchInRemainder(BattlerSlot slot)
    {
        if (Get(slot).Pokemon.CurrentHp == 0) return;
        string ability = WildBattleEffective.Ability(Get(slot));
        if (!SuppressIntimidateTrace)
        {
            if (ability == "ABILITY_TRACE") Set(slot, Get(slot) with { SourceTracePending = true });
            if (ability == "ABILITY_INTIMIDATE") ArmIntimidate(slot);
        }
        if (ability == "ABILITY_FORECAST") Forecast(slot);
        if (ability is "ABILITY_CLOUD_NINE" or "ABILITY_AIR_LOCK") ForecastAll();
    }

    internal void SwitchIn(BattlerSlot slot) { WeatherEntry(slot); SwitchInRemainder(slot); }

    internal void Intimidate(BattlerSlot slot)
    {
        if (SuppressIntimidateTrace || WildBattleEffective.Ability(Get(slot)) != "ABILITY_INTIMIDATE") return;
        var targets = Slots.Where(t => t.Side != slot.Side && !IsAbsent(t) &&
            (!SinglesLivingTargets || Get(t).Pokemon.CurrentHp > 0)).ToArray();
        if (SinglesLivingTargets && (Get(slot).Pokemon.CurrentHp == 0 || targets.Length == 0)) return;
        RecordAbility(slot, "ABILITY_INTIMIDATE");
        Add(n => new SourceAbilityActivatedEvent(n, Turn, slot, "ABILITY_INTIMIDATE"));
        // Cmd_trygetintimidatetarget tests absence, never HP. The source script separately
        // skips Substitute. Its stat command supplies Mist and Clear Body/Hyper Cutter/White Smoke.
        foreach (var target in targets)
        {
            if (SourceCalledSecondaryBlocked(target)) continue;
            string ability = WildBattleEffective.Ability(Get(target));
            if (ability is "ABILITY_CLEAR_BODY" or "ABILITY_HYPER_CUTTER" or "ABILITY_WHITE_SMOKE")
            {
                RecordAbility(target, ability);
                Add(n => new SourceAbilityPreventedEvent(n, Turn, slot, target, new("pret:MOVE_NONE"), ability));
            }
            else LowerAttack(slot, target);
        }
    }

    internal void ResolveTrace()
    {
        if (SuppressIntimidateTrace) return;
        foreach (var slot in Slots)
        {
            var mon = Get(slot);
            if (!mon.SourceTracePending || WildBattleEffective.Ability(mon) != "ABILITY_TRACE") continue;
            int? target = SourceTraceForecastRules.TraceTarget(Slots.Select(s => (WildBattleCombatant?)Get(s)).ToArray(), slot.Value,
                () => Draw("wild-battle.trace.target", 2));
            if (target is null) continue;
            string ability = WildBattleEffective.Ability(Get(new(target.Value)));
            var before = mon.CalledOverlay; var after = (before ?? new()) with { Ability = ability };
            Set(slot, mon with { SourceTracePending = false, CalledOverlay = after });
            RecordAbility(slot, ability);
            Add(n => new SourceAbilityActivatedEvent(n, Turn, slot, "ABILITY_TRACE"));
            Add(n => new SourceCalledOverlayChangedEvent(n, Turn, slot, new("pret:MOVE_NONE"), before, after) { Source = new(target.Value) });
        }
    }

    internal void Forecast(BattlerSlot slot)
    {
        var mon = Get(slot); var field = Field;
        bool suppressed = SourceWeatherRules.IsSuppressed(Slots.Select(Get));
        var change = SourceTraceForecastRules.ForecastChange(mon, !suppressed,
            SourceWeatherRules.HasSun(field), SourceWeatherRules.HasRain(field), field.HailTurnsRemaining > 0);
        if (change is null) return;
        var before = mon.CalledOverlay;
        var after = (before ?? new()) with { Type1 = change.Type, Type2 = change.Type, CastformForm = change.Form };
        Set(slot, mon with { CalledOverlay = after });
        RecordAbility(slot, "ABILITY_FORECAST");
        Add(n => new SourceAbilityActivatedEvent(n, Turn, slot, "ABILITY_FORECAST"));
        Add(n => new SourceCalledOverlayChangedEvent(n, Turn, slot, new("pret:MOVE_NONE"), before, after));
    }
    internal void ForecastAll() { foreach (var slot in Slots) Forecast(slot); }
}
