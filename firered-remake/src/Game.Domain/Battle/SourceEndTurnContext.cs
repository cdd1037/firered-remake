using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Ids;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

// Names and order are DoFieldEndTurnEffects / DoBattlerEndTurnEffects at the pinned source.
internal enum SourceFieldEndTurnStep { Reflect, LightScreen, Mist, Safeguard, Wish, Rain, Sandstorm, Sun, Hail }
internal enum SourceBattlerEndTurnStep { Ingrain, Abilities, Items1, LeechSeed, Poison, Toxic, Burn, Nightmare,
    Curse, Wrap, Uproar, Thrash, Disable, Encore, LockOn, Charge, Taunt, Yawn, Items2 }

/// <summary>The one source residual scheduler and migrated recipes. Contexts supply storage,
/// phase-only compatibility commands and boundaries; they never supply another weather/status loop.</summary>
internal abstract partial class SourceEndTurnContext
{
    internal abstract int Turn { get; }
    internal abstract long Revision { get; }
    internal abstract long EventSequence { get; }
    internal abstract int BattlerCount { get; }
    internal abstract bool Ongoing { get; }
    internal abstract bool AdvancedAbilitiesEnabled { get; }
    internal abstract bool ToxicEnabled { get; }
    internal abstract bool RainTimerEnabled { get; }
    internal abstract WildBattleCombatant Get(BattlerSlot slot);
    internal abstract void Set(BattlerSlot slot, WildBattleCombatant body);
    internal abstract bool IsAbsent(BattlerSlot slot);
    internal abstract bool WeatherSuppressed { get; }
    internal abstract bool HasWeather(string weather);
    internal abstract bool WeatherDamageEnabled(BattlerSlot slot, string weather);
    internal abstract bool UnderGroundOrWater(BattlerSlot slot);
    internal abstract int GetTimer(BattleSide side, string effect);
    internal abstract void SetTimer(BattleSide side, string effect, int value);
    internal abstract void Add(Func<long, SourceBattleEvent> create);
    internal abstract ushort Draw(string purpose, int bound);
    internal abstract string Purpose(BattlerSlot slot, string effect);
    internal abstract void RecordAbility(BattlerSlot slot, string ability);
    internal abstract void ChangeStatus(BattlerSlot slot, uint status, string reason);
    internal abstract void Faint(BattlerSlot slot);
    internal abstract void DamageOutcome();
    internal abstract void CheckTeamsLost();
    internal abstract void Prepare();
    internal abstract void ExposeFaintedActions();
    internal abstract void FaintedActionItems();
    internal abstract void Wish(PokemonCatalog catalog, BattlerSlot slot);
    internal abstract void UnmigratedBattlerStep(SourceBattlerEndTurnStep step, BattlerSlot slot);
    internal abstract bool Future(PokemonCatalog catalog, BattlerSlot slot);
    internal abstract bool Perish(BattlerSlot slot);
    internal abstract void Cleanup();
    internal virtual bool BindingStoresMove => true;
    internal virtual void ClearBindingSource(BattlerSlot slot) { }

    internal void BindingResidual(BattlerSlot slot)
    {
        var mon = Get(slot);
        if (mon.Pokemon.CurrentHp == 0 || mon.BindTurnsRemaining == 0) return;
        int remaining = mon.BindTurnsRemaining - 1;
        Set(slot, mon with { BindTurnsRemaining = remaining, BindingMove = remaining == 0 ? null : mon.BindingMove });
        if (remaining == 0)
        {
            ClearBindingSource(slot);
            Add(n => new SourceBindingChangedEvent(n, Turn, slot, 1, 0, "expired")
                { Move = BindingStoresMove ? mon.BindingMove ?? new MoveId("pret:MOVE_BIND") : null });
            return;
        }
        int before = mon.Pokemon.CurrentHp, damage = Math.Max(1, mon.Pokemon.Stats.MaxHp / 16);
        int after = Math.Max(0, before - damage);
        Set(slot, Get(slot) with { Pokemon = mon.Pokemon with { CurrentHp = after } });
        Add(n => new SourceBindingDamageEvent(n, Turn, slot, mon.BindingMove ?? new MoveId("pret:MOVE_BIND"), before, after, damage, remaining));
        if (after == 0) { Faint(slot); DamageOutcome(); }
    }

