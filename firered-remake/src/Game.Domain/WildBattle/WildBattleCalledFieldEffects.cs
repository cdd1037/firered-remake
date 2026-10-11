using System.Text.Json.Serialization;
using Game.Domain.Ids;
using Game.Domain.Battle;
using Game.Domain.Pokemon;
using Game.Domain.Rules;

namespace Game.Domain.WildBattle;

/// <summary>Opt-in source field registers; never admitted by the historical Fuchsia profile.</summary>
public sealed record WildBattleCalledFieldState
{
    public string RulesVersion { get; init; } = WildBattleRules.CalledMoveKernelRulesVersion;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int SunTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int HailTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int PlayerLightScreenTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int OpponentLightScreenTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int PlayerMistTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int OpponentMistTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int PlayerSafeguardTurnsRemaining { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public int OpponentSafeguardTurnsRemaining { get; init; }
}

/// <summary>Bounded source commands, shared with the executor and its later full-turn caller.</summary>
public static class WildBattleSourceFieldEffects
{
    public const uint ToxicPoison = 0x80, ToxicCounterMask = 0xF00;
    public static bool IsToxic(uint status) => (status & ToxicPoison) != 0;
    public static bool IsValidToxic(uint status) => IsToxic(status) && (status & ~(ToxicPoison | ToxicCounterMask)) == 0;
    public static (uint Status, int Damage) ToxicTick(uint status, int maximumHp)
    {
        if (!IsValidToxic(status) || maximumHp <= 0) throw new ArgumentOutOfRangeException(nameof(status));
        uint after = (status & ToxicCounterMask) == ToxicCounterMask ? status : status + 0x100;
        // FireRed divides first, clamps the quotient, then multiplies by the counter.
        return (after, checked(Math.Max(1, maximumHp / 16) * (int)((after & ToxicCounterMask) >> 8)));
    }
    public static uint ResetToxicCounter(uint status) => IsValidToxic(status) ? ToxicPoison : status;
    public static int TickTimer(int remaining) => remaining is < 0 or > 5
        ? throw new ArgumentOutOfRangeException(nameof(remaining)) : Math.Max(0, remaining - 1);
    public static (ElementType Type, int Multiplier) WeatherBall(bool rain, bool sand, bool sun, bool hail, bool suppressed)
    {
        if ((rain ? 1 : 0) + (sand ? 1 : 0) + (sun ? 1 : 0) + (hail ? 1 : 0) > 1)
            throw new ArgumentException("Mutually exclusive source weather is required.");
        if (suppressed) return (ElementType.Normal, 1);
        return rain ? (ElementType.Water, 2) : sand ? (ElementType.Rock, 2) : sun ? (ElementType.Fire, 2) :
            hail ? (ElementType.Ice, 2) : (ElementType.Normal, 1);
    }
}

public static partial class WildBattleRules
{
    private static bool HasCalledFieldRecipe(string effect) => effect is "EFFECT_FREEZE_HIT" or "EFFECT_MIST" or
        "EFFECT_THUNDER" or "EFFECT_TOXIC" or "EFFECT_LIGHT_SCREEN" or "EFFECT_THAW_HIT" or
        "EFFECT_SAFEGUARD" or "EFFECT_SUNNY_DAY" or "EFFECT_HAIL" or "EFFECT_POISON_FANG" or "EFFECT_WEATHER_BALL";
    private static void AddCalledFieldEffectSupport(PokemonMoveDefinition move, ref bool supported)
    {
        if (MetronomeSourceMoveDefinition.Matches(move) && HasCalledFieldRecipe(move.Effect)) supported = true;
    }

