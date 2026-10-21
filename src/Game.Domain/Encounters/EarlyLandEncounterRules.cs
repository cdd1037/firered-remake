using System.Collections.Immutable;
using Game.Domain.Pokemon;
using Game.Domain.Items;
using Game.Domain.Roaming;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.Encounters;

/// <summary>
/// FireRed land encounters with explicit map admission and the current walking process.
/// Ordinary table values come from content; a candidate remains pending until generation and battle start commit.
/// </summary>
public static class EarlyLandEncounterRules
{
    public const string SourceCommit = Route1EncounterRules.SourceCommit;
    public const string FiveIslandRulesVersion = "firered-five-island-walking-candidates-v1";
    // Movement adapter identity only; Safari encounters share the current walking process.
    public const string SafariRulesVersion = "firered-safari-walking-candidates-v1";
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
    public static EarlyLandEncounterTable Route1Table { get; } = new(Route1EncounterRules.Map, 21,
        Route1EncounterRules.Slots.Select(s => new EarlyLandEncounterSlot(s.Index, s.Species,
            s.MinimumLevel, s.MaximumLevel, s.Weight)).ToImmutableArray());
    public static EarlyLandEncounterTable Route22Table { get; } = new(new("map.route-22"), 21,
        new[] { ("RATTATA", 3), ("MANKEY", 3), ("RATTATA", 4), ("MANKEY", 4),
            ("RATTATA", 2), ("MANKEY", 2), ("SPEAROW", 3), ("SPEAROW", 5),
            ("RATTATA", 5), ("MANKEY", 5), ("RATTATA", 5), ("MANKEY", 5) }
            .Select((s, i) => new EarlyLandEncounterSlot(i, new("pret:SPECIES_" + s.Item1),
                (byte)s.Item2, (byte)s.Item2, SlotWeights[i])).ToImmutableArray());

    // Source Route3 species and ordered encounter slots.
    public static ImmutableArray<SpeciesId> Route3WildSpecies { get; } =
        [new("pret:SPECIES_EKANS"), new("pret:SPECIES_NIDORAN_F"),
         new("pret:SPECIES_NIDORAN_M"), new("pret:SPECIES_JIGGLYPUFF")];
    public static EarlyLandEncounterTable Route3Table { get; } = new(new("map.route-3"), 21,
        new[] { ("SPEAROW", 6), ("PIDGEY", 6), ("SPEAROW", 7), ("MANKEY", 7),
            ("NIDORAN_M", 6), ("PIDGEY", 7), ("SPEAROW", 8), ("JIGGLYPUFF", 3),
            ("NIDORAN_M", 7), ("JIGGLYPUFF", 5), ("NIDORAN_F", 6), ("JIGGLYPUFF", 7) }
            .Select((s, i) => new EarlyLandEncounterSlot(i, new("pret:SPECIES_" + s.Item1),
                (byte)s.Item2, (byte)s.Item2, SlotWeights[i])).ToImmutableArray());

    public static EarlyLandEncounterInitialization SeedFromGlobal(RngState globalRng, ushort previousMetatileBehavior = 0,
        string rulesVersion = FiveIslandRulesVersion)
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