    internal SourceEndTurnCursor Advance(PokemonCatalog catalog, ImmutableArray<BattlerSlot> order,
        SourceEndTurnCursor? cursor = null)
    {
        if (cursor is null)
        {
            FieldEffects(catalog, order);
            foreach (var slot in order)
                if (Ongoing && !IsAbsent(slot)) BattlerEffects(slot);
            ExposeFaintedActions();
            return new(Turn, order, WildBattleSourceEndTurnStage.FutureAttacks, 0, Revision, EventSequence,
                WildBattleRules.SourceEndTurnRulesVersion);
        }
        FaintedActionItems();
        var stage = cursor.Stage;
        int next = cursor.NextBattler;
        if (stage == WildBattleSourceEndTurnStage.FutureAttacks)
        {
            for (; next < BattlerCount; next++)
                if (Future(catalog, new(next)))
                {
                    ExposeFaintedActions();
                    return cursor with { NextBattler = next + 1, MinimumRevision = Revision, MinimumEventSequence = EventSequence };
                }
            stage = WildBattleSourceEndTurnStage.PerishSong;
            next = 0;
        }
        for (; next < BattlerCount; next++)
            if (Perish(cursor.Order[next]))
            {
                ExposeFaintedActions();
                return cursor with { Stage = stage, NextBattler = next + 1, MinimumRevision = Revision, MinimumEventSequence = EventSequence };
            }
        Cleanup();
        return cursor with { Stage = WildBattleSourceEndTurnStage.Complete, NextBattler = BattlerCount,
            MinimumRevision = Revision, MinimumEventSequence = EventSequence };
    }

    internal void FieldEffects(PokemonCatalog catalog, ImmutableArray<BattlerSlot> order)
    {
        foreach (var effect in new[] { "reflect", "light-screen", "mist", "safeguard" })
            foreach (var side in new[] { BattleSide.Player, BattleSide.Opponent }) TickTimer(side, effect);
        foreach (var slot in order) Wish(catalog, slot);
        if (RainTimerEnabled) TickTimer(BattleSide.Player, "rain");
        TickTimer(BattleSide.Player, "sandstorm");
        // WEATHER_HAS_EFFECT is evaluated for each weatherdamage command, not from presence
        // or action eligibility. A source-absent living Cloud Nine still suppresses weather.
        if (!WeatherSuppressed) foreach (var slot in order)
        {
            if (!Ongoing) break;
            WeatherDamage(catalog, slot, "sandstorm");
            CheckTeamsLost();
        }
        if (!Ongoing) return;
        TickTimer(BattleSide.Player, "sun");
        TickTimer(BattleSide.Player, "hail");
        foreach (var slot in order)
        {
            if (!Ongoing) break;
            WeatherDamage(catalog, slot, "hail");
            CheckTeamsLost();
        }
    }

    internal void BattlerEffects(BattlerSlot slot)
    {
        foreach (var step in Enum.GetValues<SourceBattlerEndTurnStep>())
        {
            if (!Ongoing) break;
            switch (step)
            {
                case SourceBattlerEndTurnStep.Ingrain: IngrainResidual(slot); break;
                case SourceBattlerEndTurnStep.LeechSeed: LeechSeedResidual(slot); break;
                case SourceBattlerEndTurnStep.Abilities: Ability(slot); break;
                case SourceBattlerEndTurnStep.Poison: StatusDamage(slot, WildBattleStatus.Poison); break;
                case SourceBattlerEndTurnStep.Toxic: ToxicDamage(slot); break;
                case SourceBattlerEndTurnStep.Burn: StatusDamage(slot, WildBattleStatus.Burn); break;
                case SourceBattlerEndTurnStep.Wrap: BindingResidual(slot); break;
                default: UnmigratedBattlerStep(step, slot); break;
            }
            CheckTeamsLost();
        }
    }

    internal void Disable(BattlerSlot slot)
    {
        var body = Get(slot);
        if (body.DisableTurnsRemaining == 0) return;
        bool changed = !body.Pokemon.Moves.Any(m => m.Move == body.DisabledMove);
        int after = changed ? 0 : body.DisableTurnsRemaining - 1;
        Set(slot, body with { DisableTurnsRemaining = after, DisabledMove = after == 0 ? null : body.DisabledMove });
        Add(n => new SourceDisableChangedEvent(n, Turn, slot, body.DisabledMove, body.DisableTurnsRemaining, after,
            changed ? "move-changed" : after == 0 ? "expired" : "countdown"));
    }

