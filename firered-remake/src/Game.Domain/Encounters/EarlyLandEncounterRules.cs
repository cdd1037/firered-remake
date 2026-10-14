using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Items;
using Game.Domain.Roaming;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>
/// Bounded FireRed land encounters with separately admitted Route 22, Route 3 and Mt. Moon walking.
/// Content authenticates table bytes; a candidate remains pending until generation and battle start commit.
/// </summary>
public static class EarlyLandEncounterRules
{
    public const string SourceCommit = Route1EncounterRules.SourceCommit;
    public const string RulesVersion = "firered-early-kanto-walking-candidates-v1";
    public const string Route22RulesVersion = "firered-route22-walking-candidates-v1";
    public const string FiveIslandRulesVersion = "firered-five-island-walking-candidates-v1";
    public const string SixIslandRulesVersion = "firered-six-island-walking-candidates-v1";
    public const string FourIslandRulesVersion = "firered-four-island-walking-candidates-v1";
    public const string SeviiRubyRulesVersion = "firered-sevii-ruby-walking-candidates-v1";
    public const string VictoryRoadRulesVersion = "firered-victory-road-walking-candidates-v1";
    public const string FirstSeviiRulesVersion = "firered-first-sevii-walking-candidates-v1";
    public const string CinnabarRulesVersion = "firered-cinnabar-walking-candidates-v1";
    public const string SafariRulesVersion = "firered-safari-walking-candidates-v1";
    public const string FuchsiaRulesVersion = "firered-fuchsia-route-walking-candidates-v1";
    public const string SnorlaxRulesVersion = "firered-snorlax-walking-candidates-v1";
    public const string PokemonTowerRulesVersion = "firered-pokemon-tower-walking-candidates-v1";
    public const string RocketHideoutRulesVersion = "firered-rocket-hideout-walking-candidates-v1";
    public const string CeladonGymRulesVersion = "firered-celadon-gym-walking-candidates-v1";
    public const string CeladonApproachRulesVersion = "firered-celadon-approach-walking-candidates-v1";
    public const string RockTunnelRulesVersion = "firered-rocktunnel-walking-candidates-v1";
    public const string Route11RulesVersion = "firered-route11-walking-candidates-v1";
    public const string SsAnneRulesVersion = "firered-ssanne-walking-candidates-v1";
    public const string VermilionRulesVersion = "firered-vermilion-walking-candidates-v1";
    public const string BillRulesVersion = "firered-bill-walking-candidates-v1";
    public const string MtMoonRulesVersion = "firered-mtmoon-walking-candidates-v1";
    public const string Route3RulesVersion = "firered-route3-walking-candidates-v1";
    public const string SourcePath = "src/data/wild_encounters.json";
    public const byte TallGrassBehavior = Route1EncounterRules.TallGrassBehavior;
    public static ImmutableArray<int> SlotWeights { get; } = [20, 20, 10, 10, 10, 10, 5, 5, 4, 4, 1, 1];
    public static ImmutableArray<SpeciesId> WildSpecies { get; } =
    [
        new("pret:SPECIES_PIDGEY"), new("pret:SPECIES_RATTATA"), new("pret:SPECIES_CATERPIE"),
        new("pret:SPECIES_METAPOD"), new("pret:SPECIES_WEEDLE"), new("pret:SPECIES_KAKUNA"),
        new("pret:SPECIES_PIKACHU")
    ];
    public static ImmutableArray<SpeciesId> Route22WildSpecies { get; } =
        [new("pret:SPECIES_RATTATA"), new("pret:SPECIES_MANKEY"), new("pret:SPECIES_SPEAROW")];
    public static EarlyLandEncounterTable Route1Table { get; } = new(Route1EncounterRules.Map, SourceCommit,
        SourcePath, "sRoute1_FireRed", "a6399199987e1bdf8502e9bfdcfc9d8ad57d74df206d22ba20f020492bd3bf7c", 21,
        Route1EncounterRules.Slots.Select(s => new EarlyLandEncounterSlot(s.Index, s.Species,
            s.MinimumLevel, s.MaximumLevel, s.Weight)).ToImmutableArray());
    public static EarlyLandEncounterTable Route22Table { get; } = new(new("map.route-22"), SourceCommit,
        SourcePath, "sRoute22_FireRed", Route1Table.SourceSha256, 21,
        new[] { ("RATTATA", 3), ("MANKEY", 3), ("RATTATA", 4), ("MANKEY", 4),
            ("RATTATA", 2), ("MANKEY", 2), ("SPEAROW", 3), ("SPEAROW", 5),
            ("RATTATA", 5), ("MANKEY", 5), ("RATTATA", 5), ("MANKEY", 5) }
            .Select((s, i) => new EarlyLandEncounterSlot(i, new("pret:SPECIES_" + s.Item1),
                (byte)s.Item2, (byte)s.Item2, SlotWeights[i])).ToImmutableArray());

    // Route3 authenticates Ekans held metadata; its Route4 encounter table requires the later MtMoon profile.
    public static ImmutableArray<SpeciesId> Route3WildSpecies { get; } =
        [new("pret:SPECIES_EKANS"), new("pret:SPECIES_NIDORAN_F"),
         new("pret:SPECIES_NIDORAN_M"), new("pret:SPECIES_JIGGLYPUFF")];
    public static EarlyLandEncounterTable Route3Table { get; } = new(new("map.route-3"), SourceCommit,
        SourcePath, "sRoute3_FireRed", Route1Table.SourceSha256, 21,
        new[] { ("SPEAROW", 6), ("PIDGEY", 6), ("SPEAROW", 7), ("MANKEY", 7),
            ("NIDORAN_M", 6), ("PIDGEY", 7), ("SPEAROW", 8), ("JIGGLYPUFF", 3),
            ("NIDORAN_M", 7), ("JIGGLYPUFF", 5), ("NIDORAN_F", 6), ("JIGGLYPUFF", 7) }
            .Select((s, i) => new EarlyLandEncounterSlot(i, new("pret:SPECIES_" + s.Item1),
                (byte)s.Item2, (byte)s.Item2, SlotWeights[i])).ToImmutableArray());

    public static EarlyLandEncounterInitialization SeedFromGlobal(RngState globalRng, ushort previousMetatileBehavior = 0,
        string rulesVersion = RulesVersion)
    {
        ValidateRulesVersion(rulesVersion);
        if (previousMetatileBehavior > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(previousMetatileBehavior));
        var draw = Gen3Rng.Next(globalRng, "encounter.seed-rate-rng");
        return new(new(new(draw.Value), previousMetatileBehavior, RulesVersion: rulesVersion), draw.State, [new("global", draw.Trace)]);
    }