    private static string? ValidateCalledFieldState(WildBattleState state)
    {
        var field = state.CalledField;
        foreach (var mon in new[] { state.Player, state.Opponent })
        {
            if ((mon.Status & (WildBattleSourceFieldEffects.ToxicPoison | WildBattleSourceFieldEffects.ToxicCounterMask)) != 0 &&
                (field is null || !WildBattleSourceFieldEffects.IsValidToxic(mon.Status))) return "called_field_toxic_status";
        }
        if (field is null) return null;
        if (field.RulesVersion != CalledMoveKernelRulesVersion) return "called_field_version";
        if (new[] { field.SunTurnsRemaining, field.HailTurnsRemaining, field.PlayerLightScreenTurnsRemaining,
            field.OpponentLightScreenTurnsRemaining, field.PlayerMistTurnsRemaining, field.OpponentMistTurnsRemaining,
            field.PlayerSafeguardTurnsRemaining, field.OpponentSafeguardTurnsRemaining }.Any(t => t is < 0 or > 5))
            return "called_field_timer";
        if (new[] { field.SunTurnsRemaining, field.HailTurnsRemaining, state.RainTurnsRemaining,
            state.SandstormTurnsRemaining }.Count(t => t > 0) > 1) return "called_field_weather_conflict";
        return null;
    }

    internal sealed partial class Execution
    {
        private bool SourceCalledFieldActive => activeInvocation is not null || State.CalledField is not null || State.CalledDelayed is not null || State.Player.CalledVolatile is not null || State.Opponent.CalledVolatile is not null;
        private bool SourceCalledRain => SourceWeatherRules.HasRain(State);
        private bool SourceCalledSandstorm => SourceWeatherRules.HasSandstorm(State);
        private bool SourceCalledSun => SourceWeatherRules.HasSun(State);
        private bool SourceCalledWeatherPresent => SourceCalledSun || (State.CalledField?.HailTurnsRemaining ?? 0) > 0 ||
            SourceCalledRain || SourceCalledSandstorm;
        private bool SourceCalledWeatherSuppressed => (SourceCalledFieldActive || SourceWeatherAbilitiesEnabled) &&
            SourceWeatherRules.IsSuppressed([State.Player, State.Opponent]);
        private bool SourceDamageWeatherSuppressed => SourceWeatherAbilitiesEnabled
            ? SourceWeatherRules.IsSuppressed([State.Player, State.Opponent], includeFainted: true) : SourceCalledWeatherSuppressed;
        private bool SourceCalledLightScreen(WildBattleSide side) => (side == WildBattleSide.Player
            ? State.CalledField?.PlayerLightScreenTurnsRemaining : State.CalledField?.OpponentLightScreenTurnsRemaining) > 0;
        private bool SourceCalledMist(WildBattleSide side) => (side == WildBattleSide.Player
            ? State.CalledField?.PlayerMistTurnsRemaining : State.CalledField?.OpponentMistTurnsRemaining) > 0;
        private bool SourceCalledSafeguard(WildBattleSide side) => (side == WildBattleSide.Player
            ? State.CalledField?.PlayerSafeguardTurnsRemaining : State.CalledField?.OpponentSafeguardTurnsRemaining) > 0;
        private void SourceCalledProtectionFailure(WildBattleSide actor, WildBattleSide target, MoveId move, string reason) =>
            Add(n => new WildBattleMoveFailedEvent(n, State.Turn, actor, target, move, reason));
        private bool SourceCalledSafeguardBlocks(WildBattleSide actor, WildBattleSide target, MoveId move)
        {
            if (!SourceCalledSafeguard(target)) return false;
            // Only explicit primary scripts jump to SafeguardProtected/end2.
            // Secondary/recursive SetMoveEffect uses SourceCalledSafeguard directly.
            if (activeInvocation is not null) invocationSkipsMoveEnd = true;
            SourceCalledProtectionFailure(actor, target, move, "safeguard");
            return true;
        }
        private bool SourceCalledMistBlocks(WildBattleSide actor, WildBattleSide target, int amount, MoveId move, bool secondary)
        {
            if (amount >= 0 || move.Value == "pret:MOVE_CURSE" || !SourceCalledMist(target)) return false;
            if (!secondary) SourceCalledProtectionFailure(actor, target, move, "mist");
            return true;
        }
        private void ExecuteCalledFieldEffect(PokemonCatalog catalog, WildBattleSide side, int slot,
            PokemonMoveDefinition move, string purpose, ref bool handled)
        {
            // Override weather replacement even for recipes already present in the base profile.
            bool existingFieldRecipe = move.Effect is "EFFECT_RAIN_DANCE" or "EFFECT_SANDSTORM" or "EFFECT_BRICK_BREAK";
            if (!HasCalledFieldRecipe(move.Effect) && !existingFieldRecipe) return;
            if (activeInvocation is null || !MetronomeSourceMoveDefinition.Matches(move))
                throw new UnsupportedAction("called_field_source_definition");
            handled = true;
            var target = activeInvocation.Target;
            if (move.Power > 0) { SourceCalledFieldHit(catalog, side, target, slot, move, purpose); return; }
            SpendEarlyPartyPp(side, target, slot, move);
            if (move.Effect == "EFFECT_TOXIC") { SourceCalledToxic(catalog, side, target, move, purpose); return; }
            new SinglesRecipeBridge(this).ApplyFieldSetting(new((int)side), new((int)target), move);
        }

