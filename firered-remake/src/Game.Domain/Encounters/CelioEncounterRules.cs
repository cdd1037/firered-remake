using System.Collections.Immutable;
using Game.Domain.Battle;
using Game.Domain.Items;
using Game.Domain.Pokemon;
using Game.Domain.Roaming;

namespace Game.Domain.Encounters;

/// <summary>Pure, immutable gate data, available only after authenticating Celio ownership.</summary>
internal sealed record CelioRoamerEncounterGate(RoamerState State, RoamerMap PlayerMap,
    ImmutableArray<RoamerPartyMember> Party);

/// <summary>Celio reuses the exact Five encounter process IDs. The new campaign profile and
/// authenticated Celio catalog select the new overloads; historical Five APIs are unchanged.</summary>
public static class CelioEncounterRules
{
    internal static CelioRoamerEncounterGate Validate(CelioOwnedPokemonCatalog owned,
        ImmutableArray<PartyPokemon> party, ushort repelSteps, RoamerState roamer,
        RoamerMap playerMap, SaffronGymShopCatalog? saffronShop)
    {
        if (owned is null || !ReferenceEquals(CelioOwnedPokemonCatalog.For(owned.Catalog), owned))
            throw new ArgumentException("celio_encounter_catalog");
        return ValidateParty(owned.Catalog, owned.Mechanics, party, repelSteps, roamer, playerMap, saffronShop);
    }

    internal static CelioRoamerEncounterGate Validate(CeruleanCaveOwnedPokemonCatalog owned,
        ImmutableArray<PartyPokemon> party, ushort repelSteps, RoamerState roamer,
        RoamerMap playerMap, SaffronGymShopCatalog? saffronShop)
    {
        CeruleanCaveWildPokemonFactory.ValidateOwned(owned);
        return ValidateParty(owned.Catalog, owned.Mechanics, party, repelSteps, roamer, playerMap, saffronShop);
    }

    internal static CelioRoamerEncounterGate Validate(SevenIslandOwnedPokemonCatalog owned,
        ImmutableArray<PartyPokemon> party, ushort repelSteps, RoamerState roamer,
        RoamerMap playerMap, SaffronGymShopCatalog? saffronShop)
    {
        SevenIslandWildPokemonFactory.ValidateOwned(owned);
        return ValidateParty(owned.Catalog, owned.Mechanics, party, repelSteps, roamer, playerMap, saffronShop);
    }

    internal static CelioRoamerEncounterGate Validate(EventIslandOwnedPokemonCatalog owned,
        ImmutableArray<PartyPokemon> party, ushort repelSteps, RoamerState roamer,
        RoamerMap playerMap, SaffronGymShopCatalog? saffronShop)
    {
        if (owned is null || !ReferenceEquals(EventIslandOwnedPokemonCatalog.For(owned.Catalog), owned))
            throw new ArgumentException("event_island_encounter_catalog");
        return ValidateParty(owned.Catalog, owned.Mechanics, party, repelSteps, roamer, playerMap, saffronShop);
    }

    internal static CelioRoamerEncounterGate Validate(PowerPlantOwnedPokemonCatalog owned,
        ImmutableArray<PartyPokemon> party, ushort repelSteps, RoamerState roamer,
        RoamerMap playerMap, SaffronGymShopCatalog? saffronShop)
    {
        if (owned is null || !ReferenceEquals(PowerPlantOwnedPokemonCatalog.For(owned.Catalog), owned))
            throw new ArgumentException("power_plant_encounter_catalog");
        return ValidateParty(owned.Catalog, owned.Mechanics, party, repelSteps, roamer, playerMap, saffronShop);
    }

    private static CelioRoamerEncounterGate ValidateParty(PokemonCatalog catalog, SourceMechanicsCatalog mechanics,
        ImmutableArray<PartyPokemon> party, ushort repelSteps, RoamerState roamer,
        RoamerMap playerMap, SaffronGymShopCatalog? saffronShop)
    {
        if (saffronShop is null) EarlyRepelRules.ValidateSteps(repelSteps, pokemonTowerRules: true);
        else SaffronGymRepelRules.ValidateSteps(saffronShop, repelSteps);
        SourceRoamerRules.Validate(roamer);
        if (playerMap.Group is < 0 or > 255 || playerMap.Number is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(playerMap));
        if (party.IsDefaultOrEmpty || party.Length > 6 || party.Any(member => member is null ||
            PokemonStateValidator.Validate(catalog, member.Pokemon) is not null ||
            !SourceBattleAdmission.SupportsHeldItem(mechanics.Catalog, member.HeldItem)))
            throw new ArgumentException("celio_encounter_party");
        return new(roamer, playerMap, party.Select(member =>
            new RoamerPartyMember(member.Pokemon.Level, member.Pokemon.CurrentHp, member.IsEgg)).ToImmutableArray());
    }

    /// <summary>Commit only together with successful roamer battle construction. Ordinary
    /// candidates retain their existing CompleteCandidate API and source pending metadata.</summary>
    public static EarlyLandEncounterState CompleteRoamerCandidate(EarlyLandEncounterResult result,
        EarlyLandEncounterTable table)
    {
        ArgumentNullException.ThrowIfNull(result);
        EarlyLandEncounterRules.ValidateState(result.State, table);
        if (result.State.RulesVersion != EarlyLandEncounterRules.FiveIslandRulesVersion ||
            result.Outcome != EncounterCheckOutcome.RoamerCandidateReady || result.RoamerCandidate is null ||
            result.Candidate is not null || result.State.Pending is not null)
            throw new ArgumentException("celio_land_roamer_candidate");
        SourceRoamerRules.RestoreEncounter(result.RoamerCandidate.Individual);
        return EarlyLandEncounterRules.ResetRateModifiers(result.State);
    }

    public static WaterEncounterState CompleteRoamerCandidate(WaterEncounterResult result,
        WaterEncounterTable table)
    {
        ArgumentNullException.ThrowIfNull(result);
        WaterEncounterRules.ValidateState(result.State, table);
        if (result.State.RulesVersion != WaterEncounterRules.FiveIslandRulesVersion ||
            result.Outcome != EncounterCheckOutcome.RoamerCandidateReady || result.RoamerCandidate is null ||
            result.Candidate is not null || result.State.Pending is not null)
            throw new ArgumentException("celio_water_roamer_candidate");
        SourceRoamerRules.RestoreEncounter(result.RoamerCandidate.Individual);
        return WaterEncounterRules.ResetRateModifiers(result.State, table);
    }
}