    /// <summary>GetMapBaseEncounterCooldown uses the current table's source u8 rate, before modifiers.</summary>
    public static int GetBaseCooldown(EarlyLandEncounterTable table)
    {
        ValidateTable(table);
        return table.RawEncounterRate >= 80 ? 0 : table.RawEncounterRate < 10 ? 8 : 8 - table.RawEncounterRate / 10;
    }

    public static EarlyLandEncounterResult Check(EarlyLandEncounterState state, EarlyLandEncounterTable table,
        EarlyLandEncounterCheck check, EarlyLandEncounterProfile profile, RngState globalRng)
        => CheckValidated(state, table, check, profile, globalRng);

    /// <summary>Celio's authenticated party138 entry; the inherited Five process and source tables
    /// remain unchanged. The roamer gate runs only after the ordinary rate test succeeds.</summary>
    public static EarlyLandEncounterResult Check(CelioOwnedPokemonCatalog owned,
        EarlyLandEncounterState state, EarlyLandEncounterTable table, EarlyLandEncounterCheck check,
        ImmutableArray<PartyPokemon> party, ushort repelSteps, RngState globalRng,
        RoamerState roamer, RoamerMap playerMap, bool wildEncountersDisabled = false,
        SaffronGymShopCatalog? saffronShop = null)
    {
        var gate = CelioEncounterRules.Validate(owned, party, repelSteps, roamer, playerMap, saffronShop);
        if (state.RulesVersion != FiveIslandRulesVersion) throw new ArgumentException("celio_land_profile");
        var lead = party[0];
        // Only these source field modifiers matter; all real party fields were authenticated above.
        var profile = new EarlyLandEncounterProfile(RulesVersion: FiveIslandRulesVersion,
            LeadAbility: !lead.IsEgg && lead.Pokemon.AbilityId is "ABILITY_STENCH" or "ABILITY_ILLUMINATE"
                ? lead.Pokemon.AbilityId : "ABILITY_NONE",
            LeadHeldItem: lead.HeldItem == "ITEM_CLEANSE_TAG" ? lead.HeldItem : "ITEM_NONE",
            WildEncountersDisabled: wildEncountersDisabled);
        int? repelLevel = party.FirstOrDefault(p => !p.IsEgg && p.Pokemon.CurrentHp > 0)?.Pokemon.Level;
        return CheckValidated(state, table, check, profile, globalRng, gate, repelSteps, repelLevel);
    }

    /// <summary>Current party139 reuses the same source walking and roaming process.</summary>
    public static EarlyLandEncounterResult Check(CeruleanCaveOwnedPokemonCatalog owned,
        EarlyLandEncounterState state, EarlyLandEncounterTable table, EarlyLandEncounterCheck check,
        ImmutableArray<PartyPokemon> party, ushort repelSteps, RngState globalRng,
        RoamerState roamer, RoamerMap playerMap, bool wildEncountersDisabled = false,
        SaffronGymShopCatalog? saffronShop = null)
    {
        var gate = CelioEncounterRules.Validate(owned, party, repelSteps, roamer, playerMap, saffronShop);
        if (state.RulesVersion != FiveIslandRulesVersion) throw new ArgumentException("cerulean_cave_land_profile");
        var lead = party[0];
        // Only these source field modifiers matter; all real party fields were authenticated above.
        var profile = new EarlyLandEncounterProfile(RulesVersion: FiveIslandRulesVersion,
            LeadAbility: !lead.IsEgg && lead.Pokemon.AbilityId is "ABILITY_STENCH" or "ABILITY_ILLUMINATE"
                ? lead.Pokemon.AbilityId : "ABILITY_NONE",
            LeadHeldItem: lead.HeldItem == "ITEM_CLEANSE_TAG" ? lead.HeldItem : "ITEM_NONE",
            WildEncountersDisabled: wildEncountersDisabled);
        int? repelLevel = party.FirstOrDefault(p => !p.IsEgg && p.Pokemon.CurrentHp > 0)?.Pokemon.Level;
        return CheckValidated(state, table, check, profile, globalRng, gate, repelSteps, repelLevel);
    }

    /// <summary>Current party146 reuses the same source walking and roaming process.</summary>
    public static EarlyLandEncounterResult Check(SevenIslandOwnedPokemonCatalog owned,
        EarlyLandEncounterState state, EarlyLandEncounterTable table, EarlyLandEncounterCheck check,
        ImmutableArray<PartyPokemon> party, ushort repelSteps, RngState globalRng,
        RoamerState roamer, RoamerMap playerMap, bool wildEncountersDisabled = false,
        SaffronGymShopCatalog? saffronShop = null)
    {
        var gate = CelioEncounterRules.Validate(owned, party, repelSteps, roamer, playerMap, saffronShop);
        if (state.RulesVersion != FiveIslandRulesVersion) throw new ArgumentException("seven_island_land_profile");
        var lead = party[0];
        // Only these source field modifiers matter; all real party fields were authenticated above.
        var profile = new EarlyLandEncounterProfile(RulesVersion: FiveIslandRulesVersion,
            LeadAbility: !lead.IsEgg && lead.Pokemon.AbilityId is "ABILITY_STENCH" or "ABILITY_ILLUMINATE"
                ? lead.Pokemon.AbilityId : "ABILITY_NONE",
            LeadHeldItem: lead.HeldItem == "ITEM_CLEANSE_TAG" ? lead.HeldItem : "ITEM_NONE",
            WildEncountersDisabled: wildEncountersDisabled);
        int? repelLevel = party.FirstOrDefault(p => !p.IsEgg && p.Pokemon.CurrentHp > 0)?.Pokemon.Level;
        return CheckValidated(state, table, check, profile, globalRng, gate, repelSteps, repelLevel);
    }

