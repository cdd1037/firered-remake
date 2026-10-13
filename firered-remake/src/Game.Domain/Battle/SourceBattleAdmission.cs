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

    public static bool IsSafariOwnedCampaign(PokemonCatalog? catalog, WildBattleState state) =>
        state.SourceCampaignRulesVersion == SafariOwnedPokemonCatalog.Profile && IsOwnedCampaign(catalog, state);

    public static bool IsOwnedCampaign(PokemonCatalog? catalog, WildBattleState state) =>
        IsCanonicalSourceState(catalog, state) &&
        OwnedBattlePokemonCatalog.ForMechanics(catalog, state.SourceCampaignRulesVersion) is not null;

    public static bool IsOwnedCampaign(OwnedBattlePokemonCatalog owned, WildBattleState state) =>
        (state.SourceCampaignRulesVersion == owned.RulesVersion ||
         state.SourceCampaignRulesVersion == SeviiRubyOpponentPokemonCatalog.Profile &&
         SeviiRubyOpponentPokemonCatalog.ForMechanics(owned.Mechanics.Catalog) is { } ruby &&
         ReferenceEquals(ruby.Owned.Catalog, owned.Catalog) ||
         state.SourceCampaignRulesVersion == PokemonLeagueOpponentPokemonCatalog.Profile &&
         PokemonLeagueOpponentPokemonCatalog.ForMechanics(owned.Mechanics.Catalog) is { } league &&
         ReferenceEquals(league.Owned.Catalog, owned.Catalog) ||
         state.SourceCampaignRulesVersion == VictoryRoadOpponentPokemonCatalog.Profile &&
         VictoryRoadOpponentPokemonCatalog.ForMechanics(owned.Mechanics.Catalog) is { } victory &&
         ReferenceEquals(victory.Owned.Catalog, owned.Catalog) ||
         state.SourceCampaignRulesVersion == CinnabarOpponentPokemonCatalog.Profile &&
         CinnabarOpponentPokemonCatalog.ForMechanics(owned.Mechanics.Catalog) is { } cinnabar &&
         ReferenceEquals(cinnabar.Owned.Catalog, owned.Catalog) ||
         state.SourceCampaignRulesVersion == SaffronGymOpponentPokemonCatalog.Profile &&
         SaffronGymOpponentPokemonCatalog.ForMechanics(owned.Mechanics.Catalog) is { } saffron &&
         ReferenceEquals(saffron.Owned.Catalog, owned.Catalog)) && IsOwnedCampaign(owned.Mechanics.Catalog, state);

    internal static string? ValidateCampaignMembership(PokemonCatalog? catalog, WildBattleState state)
    {
        if (state.SourceCampaignRulesVersion is null) return null;
        if (!IsOwnedCampaign(catalog, state)) return "source_campaign_admission";
        var owned = OwnedBattlePokemonCatalog.ForMechanics(catalog, state.SourceCampaignRulesVersion)!;
        if (state.CalledParty is not { } party || party.Player.IsDefaultOrEmpty || party.Opponent.IsDefaultOrEmpty)
            return "source_campaign_actual_parties";
        if (party.Player.Any(m => m?.Pokemon is null || !owned.Catalog.Species.ContainsKey(m.Pokemon.Species)) ||
            party.Opponent.Any(m => m?.Pokemon is null || !(party.BattleKind == WildBattleCalledBattleKind.Trainer
                ? owned.AdmitsTrainerOpponent(m.Pokemon.Species, state.SourceCampaignRulesVersion) : owned.Catalog.Species.ContainsKey(m.Pokemon.Species))))
            return "source_campaign_species_unsupported";
        return null;
    }

    internal static string? ValidateOwnedEntry(OwnedBattlePokemonCatalog owned,
        ImmutableArray<WildBattleCombatant> players, int activeIndex)
    {
        if (owned is null || !ReferenceEquals(OwnedBattlePokemonCatalog.For(owned.Catalog), owned)) return "source_campaign_admission";
        if (players.IsDefaultOrEmpty || players.Length > 6 || activeIndex < 0 || activeIndex >= players.Length)
            return "source_campaign_party";
        foreach (var member in players)
        {
            if (member?.Pokemon is null || !owned.Catalog.Species.ContainsKey(member.Pokemon.Species))
                return "source_campaign_species_unsupported";
            if (SourcePokemonValidator.ValidatePermanent(owned.Mechanics, member.Pokemon) is not null)
                return "source_mechanics_pokemon";
            if (ValidatePokemonCapabilities(owned.Mechanics.Catalog, member.Pokemon) is { } error) return error;
        }
        return null;
    }

    internal static bool HasCapability(PokemonCatalog? catalog, SourceBattleCapability capability) =>
        SourceMechanicsCatalog.For(catalog) is not null && BattleRulesProfile.CanonicalSourceSingles.Supports(capability);

    internal static bool IsCanonicalSourceState(PokemonCatalog? catalog, WildBattleState state) =>
        state.SourceMechanicsRulesVersion == BattleRulesProfile.CanonicalSourceSinglesVersion &&
        state.RulesVersion == WildBattleRules.FuchsiaRulesVersion &&
        HasCapability(catalog, SourceBattleCapability.CanonicalSpecies);

    internal static string? ValidateRulesProfile(PokemonCatalog? catalog, WildBattleState state) =>
        ValidateCampaignMembership(catalog, state) ?? (
        (!SourceBattleMoneyRules.ValidMultiplier(state.SourceMoneyMultiplier) ||
            state.SourceMoneyMultiplier is not null && !IsCanonicalSourceState(catalog, state)) ? "source_money_multiplier" :
        (state.SourcePlayerHitReceipt is not null || state.SourceOpponentHitReceipt is not null) &&
            (!IsCanonicalSourceState(catalog, state) || !SourceHitItemRules.Valid(state.SourcePlayerHitReceipt) || !SourceHitItemRules.Valid(state.SourceOpponentHitReceipt)) ? "source_hit_item_receipt" :
        state.SourceFocusBandedMask != 0 && (state.SourceFocusBandedMask > 3 || !IsCanonicalSourceState(catalog, state)) ? "source_focus_band_state" :
        SourceObedienceRules.Validate(state) ??
        SourceTraceForecastRules.Validate(state.Player, state.SourceMechanicsRulesVersion) ??
        SourceTraceForecastRules.Validate(state.Opponent, state.SourceMechanicsRulesVersion) ?? SourceWeatherRules.Validate(state) ?? (SourceMechanicsCatalog.For(catalog) is not null || state.SourceMechanicsRulesVersion is not null
            ? IsCanonicalSourceState(catalog, state) ? null : "source_mechanics_rules_profile" : null));

    internal static bool SupportsHeldItem(PokemonCatalog? catalog, string item) =>
        FuchsiaBattleScope.SupportsHeldItem(item) ||
        HasCapability(catalog, SourceBattleCapability.SourceHeldItems) && (SourceHeldItemCatalog.Supported(catalog, item) || SourceBattleHeldItems.OrderEffect(item) is not null || item is "ITEM_CHOICE_BAND" or "ITEM_FOCUS_BAND" or "ITEM_KINGS_ROCK" or "ITEM_SHELL_BELL");

    internal static bool SupportsAbility(PokemonCatalog catalog, string ability) =>
        SourceMechanicsCatalog.For(catalog) is { } proof && proof.Abilities.ContainsKey(ability) &&
        (FuchsiaBattleScope.SupportsAbility(ability) || ability is "ABILITY_HUGE_POWER" or "ABILITY_PURE_POWER" or
            "ABILITY_WONDER_GUARD" or "ABILITY_SERENE_GRACE" or "ABILITY_HUSTLE" or "ABILITY_MARVEL_SCALE" or "ABILITY_PRESSURE" or
            "ABILITY_VOLT_ABSORB" or "ABILITY_ROUGH_SKIN" or "ABILITY_FLAME_BODY" or "ABILITY_COLOR_CHANGE" or
            "ABILITY_TRUANT" or "ABILITY_SPEED_BOOST" or "ABILITY_SHADOW_TAG" or "ABILITY_SUCTION_CUPS" or
            "ABILITY_DRIZZLE" or "ABILITY_DROUGHT" or "ABILITY_SAND_STREAM" or "ABILITY_RAIN_DISH" or
            "ABILITY_CLOUD_NINE" or "ABILITY_AIR_LOCK" or "ABILITY_TRACE" or "ABILITY_FORECAST" or
            "ABILITY_BATTLE_ARMOR" or "ABILITY_MAGMA_ARMOR" or "ABILITY_HYPER_CUTTER" or
            "ABILITY_WHITE_SMOKE" or "ABILITY_PLUS" or "ABILITY_MINUS");

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
