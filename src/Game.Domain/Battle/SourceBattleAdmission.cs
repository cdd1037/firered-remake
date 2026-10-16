using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>Explicit singles admission identity. Actual move and ability support is owned by the kernel.</summary>
public sealed class BattleRulesProfile
{
    public const string CanonicalSourceSinglesVersion = "firered-canonical-source-singles-v1";
    public static BattleRulesProfile CanonicalSourceSingles { get; } = new();
    public string Version => CanonicalSourceSinglesVersion;
    public int ActiveBattlersPerSide => 1;
    private BattleRulesProfile() { }
}

public static class SourceBattleAdmission
{
    public static bool IsOwnedCampaign(PokemonCatalog? catalog, WildBattleState state) =>
        IsCanonicalSourceState(catalog, state) && state.Campaign is { } campaign &&
        ReferenceEquals(campaign.Mechanics.Catalog, catalog) &&
        state.SourceCampaignRulesVersion is GamePokemonCatalog.Profile or GameOpponentPokemonCatalog.Profile;

    public static bool IsOwnedCampaign(GamePokemonCatalog owned, WildBattleState state) =>
        ReferenceEquals(state.Campaign, owned) && IsOwnedCampaign(owned.Mechanics.Catalog, state);

    internal static string? ValidateCampaignMembership(PokemonCatalog? catalog, WildBattleState state)
    {
        if (state.SourceCampaignRulesVersion is null) return null;
        if (!IsOwnedCampaign(catalog, state)) return "source_campaign_admission";
        var owned = state.Campaign!;
        if (state.CalledParty is not { } party || party.Player.IsDefaultOrEmpty || party.Opponent.IsDefaultOrEmpty)
            return "source_campaign_actual_parties";
        if (party.Player.Any(m => m?.Pokemon is null || !owned.Catalog.Species.ContainsKey(m.Pokemon.Species)) ||
            party.Opponent.Any(m => m?.Pokemon is null || !(party.BattleKind == WildBattleCalledBattleKind.Trainer
                ? owned.Opponents.Catalog.Species.ContainsKey(m.Pokemon.Species) : owned.Catalog.Species.ContainsKey(m.Pokemon.Species))))
            return "source_campaign_species_unsupported";
        return null;
    }

    internal static string? ValidateOwnedEntry(GamePokemonCatalog owned,
        ImmutableArray<WildBattleCombatant> players, int activeIndex)
    {
        if (owned is null) return "source_campaign_admission";
        if (players.IsDefaultOrEmpty || players.Length > 6 || activeIndex < 0 || activeIndex >= players.Length)
            return "source_campaign_party";
        if (players[activeIndex]?.IsEgg != false) return "source_campaign_active_egg";
        foreach (var member in players)
        {
            if (member?.Pokemon is null || !owned.Catalog.Species.ContainsKey(member.Pokemon.Species))
                return "source_campaign_species_unsupported";
            if (SourcePokemonValidator.ValidatePartyMember(owned.Mechanics.Catalog, new(member.Pokemon, member.HeldItem, member.Status, member.IsEgg), owned) is not null)
                return "source_mechanics_pokemon";
            if (!member.IsEgg && ValidatePokemonCapabilities(owned.Mechanics.Catalog, member.Pokemon) is { } error) return error;
        }
        return null;
    }

    internal static bool IsCanonicalSourceState(PokemonCatalog? catalog, WildBattleState state) =>
        state.SourceMechanicsRulesVersion == BattleRulesProfile.CanonicalSourceSinglesVersion &&
        state.RulesVersion == WildBattleRules.FuchsiaRulesVersion &&
        catalog?.MechanicsData is not null;

    internal static string? ValidateRulesProfile(PokemonCatalog? catalog, WildBattleState state) =>
        ValidateCampaignMembership(catalog, state) ?? (
        (!SourceBattleMoneyRules.ValidMultiplier(state.SourceMoneyMultiplier) ||
            state.SourceMoneyMultiplier is not null && !IsCanonicalSourceState(catalog, state)) ? "source_money_multiplier" :
        (state.SourcePlayerHitReceipt is not null || state.SourceOpponentHitReceipt is not null) &&
            (!IsCanonicalSourceState(catalog, state) || !SourceHitItemRules.Valid(state.SourcePlayerHitReceipt) || !SourceHitItemRules.Valid(state.SourceOpponentHitReceipt)) ? "source_hit_item_receipt" :
        state.SourceFocusBandedMask != 0 && (state.SourceFocusBandedMask > 3 || !IsCanonicalSourceState(catalog, state)) ? "source_focus_band_state" :
        SourceObedienceRules.Validate(state) ??
        SourceTraceForecastRules.Validate(state.Player, state.SourceMechanicsRulesVersion) ??
        SourceTraceForecastRules.Validate(state.Opponent, state.SourceMechanicsRulesVersion) ?? SourceWeatherRules.Validate(state) ?? (catalog?.MechanicsData is not null || state.SourceMechanicsRulesVersion is not null
            ? IsCanonicalSourceState(catalog, state) ? null : "source_mechanics_rules_profile" : null));

    internal static bool SupportsHeldItem(PokemonCatalog? catalog, string item) =>
        item == "ITEM_NONE" || SourceFrameBerryItems.SupportsHeldItem(item, catalog);

    internal static bool SupportsAbility(PokemonCatalog catalog, string ability) =>
        catalog?.MechanicsData is { } data && data.Abilities.ContainsKey(ability) &&
        SourceBattleKernel.SupportsAbility(ability);

    internal static string? ValidatePokemonCapabilities(PokemonCatalog catalog, PokemonInstance pokemon)
    {
        if (catalog?.MechanicsData is null) return null;
        if (!SupportsAbility(catalog, pokemon.AbilityId)) return "source_mechanics_ability_not_implemented";
        if (pokemon.Moves.Any(m => !WildBattleRules.IsCalledMoveEffectSupported(catalog.Moves[m.Move])))
            return "source_mechanics_move_not_implemented";
        return null;
    }

    public static string? ValidateEntry(SourceMechanicsCatalog catalog, BattleRulesProfile profile,
        ImmutableArray<WildBattleCombatant> players, int activeIndex, WildBattleCombatant opponent, GamePokemonCatalog? ownership = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        if (!ReferenceEquals(profile, BattleRulesProfile.CanonicalSourceSingles)) return "source_mechanics_rules_profile";
        if (players.IsDefaultOrEmpty || players.Length > 6 || activeIndex < 0 || activeIndex >= players.Length || opponent?.Pokemon is null)
            return "source_mechanics_party";
        if (players[activeIndex]?.IsEgg != false || opponent.IsEgg) return "source_mechanics_active_egg";
        foreach (var mon in players.Append(opponent))
        {
            if (mon?.Pokemon is null || SourcePokemonValidator.ValidatePartyMember(catalog.Catalog, new(mon.Pokemon, mon.HeldItem, mon.Status, mon.IsEgg), ownership) is not null)
                return "source_mechanics_pokemon";
            if (!mon.IsEgg && ValidatePokemonCapabilities(catalog.Catalog, mon.Pokemon) is { } error) return error;
        }
        return null;
    }
}
