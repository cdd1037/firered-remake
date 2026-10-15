using Game.Domain.Ids;
using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Items;
using Game.Domain.Randomness;
using Game.Domain.Roaming;

namespace Game.Domain.Encounters;

/// <summary>Water-table adapter around the existing source rate/cooldown/slot/level kernel.
/// Field ownership, completed-step precedence, movement and battle dispatch belong to the session.</summary>
public static class WaterEncounterRules
{
    public const string FiveIslandRulesVersion = "firered-five-island-water-candidates-v1";
    public const string SixIslandRulesVersion = "firered-six-island-water-candidates-v1";
    public const string FourIslandRulesVersion = "firered-four-island-water-candidates-v1";
    public const string SeviiRubyRulesVersion = "firered-sevii-ruby-water-candidates-v1";
    public const string VictoryRoadRulesVersion = "firered-victory-road-water-candidates-v1";
    public const string RulesVersion = "firered-surf-water-candidates-v1";
    public static ImmutableArray<int> SlotWeights { get; } = [60,30,5,4,1];
    public static int ChooseSlot(int roll)
    {
        if (roll is < 0 or >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
        return roll < 60 ? 0 : roll < 90 ? 1 : roll < 95 ? 2 : roll < 99 ? 3 : 4;
    }
    public static void ValidateState(WaterEncounterState state, WaterEncounterTable table)
    {
        ArgumentNullException.ThrowIfNull(state);
        WaterEncounterCatalog.ValidateTable(table);
        if (state.RulesVersion is not (RulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion) || state.RateRng.AlgorithmId != EncounterRateRng.AlgorithmId ||
            state.PreviousMetatileBehavior > 255 || state.StepsSinceLastEncounter > 21)
            throw new ArgumentException("water_state");
        if (state.Pending is not { } pending) return;
        var c = pending.Candidate;
        if (c is null || c.Map != table.Map || c.Slot is < 0 or >= 5) throw new ArgumentException("water_pending");
        var s = table.Slots[c.Slot];
        if (c.Species != s.Species || c.Level < Math.Min(s.MinimumLevel,s.MaximumLevel) || c.Level > Math.Max(s.MinimumLevel,s.MaximumLevel))
            throw new ArgumentException("water_pending_source");
    }
    public static WaterEncounterResult Check(GamePokemonCatalog owned, IReadOnlyDictionary<MapId, WaterEncounterTable> tables, WaterEncounterState state,
        WaterEncounterTable table, EarlyLandEncounterCheck check, ImmutableArray<PartyPokemon> party,
        ushort repelSteps, RngState globalRng, bool wildEncountersDisabled = false,
        EncounterMovementMode movement = EncounterMovementMode.Surfing, bool extendedRepel = false)
    {
        ArgumentNullException.ThrowIfNull(owned);
        if (!extendedRepel) EarlyRepelRules.ValidateSteps(repelSteps, pokemonTowerRules: true);
        else RepelRules.ValidateSteps(repelSteps);
        WaterEncounterCatalog.ValidateAdmission(tables, table);
        return CheckOwned(owned, state, table, check, party, repelSteps, globalRng, wildEncountersDisabled, movement);
    }

    public static WaterEncounterResult Check(GamePokemonCatalog owned, IReadOnlyDictionary<MapId, WaterEncounterTable> tables, WaterEncounterState state,
        WaterEncounterTable table, EarlyLandEncounterCheck check, ImmutableArray<PartyPokemon> party,
        ushort repelSteps, RngState globalRng, RoamerState roamer, RoamerMap playerMap,
        bool wildEncountersDisabled = false, EncounterMovementMode movement = EncounterMovementMode.Surfing,
        bool extendedRepel = false)
    {
        var gate = CelioEncounterRules.Validate(owned, party, repelSteps, roamer, playerMap, extendedRepel);
        if (state.RulesVersion != FiveIslandRulesVersion) throw new ArgumentException("campaign_water_profile");
        WaterEncounterCatalog.ValidateAdmission(tables, table);
        return CheckOwned(owned, state, table, check, party, repelSteps, globalRng, wildEncountersDisabled, movement, gate);
    }

    private static WaterEncounterResult CheckOwned(GamePokemonCatalog owned,
        WaterEncounterState state, WaterEncounterTable table, EarlyLandEncounterCheck check,
        ImmutableArray<PartyPokemon> party, ushort repelSteps, RngState globalRng,
        bool wildEncountersDisabled, EncounterMovementMode movement, CelioRoamerEncounterGate? roamerGate = null)
    {
        var catalog = owned.Catalog;
        var mechanics = owned.Mechanics;
        ArgumentNullException.ThrowIfNull(check); ValidateState(state,table);
        if (state.Pending is not null ||
            movement != EncounterMovementMode.Surfing || check.Map != table.Map ||
            !Enum.IsDefined(check.Trigger) || check.EncounterType is not (EncounterTileType.Water or EncounterTileType.None) ||
            globalRng.AlgorithmId != Gen3Rng.AlgorithmId || party.IsDefaultOrEmpty || party.Length > 6)
            throw new ArgumentException("water_check_scope");
        foreach (var member in party)
            if (member is null || (member.IsEgg ? SourcePokemonValidator.ValidatePartyMember(mechanics.Catalog, new(member.Pokemon, member.HeldItem, member.Status, true), owned) : PokemonStateValidator.Validate(catalog, member.Pokemon)) is not null ||
                !Game.Domain.Battle.SourceBattleAdmission.SupportsHeldItem(mechanics.Catalog,member.HeldItem))
                throw new ArgumentException("water_party_source");
        var lead = party[0];
        bool stench = !lead.IsEgg && lead.Pokemon.AbilityId == "ABILITY_STENCH";
        bool illuminate = !lead.IsEgg && lead.Pokemon.AbilityId == "ABILITY_ILLUMINATE";
        int? repelLevel = party.FirstOrDefault(p => !p.IsEgg && p.Pokemon.CurrentHp > 0)?.Pokemon.Level;
        // This internal representation only shares arithmetic. Public land validation still rejects
        // five-slot water tables and EncounterTileType.Water; no land admission is widened.
        var commonTable = new EarlyLandEncounterTable(table.Map,table.RawEncounterRate,table.Slots.Select(s => new EarlyLandEncounterSlot(s.Index,s.Species,s.MinimumLevel,s.MaximumLevel,s.Weight)).ToImmutableArray());
        var commonState = new EarlyLandEncounterState(state.RateRng,state.PreviousMetatileBehavior,state.EncounterRateBuff,state.StepsSinceLastEncounter)
            { CachedLeadHasCleanseTag = state.CachedLeadHasCleanseTag };
        var result = EarlyLandEncounterRules.CheckCore(commonState,commonTable,check,wildEncountersDisabled,globalRng,
            stench,illuminate,lead.HeldItem == "ITEM_CLEANSE_TAG",ChooseSlot,"encounter.water-slot",repelSteps,repelLevel,roamerGate,RoamerEncounterMethod.Surfing);
        var c = result.Candidate is { } candidate ? new WaterEncounterCandidate(candidate.Map,candidate.Slot,candidate.Species,candidate.Level) : null;
        return new(new(result.State.RateRng,result.State.PreviousMetatileBehavior,result.State.EncounterRateBuff,
            result.State.StepsSinceLastEncounter,c is null ? null : new(c,check.MetatileBehavior),RulesVersion:state.RulesVersion)
            { CachedLeadHasCleanseTag = result.State.CachedLeadHasCleanseTag },result.GlobalRng,result.Outcome,c,result.RateThreshold,result.RandomTrace)
            { RoamerCandidate = result.RoamerCandidate };
    }
    public static WaterEncounterState CompleteCandidate(WaterEncounterState state, WaterEncounterTable table)
    {
        ValidateState(state,table); var pending = state.Pending ?? throw new InvalidOperationException("water_pending_missing");
        return state with { Pending = null, EncounterRateBuff = 0, StepsSinceLastEncounter = 0, PreviousMetatileBehavior = pending.MetatileBehavior };
    }
    public static WaterEncounterState ResetRateModifiers(WaterEncounterState state, WaterEncounterTable table)
    {
        ValidateState(state,table); if (state.Pending is not null) throw new InvalidOperationException("water_pending_reset");
        return state with { EncounterRateBuff = 0, StepsSinceLastEncounter = 0 };
    }
    public static Route1WildPokemonResult Generate(GamePokemonCatalog owned, IReadOnlyDictionary<MapId, WaterEncounterTable> tables, WaterEncounterState state,
        WaterEncounterTable table, RngState globalRng, uint playerTrainerId)
    {
        ArgumentNullException.ThrowIfNull(owned);
        WaterEncounterCatalog.ValidateAdmission(tables, table);
        ValidateState(state, table);
        var pending = state.Pending ?? throw new InvalidOperationException("water_pending_missing");
        if (!owned.Catalog.Species.ContainsKey(pending.Candidate.Species) || globalRng.AlgorithmId != Gen3Rng.AlgorithmId)
            throw new ArgumentException("water_generation_scope");
        return Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog, pending.Candidate.Species,
            pending.Candidate.Level, globalRng, playerTrainerId);
    }

    /// <summary>All3 exact source water species have NONE/NONE, but SetWildMonHeldItem draws once.</summary>
    public static EarlyLandWildHeldItemResult AssignHeldItem(WaterEncounterState state, WaterEncounterTable table, RngState globalRng)
    {
        ValidateState(state,table); if (state.Pending is null) throw new InvalidOperationException("water_pending_missing");
        var draw = Gen3Rng.Next(globalRng,"wild-battle.held-item",100);
        return new("ITEM_NONE",draw.State,[draw.Trace]);
    }
}
