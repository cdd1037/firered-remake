using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public enum SourcePermanentWeatherKind { Rain, Sandstorm, Sun }

/// <summary>Ability-created weather has no duration. Temporary move timers must all be zero.</summary>
public sealed record SourcePermanentWeather(SourcePermanentWeatherKind Kind)
{
    public const string Version = "firered-source-permanent-weather-v1";
    public string RulesVersion { get; init; } = Version;
}

/// <summary>One source recipe and field query contract, shared by singles owners and canonical slots.</summary>
public static class SourceWeatherRules
{
    public static bool HasRain(SourceFieldRegisters f) => f.RainTurnsRemaining > 0 || f.PermanentWeather?.Kind == SourcePermanentWeatherKind.Rain;
    public static bool HasSandstorm(SourceFieldRegisters f) => f.SandstormTurnsRemaining > 0 || f.PermanentWeather?.Kind == SourcePermanentWeatherKind.Sandstorm;
    public static bool HasSun(SourceFieldRegisters f) => f.SunTurnsRemaining > 0 || f.PermanentWeather?.Kind == SourcePermanentWeatherKind.Sun;
    public static bool HasRain(WildBattleState s) => s.RainTurnsRemaining > 0 || s.PermanentWeather?.Kind == SourcePermanentWeatherKind.Rain;
    public static bool HasSandstorm(WildBattleState s) => s.SandstormTurnsRemaining > 0 || s.PermanentWeather?.Kind == SourcePermanentWeatherKind.Sandstorm;
    public static bool HasSun(WildBattleState s) => s.CalledField?.SunTurnsRemaining > 0 || s.PermanentWeather?.Kind == SourcePermanentWeatherKind.Sun;

    // CHECK_ON_FIELD tests hp != 0; FIELD_SPORT (damage's WEATHER_HAS_EFFECT2) does not.
    // Neither original query checks gAbsentBattlerFlags. Retained fainted slot bodies matter.
    public static bool IsSuppressed(IEnumerable<WildBattleCombatant> battlers, bool includeFainted = false) =>
        battlers.Any(m => (includeFainted || m.Pokemon.CurrentHp > 0) &&
            WildBattleEffective.Ability(m) is "ABILITY_CLOUD_NINE" or "ABILITY_AIR_LOCK");
    public static bool IsSuppressed(SourceBattleFrame frame, bool includeFainted = false) =>
        IsSuppressed(frame.Slots.Take(frame.BattlerCount).Where(s => s.Body is not null).Select(s => s.Body!), includeFainted);

    // BtlController_EmitMoveAnimation uses WEATHER_HAS_EFFECT2, including
    // fainted bodies. Preserve the controller flags, not Weather Ball's type
    // result (whose live-only suppression and weather precedence differ).
    internal static ushort AnimationFlags(SourceBattleFrame frame)
    {
        if(IsSuppressed(frame,includeFainted:true))return 0;
        var field=frame.Field;
        int flags=(HasRain(field)?1:0)|(HasSandstorm(field)?8:0)|(HasSun(field)?32:0)|(field.HailTurnsRemaining>0?128:0);
        flags|=field.PermanentWeather?.Kind switch{SourcePermanentWeatherKind.Rain=>4,SourcePermanentWeatherKind.Sandstorm=>16,SourcePermanentWeatherKind.Sun=>64,_=>0};
        return (ushort)flags;
    }

    public static SourcePermanentWeatherKind? EntryWeather(string ability) => ability switch {
        "ABILITY_DRIZZLE" => SourcePermanentWeatherKind.Rain,
        "ABILITY_SAND_STREAM" => SourcePermanentWeatherKind.Sandstorm,
        "ABILITY_DROUGHT" => SourcePermanentWeatherKind.Sun, _ => null };

    public static SourceFieldRegisters ApplyEntry(SourceFieldRegisters field, string ability)
    {
        var kind = EntryWeather(ability);
        if (kind is null || field.PermanentWeather?.Kind == kind) return field;
        return field with { PermanentWeather = new(kind.Value), RainTurnsRemaining = 0,
            SandstormTurnsRemaining = 0, SunTurnsRemaining = 0, HailTurnsRemaining = 0 };
    }

    public static int RainDishRecovery(string ability, int hp, int maximumHp, bool rain, bool suppressed) =>
        ability != "ABILITY_RAIN_DISH" || hp <= 0 || hp >= maximumHp || !rain || suppressed ? 0 :
            Math.Min(maximumHp - hp, Math.Max(1, maximumHp / 16));

    public static string? Validate(SourcePermanentWeather? weather, string? profile,
        int rain, int sandstorm, int sun, int hail)
    {
        if (weather is null) return null;
        if (profile is not (BattleRulesProfile.CanonicalSourceSinglesVersion or SourceBattleKernel.Version)) return "source_weather_profile";
        if (weather.RulesVersion != SourcePermanentWeather.Version || !Enum.IsDefined(weather.Kind)) return "source_weather_version";
        if (rain != 0 || sandstorm != 0 || sun != 0 || hail != 0) return "source_weather_competing_duration";
        return null;
    }
    internal static string? Validate(WildBattleState state) => Validate(state.PermanentWeather, state.SourceMechanicsRulesVersion,
        state.RainTurnsRemaining, state.SandstormTurnsRemaining, state.CalledField?.SunTurnsRemaining ?? 0, state.CalledField?.HailTurnsRemaining ?? 0);
}
