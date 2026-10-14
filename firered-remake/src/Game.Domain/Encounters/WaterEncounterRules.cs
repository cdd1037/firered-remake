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
        if (!CinnabarWaterEncounterCatalog.IsAuthenticated(table) && !FirstSeviiWaterEncounterCatalog.IsAuthenticated(table) && !VictoryRoadWaterEncounterCatalog.IsAuthenticated(table) && !SeviiRubyWaterEncounterCatalog.IsAuthenticated(table) && !FourIslandWaterEncounterCatalog.IsAuthenticated(table) && !SixIslandWaterEncounterCatalog.IsAuthenticated(table) && !FiveIslandWaterEncounterCatalog.IsAuthenticated(table) && !CeruleanCaveWaterEncounterCatalog.IsAuthenticated(table) && !SevenIslandWaterEncounterCatalog.IsAuthenticated(table) && !SeafoamWaterEncounterCatalog.IsAuthenticated(table) && !CapeBrinkWaterEncounterCatalog.IsAuthenticated(table) && !SourceTableWaterEncounterCatalog.IsAuthenticated(table)) WaterEncounterCatalog.ValidateTable(table);
        if (VictoryRoadWaterEncounterCatalog.IsAuthenticated(table) && state.RulesVersion is not (VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion))
            throw new ArgumentException("victory_road_water_profile");
        if (SeviiRubyWaterEncounterCatalog.IsAuthenticated(table) && state.RulesVersion is not (SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion))
            throw new ArgumentException("sevii_ruby_water_profile");
        if (SourceTableWaterEncounterCatalog.IsAuthenticated(table) && state.RulesVersion != FiveIslandRulesVersion) throw new ArgumentException("source_table_water_profile");
        if (CapeBrinkWaterEncounterCatalog.IsAuthenticated(table) && state.RulesVersion != FiveIslandRulesVersion) throw new ArgumentException("cape_brink_water_profile");
        if (SeafoamWaterEncounterCatalog.IsAuthenticated(table) && state.RulesVersion != FiveIslandRulesVersion) throw new ArgumentException("seafoam_water_profile");
        if (SevenIslandWaterEncounterCatalog.IsAuthenticated(table) && state.RulesVersion != FiveIslandRulesVersion)
            throw new ArgumentException("seven_island_water_profile");
        if (CeruleanCaveWaterEncounterCatalog.IsAuthenticated(table) && state.RulesVersion != FiveIslandRulesVersion)
            throw new ArgumentException("cerulean_cave_water_profile");
        if (FiveIslandWaterEncounterCatalog.IsAuthenticated(table) && state.RulesVersion != FiveIslandRulesVersion)
            throw new ArgumentException("five_island_water_profile");
        if (SixIslandWaterEncounterCatalog.IsAuthenticated(table) && state.RulesVersion is not (SixIslandRulesVersion or FiveIslandRulesVersion))
            throw new ArgumentException("six_island_water_profile");
        if (FourIslandWaterEncounterCatalog.IsAuthenticated(table) && state.RulesVersion is not (FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion))
            throw new ArgumentException("four_island_water_profile");
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
    public static WaterEncounterResult Check(SurfOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, EarlyLandEncounterCheck check, ImmutableArray<PartyPokemon> party,
        ushort repelSteps, RngState globalRng, bool wildEncountersDisabled = false,
        EncounterMovementMode movement = EncounterMovementMode.Surfing)
    {
        ArgumentNullException.ThrowIfNull(owned); WaterEncounterCatalog.ValidateTable(table);
        if (!SurfOwnedPokemonCatalog.IsAuthenticated(owned.Catalog)) throw new ArgumentException("water_check_scope");
        EarlyRepelRules.ValidateSteps(repelSteps, pokemonTowerRules: true);
        return CheckOwned(owned, state, table, check, party, repelSteps, globalRng, wildEncountersDisabled, movement);
    }

    /// <summary>Silph keeps the same source water tables while admitting its exact113 party.
    /// The Saffron proof is required only for counters above the inherited200-step limit.</summary>
    public static WaterEncounterResult Check(SilphOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, EarlyLandEncounterCheck check, ImmutableArray<PartyPokemon> party,
        ushort repelSteps, RngState globalRng, bool wildEncountersDisabled = false,
        EncounterMovementMode movement = EncounterMovementMode.Surfing, SaffronGymShopCatalog? saffronShop = null)
    {
        ArgumentNullException.ThrowIfNull(owned); WaterEncounterCatalog.ValidateTable(table);
        if (!SilphOwnedPokemonCatalog.IsAuthenticated(owned.Catalog)) throw new ArgumentException("water_check_scope");
        if (saffronShop is null) EarlyRepelRules.ValidateSteps(repelSteps, pokemonTowerRules: true);
        else SaffronGymRepelRules.ValidateSteps(saffronShop, repelSteps);
        return CheckOwned(owned, state, table, check, party, repelSteps, globalRng, wildEncountersDisabled, movement);
    }

    public static WaterEncounterResult Check(CinnabarOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, EarlyLandEncounterCheck check, ImmutableArray<PartyPokemon> party,
        ushort repelSteps, RngState globalRng, bool wildEncountersDisabled = false,
        EncounterMovementMode movement = EncounterMovementMode.Surfing, SaffronGymShopCatalog? saffronShop = null)
    {
        if (owned is null || !ReferenceEquals(CinnabarOwnedPokemonCatalog.For(owned.Catalog),owned)) throw new ArgumentException("water_check_scope");
        if (saffronShop is null) EarlyRepelRules.ValidateSteps(repelSteps,pokemonTowerRules:true);
        else SaffronGymRepelRules.ValidateSteps(saffronShop,repelSteps);
        return CheckOwned(owned,state,table,check,party,repelSteps,globalRng,wildEncountersDisabled,movement);
    }

    public static WaterEncounterResult Check(SeviiRubyOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, EarlyLandEncounterCheck check, ImmutableArray<PartyPokemon> party,
        ushort repelSteps, RngState globalRng, bool wildEncountersDisabled = false,
        EncounterMovementMode movement = EncounterMovementMode.Surfing, SaffronGymShopCatalog? saffronShop = null)
    {
        if (owned is null || !ReferenceEquals(SeviiRubyOwnedPokemonCatalog.For(owned.Catalog),owned)) throw new ArgumentException("water_check_scope");
        if (saffronShop is null) EarlyRepelRules.ValidateSteps(repelSteps,pokemonTowerRules:true);
        else SaffronGymRepelRules.ValidateSteps(saffronShop,repelSteps);
        return CheckOwned(owned,state,table,check,party,repelSteps,globalRng,wildEncountersDisabled,movement);
    }

    public static WaterEncounterResult Check(FourIslandOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, EarlyLandEncounterCheck check, ImmutableArray<PartyPokemon> party,
        ushort repelSteps, RngState globalRng, bool wildEncountersDisabled = false,
        EncounterMovementMode movement = EncounterMovementMode.Surfing, SaffronGymShopCatalog? saffronShop = null)
    {
        if (owned is null || !ReferenceEquals(FourIslandOwnedPokemonCatalog.For(owned.Catalog),owned)) throw new ArgumentException("water_check_scope");
        if (saffronShop is null) EarlyRepelRules.ValidateSteps(repelSteps,pokemonTowerRules:true);
        else SaffronGymRepelRules.ValidateSteps(saffronShop,repelSteps);
        return CheckOwned(owned,state,table,check,party,repelSteps,globalRng,wildEncountersDisabled,movement);
    }

    public static WaterEncounterResult Check(FiveIslandOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, EarlyLandEncounterCheck check, ImmutableArray<PartyPokemon> party,
        ushort repelSteps, RngState globalRng, bool wildEncountersDisabled = false,
        EncounterMovementMode movement = EncounterMovementMode.Surfing, SaffronGymShopCatalog? saffronShop = null)
    {
        if (owned is null || !ReferenceEquals(FiveIslandOwnedPokemonCatalog.For(owned.Catalog),owned)) throw new ArgumentException("water_check_scope");
        if (saffronShop is null) EarlyRepelRules.ValidateSteps(repelSteps,pokemonTowerRules:true);
        else SaffronGymRepelRules.ValidateSteps(saffronShop,repelSteps);
        return CheckOwned(owned,state,table,check,party,repelSteps,globalRng,wildEncountersDisabled,movement);
    }

    public static WaterEncounterResult Check(SixIslandOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, EarlyLandEncounterCheck check, ImmutableArray<PartyPokemon> party,
        ushort repelSteps, RngState globalRng, bool wildEncountersDisabled = false,
        EncounterMovementMode movement = EncounterMovementMode.Surfing, SaffronGymShopCatalog? saffronShop = null)
    {
        if (owned is null || !ReferenceEquals(SixIslandOwnedPokemonCatalog.For(owned.Catalog),owned)) throw new ArgumentException("water_check_scope");
        if (saffronShop is null) EarlyRepelRules.ValidateSteps(repelSteps,pokemonTowerRules:true);
        else SaffronGymRepelRules.ValidateSteps(saffronShop,repelSteps);
        return CheckOwned(owned,state,table,check,party,repelSteps,globalRng,wildEncountersDisabled,movement);
    }

    /// <summary>Authenticated Celio party138 Surf entry; fishing and Rock Smash use other APIs.</summary>
    public static WaterEncounterResult Check(CelioOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, EarlyLandEncounterCheck check, ImmutableArray<PartyPokemon> party,
        ushort repelSteps, RngState globalRng, RoamerState roamer, RoamerMap playerMap,
        bool wildEncountersDisabled = false, EncounterMovementMode movement = EncounterMovementMode.Surfing,
        SaffronGymShopCatalog? saffronShop = null)
    {
        var gate = CelioEncounterRules.Validate(owned, party, repelSteps, roamer, playerMap, saffronShop);
        if (state.RulesVersion != FiveIslandRulesVersion) throw new ArgumentException("celio_water_profile");
        return CheckOwned(owned.Catalog, owned.Mechanics, state, table, check, party, repelSteps,
            globalRng, wildEncountersDisabled, movement, gate);
    }

    public static WaterEncounterResult Check(CeruleanCaveOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, EarlyLandEncounterCheck check, ImmutableArray<PartyPokemon> party,
        ushort repelSteps, RngState globalRng, RoamerState roamer, RoamerMap playerMap,
        bool wildEncountersDisabled = false, EncounterMovementMode movement = EncounterMovementMode.Surfing,
        SaffronGymShopCatalog? saffronShop = null)
    {
        var gate = CelioEncounterRules.Validate(owned, party, repelSteps, roamer, playerMap, saffronShop);
        if (state.RulesVersion != FiveIslandRulesVersion) throw new ArgumentException("cerulean_cave_water_profile");
        return CheckOwned(owned.Catalog, owned.Mechanics, state, table, check, party, repelSteps,
            globalRng, wildEncountersDisabled, movement, gate);
    }

    public static WaterEncounterResult Check(SevenIslandOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, EarlyLandEncounterCheck check, ImmutableArray<PartyPokemon> party,
        ushort repelSteps, RngState globalRng, RoamerState roamer, RoamerMap playerMap,
        bool wildEncountersDisabled = false, EncounterMovementMode movement = EncounterMovementMode.Surfing,
        SaffronGymShopCatalog? saffronShop = null)
    {
        var gate = CelioEncounterRules.Validate(owned, party, repelSteps, roamer, playerMap, saffronShop);
        if (state.RulesVersion != FiveIslandRulesVersion) throw new ArgumentException("seven_island_water_profile");
        return CheckOwned(owned.Catalog, owned.Mechanics, state, table, check, party, repelSteps,
            globalRng, wildEncountersDisabled, movement, gate);
    }

    public static WaterEncounterResult Check(EventIslandOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, EarlyLandEncounterCheck check, ImmutableArray<PartyPokemon> party,
        ushort repelSteps, RngState globalRng, RoamerState roamer, RoamerMap playerMap,
        bool wildEncountersDisabled = false, EncounterMovementMode movement = EncounterMovementMode.Surfing,
        SaffronGymShopCatalog? saffronShop = null)
    {
        var gate = CelioEncounterRules.Validate(owned, party, repelSteps, roamer, playerMap, saffronShop);
        if (state.RulesVersion != FiveIslandRulesVersion) throw new ArgumentException("event_island_water_profile");
        return CheckOwned(owned.Catalog, owned.Mechanics, state, table, check, party, repelSteps,
            globalRng, wildEncountersDisabled, movement, gate);
    }

    public static WaterEncounterResult Check(PowerPlantOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, EarlyLandEncounterCheck check, ImmutableArray<PartyPokemon> party,
        ushort repelSteps, RngState globalRng, RoamerState roamer, RoamerMap playerMap,
        bool wildEncountersDisabled = false, EncounterMovementMode movement = EncounterMovementMode.Surfing,
        SaffronGymShopCatalog? saffronShop = null, OwnedBattlePokemonCatalog? currentOwned = null)
    {
        var gate = CelioEncounterRules.Validate(owned, party, repelSteps, roamer, playerMap, saffronShop, currentOwned);
        if (state.RulesVersion != FiveIslandRulesVersion) throw new ArgumentException("power_plant_water_profile");
        var current = OwnedBattlePokemonCatalog.ResolveCurrent(owned.SharedBattle, currentOwned);
        return CheckOwned(current.Catalog, current.Mechanics, state, table, check, party, repelSteps,
            globalRng, wildEncountersDisabled, movement, gate);
    }

    private static WaterEncounterResult CheckOwned(OwnedBattlePokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, EarlyLandEncounterCheck check, ImmutableArray<PartyPokemon> party,
        ushort repelSteps, RngState globalRng, bool wildEncountersDisabled, EncounterMovementMode movement)
        => CheckOwned(owned.Catalog, owned.Mechanics, state, table, check, party, repelSteps,
            globalRng, wildEncountersDisabled, movement);

    private static WaterEncounterResult CheckOwned(PokemonCatalog catalog, SourceMechanicsCatalog mechanics,
        WaterEncounterState state, WaterEncounterTable table, EarlyLandEncounterCheck check,
        ImmutableArray<PartyPokemon> party, ushort repelSteps, RngState globalRng,
        bool wildEncountersDisabled, EncounterMovementMode movement, CelioRoamerEncounterGate? roamerGate = null)
    {
        ArgumentNullException.ThrowIfNull(check); ValidateState(state,table);
        if (state.Pending is not null ||
            movement != EncounterMovementMode.Surfing || check.Map != table.Map ||
            !Enum.IsDefined(check.Trigger) || check.EncounterType is not (EncounterTileType.Water or EncounterTileType.None) ||
            globalRng.AlgorithmId != Gen3Rng.AlgorithmId || party.IsDefaultOrEmpty || party.Length > 6)
            throw new ArgumentException("water_check_scope");
        foreach (var member in party)
            if (member is null || (member.IsEgg ? SourcePokemonValidator.ValidatePartyMember(mechanics.Catalog, new(member.Pokemon, member.HeldItem, member.Status, true), OwnedBattlePokemonCatalog.For(catalog)) : PokemonStateValidator.Validate(catalog, member.Pokemon)) is not null ||
                !Game.Domain.Battle.SourceBattleAdmission.SupportsHeldItem(mechanics.Catalog,member.HeldItem))
                throw new ArgumentException("water_party_source");
        var lead = party[0];
        bool stench = !lead.IsEgg && lead.Pokemon.AbilityId == "ABILITY_STENCH";
        bool illuminate = !lead.IsEgg && lead.Pokemon.AbilityId == "ABILITY_ILLUMINATE";
        int? repelLevel = party.FirstOrDefault(p => !p.IsEgg && p.Pokemon.CurrentHp > 0)?.Pokemon.Level;
        // This internal representation only shares arithmetic. Public land validation still rejects
        // five-slot water tables and EncounterTileType.Water; no land admission is widened.
        var commonTable = new EarlyLandEncounterTable(table.Map,table.SourceCommit,table.SourcePath,table.SourceSymbol,
            table.SourceSha256,table.RawEncounterRate,table.Slots.Select(s => new EarlyLandEncounterSlot(s.Index,s.Species,s.MinimumLevel,s.MaximumLevel,s.Weight)).ToImmutableArray());
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
    public static Route1WildPokemonResult Generate(SurfOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, RngState globalRng, uint playerTrainerId)
    {
        ArgumentNullException.ThrowIfNull(owned); WaterEncounterCatalog.ValidateTable(table); ValidateState(state,table);
        var pending = state.Pending ?? throw new InvalidOperationException("water_pending_missing");
        if (!SurfOwnedPokemonCatalog.IsAuthenticated(owned.Catalog) || !owned.Catalog.Species.ContainsKey(pending.Candidate.Species) || globalRng.AlgorithmId != Gen3Rng.AlgorithmId)
            throw new ArgumentException("water_generation_scope");
        return Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog,pending.Candidate.Species,pending.Candidate.Level,globalRng,playerTrainerId);
    }
    public static Route1WildPokemonResult Generate(CinnabarOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, RngState globalRng, uint playerTrainerId)
    {
        ValidateState(state,table);
        var pending = state.Pending ?? throw new InvalidOperationException("water_pending_missing");
        if (owned is null || !ReferenceEquals(CinnabarOwnedPokemonCatalog.For(owned.Catalog),owned) ||
            !owned.Catalog.Species.ContainsKey(pending.Candidate.Species)) throw new ArgumentException("water_generation_scope");
        return Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog,pending.Candidate.Species,pending.Candidate.Level,globalRng,playerTrainerId);
    }

    public static Route1WildPokemonResult Generate(SeviiRubyOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, RngState globalRng, uint playerTrainerId)
    {
        ValidateState(state,table);
        var pending = state.Pending ?? throw new InvalidOperationException("water_pending_missing");
        if (owned is null || !ReferenceEquals(SeviiRubyOwnedPokemonCatalog.For(owned.Catalog),owned) ||
            !owned.Catalog.Species.ContainsKey(pending.Candidate.Species)) throw new ArgumentException("water_generation_scope");
        return Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog,pending.Candidate.Species,pending.Candidate.Level,globalRng,playerTrainerId);
    }

    public static Route1WildPokemonResult Generate(FourIslandOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, RngState globalRng, uint playerTrainerId)
    {
        ValidateState(state,table);
        var pending = state.Pending ?? throw new InvalidOperationException("water_pending_missing");
        if (owned is null || !ReferenceEquals(FourIslandOwnedPokemonCatalog.For(owned.Catalog),owned) ||
            !owned.Catalog.Species.ContainsKey(pending.Candidate.Species)) throw new ArgumentException("water_generation_scope");
        return Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog,pending.Candidate.Species,pending.Candidate.Level,globalRng,playerTrainerId);
    }

    public static Route1WildPokemonResult Generate(FiveIslandOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, RngState globalRng, uint playerTrainerId)
    {
        ValidateState(state,table);
        var pending = state.Pending ?? throw new InvalidOperationException("water_pending_missing");
        if (owned is null || !ReferenceEquals(FiveIslandOwnedPokemonCatalog.For(owned.Catalog),owned) ||
            !owned.Catalog.Species.ContainsKey(pending.Candidate.Species)) throw new ArgumentException("water_generation_scope");
        return Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog,pending.Candidate.Species,pending.Candidate.Level,globalRng,playerTrainerId);
    }

    public static Route1WildPokemonResult Generate(SixIslandOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, RngState globalRng, uint playerTrainerId)
    {
        ValidateState(state,table);
        var pending = state.Pending ?? throw new InvalidOperationException("water_pending_missing");
        if (owned is null || !ReferenceEquals(SixIslandOwnedPokemonCatalog.For(owned.Catalog),owned) ||
            !owned.Catalog.Species.ContainsKey(pending.Candidate.Species)) throw new ArgumentException("water_generation_scope");
        return Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog,pending.Candidate.Species,pending.Candidate.Level,globalRng,playerTrainerId);
    }

    public static Route1WildPokemonResult Generate(CelioOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, RngState globalRng, uint playerTrainerId)
    {
        ValidateState(state, table);
        var pending = state.Pending ?? throw new InvalidOperationException("water_pending_missing");
        if (owned is null || !ReferenceEquals(CelioOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            state.RulesVersion != FiveIslandRulesVersion || !owned.Catalog.Species.ContainsKey(pending.Candidate.Species))
            throw new ArgumentException("celio_water_generation_scope");
        return Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog, pending.Candidate.Species,
            pending.Candidate.Level, globalRng, playerTrainerId);
    }

    public static Route1WildPokemonResult Generate(CeruleanCaveOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, RngState globalRng, uint playerTrainerId)
    {
        ValidateState(state, table);
        var pending = state.Pending ?? throw new InvalidOperationException("water_pending_missing");
        if (owned is null || !ReferenceEquals(CeruleanCaveOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            state.RulesVersion != FiveIslandRulesVersion || !owned.Catalog.Species.ContainsKey(pending.Candidate.Species))
            throw new ArgumentException("cerulean_cave_water_generation_scope");
        return Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog, pending.Candidate.Species,
            pending.Candidate.Level, globalRng, playerTrainerId);
    }

    public static Route1WildPokemonResult Generate(SevenIslandOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, RngState globalRng, uint playerTrainerId)
    {
        ValidateState(state, table);
        var pending = state.Pending ?? throw new InvalidOperationException("water_pending_missing");
        if (owned is null || !ReferenceEquals(SevenIslandOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            state.RulesVersion != FiveIslandRulesVersion || !owned.Catalog.Species.ContainsKey(pending.Candidate.Species))
            throw new ArgumentException("seven_island_water_generation_scope");
        return Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog, pending.Candidate.Species,
            pending.Candidate.Level, globalRng, playerTrainerId);
    }

    public static Route1WildPokemonResult Generate(EventIslandOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, RngState globalRng, uint playerTrainerId)
    {
        ValidateState(state, table);
        var pending = state.Pending ?? throw new InvalidOperationException("water_pending_missing");
        if (owned is null || !ReferenceEquals(EventIslandOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            state.RulesVersion != FiveIslandRulesVersion || !owned.Catalog.Species.ContainsKey(pending.Candidate.Species))
            throw new ArgumentException("event_island_water_generation_scope");
        return Route1WildPokemonFactory.GenerateOrdinary(owned.Catalog, pending.Candidate.Species,
            pending.Candidate.Level, globalRng, playerTrainerId);
    }

    public static Route1WildPokemonResult Generate(PowerPlantOwnedPokemonCatalog owned, WaterEncounterState state,
        WaterEncounterTable table, RngState globalRng, uint playerTrainerId)
    {
        ValidateState(state, table);
        var pending = state.Pending ?? throw new InvalidOperationException("water_pending_missing");
        if (owned is null || !ReferenceEquals(PowerPlantOwnedPokemonCatalog.For(owned.Catalog), owned) ||
            state.RulesVersion != FiveIslandRulesVersion || !owned.Catalog.Species.ContainsKey(pending.Candidate.Species))
            throw new ArgumentException("power_plant_water_generation_scope");
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