    public static EarlyLandEncounterResult Check(EventIslandOwnedPokemonCatalog owned,
        EarlyLandEncounterState state, EarlyLandEncounterTable table, EarlyLandEncounterCheck check,
        ImmutableArray<PartyPokemon> party, ushort repelSteps, RngState globalRng,
        RoamerState roamer, RoamerMap playerMap, bool wildEncountersDisabled = false,
        SaffronGymShopCatalog? saffronShop = null)
    {
        var gate = CelioEncounterRules.Validate(owned, party, repelSteps, roamer, playerMap, saffronShop);
        if (state.RulesVersion != FiveIslandRulesVersion) throw new ArgumentException("event_island_land_profile");
        var lead = party[0];
        // Only these source field modifiers matter; all real party fields were authenticated above.
        var profile = new EarlyLandEncounterProfile(RulesVersion: FiveIslandRulesVersion,
            LeadAbility: !lead.IsEgg && lead.Pokemon.AbilityId is "ABILITY_STENCH" or "ABILITY_ILLUMINATE"
                ? lead.Pokemon.AbilityId : "ABILITY_NONE",
            LeadHeldItem: lead.HeldItem == "ITEM_CLEANSE_TAG" ? lead.HeldItem : "ITEM_NONE",
            WildEncountersDisabled: wildEncountersDisabled);
        int? repelLevel = party.FirstOrDefault(p => !p.IsEgg && p.Pokemon.CurrentHp > 0)?.Pokemon.Level;
        return CheckValidated(state, table, check, profile, globalRng, gate, repelSteps, repelLevel);
    }

    public static EarlyLandEncounterResult Check(PowerPlantOwnedPokemonCatalog owned,
        EarlyLandEncounterState state, EarlyLandEncounterTable table, EarlyLandEncounterCheck check,
        ImmutableArray<PartyPokemon> party, ushort repelSteps, RngState globalRng,
        RoamerState roamer, RoamerMap playerMap, bool wildEncountersDisabled = false,
        SaffronGymShopCatalog? saffronShop = null, OwnedBattlePokemonCatalog? currentOwned = null)
    {
        var gate = CelioEncounterRules.Validate(owned, party, repelSteps, roamer, playerMap, saffronShop, currentOwned);
        if (state.RulesVersion != FiveIslandRulesVersion) throw new ArgumentException("power_plant_land_profile");
        var lead = party[0];
        // Only these source field modifiers matter; all real party fields were authenticated above.
        var profile = new EarlyLandEncounterProfile(RulesVersion: FiveIslandRulesVersion,
            LeadAbility: !lead.IsEgg && lead.Pokemon.AbilityId is "ABILITY_STENCH" or "ABILITY_ILLUMINATE"
                ? lead.Pokemon.AbilityId : "ABILITY_NONE",
            LeadHeldItem: lead.HeldItem == "ITEM_CLEANSE_TAG" ? lead.HeldItem : "ITEM_NONE",
            WildEncountersDisabled: wildEncountersDisabled);
        int? repelLevel = party.FirstOrDefault(p => !p.IsEgg && p.Pokemon.CurrentHp > 0)?.Pokemon.Level;
        return CheckValidated(state, table, check, profile, globalRng, gate, repelSteps, repelLevel);
    }