        private void SourceCalledToxic(PokemonCatalog catalog, WildBattleSide side, WildBattleSide target,
            PokemonMoveDefinition move, string purpose)
        {
            var mon = Get(target); var species = WildBattleEffective.Types(catalog, mon);
            if (WildBattleEffective.Ability(mon) == "ABILITY_IMMUNITY")
            {
                RecordAbility?.Invoke(target, "ABILITY_IMMUNITY");
                Add(n => new WildBattleAbilityPreventedEvent(n, State.Turn, side, target, move.Id, "ABILITY_IMMUNITY")); return;
            }
            if (mon.Status == WildBattleStatus.Poison || WildBattleSourceFieldEffects.IsToxic(mon.Status))
            { SourceCalledProtectionFailure(side, target, move.Id, "already-poisoned"); return; }
            if (mon.Status != 0) { SourceCalledProtectionFailure(side, target, move.Id, "existing-status"); return; }
            if (species.Type1 is "TYPE_POISON" or "TYPE_STEEL" || species.Type2 is "TYPE_POISON" or "TYPE_STEEL")
            {
                Add(n => new WildBattleEffectivenessEvent(n, State.Turn, side, target, move.Id, WildBattleEffectiveness.NoEffect)); return;
            }
            if (!SourceGenericAccuracy(side, target, move, purpose))
            { SourceCalledProtectionFailure(side, target, move.Id, "accuracy"); return; }
            if (SourceCalledSafeguardBlocks(side, target, move.Id)) return;
            SourceCalledInflictStatus(catalog, target, WildBattleSourceFieldEffects.ToxicPoison, true);
        }

