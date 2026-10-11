using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

// The script's dynamic-power commands, including their original integer truncation.
// A zero Return/Frustration result means CalculateBaseDamage uses metadata power 1.
internal static class SourceDynamicPowerRules
{
    internal static int Friendship(int friendship, bool frustration) => Math.Max(1, 10 * (frustration ? 255 - friendship : friendship) / 25);
    internal static int Flail(int hp, int maxHp)
    {
        int fraction = hp * 48 / maxHp;
        return fraction <= 1 ? 200 : fraction <= 4 ? 150 : fraction <= 9 ? 100 : fraction <= 16 ? 80 : fraction <= 32 ? 40 : 20;
    }
    internal static int Eruption(int hp, int maxHp, int power) => Math.Max(1, hp * power / maxHp);
    internal static int Weight(int hectograms) => hectograms < 100 ? 20 : hectograms < 250 ? 40 : hectograms < 500 ? 60 : hectograms < 1000 ? 80 : hectograms < 2000 ? 100 : 120;
}

internal abstract partial class SourceRecipeContext
{
    internal static bool HasDynamicPowerRecipe(string effect) => effect is
        "EFFECT_RETURN" or "EFFECT_FRUSTRATION" or "EFFECT_FLAIL" or "EFFECT_ERUPTION" or "EFFECT_LOW_KICK";

    private int? preparedDynamicPower;
    // Flail and Eruption enter these commands before attackcanceler. Each real action
    // resets the register; MOVEEND_NEXT_TARGET re-enters Eruption without resetting it.
    internal void PrepareDynamicPower(BattlerSlot actor, PokemonMoveDefinition move)
    {
        var mon = Get(actor).Pokemon;
        preparedDynamicPower = move.Effect switch {
            "EFFECT_FLAIL" => SourceDynamicPowerRules.Flail(mon.CurrentHp, mon.Stats.MaxHp),
            "EFFECT_ERUPTION" => SourceDynamicPowerRules.Eruption(mon.CurrentHp, mon.Stats.MaxHp, move.Power),
            _ => null };
    }

    internal void ExecuteDynamicPower(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, int slot,
        PokemonMoveDefinition move, string purpose)
    {
        if (!HasDynamicPowerRecipe(move.Effect) || !MetronomeSourceMoveDefinition.Matches(move))
            throw new SourceRecipeUnsupported("source_dynamic_power_definition");
        int power = preparedDynamicPower ?? 0;
        bool weight = move.Effect == "EFFECT_LOW_KICK";
        if (weight)
        {
            // EffectLowKick: attackstring, ppreduce, weight, accuracy. Weight uses the
            // target's live battle species, including the existing singles Transform view.
            SpendEarlyPartyPp(actor, target, slot, move);
            var mechanics = SourceMechanicsCatalog.For(catalog) ?? throw new SourceRecipeUnsupported("source_dynamic_power_catalog");
            power = SourceDynamicPowerRules.Weight(mechanics.SpeciesMechanics[Get(target).Pokemon.Species].WeightHectograms);
        }
        bool hit = FixedDamageAccuracy(actor, target, move, purpose);
        if (hit && move.Effect is "EFFECT_RETURN" or "EFFECT_FRUSTRATION")
            power = SourceDynamicPowerRules.Friendship(Get(actor).Pokemon.Friendship, move.Effect == "EFFECT_FRUSTRATION");
        if (!weight) SpendEarlyPartyPp(actor, target, slot, move);
        if (!hit) { SourceGenericMiss(actor, target, move); return; }
        if (Route11WaterAbsorb(actor, target, move) || SsAnneFlashFire(actor, target, move)) return;
        if (power == 0) throw new SourceRecipeUnsupported("source_dynamic_power_unprepared");
        bool critical = SourceGenericCritical(Get(actor), Get(target), move, purpose);
        int percent = 100 - Draw($"{purpose}.damage", 16) % 16;
        int rawDamage = SourceGenericDamage(catalog, Get(actor), Get(target), target, move, critical, percent, power);
        int damage = FixedDamageAdjust(target, move, rawDamage, purpose);
        int lost = SourceGenericApplyDamage(actor, target, move, damage, critical, percent);
        if (critical && lost == 0 && rawDamage > 0 && !FixedDamageZeroCriticalPrinted(target))
            Add(n => new SourceCriticalHitEvent(n, Turn, actor, target));
        DynamicPowerReportType(catalog, actor, target, move);
        SourceGenericSecondary(catalog, actor, target, move, purpose, rawDamage);
        DynamicPowerFinish(catalog, actor, target, move, purpose, lost);
    }

    internal virtual void DynamicPowerReportType(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move) =>
        SourceGenericEffectiveness(catalog, actor, target, move);
    internal virtual void DynamicPowerFinish(PokemonCatalog catalog, BattlerSlot actor, BattlerSlot target, PokemonMoveDefinition move, string purpose, int lost)
    {
        if (Get(target).Pokemon.CurrentHp == 0) ForestFaint(target);
        if (lost > 0) MtMoonRageOnDamage(actor, target, move.Id);
        SourceGenericContact(catalog, actor, target, move, purpose, lost);
        ForestOutcome(true);
    }
}
