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
    internal static CelioRoamerEncounterGate Validate(GamePokemonCatalog owned,
        ImmutableArray<PartyPokemon> party, ushort repelSteps, RoamerState roamer,
        RoamerMap playerMap, bool extendedRepel)
    {
        ArgumentNullException.ThrowIfNull(owned);
        var catalog = owned.Catalog;
        var mechanics = owned.Mechanics;
        if (!extendedRepel) EarlyRepelRules.ValidateSteps(repelSteps, pokemonTowerRules: true);
        else RepelRules.ValidateSteps(repelSteps);
        SourceRoamerRules.Validate(roamer);
        if (playerMap.Group is < 0 or > 255 || playerMap.Number is < 0 or > 255)
            throw new ArgumentOutOfRangeException(nameof(playerMap));
        if (party.IsDefaultOrEmpty || party.Length > 6 || party.Any(member => member is null ||
            (member.IsEgg ? SourcePokemonValidator.ValidatePartyMember(mechanics.Catalog, new(member.Pokemon, member.HeldItem, member.Status, true), owned) : PokemonStateValidator.Validate(catalog, member.Pokemon)) is not null ||
            !SourceBattleAdmission.SupportsHeldItem(mechanics.Catalog, member.HeldItem)))
            throw new ArgumentException("celio_encounter_party");
        return new(roamer, playerMap, party.Select(member =>
            new RoamerPartyMember(member.Pokemon.Level, member.Pokemon.CurrentHp, member.IsEgg)).ToImmutableArray());
    }

    /// <summary>Commit only together with successful roamer battle construction. Ordinary
    /// candidates retain their existing CompleteCandidate API and source pending metadata.</summary>
    public static EarlyLandEncounterState CompleteRoamerCandidate(EarlyLandEncounterResult result,
        EarlyLandEncounterTable table, OrdinaryLandEncounterDefinition? ordinary = null)
    {
        ArgumentNullException.ThrowIfNull(result);
        if (ordinary is null) EarlyLandEncounterRules.ValidateState(result.State, table);
        else ordinary.ValidateState(result.State, table);
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