        private void SourceCalledInflictStatus(PokemonCatalog catalog, WildBattleSide targetSide, uint status, bool primary = false)
        {
            var target = Get(targetSide);
            if (target.Pokemon.CurrentHp == 0 || target.Status != 0 || !primary &&
                (SourceCalledSecondaryBlocked(targetSide) || WildBattleEffective.Ability(target) == "ABILITY_SHIELD_DUST" || SourceCalledSafeguard(targetSide))) return;
            var species = WildBattleEffective.Types(catalog, target);
            bool HasType(string type) => species.Type1 == type || species.Type2 == type;
            if (status == WildBattleStatus.Freeze)
            {
                if (HasType("TYPE_ICE") || WildBattleEffective.Ability(target) == "ABILITY_MAGMA_ARMOR" || SourceCalledSun && !SourceCalledWeatherSuppressed) return;
                CancelRoute3Rollout(targetSide, "freeze");
            }
            else if (WildBattleSourceFieldEffects.IsToxic(status))
            {
                if (HasType("TYPE_POISON") || HasType("TYPE_STEEL"))
                {
                    // Toxic secondary differs from ordinary poison: source marks NO_EFFECT
                    // after a damaging Poison Fang when its target type rejects bad poison.
                    if (!primary && activeInvocation is { } invocation)
                    {
                        invocationCommandNoEffect = true;
                        Add(n => new WildBattleEffectivenessEvent(n, State.Turn, invocation.Actor, targetSide, invocation.CurrentMove, WildBattleEffectiveness.NoEffect));
                    }
                    return;
                }
                if (WildBattleEffective.Ability(target) == "ABILITY_IMMUNITY") return;
                State = State with { CalledField = State.CalledField ?? new() };
            }
            ChangeStatus(targetSide, status, "move");
            // Gen III Synchronize transmits ordinary poison when triggered by Toxic.
            if (WildBattleSourceFieldEffects.IsToxic(status)) CeruleanSynchronize(catalog, targetSide, WildBattleStatus.Poison);
        }

        private void SourceCalledFieldHit(PokemonCatalog catalog, WildBattleSide side, WildBattleSide targetSide, int slot,
            PokemonMoveDefinition move, string purpose)
        {
            ElementType type = ScalarType(move.Type); int multiplier = 1;
            if (move.Effect == "EFFECT_WEATHER_BALL")
            {
                (type, multiplier) = WildBattleSourceFieldEffects.WeatherBall(SourceCalledRain,
                    SourceCalledSandstorm, SourceCalledSun, (State.CalledField?.HailTurnsRemaining ?? 0) > 0, SourceCalledWeatherSuppressed);
                invocationEffectiveType = "TYPE_" + type.ToString().ToUpperInvariant();
            }
            bool hit;
            if (move.Effect == "EFFECT_THUNDER" && SourceCalledRain && !SourceCalledWeatherSuppressed)
                hit = ScalarAccuracy(side, targetSide, move, purpose, type);
            else hit = ScalarAccuracy(side, targetSide, move.Effect == "EFFECT_THUNDER" && SourceCalledSun && !SourceCalledWeatherSuppressed
                ? move with { Accuracy = 50 } : move, purpose, type);
            SpendEarlyPartyPp(side, targetSide, slot, move);
            if (!hit) { SourceGenericMiss(side, targetSide, move); return; }
            if (move.Effect == "EFFECT_BRICK_BREAK") SourceCalledBreakScreens(side);
            var effectiveMove = move.Effect == "EFFECT_WEATHER_BALL" ? move with { Type = invocationEffectiveType! } : move;
            if (Route11WaterAbsorb(side, targetSide, move, move.Effect == "EFFECT_WEATHER_BALL" ? invocationEffectiveType : null) ||
                SsAnneFlashFire(side, targetSide, effectiveMove)) return;
            bool critical = SourceGenericCritical(Get(side), Get(targetSide), move, purpose);
            int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
            int damage = ScalarDamage(catalog, side, targetSide, move, critical, percent,
                type: move.Effect == "EFFECT_WEATHER_BALL" ? type : null, multiplier: multiplier);
            damage = ScalarAdjust(targetSide, move, damage, purpose);
            ScalarApplyDamage(catalog, side, targetSide, move, damage, critical, percent, type);
            ScalarReportType(catalog, side, targetSide, move, type);
            int chance = move.SecondaryEffectChance * (WildBattleEffective.Ability(Get(side)) == "ABILITY_SERENE_GRACE" ? 2 : 1);
            int roll = Draw($"{purpose}.secondary-effect", 100) % 100;
            if (roll <= chance && damage > 0)
            {
                switch (move.Effect)
                {
                    case "EFFECT_FREEZE_HIT": SourceCalledInflictStatus(catalog, targetSide, WildBattleStatus.Freeze); break;
                    case "EFFECT_POISON_FANG": SourceCalledInflictStatus(catalog, targetSide, WildBattleSourceFieldEffects.ToxicPoison); break;
                    case "EFFECT_THUNDER": InflictStatus(catalog, targetSide, WildBattleStatus.Paralysis, false); break;
                    case "EFFECT_THAW_HIT": InflictStatus(catalog, targetSide, WildBattleStatus.Burn, false); break;
                }
            }
            ScalarFinish(catalog, side, targetSide, move, purpose, damage, type);
        }

