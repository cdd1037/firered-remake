using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

public enum SourceBattleCapability { CanonicalSpecies, CanonicalWeights, SourceMoveRecipes, ExpandedDamageAbilities, SourceVolatiles, SourceHeldItems }

/// <summary>Engine capability selection is distinct from source authenticity and story availability.
/// Only reviewed profiles are constructible. This singles profile does not claim every ability is implemented.</summary>
public sealed class BattleRulesProfile
{
    public const string CanonicalSourceSinglesVersion = "firered-canonical-source-singles-v1";
    public static BattleRulesProfile CanonicalSourceSingles { get; } = new(CanonicalSourceSinglesVersion,
        [SourceBattleCapability.CanonicalSpecies, SourceBattleCapability.CanonicalWeights,
         SourceBattleCapability.SourceMoveRecipes, SourceBattleCapability.ExpandedDamageAbilities,
         SourceBattleCapability.SourceVolatiles, SourceBattleCapability.SourceHeldItems]);
    public string Version { get; }
    public int ActiveBattlersPerSide => 1;
    private ImmutableHashSet<SourceBattleCapability> Capabilities { get; }
    private BattleRulesProfile(string version, ImmutableHashSet<SourceBattleCapability> capabilities)
    { Version = version; Capabilities = capabilities; }
    public bool Supports(SourceBattleCapability capability) => Capabilities.Contains(capability);
}

/// <summary>Current permanent/campaign membership. A full mechanics catalog does not enlarge it.</summary>
public sealed class CampaignAdmissionPolicy
{
    public static CampaignAdmissionPolicy CurrentFuchsia { get; } = new(PokemonFactory.FuchsiaSupportedSpecies);
    private readonly ImmutableHashSet<SpeciesId> admitted;
    private CampaignAdmissionPolicy(ImmutableArray<SpeciesId> species) => admitted = species.ToImmutableHashSet();
    public bool Admits(SpeciesId species) => admitted.Contains(species);
    public string? ValidatePokemon(PokemonInstance? pokemon) => pokemon is null ? "campaign_pokemon_missing" :
        !Admits(pokemon.Species) ? "campaign_species_unsupported" : null;
}

public static class SourceBattleAdmission
{
    internal static bool IsCurrentCampaignCatalog(PokemonCatalog? catalog) => catalog is not null &&
        catalog.SourceCommit == PokemonFactory.SourceCommit && catalog.Species.Count == PokemonFactory.FuchsiaSupportedSpecies.Length &&
        catalog.Moves.Count == 354 && catalog.Species.Keys.All(CampaignAdmissionPolicy.CurrentFuchsia.Admits);

    internal static bool HasCapability(PokemonCatalog? catalog, SourceBattleCapability capability) =>
        SourceMechanicsCatalog.For(catalog) is not null && BattleRulesProfile.CanonicalSourceSingles.Supports(capability);

    internal static bool IsCanonicalSourceState(PokemonCatalog? catalog, WildBattleState state) =>
        state.SourceMechanicsRulesVersion == BattleRulesProfile.CanonicalSourceSinglesVersion &&
        state.RulesVersion == WildBattleRules.FuchsiaRulesVersion &&
        HasCapability(catalog, SourceBattleCapability.CanonicalSpecies);

    internal static string? ValidateRulesProfile(PokemonCatalog? catalog, WildBattleState state) =>
        state.SourceFocusBandedMask != 0 && (state.SourceFocusBandedMask > 3 || !IsCanonicalSourceState(catalog, state)) ? "source_focus_band_state" :
        SourceTraceForecastRules.Validate(state.Player, state.SourceMechanicsRulesVersion) ??
        SourceTraceForecastRules.Validate(state.Opponent, state.SourceMechanicsRulesVersion) ?? SourceWeatherRules.Validate(state) ?? (SourceMechanicsCatalog.For(catalog) is not null || state.SourceMechanicsRulesVersion is not null
            ? IsCanonicalSourceState(catalog, state) ? null : "source_mechanics_rules_profile" : null);

    internal static bool SupportsHeldItem(PokemonCatalog? catalog, string item) =>
        FuchsiaBattleScope.SupportsHeldItem(item) ||
        HasCapability(catalog, SourceBattleCapability.SourceHeldItems) && (SourceBattleHeldItems.OrderEffect(item) is not null || item is "ITEM_CHOICE_BAND" or "ITEM_FOCUS_BAND");

    internal static bool SupportsAbility(PokemonCatalog catalog, string ability) =>
        SourceMechanicsCatalog.For(catalog) is { } proof && proof.Abilities.ContainsKey(ability) &&
        (FuchsiaBattleScope.SupportsAbility(ability) || ability is "ABILITY_HUGE_POWER" or "ABILITY_PURE_POWER" or
            "ABILITY_HUSTLE" or "ABILITY_MARVEL_SCALE" or "ABILITY_PRESSURE" or
            "ABILITY_VOLT_ABSORB" or "ABILITY_ROUGH_SKIN" or "ABILITY_FLAME_BODY" or "ABILITY_COLOR_CHANGE" or
            "ABILITY_TRUANT" or "ABILITY_SPEED_BOOST" or "ABILITY_SHADOW_TAG" or "ABILITY_SUCTION_CUPS" or
            "ABILITY_DRIZZLE" or "ABILITY_DROUGHT" or "ABILITY_SAND_STREAM" or "ABILITY_RAIN_DISH" or
            "ABILITY_CLOUD_NINE" or "ABILITY_AIR_LOCK" or "ABILITY_TRACE" or "ABILITY_FORECAST");

    internal static string? ValidatePokemonCapabilities(PokemonCatalog catalog, PokemonInstance pokemon)
    {
        if (!HasCapability(catalog, SourceBattleCapability.CanonicalSpecies)) return null;
        if (!SupportsAbility(catalog, pokemon.AbilityId)) return "source_mechanics_ability_not_implemented";
        if (pokemon.Moves.Any(m => !WildBattleRules.IsCalledMoveEffectSupported(catalog.Moves[m.Move])))
            return "source_mechanics_move_not_implemented";
        return null;
    }

    public static string? ValidateEntry(SourceMechanicsCatalog catalog, BattleRulesProfile profile,
        ImmutableArray<WildBattleCombatant> players, int activeIndex, WildBattleCombatant opponent)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!ReferenceEquals(profile, BattleRulesProfile.CanonicalSourceSingles)) return "source_mechanics_rules_profile";
        if (players.IsDefaultOrEmpty || players.Length > 6 || activeIndex < 0 || activeIndex >= players.Length || opponent?.Pokemon is null)
            return "source_mechanics_party";
        foreach (var mon in players.Append(opponent))
        {
            if (mon?.Pokemon is null || SourcePokemonValidator.ValidatePermanent(catalog, mon.Pokemon) is not null)
                return "source_mechanics_pokemon";
            if (ValidatePokemonCapabilities(catalog.Catalog, mon.Pokemon) is { } error) return error;
        }
        return null;
    }
}
