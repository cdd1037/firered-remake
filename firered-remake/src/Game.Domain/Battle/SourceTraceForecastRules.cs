using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

// Source gBattleMonForms indices, not permanent species IDs.
public enum SourceCastformForm { Normal, Sunny, Rainy, Snowy }
public sealed record SourceForecastChange(string Type, SourceCastformForm Form);

/// <summary>Original Gen III ability queries shared by singles and canonical position consumers.
/// These queries do not admit these abilities to the four-action owner.</summary>
public static class SourceTraceForecastRules
{
    /// <summary>Index is the source battler position (player=0, foe=1, partners=2/3).
    /// The source checks HP and ability only, including retained absent bodies.</summary>
    public static int? TraceTarget(IReadOnlyList<WildBattleCombatant?> positions, int tracer, Func<ushort> random)
    {
        if (positions.Count is not (2 or 4) || tracer < 0 || tracer >= positions.Count)
            throw new ArgumentOutOfRangeException(nameof(tracer));
        int first = (tracer ^ 1) & 1, second = first + 2;
        bool Eligible(int i) => positions[i] is { } mon && mon.Pokemon.CurrentHp != 0 && WildBattleEffective.Ability(mon) != "ABILITY_NONE";
        bool left = Eligible(first), right = positions.Count == 4 && Eligible(second);
        if (left && right) return ((random() & 1) * 2) | first;
        return left ? first : right ? second : null;
    }

    public static BattlerSlot? TraceTarget(SourceBattleFrame frame, BattlerSlot tracer, Func<ushort> random)
    {
        var target = TraceTarget(frame.Slots.Take(frame.BattlerCount).Select(s => s.Body).ToArray(), tracer.Value, random);
        return target is { } value ? new BattlerSlot(value) : null;
    }

    /// <summary>Port of CastformDataTypeChange. Both-type membership checks and sequential
    /// sun/rain/hail branches intentionally retain the original behavior.</summary>
    public static SourceForecastChange? ForecastChange(WildBattleCombatant mon, bool weatherHasEffect,
        bool sun, bool rain, bool hail)
    {
        if (mon.Pokemon.Species.Value != "pret:SPECIES_CASTFORM" || WildBattleEffective.Ability(mon) != "ABILITY_FORECAST" || mon.Pokemon.CurrentHp == 0) return null;
        string a = mon.CalledOverlay?.Type1 ?? "TYPE_NORMAL", b = mon.CalledOverlay?.Type2 ?? "TYPE_NORMAL";
        SourceForecastChange? change = null;
        bool Has(string type) => a == type || b == type;
        void Set(string type, SourceCastformForm form) { a = b = type; change = new(type, form); }
        if (!weatherHasEffect) return !Has("TYPE_NORMAL") ? new("TYPE_NORMAL", SourceCastformForm.Normal) : null;
        if (!sun && !rain && !hail && !Has("TYPE_NORMAL")) Set("TYPE_NORMAL", SourceCastformForm.Normal);
        if (sun && !Has("TYPE_FIRE")) Set("TYPE_FIRE", SourceCastformForm.Sunny);
        if (rain && !Has("TYPE_WATER")) Set("TYPE_WATER", SourceCastformForm.Rainy);
        if (hail && !Has("TYPE_ICE")) Set("TYPE_ICE", SourceCastformForm.Snowy);
        return change;
    }

    internal static string? Validate(WildBattleCombatant? mon, string? profile)
    {
        if (mon is null) return null;
        if ((mon.SourceTracePending || mon.CalledOverlay?.CastformForm is not null) && profile != BattleRulesProfile.CanonicalSourceSinglesVersion)
            return "source_trace_forecast_profile";
        if (mon.SourceTracePending && (mon.Pokemon?.CurrentHp == 0 ||
            (mon.Transform?.OriginalPokemon?.AbilityId ?? mon.Pokemon?.AbilityId) != "ABILITY_TRACE")) return "source_trace_pending_identity";
        if (mon.CalledOverlay?.CastformForm is { } form && (!Enum.IsDefined(form) || mon.Pokemon?.Species.Value != "pret:SPECIES_CASTFORM" || mon.CalledOverlay.Type1 is null))
            return "source_forecast_form";
        return null;
    }
}