    private static EarlyLandEncounterResult CheckValidated(EarlyLandEncounterState state,
        EarlyLandEncounterTable table, EarlyLandEncounterCheck check, EarlyLandEncounterProfile profile,
        RngState globalRng, CelioRoamerEncounterGate? roamerGate = null,
        ushort repelSteps = 0, int? repelLevel = null)
    {
        ValidateState(state, table);
        ArgumentNullException.ThrowIfNull(check);
        ValidateProfile(profile);
        if (profile.RulesVersion != state.RulesVersion)
            throw new NotSupportedException("Encounter state and walking profile versions must agree.");
        if (state.Pending is not null) throw new InvalidOperationException("A wild candidate is pending; complete its generation/start transaction before another field check.");
        if (globalRng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unknown global RNG algorithm.");
        if (check.Map != table.Map) throw new ArgumentException("The current map and authenticated land table disagree.");
        if (!Enum.IsDefined(check.Trigger) || !Enum.IsDefined(check.EncounterType)) throw new NotSupportedException("Unknown field check or encounter terrain.");
        bool caveAttributes = state.RulesVersion == FiveIslandRulesVersion && (SeafoamEncounterTables.Land.Any(t=>t.Map==table.Map) || CeruleanCaveEncounterTables.IsCave(table.Map) || SevenIslandEncounterTables.IsChamber(table.Map) || table.Map == PowerPlantEncounterTables.Map) || state.RulesVersion is FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && FourIslandEncounterTables.IsCave(table.Map) || state.RulesVersion is SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && SeviiRubyEncounterTables.IsCave(table.Map) || state.RulesVersion is VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && VictoryRoadEncounterTables.IsCave(table.Map) || state.RulesVersion is CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && CinnabarLandEncounterTables.IsCave(table.Map) || state.RulesVersion is MtMoonRulesVersion or BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && (MtMoonLandEncounterTables.IsCave(table.Map) || state.RulesVersion is Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && Route11LandEncounterTables.IsCave(table.Map) || state.RulesVersion is RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && RockTunnelLandEncounterTables.IsCave(table.Map) || state.RulesVersion is PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && PokemonTowerLandEncounterTables.IsCave(table.Map));
        if (check.EncounterType == EncounterTileType.Water ||
            (!caveAttributes && check.EncounterType == EncounterTileType.Land && check.MetatileBehavior != TallGrassBehavior))
            throw new NotSupportedException("Only admitted source cave land, tall-grass land and non-encounter tiles are supported.");
        if (!caveAttributes && check.EncounterType == EncounterTileType.None && check.MetatileBehavior == TallGrassBehavior)
            throw new ArgumentException("Tall grass must retain its source land encounter attribute.");

        return CheckCore(state, table, check, profile.WildEncountersDisabled, globalRng,
            stench: state.RulesVersion is MtMoonRulesVersion or BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && profile.LeadAbility == "ABILITY_STENCH",
            illuminate: state.RulesVersion is BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && profile.LeadAbility == "ABILITY_ILLUMINATE", cleanseTag: state.RulesVersion is PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && profile.LeadHeldItem == "ITEM_CLEANSE_TAG",
            repelSteps: repelSteps, repelLevel: repelLevel, roamerGate: roamerGate);
    }

    // Public profiles validate their own contracts before entering this single source engine.
    internal static EarlyLandEncounterResult CheckCore(EarlyLandEncounterState state, EarlyLandEncounterTable table,
        EarlyLandEncounterCheck check, bool wildEncountersDisabled, RngState globalRng, bool stench = false, bool illuminate = false, bool cleanseTag = false,
        Func<int, int>? chooseSlot = null, string slotPurpose = "encounter.land-slot", ushort repelSteps = 0, int? repelLevel = null,
        CelioRoamerEncounterGate? roamerGate = null, RoamerEncounterMethod roamerMethod = RoamerEncounterMethod.Land)
    {

        var trace = ImmutableArray.CreateBuilder<EncounterRandomTrace>();
        int? threshold = null;
        int DrawGlobal(string purpose, int bound)
        {
            var draw = Gen3Rng.Next(globalRng, purpose, bound);
            globalRng = draw.State; trace.Add(new("global", draw.Trace)); return draw.Trace.Result;
        }
        EarlyLandEncounterResult Finish(EncounterCheckOutcome outcome, bool recordBehavior = true) =>
            new(recordBehavior ? state with { PreviousMetatileBehavior = check.MetatileBehavior } : state,
                globalRng, outcome, null, threshold, trace.ToImmutable());

        if (check.Trigger is not (EncounterCheckTrigger.WalkingStepCompleted or EncounterCheckTrigger.TurnInPlaceCompleted))
            return Finish(EncounterCheckOutcome.NoCheck, false);
        if (check.EncounterType == EncounterTileType.None) return Finish(EncounterCheckOutcome.NoEncounterTerrain);
        // HandleWildEncounterCooldown retains 8-bit fixed point through held-item
        // then ability modifiers; truncating before Stench/Illuminate changes boundaries.
        int cooldown = (table.RawEncounterRate >= 80 ? 0 : table.RawEncounterRate < 10 ? 8 : 8 - table.RawEncounterRate / 10) * 256, bypass = 5 * 256;
        if (cleanseTag) { cooldown += cooldown / 3; bypass -= bypass / 3; }
        if (stench) { cooldown *= 2; bypass /= 2; }
        else if (illuminate) { cooldown /= 2; bypass *= 2; }
        cooldown /= 256; bypass /= 256;
        if (state.StepsSinceLastEncounter < cooldown)
        {
            state = state with { StepsSinceLastEncounter = (byte)(state.StepsSinceLastEncounter + 1) };
            if (DrawGlobal("encounter.cooldown-bypass", 100) >= bypass)
                return Finish(EncounterCheckOutcome.CooldownSuppressed);
        }
        if (wildEncountersDisabled) return Finish(EncounterCheckOutcome.Disabled);
        if (state.PreviousMetatileBehavior != check.MetatileBehavior && DrawGlobal("encounter.terrain-change", 100) >= 60)
            return Finish(EncounterCheckOutcome.TerrainChangeSuppressed);

        int modifiedRate = table.RawEncounterRate * 16 + state.EncounterRateBuff * 16 / 200;
        if (cleanseTag) modifiedRate = modifiedRate * 2 / 3;
        if (stench) modifiedRate /= 2;
        else if (illuminate) modifiedRate *= 2;
        threshold = Math.Min(1600, modifiedRate);
        var rate = EncounterRateRng.Next(state.RateRng);
        state = state with { RateRng = rate.State };
        trace.Add(new("wild-rate", rate.Trace));
        if (rate.Trace.Result >= threshold)
        {
            state = state with { EncounterRateBuff = repelSteps == 0 ? unchecked((ushort)(state.EncounterRateBuff + table.RawEncounterRate)) : (ushort)0 };
            return Finish(EncounterCheckOutcome.RateMiss);
        }
        // StandardWildEncounter: rate succeeds before TryStartRoamerEncounter, and its
        // successful Repel rejection returns immediately without AddToWildEncounterRateBuff.
        if (roamerGate is not null)
        {
            var roamer = SourceRoamerRules.TryEncounter(roamerGate.State,
                new(roamerGate.PlayerMap, roamerMethod, OrdinaryGatesPassed: true,
                    HasEncounterTable: true, RepelActive: repelSteps > 0, roamerGate.Party), globalRng);
            globalRng = roamer.Rng;
            trace.AddRange(roamer.Trace.Select(draw => new EncounterRandomTrace("global", draw)));
            if (roamer.Decision == RoamerEncounterDecision.NoEncounter)
                return Finish(EncounterCheckOutcome.RepelSuppressed);
            if (roamer.Decision == RoamerEncounterDecision.RoamerEncounter)
                return Finish(EncounterCheckOutcome.RoamerCandidateReady) with { RoamerCandidate = roamer.Candidate };
        }
        // FR does not add Emerald's Static/Synchronize/Keen Eye branches.
        var slot = table.Slots[(chooseSlot ?? ChooseSlot)(DrawGlobal(slotPurpose, 100))];
        int lo = Math.Min(slot.MinimumLevel, slot.MaximumLevel), hi = Math.Max(slot.MinimumLevel, slot.MaximumLevel);
        byte level = (byte)(lo + DrawGlobal("encounter.level", hi - lo + 1));
        if (repelSteps > 0 && (repelLevel is null || level < repelLevel))
        {
            state = state with { EncounterRateBuff = 0 };
            return Finish(EncounterCheckOutcome.RepelSuppressed);
        }
        var candidate = new EarlyLandEncounterCandidate(table.Map, slot.Index, slot.Species, level);
        state = state with { Pending = new(candidate, check.MetatileBehavior) };
        return new(state, globalRng, EncounterCheckOutcome.CandidateReady, candidate, threshold, trace.ToImmutable());
    }

    public static EarlyLandEncounterState CompleteCandidate(EarlyLandEncounterState state, EarlyLandEncounterTable table)
    {
        ValidateState(state, table);
        var pending = state.Pending ?? throw new InvalidOperationException("No encounter candidate is pending.");
        return state with { EncounterRateBuff = 0, StepsSinceLastEncounter = 0,
            PreviousMetatileBehavior = pending.MetatileBehavior, Pending = null };
    }

    /// <summary>Connected-map loads, warps and battle starts reset modifiers without reseeding either stream.</summary>
    public static EarlyLandEncounterState ResetRateModifiers(EarlyLandEncounterState state)
    {
        ValidateState(state);
        if (state.Pending is not null) throw new InvalidOperationException("Cannot reset across an unfinished encounter transaction.");
        return state with { EncounterRateBuff = 0, StepsSinceLastEncounter = 0 };
    }

    public static int ChooseSlot(int roll)
    {
        if (roll is < 0 or >= 100) throw new ArgumentOutOfRangeException(nameof(roll));
        int end = 0;
        for (int i = 0; i < SlotWeights.Length; i++)
        {
            end += SlotWeights[i];
            if (roll < end) return i;
        }
        throw new InvalidOperationException("Invalid source land slot weights.");
    }

    public static void ValidateTable(EarlyLandEncounterTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        // Admission of a new exact map/symbol pair leaves old rejected aliases unchanged.
        var mtMoonTable = MtMoonLandEncounterTables.All.Concat(BillLandEncounterTables.All).Concat(VermilionLandEncounterTables.All).Concat(Route11LandEncounterTables.All).Concat(RockTunnelLandEncounterTables.All).Concat(CeladonApproachLandEncounterTables.All).Concat(PokemonTowerLandEncounterTables.All).Concat(FuchsiaRouteLandEncounterTables.All).Concat(SafariLandEncounterTables.All).Concat(CinnabarLandEncounterTables.All).Concat(FirstSeviiEncounterTables.Land).Concat(VictoryRoadEncounterTables.Land).Concat(SeviiRubyEncounterTables.Land).Concat(FourIslandEncounterTables.Land).Concat(SixIslandEncounterTables.Land).Concat(FiveIslandEncounterTables.Land).Concat(CeruleanCaveEncounterTables.Land).Concat(SevenIslandEncounterTables.Land).Append(PowerPlantEncounterTables.Land).Concat(SeafoamEncounterTables.Land).FirstOrDefault(t => t.Map == table.Map && t.SourceSymbol == table.SourceSymbol);
        string symbol = table.Map.Value switch
        {
            "map.route-1" => "sRoute1_FireRed", "map.route-2" => "sRoute2_FireRed",
            "map.viridian-forest" => "sViridianForest_FireRed",
            "map.route-22" => "sRoute22_FireRed",
            "map.route-3" => "sRoute3_FireRed",
            _ when mtMoonTable is not null => mtMoonTable.SourceSymbol,
            _ => throw new NotSupportedException("Only the three early-Kanto land maps and explicit Route 22/Route 3 walking are supported.")
        };
        if (table.SourceCommit != SourceCommit || table.SourcePath != SourcePath || table.SourceSymbol != symbol ||
            table.SourceSha256 is null || table.SourceSha256.Length != 64 || table.SourceSha256.Any(c => !char.IsAsciiHexDigitLower(c)))
            throw new ArgumentException("Invalid early land table source metadata.");
        if (table.RawEncounterRate is < 1 or > 255 || table.Slots.IsDefault || table.Slots.Length != 12)
            throw new ArgumentException("Invalid source land rate or slot count.");
        if (table.Map == Route22Table.Map || table.Map == Route3Table.Map || mtMoonTable is not null)
        {
            var expected = mtMoonTable ?? (table.Map == Route3Table.Map ? Route3Table : Route22Table);
            // New map profiles admit only these exact source tables, not later maps or levels.
            if (table.SourceSha256 != expected.SourceSha256 || table.RawEncounterRate != expected.RawEncounterRate ||
                !table.Slots.SequenceEqual(expected.Slots))
                throw new ArgumentException("Extended maps require their exact pinned FireRed land tables.");
            return;
        }
        for (int i = 0; i < table.Slots.Length; i++)
        {
            var slot = table.Slots[i];
            if (slot is null || slot.Index != i || slot.Weight != SlotWeights[i] || !WildSpecies.Contains(slot.Species) ||
                slot.MinimumLevel is < 1 or > 100 || slot.MaximumLevel is < 1 or > 100)
                throw new ArgumentException("Invalid early land slot metadata.");
        }
    }

    public static void ValidateState(EarlyLandEncounterState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateRulesVersion(state.RulesVersion);
        if (state.RateRng.AlgorithmId != EncounterRateRng.AlgorithmId)
            throw new NotSupportedException("Unsupported early land process version or rate RNG.");
        // The counter can retain the preceding map's cooldown until the caller's map-load reset.
        if (state.PreviousMetatileBehavior > byte.MaxValue || state.StepsSinceLastEncounter > (state.RulesVersion is PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion ? 21 : state.RulesVersion is MtMoonRulesVersion or BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion ? 16 : 8))
            throw new ArgumentException("Encounter state is outside the bounded walking profile.");
        if (state.Pending is {} pending && (pending.Candidate is null ||
            (pending.MetatileBehavior != TallGrassBehavior && !(state.RulesVersion == FiveIslandRulesVersion && (SeafoamEncounterTables.Land.Any(t=>t.Map==pending.Candidate.Map) || CeruleanCaveEncounterTables.IsCave(pending.Candidate.Map) || SevenIslandEncounterTables.IsChamber(pending.Candidate.Map) || pending.Candidate.Map == PowerPlantEncounterTables.Map)) && !(state.RulesVersion is FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && FourIslandEncounterTables.IsCave(pending.Candidate.Map)) && !(state.RulesVersion is SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && SeviiRubyEncounterTables.IsCave(pending.Candidate.Map)) && !(state.RulesVersion is VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && VictoryRoadEncounterTables.IsCave(pending.Candidate.Map)) && !(state.RulesVersion is CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && CinnabarLandEncounterTables.IsCave(pending.Candidate.Map)) && !(state.RulesVersion is MtMoonRulesVersion or BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && (MtMoonLandEncounterTables.IsCave(pending.Candidate.Map) || state.RulesVersion is Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && Route11LandEncounterTables.IsCave(pending.Candidate.Map) || state.RulesVersion is RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && RockTunnelLandEncounterTables.IsCave(pending.Candidate.Map) || state.RulesVersion is PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && PokemonTowerLandEncounterTables.IsCave(pending.Candidate.Map)))) ||
            pending.Candidate.Slot is < 0 or >= 12 || !IsAdmittedCandidate(pending.Candidate, state.RulesVersion) ||
            pending.Candidate.Level is < 1 or > 100))
            throw new ArgumentException("Invalid pending early land candidate.");
    }

    public static void ValidateState(EarlyLandEncounterState state, EarlyLandEncounterTable table)
    {
        ValidateTable(table); ValidateState(state);
        if (SevenIslandEncounterTables.Land.Any(t => t.Map == table.Map) && state.RulesVersion != FiveIslandRulesVersion)
            throw new NotSupportedException("Seven Island requires the current walking process.");
        if ((SeafoamEncounterTables.Land.Any(t=>t.Map==table.Map) || CeruleanCaveEncounterTables.IsCave(table.Map) || SevenIslandEncounterTables.IsChamber(table.Map) || table.Map == PowerPlantEncounterTables.Map) && state.RulesVersion != FiveIslandRulesVersion)
            throw new NotSupportedException("Indoor current-region tables require the current walking process.");
        if (FiveIslandEncounterTables.Land.Any(t=>t.Map==table.Map) && state.RulesVersion != FiveIslandRulesVersion)
            throw new NotSupportedException("Six Island tables require the exact walking profile.");
        if (SixIslandEncounterTables.Land.Any(t=>t.Map==table.Map) && state.RulesVersion is not (SixIslandRulesVersion or FiveIslandRulesVersion))
            throw new NotSupportedException("Six Island tables require the exact walking profile.");
        if (FourIslandEncounterTables.Land.Any(t=>t.Map==table.Map) && state.RulesVersion is not (FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion))
            throw new NotSupportedException("Four Island tables require their explicit walking profile.");
        if (SeviiRubyEncounterTables.Land.Any(t=>t.Map==table.Map) && state.RulesVersion is not (SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion))
            throw new NotSupportedException("Ruby land requires its explicit walking profile.");
        if (VictoryRoadEncounterTables.Land.Any(t=>t.Map==table.Map) && state.RulesVersion is not (VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion))
            throw new NotSupportedException("Victory Road land requires its explicit walking profile.");
        if (FirstSeviiEncounterTables.Land.Any(t => t.Map == table.Map) && state.RulesVersion is not (FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion))
            throw new NotSupportedException("First Sevii land requires its explicit walking profile.");
        if (CinnabarLandEncounterTables.All.Any(t => t.Map == table.Map) && state.RulesVersion is not (CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion))
            throw new NotSupportedException("Cinnabar land requires its explicit walking profile.");
        if(SafariLandEncounterTables.All.Any(t=>t.Map==table.Map)&&state.RulesVersion is not (SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion))throw new NotSupportedException("Safari ground tables require their explicit Safari walking profile.");
        if(FuchsiaRouteLandEncounterTables.All.Any(t=>t.Map==table.Map)&&state.RulesVersion is not (FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion))throw new NotSupportedException("Southern route tables require their explicit Fuchsia walking profile.");
        if (PokemonTowerLandEncounterTables.IsCave(table.Map) && state.RulesVersion is not (PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion)) throw new NotSupportedException("Tower tables require their explicit walking profile.");
        if (CeladonApproachLandEncounterTables.All.Any(t=>t.Map==table.Map) && state.RulesVersion is not (CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion)) throw new NotSupportedException("Celadon approach tables require the explicit walking profile.");
        if (RockTunnelLandEncounterTables.All.Any(t=>t.Map==table.Map) && state.RulesVersion is not (RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion)) throw new NotSupportedException("Rock Tunnel tables require their explicit profile.");
        if (Route11LandEncounterTables.All.Any(t=>t.Map==table.Map) && state.RulesVersion is not (Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion)) throw new NotSupportedException("Route11 tables require their explicit walking profile.");
        if (VermilionLandEncounterTables.All.Any(t=>t.Map==table.Map) && state.RulesVersion is not (VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion)) throw new NotSupportedException("Vermilion tables require the explicit Vermilion walking profile.");
        if (BillLandEncounterTables.All.Any(t=>t.Map==table.Map) && state.RulesVersion is not (BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion)) throw new NotSupportedException("Bill land table requires explicit Bill walking profile.");
        if ((table.Map == Route22Table.Map && state.RulesVersion is not (Route22RulesVersion or Route3RulesVersion or MtMoonRulesVersion or BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion)) ||
            (table.Map == Route3Table.Map && state.RulesVersion is not (Route3RulesVersion or MtMoonRulesVersion or BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion)) ||
            (MtMoonLandEncounterTables.All.Any(t => t.Map == table.Map) && state.RulesVersion is not (MtMoonRulesVersion or BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion)))
            throw new NotSupportedException("Extended maps require explicit encounter-profile admission.");
        if (state.Pending is not {} pending) return;
        var candidate = pending.Candidate;
        var slot = table.Slots[candidate.Slot];
        if (candidate.Map != table.Map || candidate.Species != slot.Species ||
            candidate.Level < Math.Min(slot.MinimumLevel, slot.MaximumLevel) ||
            candidate.Level > Math.Max(slot.MinimumLevel, slot.MaximumLevel))
            throw new ArgumentException("Pending candidate disagrees with the authenticated land table.");
    }

    public static void ValidateProfile(EarlyLandEncounterProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ValidateRulesVersion(profile.RulesVersion);
        bool ordinaryAbility = profile.RulesVersion is CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && profile.LeadAbility is "ABILITY_HYPER_CUTTER" or "ABILITY_WATER_ABSORB" || profile.RulesVersion == SnorlaxRulesVersion && profile.LeadAbility is "ABILITY_IMMUNITY" or "ABILITY_THICK_FAT" || profile.LeadAbility is "ABILITY_NONE" or "ABILITY_OVERGROW" or "ABILITY_BLAZE" or "ABILITY_TORRENT" or
                "ABILITY_KEEN_EYE" or "ABILITY_RUN_AWAY" or "ABILITY_GUTS" or "ABILITY_SHIELD_DUST" or
                "ABILITY_SHED_SKIN" or "ABILITY_SWARM" or "ABILITY_COMPOUND_EYES" or "ABILITY_STATIC";
        bool route22Ability = profile.RulesVersion is Route22RulesVersion or Route3RulesVersion or MtMoonRulesVersion or BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && profile.LeadAbility == "ABILITY_VITAL_SPIRIT";
        // FireRed only modifies walking for Stench/Illuminate, unlike Emerald. These add no draws.
        bool route3Ability = profile.RulesVersion is Route3RulesVersion or MtMoonRulesVersion or BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && profile.LeadAbility is
            "ABILITY_INTIMIDATE" or "ABILITY_SWIFT_SWIM" or "ABILITY_POISON_POINT" or "ABILITY_CUTE_CHARM" or
            "ABILITY_SAND_VEIL" or "ABILITY_ROCK_HEAD" or "ABILITY_STURDY";
        bool route3HeldItem = profile.RulesVersion is Route3RulesVersion or MtMoonRulesVersion or BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && profile.LeadHeldItem == "ITEM_ORAN_BERRY";
        bool mtMoonAbility = profile.RulesVersion is MtMoonRulesVersion or BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && profile.LeadAbility is
            "ABILITY_INNER_FOCUS" or "ABILITY_CHLOROPHYLL" or "ABILITY_EFFECT_SPORE" or "ABILITY_MAGNET_PULL" or
            "ABILITY_STENCH" or "ABILITY_STICKY_HOLD" or "ABILITY_SOUNDPROOF" or "ABILITY_LEVITATE";
        bool billAbility = profile.RulesVersion is BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && profile.LeadAbility is "ABILITY_SYNCHRONIZE" or "ABILITY_INSOMNIA" or "ABILITY_SHELL_ARMOR" or "ABILITY_WATER_VEIL" or "ABILITY_ILLUMINATE" or "ABILITY_NATURAL_CURE" or "ABILITY_OBLIVIOUS" or "ABILITY_OWN_TEMPO";
        bool mtMoonHeldItem = profile.RulesVersion is MtMoonRulesVersion or BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && profile.LeadHeldItem is
            "ITEM_TINY_MUSHROOM" or "ITEM_BIG_MUSHROOM" or "ITEM_MOON_STONE";
        bool vermilionAbility = profile.RulesVersion is VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && profile.LeadAbility is "ABILITY_PICKUP" or "ABILITY_LIMBER";
        bool vermilionHeldItem = profile.RulesVersion is VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && Game.Domain.Pokemon.VermilionPickupRules.IsKnownHeldItem(profile.LeadHeldItem);
        bool ssAnneAbility = profile.RulesVersion is SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && Game.Domain.WildBattle.SsAnneBattleScope.SupportsAbility(profile.LeadAbility);
        bool ssAnneItem = profile.RulesVersion is SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && Game.Domain.WildBattle.SsAnneBattleScope.SupportsHeldItem(profile.LeadHeldItem);
        bool route11Ability = profile.RulesVersion is Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && Game.Domain.WildBattle.Route11BattleScope.SupportsAbility(profile.LeadAbility);
        bool route11Item = profile.RulesVersion is Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && Game.Domain.WildBattle.Route11BattleScope.SupportsHeldItem(profile.LeadHeldItem);
        bool rocketAbility = profile.RulesVersion is RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && Game.Domain.WildBattle.RocketHideoutBattleScope.SupportsAbility(profile.LeadAbility);
        bool rocketItem = profile.RulesVersion is RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && Game.Domain.WildBattle.RocketHideoutBattleScope.SupportsHeldItem(profile.LeadHeldItem);
        bool rockAbility = profile.RulesVersion is RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && Game.Domain.WildBattle.RockTunnelBattleScope.SupportsAbility(profile.LeadAbility);
        bool rockItem = profile.RulesVersion is RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && (Game.Domain.WildBattle.RockTunnelBattleScope.SupportsHeldItem(profile.LeadHeldItem) || profile.LeadHeldItem == "ITEM_EVERSTONE");
        bool towerItem = profile.RulesVersion is PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && Game.Domain.WildBattle.PokemonTowerBattleScope.SupportsHeldItem(profile.LeadHeldItem);
        bool fuchsiaItem=profile.RulesVersion is FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion&&profile.LeadHeldItem=="ITEM_METAL_POWDER";
        bool safariAbility = profile.RulesVersion is SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && profile.LeadAbility is "ABILITY_SERENE_GRACE" or "ABILITY_IMMUNITY" or "ABILITY_THICK_FAT";
        bool safariItem = profile.RulesVersion is SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && profile.LeadHeldItem == "ITEM_LUCKY_EGG";
        bool victoryRoadAbility = profile.RulesVersion is VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && VictoryRoadWildSource.Species.Any(s=>s.Ability1==profile.LeadAbility || s.Ability2==profile.LeadAbility);
        bool victoryRoadItem = profile.RulesVersion is VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && (profile.LeadHeldItem is "ITEM_SITRUS_BERRY" or "ITEM_GUARD_SPEC" || VictoryRoadWildSource.Species.Any(s=>s.ItemCommon==profile.LeadHeldItem || s.ItemRare==profile.LeadHeldItem));
        bool fiveIslandAbility = profile.RulesVersion == FiveIslandRulesVersion && FiveIslandWildSource.Species.Any(s=>s.Ability1==profile.LeadAbility || s.Ability2==profile.LeadAbility);
        bool fiveIslandItem = profile.RulesVersion == FiveIslandRulesVersion && (profile.LeadHeldItem == "ITEM_UP_GRADE" || FiveIslandWildSource.Species.Any(s=>s.ItemCommon==profile.LeadHeldItem || s.ItemRare==profile.LeadHeldItem));
        bool sixIslandAbility = profile.RulesVersion is SixIslandRulesVersion or FiveIslandRulesVersion && SixIslandWildSource.Species.Any(s=>s.Ability1==profile.LeadAbility || s.Ability2==profile.LeadAbility);
        bool sixIslandItem = profile.RulesVersion is SixIslandRulesVersion or FiveIslandRulesVersion && (profile.LeadHeldItem == "ITEM_DRAGON_SCALE" || SixIslandWildSource.Species.Any(s=>s.ItemCommon==profile.LeadHeldItem || s.ItemRare==profile.LeadHeldItem));
        bool fourIslandAbility = profile.RulesVersion is FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && FourIslandWildSource.Species.Any(s=>s.Ability1==profile.LeadAbility || s.Ability2==profile.LeadAbility);
        bool fourIslandItem = profile.RulesVersion is FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && (profile.LeadHeldItem == "ITEM_NEVER_MELT_ICE" || FourIslandWildSource.Species.Any(s=>s.ItemCommon==profile.LeadHeldItem || s.ItemRare==profile.LeadHeldItem));
        bool seviiRubyAbility = profile.RulesVersion is SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && SeviiRubyWildSource.Species.Any(s=>s.Ability1==profile.LeadAbility || s.Ability2==profile.LeadAbility);
        bool seviiRubyItem = profile.RulesVersion is SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && SeviiRubyWildSource.Species.Any(s=>s.ItemCommon==profile.LeadHeldItem || s.ItemRare==profile.LeadHeldItem);
        if (profile.Movement != EncounterMovementMode.Walking || profile.RepelSteps != 0 || (!ordinaryAbility && !route22Ability && !route3Ability && !mtMoonAbility && !billAbility && !vermilionAbility && !ssAnneAbility && !route11Ability && !rockAbility && !rocketAbility && !safariAbility && !victoryRoadAbility && !seviiRubyAbility && !fourIslandAbility && !fiveIslandAbility && !sixIslandAbility) ||
            (profile.LeadHeldItem != "ITEM_NONE" && !route3HeldItem && !mtMoonHeldItem && !vermilionHeldItem && !ssAnneItem && !route11Item && !rockItem && !rocketItem && !towerItem && !fuchsiaItem && !safariItem && !victoryRoadItem && !seviiRubyItem && !fourIslandItem && !fiveIslandItem && !sixIslandItem) || profile.WhiteFluteActive || profile.BlackFluteActive ||
            profile.RoamerActive || profile.QuestLogPlayback || profile.Weather != "WEATHER_NONE")
            throw new NotSupportedException("Only ordinary walking with supported party abilities/items, no Repel, flute, roamer, weather or Quest Log playback is supported.");
    }

    internal static void ValidateRulesVersion(string rulesVersion)
    {
        if (rulesVersion is not (RulesVersion or Route22RulesVersion or Route3RulesVersion or MtMoonRulesVersion or BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion))
            throw new NotSupportedException("Unsupported early land process version.");
    }

    private static bool IsAdmittedCandidate(EarlyLandEncounterCandidate candidate, string rulesVersion)
    {
        if (candidate.Map.Value is "map.route-1" or "map.route-2" or "map.viridian-forest")
            return WildSpecies.Contains(candidate.Species);
        EarlyLandEncounterTable table;
        if (rulesVersion == FiveIslandRulesVersion && SeafoamEncounterTables.Land.FirstOrDefault(t=>t.Map==candidate.Map) is {} seafoamTable) table=seafoamTable;
        else if (rulesVersion == FiveIslandRulesVersion && candidate.Map == PowerPlantEncounterTables.Map) table = PowerPlantEncounterTables.Land;
        else if (rulesVersion == FiveIslandRulesVersion && SevenIslandEncounterTables.Land.FirstOrDefault(t => t.Map == candidate.Map) is { } sevenTable) table = sevenTable;
        else if (rulesVersion == FiveIslandRulesVersion && CeruleanCaveEncounterTables.Land.FirstOrDefault(t => t.Map == candidate.Map) is { } caveTable) table = caveTable;
        else if (rulesVersion == FiveIslandRulesVersion && FiveIslandEncounterTables.Land.FirstOrDefault(t=>t.Map==candidate.Map) is {} fiveTable) table = fiveTable;
        else if (rulesVersion is SixIslandRulesVersion or FiveIslandRulesVersion && SixIslandEncounterTables.Land.FirstOrDefault(t=>t.Map==candidate.Map) is {} sixTable) table = sixTable;
        else if (rulesVersion is FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && FourIslandEncounterTables.Land.FirstOrDefault(t=>t.Map==candidate.Map) is {} fourTable) table = fourTable;
        else if (rulesVersion is SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && SeviiRubyEncounterTables.Land.FirstOrDefault(t=>t.Map==candidate.Map) is {} rubyTable) table = rubyTable;
        else if (rulesVersion is VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && VictoryRoadEncounterTables.Land.FirstOrDefault(t=>t.Map==candidate.Map) is {} victoryTable) table = victoryTable;
        else if (rulesVersion is FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && FirstSeviiEncounterTables.Land.FirstOrDefault(t => t.Map == candidate.Map) is { } seviiTable) table = seviiTable;
        else if (rulesVersion is CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && CinnabarLandEncounterTables.All.FirstOrDefault(t => t.Map == candidate.Map) is { } cinnabarTable) table = cinnabarTable;
        else if(rulesVersion is SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && SafariLandEncounterTables.All.FirstOrDefault(t=>t.Map==candidate.Map) is {} safariTable)table=safariTable;
        else if(rulesVersion is FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && FuchsiaRouteLandEncounterTables.All.FirstOrDefault(t=>t.Map==candidate.Map) is {} fuchsiaTable)table=fuchsiaTable;
        else if (rulesVersion is Route3RulesVersion or MtMoonRulesVersion or BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && candidate.Map == Route3Table.Map) table = Route3Table;
        else if (rulesVersion is Route22RulesVersion or Route3RulesVersion or MtMoonRulesVersion or BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && candidate.Map == Route22Table.Map) table = Route22Table;
        else if (rulesVersion is MtMoonRulesVersion or BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && MtMoonLandEncounterTables.All.FirstOrDefault(t => t.Map == candidate.Map) is {} mtMoonTable) table = mtMoonTable;
        else if (rulesVersion is BillRulesVersion or VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && BillLandEncounterTables.All.FirstOrDefault(t=>t.Map==candidate.Map) is {} billTable) table=billTable;
        else if (rulesVersion is VermilionRulesVersion or SsAnneRulesVersion or Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && VermilionLandEncounterTables.All.FirstOrDefault(t=>t.Map==candidate.Map) is {} vermilionTable) table=vermilionTable;
        else if (rulesVersion is Route11RulesVersion or RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && Route11LandEncounterTables.All.FirstOrDefault(t=>t.Map==candidate.Map) is {} route11Table) table=route11Table;
        else if (rulesVersion is CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && CeladonApproachLandEncounterTables.All.FirstOrDefault(t=>t.Map==candidate.Map) is {} celadonTable) table=celadonTable;
        else if (rulesVersion is RockTunnelRulesVersion or CeladonApproachRulesVersion or CeladonGymRulesVersion or RocketHideoutRulesVersion or PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && RockTunnelLandEncounterTables.All.FirstOrDefault(t=>t.Map==candidate.Map) is {} rockTable) table=rockTable;
        else if(rulesVersion is PokemonTowerRulesVersion or SnorlaxRulesVersion or FuchsiaRulesVersion or SafariRulesVersion or CinnabarRulesVersion or FirstSeviiRulesVersion or VictoryRoadRulesVersion or SeviiRubyRulesVersion or FourIslandRulesVersion or SixIslandRulesVersion or FiveIslandRulesVersion && PokemonTowerLandEncounterTables.All.FirstOrDefault(t=>t.Map==candidate.Map) is {} towerTable) table=towerTable;
        else return false;
        var slot = table.Slots[candidate.Slot];
        return candidate.Species == slot.Species && candidate.Level == slot.MinimumLevel;
    }
}