        private void SourceCalledBreakScreens(WildBattleSide actor)
        {
            var side = Other(actor); var field = State.CalledField;
            int reflect = side == WildBattleSide.Player ? State.PlayerReflectTurnsRemaining : State.OpponentReflectTurnsRemaining;
            int light = side == WildBattleSide.Player ? field?.PlayerLightScreenTurnsRemaining ?? 0 : field?.OpponentLightScreenTurnsRemaining ?? 0;
            if (reflect == 0 && light == 0) return;
            State = side == WildBattleSide.Player ? State with { PlayerReflectTurnsRemaining = 0,
                CalledField = field is null ? null : field with { PlayerLightScreenTurnsRemaining = 0 } } :
                State with { OpponentReflectTurnsRemaining = 0, CalledField = field is null ? null : field with { OpponentLightScreenTurnsRemaining = 0 } };
            if (reflect > 0) Add(n => new WildBattleMtMoonVolatileEvent(n, State.Turn, side, "reflect", reflect, 0, "brick-break"));
            if (light > 0) Add(n => new WildBattleMtMoonVolatileEvent(n, State.Turn, side, "light-screen", light, 0, "brick-break"));
        }

        // B01 deliberately does not call end-turn commands. A complete turn successor must
        // run these at the pinned command positions, alongside existing rain/sand/Reflect.
        internal void CalledFieldCounters()
        {
            if (State.CalledField is not { } field) return;
            int Tick(WildBattleSide side, string name, int before)
            {
                int after = WildBattleSourceFieldEffects.TickTimer(before);
                if (before > 0) Add(n => new WildBattleMtMoonVolatileEvent(n, State.Turn, side, name, before, after, after == 0 ? "expired" : "countdown"));
                return after;
            }
            // Each source field command visits player then opponent, before weather.
            var next = field with {
                PlayerLightScreenTurnsRemaining = Tick(WildBattleSide.Player, "light-screen", field.PlayerLightScreenTurnsRemaining),
                OpponentLightScreenTurnsRemaining = Tick(WildBattleSide.Opponent, "light-screen", field.OpponentLightScreenTurnsRemaining),
                PlayerMistTurnsRemaining = Tick(WildBattleSide.Player, "mist", field.PlayerMistTurnsRemaining),
                OpponentMistTurnsRemaining = Tick(WildBattleSide.Opponent, "mist", field.OpponentMistTurnsRemaining),
                PlayerSafeguardTurnsRemaining = Tick(WildBattleSide.Player, "safeguard", field.PlayerSafeguardTurnsRemaining),
                OpponentSafeguardTurnsRemaining = Tick(WildBattleSide.Opponent, "safeguard", field.OpponentSafeguardTurnsRemaining),
                SunTurnsRemaining = Tick(WildBattleSide.Player, "sun", field.SunTurnsRemaining),
                HailTurnsRemaining = Tick(WildBattleSide.Player, "hail", field.HailTurnsRemaining) };
            State = State with { CalledField = next };
        }
        internal void CalledFieldHailDamage(PokemonCatalog catalog, WildBattleSide side) =>
            new SinglesEndTurnBridge(this).WeatherDamage(catalog, new((int)side), "hail");
        internal void CalledFieldToxicResidual(WildBattleSide side) =>
            new SinglesEndTurnBridge(this).ToxicDamage(new((int)side));
    }
}