    public static EarlyLandEncounterResult Check(EarlyLandEncounterState state, OrdinaryLandEncounterDefinition definition,
        EarlyLandEncounterCheck check, EarlyLandEncounterProfile profile, RngState globalRng)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return CheckValidated(state, definition.Table, check, profile, globalRng, ordinary: definition);
    }

    /// <summary>Celio's authenticated party138 entry; the inherited Five process and source tables
    /// remain unchanged. The roamer gate runs only after the ordinary rate test succeeds.</summary>
    public static EarlyLandEncounterResult Check(GamePokemonCatalog owned,
        EarlyLandEncounterState state, EarlyLandEncounterTable table, EarlyLandEncounterCheck check,
        ImmutableArray<PartyPokemon> party, ushort repelSteps, RngState globalRng,
        RoamerState roamer, RoamerMap playerMap, bool wildEncountersDisabled = false,
        bool extendedRepel = false, bool bicycle = false, OrdinaryLandEncounterDefinition? ordinary = null)
    {
        var gate = CelioEncounterRules.Validate(owned, party, repelSteps, roamer, playerMap, extendedRepel);
        if (state.RulesVersion != FiveIslandRulesVersion) throw new ArgumentException("celio_land_profile");
        var lead = party[0];
        // Only these source field modifiers matter; all real party fields were authenticated above.
        var profile = new EarlyLandEncounterProfile(RulesVersion: FiveIslandRulesVersion,
            Movement: bicycle ? EncounterMovementMode.Bicycle : EncounterMovementMode.Walking,
            LeadAbility: !lead.IsEgg && lead.Pokemon.AbilityId is "ABILITY_STENCH" or "ABILITY_ILLUMINATE"
                ? lead.Pokemon.AbilityId : "ABILITY_NONE",
            LeadHeldItem: lead.HeldItem == "ITEM_CLEANSE_TAG" ? lead.HeldItem : "ITEM_NONE",
            WildEncountersDisabled: wildEncountersDisabled);
        int? repelLevel = party.FirstOrDefault(p => !p.IsEgg && p.Pokemon.CurrentHp > 0)?.Pokemon.Level;
        return CheckValidated(state, table, check, profile, globalRng, gate, repelSteps, repelLevel, ordinary);
    }

    private static EarlyLandEncounterResult CheckValidated(EarlyLandEncounterState state,
        EarlyLandEncounterTable table, EarlyLandEncounterCheck check, EarlyLandEncounterProfile profile,
        RngState globalRng, CelioRoamerEncounterGate? roamerGate = null,
        ushort repelSteps = 0, int? repelLevel = null, OrdinaryLandEncounterDefinition? ordinary = null)
    {
        if (ordinary is null) ValidateState(state, table);
        else ordinary.ValidateState(state, table);
        ArgumentNullException.ThrowIfNull(check);
        if (ordinary is not null) profile = ordinary.FieldEffects(profile);
        ValidateProfile(profile);
        if (profile.RulesVersion != state.RulesVersion)
            throw new NotSupportedException("Encounter state and walking profile versions must agree.");
        if (state.Pending is not null) throw new InvalidOperationException("A wild candidate is pending; complete its generation/start transaction before another field check.");
        if (globalRng.AlgorithmId != Gen3Rng.AlgorithmId) throw new NotSupportedException("Unknown global RNG algorithm.");
        if (check.Map != table.Map) throw new ArgumentException("The current map and authenticated land table disagree.");
        if (!Enum.IsDefined(check.Trigger) || !Enum.IsDefined(check.EncounterType)) throw new NotSupportedException("Unknown field check or encounter terrain.");
        if (ordinary is not null) ordinary.ValidateCheck(check);
        else
        {
            bool caveAttributes = IsCaveMap(table.Map);
            if (check.EncounterType == EncounterTileType.Water ||
                (!caveAttributes && check.EncounterType == EncounterTileType.Land && check.MetatileBehavior != TallGrassBehavior))
                throw new NotSupportedException("Only admitted source cave land, tall-grass land and non-encounter tiles are supported.");
            if (!caveAttributes && check.EncounterType == EncounterTileType.None && check.MetatileBehavior == TallGrassBehavior)
                throw new ArgumentException("Tall grass must retain its source land encounter attribute.");
        }

        return CheckCore(state, table, check, profile.WildEncountersDisabled, globalRng,
            stench: profile.LeadAbility == "ABILITY_STENCH",
            illuminate: profile.LeadAbility == "ABILITY_ILLUMINATE", cleanseTag: profile.LeadHeldItem == "ITEM_CLEANSE_TAG",
            repelSteps: repelSteps, repelLevel: repelLevel, roamerGate: roamerGate,bicycle: profile.Movement==EncounterMovementMode.Bicycle);
    }

    // Public profiles validate their own contracts before entering this single source engine.
    internal static EarlyLandEncounterResult CheckCore(EarlyLandEncounterState state, EarlyLandEncounterTable table,
        EarlyLandEncounterCheck check, bool wildEncountersDisabled, RngState globalRng, bool stench = false, bool illuminate = false, bool cleanseTag = false,
        Func<int, int>? chooseSlot = null, string slotPurpose = "encounter.land-slot", ushort repelSteps = 0, int? repelLevel = null,
        CelioRoamerEncounterGate? roamerGate = null, RoamerEncounterMethod roamerMethod = RoamerEncounterMethod.Land, bool bicycle = false)
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
        state = state with { CachedLeadHasCleanseTag = cleanseTag };
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

        int modifiedRate = table.RawEncounterRate * 16;
        if (bicycle) modifiedRate = modifiedRate * 80 / 100;
        modifiedRate += state.EncounterRateBuff * 16 / 200;
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

    public static EarlyLandEncounterState CompleteCandidate(EarlyLandEncounterState state, EarlyLandEncounterTable table,
        OrdinaryLandEncounterDefinition? ordinary = null)
    {
        if (ordinary is null) ValidateState(state, table);
        else ordinary.ValidateState(state, table);
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
        // Map IDs determine admission; table labels and build provenance do not affect encounters.
        RegisteredLandDefinitions.SourceTables.TryGetValue(table.Map, out var registeredTable);
        if (registeredTable is null && table.Map.Value is not
            ("map.route-1" or "map.route-2" or "map.viridian-forest" or "map.route-22" or "map.route-3"))
            throw new NotSupportedException("The land table map is not registered.");
        ValidateTableDefinition(table);
        // Special origin/receipt consumers still require their source definitions.
        // Ordinary regions consume the supplied table throughout candidate generation.
        bool ordinary = RegisteredLandDefinitions.OrdinaryMaps.Contains(table.Map);
        if (!ordinary && registeredTable is not null &&
            (table.RawEncounterRate != registeredTable.RawEncounterRate || !table.Slots.SequenceEqual(registeredTable.Slots)))
            throw new ArgumentException("Special encounter origins require their source land tables.");
    }

    /// <summary>Gameplay shape only. Map and species admission belongs to the loaded catalog.</summary>
    public static void ValidateTableDefinition(EarlyLandEncounterTable table)
    {
        ArgumentNullException.ThrowIfNull(table);
        if (!StableId.IsValid(table.Map.Value) || table.RawEncounterRate is < 1 or > 255 ||
            table.Slots.IsDefault || table.Slots.Length != SlotWeights.Length)
            throw new ArgumentException("Invalid source land map, rate or slot count.");
        for (int i = 0; i < table.Slots.Length; i++)
        {
            var slot = table.Slots[i];
            if (slot is null || slot.Index != i || slot.Weight != SlotWeights[i] || string.IsNullOrWhiteSpace(slot.Species.Value) ||
                slot.MinimumLevel is < 1 or > 100 || slot.MaximumLevel < slot.MinimumLevel || slot.MaximumLevel > 100)
                throw new ArgumentException("Invalid early land slot metadata.");
        }
    }

    internal static bool RequiresSpecialTable(MapId map) =>
        RegisteredLandDefinitions.SourceTables.ContainsKey(map) && !RegisteredLandDefinitions.OrdinaryMaps.Contains(map);

    public static void ValidateState(EarlyLandEncounterState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        ValidateRulesVersion(state.RulesVersion);
        if (state.RateRng.AlgorithmId != EncounterRateRng.AlgorithmId)
            throw new NotSupportedException("Unsupported early land process version or rate RNG.");
        // The counter can retain the preceding map's cooldown until the caller's map-load reset.
        if (state.PreviousMetatileBehavior > byte.MaxValue || state.StepsSinceLastEncounter > 21)
            throw new ArgumentException("Encounter state is outside the bounded walking profile.");
        if (state.Pending is {} pending && (pending.Candidate is null ||
            string.IsNullOrWhiteSpace(pending.Candidate.Map.Value) || string.IsNullOrWhiteSpace(pending.Candidate.Species.Value) ||
            pending.Candidate.Slot is < 0 or >= 12 ||
            pending.Candidate.Level is < 1 or > 100))
            throw new ArgumentException("Invalid pending early land candidate.");
    }

    public static void ValidateState(EarlyLandEncounterState state, EarlyLandEncounterTable table)
    {
        ValidateTable(table); ValidateState(state);
        if (state.Pending is not {} pending) return;
        var candidate = pending.Candidate;
        if (pending.MetatileBehavior != TallGrassBehavior && !IsCaveMap(table.Map))
            throw new ArgumentException("Pending candidate requires grass or an admitted cave map.");
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
        // The unbound entry takes field effects, not a second authorization list of
        // every possible party ability/item. Catalog-bound callers authenticate full
        // identifiers before projecting the three effects that FireRed uses here.
        if (!(profile.Movement == EncounterMovementMode.Walking ||
                profile.Movement == EncounterMovementMode.Bicycle) ||
            profile.RepelSteps != 0 || profile.LeadAbility is not ("ABILITY_NONE" or "ABILITY_STENCH" or "ABILITY_ILLUMINATE") ||
            profile.LeadHeldItem is not ("ITEM_NONE" or "ITEM_CLEANSE_TAG") ||
            profile.WhiteFluteActive || profile.BlackFluteActive || profile.RoamerActive ||
            profile.QuestLogPlayback || profile.Weather != "WEATHER_NONE")
            throw new NotSupportedException("Unsupported walking field effects; full party identifiers require a catalog-bound entry.");
    }

    internal static void ValidateRulesVersion(string rulesVersion)
    {
        if (rulesVersion != FiveIslandRulesVersion)
            throw new NotSupportedException("Unsupported early land process version.");
    }

    // Deferred as a group: source table constructors themselves read SlotWeights.
    // Keeping this cache outside the outer type initializer avoids a static-init cycle.
    private static class RegisteredLandDefinitions
    {
        internal static readonly ImmutableDictionary<MapId, EarlyLandEncounterTable> SourceTables = BuildSourceTables();
        internal static readonly ImmutableHashSet<MapId> OrdinaryMaps =
            MtMoonLandEncounterTables.All
                .Concat(BillLandEncounterTables.All)
                .Concat(VermilionLandEncounterTables.All)
                .Concat(Route11LandEncounterTables.All)
                .Concat(RockTunnelLandEncounterTables.All)
                .Concat(CeladonApproachLandEncounterTables.All)
                .Concat(FuchsiaRouteLandEncounterTables.All)
                .Concat(CinnabarLandEncounterTables.All)
                .Select(table => table.Map)
                .Concat(new MapId[] { new("map.route-1"), new("map.route-2"), new("map.viridian-forest"),
                    new("map.route-22"), new("map.route-3") })
                .ToImmutableHashSet();

        private static ImmutableDictionary<MapId, EarlyLandEncounterTable> BuildSourceTables()
        {
            var tables = MtMoonLandEncounterTables.All
                .Concat(BillLandEncounterTables.All)
                .Concat(VermilionLandEncounterTables.All)
                .Concat(Route11LandEncounterTables.All)
                .Concat(RockTunnelLandEncounterTables.All)
                .Concat(CeladonApproachLandEncounterTables.All)
                .Concat(PokemonTowerLandEncounterTables.All)
                .Concat(FuchsiaRouteLandEncounterTables.All)
                .Concat(SafariLandEncounterTables.All)
                .Concat(CinnabarLandEncounterTables.All)
                .Concat(FirstSeviiEncounterTables.Land)
                .Concat(VictoryRoadEncounterTables.Land)
                .Concat(SeviiRubyEncounterTables.Land)
                .Concat(FourIslandEncounterTables.Land)
                .Concat(SixIslandEncounterTables.Land)
                .Concat(FiveIslandEncounterTables.Land)
                .Concat(CeruleanCaveEncounterTables.Land)
                .Concat(SevenIslandEncounterTables.Land)
                .Append(PowerPlantEncounterTables.Land)
                .Concat(SeafoamEncounterTables.Land)
                .Concat(SourceTableEncounterRegistry.Land);
            var result = ImmutableDictionary.CreateBuilder<MapId, EarlyLandEncounterTable>();
            // Preserve the previous ordered FirstOrDefault lookup even if families overlap.
            foreach (var table in tables)
                if (!result.ContainsKey(table.Map)) result.Add(table.Map, table);
            return result.ToImmutable();
        }
    }

    private static bool IsCurrentCaveMap(MapId map) => LostCaveEncounterTables.Land.Any(t => t.Map == map) ||
        SeafoamEncounterTables.Land.Any(t => t.Map == map) || CeruleanCaveEncounterTables.IsCave(map) ||
        SevenIslandEncounterTables.IsChamber(map) || map == PowerPlantEncounterTables.Map;

    private static bool IsCaveMap(MapId map) =>
        IsCurrentCaveMap(map) || FourIslandEncounterTables.IsCave(map) || SeviiRubyEncounterTables.IsCave(map) ||
        VictoryRoadEncounterTables.IsCave(map) || CinnabarLandEncounterTables.IsCave(map) ||
        MtMoonLandEncounterTables.IsCave(map) || Route11LandEncounterTables.IsCave(map) ||
        RockTunnelLandEncounterTables.IsCave(map) || PokemonTowerLandEncounterTables.IsCave(map);
}
