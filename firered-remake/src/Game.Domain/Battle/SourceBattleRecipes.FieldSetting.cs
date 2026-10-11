using Game.Domain.Pokemon;

namespace Game.Domain.Battle;

internal abstract partial class SourceRecipeContext
{
    internal static bool HasFieldSettingRecipe(string effect) => effect is
        "EFFECT_REFLECT" or "EFFECT_LIGHT_SCREEN" or "EFFECT_MIST" or "EFFECT_SAFEGUARD" or
        "EFFECT_RAIN_DANCE" or "EFFECT_SANDSTORM" or "EFFECT_SUNNY_DAY" or "EFFECT_HAIL" or
        "EFFECT_MUD_SPORT" or "EFFECT_WATER_SPORT";

    // ABILITYEFFECT_FIELD_SPORT scans all retained battler statuses. Neither HP,
    // absence, side nor weather suppression participates in this query.
    internal bool FieldMudSport => ActiveSlots.Any(slot => Get(slot).MudSport);
    internal bool FieldWaterSport => ActiveSlots.Any(slot => Get(slot).WaterSport);

    internal abstract int FieldSettingTimer(BattleSide side, string effect);
    internal abstract void SetFieldSettingTimer(BattleSide side, string effect, int value);
    internal abstract SourceFieldRegisters SettingWeather { get; }
    internal abstract void ReplaceSettingWeather(SourceFieldRegisters weather);
    internal virtual void SettingWeatherChanged() { }

    internal void ExecuteFieldSetting(BattlerSlot actor, int slot, PokemonMoveDefinition move, ref bool handled)
    {
        if (!HasFieldSettingRecipe(move.Effect)) return;
        if (Invocation is not { } invocation) throw new SourceRecipeUnsupported("source_field_setting_invocation");
        handled = true;
        SpendEarlyPartyPp(actor, invocation.Target, slot, move);
        ApplyFieldSetting(actor, invocation.Target, move);
    }

    // Cmd_setreflect/setlightscreen/setmist/setsafeguard and setrain/setsandstorm/setsunny/sethail.
    // Shared by canonical slots and the existing source singles scripts. Failed settings still
    // consume the caller's real PP command; no accuracy, critical, damage or duration RNG exists.
    internal void ApplyFieldSetting(BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move)
    {
        if (move.Effect is "EFFECT_MUD_SPORT" or "EFFECT_WATER_SPORT")
        {
            bool mud = move.Effect == "EFFECT_MUD_SPORT";
            var body = Get(actor);
            string sport = mud ? "mud-sport" : "water-sport";
            if (mud ? body.MudSport : body.WaterSport)
            {
                SourceCalledProtectionFailure(actor, target, move.Id, "already-" + sport);
                return;
            }
            Set(actor, mud ? body with { MudSport = true } : body with { WaterSport = true });
            Add(n => new SourceMtMoonVolatileEvent(n, Turn, actor, sport, 0, 1, "move"));
            return;
        }
        string effect = move.Effect switch {
            "EFFECT_REFLECT" => "reflect", "EFFECT_LIGHT_SCREEN" => "light-screen", "EFFECT_MIST" => "mist",
            "EFFECT_SAFEGUARD" => "safeguard", "EFFECT_RAIN_DANCE" => "rain", "EFFECT_SANDSTORM" => "sandstorm",
            "EFFECT_SUNNY_DAY" => "sun", "EFFECT_HAIL" => "hail",
            _ => throw new SourceRecipeUnsupported("source_field_setting_recipe") };
        bool weather = effect is "rain" or "sandstorm" or "sun" or "hail";
        var field = SettingWeather;
        bool exists = weather ? effect switch {
            "rain" => SourceWeatherRules.HasRain(field), "sandstorm" => SourceWeatherRules.HasSandstorm(field),
            "sun" => SourceWeatherRules.HasSun(field), _ => field.HailTurnsRemaining > 0 }
            : FieldSettingTimer(actor.Side, effect) > 0;
        if (exists)
        {
            SourceCalledProtectionFailure(actor, target, move.Id, effect == "reflect" ? "already-reflecting" : "already-" + effect);
            return;
        }
        if (weather)
            ReplaceSettingWeather(field with { PermanentWeather = null, RainTurnsRemaining = effect == "rain" ? 5 : 0,
                SandstormTurnsRemaining = effect == "sandstorm" ? 5 : 0, SunTurnsRemaining = effect == "sun" ? 5 : 0,
                HailTurnsRemaining = effect == "hail" ? 5 : 0 });
        else SetFieldSettingTimer(actor.Side, effect, 5);
        Add(n => new SourceMtMoonVolatileEvent(n, Turn, actor, effect, 0, 5, "move"));
        if (weather) SettingWeatherChanged();
    }
}