    internal void TickTimer(BattleSide side, string effect)
    {
        int before = GetTimer(side, effect);
        if (before == 0) return;
        int after = WildBattleSourceFieldEffects.TickTimer(before);
        SetTimer(side, effect, after);
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, new((int)side), effect, before, after, after == 0 ? "expired" : "countdown"));
    }

    internal void WeatherDamage(PokemonCatalog catalog, BattlerSlot slot, string weather)
    {
        if (!WeatherDamageEnabled(slot, weather) || !HasWeather(weather) || !Ongoing || IsAbsent(slot) || WeatherSuppressed) return;
        var mon = Get(slot); var types = WildBattleEffective.Types(catalog, mon);
        if (mon.Pokemon.CurrentHp == 0 || UnderGroundOrWater(slot)) return;
        if (weather == "sandstorm" && (WildBattleEffective.Ability(mon) == "ABILITY_SAND_VEIL" ||
            types.Type1 is "TYPE_ROCK" or "TYPE_GROUND" or "TYPE_STEEL" || types.Type2 is "TYPE_ROCK" or "TYPE_GROUND" or "TYPE_STEEL")) return;
        if (weather == "hail" && (types.Type1 == "TYPE_ICE" || types.Type2 == "TYPE_ICE")) return;
        int before = mon.Pokemon.CurrentHp, damage = Math.Max(1, mon.Pokemon.Stats.MaxHp / 16), after = Math.Max(0, before - damage);
        Set(slot, mon with { Pokemon = mon.Pokemon with { CurrentHp = after } });
        Add(n => new SourceWeatherDamageEvent(n, Turn, slot, weather, before, after, damage));
        if (after == 0) Faint(slot);
        DamageOutcome();
    }

    internal void StatusDamage(BattlerSlot slot, uint status)
    {
        var mon = Get(slot);
        if (mon.Pokemon.CurrentHp == 0 || mon.Status != status) return;
        int before = mon.Pokemon.CurrentHp, damage = Math.Max(1, mon.Pokemon.Stats.MaxHp / 8), after = Math.Max(0, before - damage);
        Set(slot, mon with { Pokemon = mon.Pokemon with { CurrentHp = after } });
        Add(n => new SourceStatusDamageEvent(n, Turn, slot, status, before, after, damage));
        if (after == 0) Faint(slot);
    }

    internal void ToxicDamage(BattlerSlot slot)
    {
        var mon = Get(slot);
        if (!ToxicEnabled || mon.Pokemon.CurrentHp == 0 || !WildBattleSourceFieldEffects.IsValidToxic(mon.Status)) return;
        var tick = WildBattleSourceFieldEffects.ToxicTick(mon.Status, mon.Pokemon.Stats.MaxHp);
        int before = mon.Pokemon.CurrentHp, after = Math.Max(0, before - tick.Damage);
        Set(slot, mon with { Status = tick.Status, Pokemon = mon.Pokemon with { CurrentHp = after } });
        Add(n => new SourceStatusDamageEvent(n, Turn, slot, tick.Status, before, after, tick.Damage));
        if (after == 0) { Faint(slot); DamageOutcome(); }
    }

    internal void Ability(BattlerSlot slot)
    {
        var mon = Get(slot);
        if (mon.Pokemon.CurrentHp == 0) return;
        string ability = WildBattleEffective.Ability(mon);
        if (AdvancedAbilitiesEnabled)
            switch (ability)
            {
                case "ABILITY_RAIN_DISH":
                    int recovered = SourceWeatherRules.RainDishRecovery(ability, mon.Pokemon.CurrentHp,
                        mon.Pokemon.Stats.MaxHp, HasWeather("rain"), WeatherSuppressed);
                    if (recovered > 0)
                    {
                        Set(slot, mon with { Pokemon = mon.Pokemon with { CurrentHp = mon.Pokemon.CurrentHp + recovered } });
                        RecordAbility(slot, ability);
                        Add(n => new SourceAbilityActivatedEvent(n, Turn, slot, ability));
                        Add(n => new SourceAbilityRecoveredEvent(n, Turn, slot, ability, mon.Pokemon.CurrentHp,
                            mon.Pokemon.CurrentHp + recovered, recovered));
                    }
                    return;
                case "ABILITY_TRUANT": Set(slot, mon with { TruantLoafing = !mon.TruantLoafing }); return;
                case "ABILITY_SPEED_BOOST":
                    if (mon.SpeedStage < 12 && mon.FirstTurnCounter != 2)
                    {
                        Set(slot, mon with { SpeedStage = mon.SpeedStage + 1 });
                        RecordAbility(slot, ability);
                        Add(n => new SourceStatChangedEvent(n, Turn, slot, slot, WildBattleStat.Speed, mon.SpeedStage, mon.SpeedStage + 1));
                        Add(n => new SourceAbilityActivatedEvent(n, Turn, slot, ability));
                    }
                    return;
            }
        if (ability != "ABILITY_SHED_SKIN" || mon.Status == 0 || Draw(Purpose(slot, "shed-skin"), 3) % 3 != 0) return;
        RecordAbility(slot, ability);
        ChangeStatus(slot, WildBattleStatus.None, "shed-skin");
    }
}
