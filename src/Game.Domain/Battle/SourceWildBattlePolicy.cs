using System.Collections.Immutable;
using System.Text.Json.Serialization;
using Game.Domain.Capture;
using Game.Domain.Encounters;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Roaming;
using Game.Domain.WildBattle;

namespace Game.Domain.Battle;

/// <summary>Original B_OUTCOME values, shared by all wild callbacks rather than inferred from display events.</summary>
public enum SourceWildBattleOutcome
{
    Won = 1, Lost = 2, Drew = 3, Ran = 4, PlayerTeleported = 5,
    MonFled = 6, Caught = 7, NoSafariBalls = 8, Forfeited = 9, MonTeleported = 10
}

/// <summary>Current encounter provenance and outcome only. Action/residual cursors live in the frame.</summary>
public sealed record SourceWildEncounterContext
{
    public const string Version = "firered-current-wild-policy-v1";
    public string RulesVersion { get; init; } = Version;
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceWildBattleOutcome? SourceOutcome { get; init; }
    [JsonIgnore] public FishingTaskProfile? FishingTables { get; init; }
    [JsonIgnore] public OrdinaryLandBattleBinding? OrdinaryLand { get; init; }
    [JsonIgnore] public OrdinaryWaterBattleBinding? OrdinaryWater { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SourceTableWildOrigin? SourceTable { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public RockSmashWildOrigin? RockSmash { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public FishingEncounterOrigin? Fishing { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public CelioRoamerBattleOrigin? CelioRoamer { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public PowerPlantWildOrigin? PowerPlant { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SeafoamWildOrigin? Seafoam { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public EventIslandWildOrigin? EventIsland { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SevenIslandWildOrigin? SevenIsland { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public CeruleanCaveWildOrigin? CeruleanCave { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public VictoryRoadWildOrigin? VictoryRoad { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SeviiRubyWildOrigin? SeviiRuby { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public FourIslandWildOrigin? FourIsland { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public SixIslandWildOrigin? SixIsland { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public FiveIslandWildOrigin? FiveIsland { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] public PokemonTowerBattleKind? TowerBattleKind { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool ScriptedSnorlax { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool FirstSeviiWild { get; init; }
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)] public bool LostelleHypno { get; init; }
}

public sealed record SourceWildEntryResult(SourceBattleFrame? State, ImmutableArray<SourceBattleEvent> Events,
    ImmutableArray<RandomTrace> Trace, string? Fault)
{
    [System.Text.Json.Serialization.JsonIgnore]
    public ImmutableArray<SourceBattleSlot> EntrySlots { get; init; } = [];
    public bool Succeeded => Fault is null;
}
public sealed record SourceCaptureSelection(CaptureBall Ball, WildBattleCaptureContext Context);

/// <summary>Wild admission, selection and terminal policies for the one shared lifecycle.
/// This type has no turn loop, action cursor or residual scheduler.</summary>
public static partial class SourceWildBattlePolicy
{
    public static SourceWildEntryResult Start(SourceMechanicsCatalog mechanics,
        ImmutableArray<WildBattleCombatant> playerParty, int activeIndex, WildBattleCombatant opponent,
        RngState rng, WildBattleTerrain terrain = WildBattleTerrain.Plain) =>
        StartCore(mechanics, null, playerParty, activeIndex, opponent, rng, new(), terrain, false);

    public static SourceWildEntryResult StartOwned(GamePokemonCatalog owned,
        ImmutableArray<WildBattleCombatant> playerParty, int activeIndex, WildBattleCombatant opponent,
        RngState rng, SourceWildEncounterContext? encounter = null,
        WildBattleTerrain terrain = WildBattleTerrain.Plain, bool oldRodFishing = false)
    {
        if (owned is null || SourceBattleAdmission.ValidateOwnedEntry(owned, playerParty, activeIndex) is { })
            return EntryFail("source_wild_owned_entry");
        if (opponent?.Pokemon is null || !AdmitsOpponent(owned, encounter ?? new(), opponent))
            return EntryFail("source_campaign_opponent_species");
        return StartCore(owned.Mechanics, owned, playerParty, activeIndex, opponent, rng, encounter ?? new(), terrain, oldRodFishing);
    }

    public static SourceWildEntryResult StartCelioRoamer(GamePokemonCatalog owned, RoamerIndividual individual,
        uint originalTrainerId, ImmutableArray<WildBattleCombatant> playerParty, int activeIndex, RngState rng,
        WildBattleTerrain terrain = WildBattleTerrain.Grass)
    {
        try
        {
            var opponent = CelioRoamerBattleContract.Restore(owned, individual, originalTrainerId);
            return StartOwned(owned, playerParty, activeIndex, opponent, rng,
                new() { CelioRoamer = new(individual, originalTrainerId) }, terrain);
        }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        { return EntryFail("celio_roamer_individual"); }
    }

    private static SourceWildEntryResult StartCore(SourceMechanicsCatalog mechanics, GamePokemonCatalog? owned,
        ImmutableArray<WildBattleCombatant> players, int active, WildBattleCombatant opponent, RngState rng,
        SourceWildEncounterContext encounter, WildBattleTerrain terrain, bool oldRodFishing)
    {
        if (mechanics is null) return EntryFail("source_wild_mechanics");
        if (SourceBattleAdmission.ValidateEntry(mechanics, BattleRulesProfile.CanonicalSourceSingles, players, active, opponent, owned) is { } invalid)
            return EntryFail(invalid);
        if (rng.AlgorithmId != Gen3Rng.AlgorithmId || !Enum.IsDefined(terrain)) return EntryFail("source_wild_entry_options");
        if (players[active].Pokemon.CurrentHp == 0 || opponent.Pokemon.CurrentHp == 0) return EntryFail("wild_battle_entry_fainted");
        foreach (var mon in players.Append(opponent))
        {
            var neutral = new WildBattleCombatant(mon.Pokemon, HeldItem: mon.HeldItem, Status: mon.Status,
                AttackBadgeBoost: mon.AttackBadgeBoost) { DefenseBadgeBoost = mon.DefenseBadgeBoost,
                SpeedBadgeBoost = mon.SpeedBadgeBoost, SpecialBadgeBoost = mon.SpecialBadgeBoost, IsEgg = mon.IsEgg };
            if (mon != neutral) return EntryFail("source_wild_entry_volatile");
            if (WildBattleSourceFieldEffects.IsValidToxic(mon.Status) && mon.Status != WildBattleSourceFieldEffects.ResetToxicCounter(mon.Status))
                return EntryFail("source_wild_entry_status");
        }
        if (oldRodFishing && (encounter.Fishing is { Rod: not FishingRod.Old } ||
            opponent.Pokemon.SourceSpeciesId != 129 || opponent.Pokemon.Level is < 5 or > 10))
            return EntryFail("wild_battle_old_rod_context_unsupported");
        if (ValidateSpecialEntry(encounter, opponent) is { } special) return EntryFail(special);
        WildBattleCalledPartyMember Member(WildBattleCombatant mon) => new(mon.Pokemon, mon.HeldItem, mon.Status, mon.IsEgg);
        var frame = new SourceBattleFrame {
            Layout = SourceBattleLayout.Singles, KoReplacementTiming = KoReplacementTiming.SourceImmediate,
            SourceMechanicsRulesVersion = SourceBattleKernel.Version, SourceCampaignRulesVersion = owned?.RulesVersion, Campaign = owned,
            BattleKind = WildBattleCalledBattleKind.Wild, Rng = rng, SourceAbsentMask = 12, TurnStartAbsentMask = 12,
            Field = new() { Terrain = terrain }, WildEncounter = encounter,
            Parties = [new(BattleSide.Player, players.Select(Member).ToImmutableArray()), new(BattleSide.Opponent, [Member(opponent)])],
            Slots = [new(new(0), new(BattleSide.Player, active), 1, players[active] with { FirstTurnCounter = 2 }),
                new(new(1), new(BattleSide.Opponent, 0), 1, opponent with { FirstTurnCounter = 2 }),
                new(new(2), null, 0, null), new(new(3), null, 0, null)]
        };
        if (SourceBattleIdentityRules.Validate(frame) is { } shape) return EntryFail(shape);
        try
        {
            var trace = ImmutableArray.CreateBuilder<RandomTrace>();
            ushort Draw(string purpose, int bound = 65536) { var d = Gen3Rng.Next(frame.Rng, purpose, bound); frame = frame with { Rng = d.State }; trace.Add(d.Trace); return d.Value; }
            for (int i = 0; i < 4; i++) Draw($"wild-battle.entry.ai-setup.slot-{i}", 16);
            // pokemon.c SetWildMonHeldItem skips BATTLE_TYPE_LEGENDARY. Scripted Electrode
            // is WILD_SCRIPTED and still consumes this draw, including its NONE/NONE item pair.
            if (owned is not null && SourceScriptedEncounterRegistry.Find(encounter.SourceTable)?.Legendary != true && encounter.EventIsland is null && encounter.TowerBattleKind != PokemonTowerBattleKind.RevealedMarowak &&
                encounter.CeruleanCave?.Kind != CeruleanCaveEncounterKind.Mewtwo &&
                encounter.PowerPlant?.Kind != PowerPlantEncounterKind.Zapdos && encounter.Seafoam?.Kind != SeafoamEncounterKind.Articuno)
            {
                int roll = Draw("wild-battle.held-item", 100) % 100;
                string item = HeldItem(encounter, opponent.Pokemon, roll, owned);
                var cell = frame.Get(new(1)); var party = frame.Party(BattleSide.Opponent);
                frame = frame with { Slots = frame.Slots.SetItem(1, cell with { Body = cell.Body! with { HeldItem = item } }),
                    Parties = frame.Parties.SetItem(1, party with { Members = party.Members.SetItem(0, party.Members[0] with { HeldItem = item }) }) };
            }
            var started = SourceBattleEventHooks.Publish(frame, (n, t) => new SourceStartedEvent(n, t));
            var entrySlots = started.State.Slots;
            var entry = SourceFourEntryAbilities.InitializeEntry(mechanics.Catalog, started.State, started.State.Revision);
            if (!entry.Succeeded) return EntryFail(entry.Fault!);
            frame = entry.State; trace.AddRange(entry.Trace);
            ushort ready = Draw("wild-battle.entry.random-turn-number");
            frame = frame with { RandomTurnNumber = ready };
            var published = SourceBattleEventHooks.Publish(frame, (n, t) => new SourceTurnReadyEvent(n, t, ready));
            return new(published.State, [started.Event, .. entry.Events, published.Event], trace.ToImmutable(), null) { EntrySlots = entrySlots };
        }
        catch (Exception e) when (e is ArgumentException or OverflowException or NotSupportedException)
        { return EntryFail(e.Message); }
    }

    private static string HeldItem(SourceWildEncounterContext context, PokemonInstance mon, int roll, GamePokemonCatalog owned) => context switch {
        { OrdinaryLand: { } ordinary } => ordinary.HeldItem(owned, roll),
        { OrdinaryWater: { } water } => water.HeldItem(owned, roll),
        { CelioRoamer: not null } => CanonicalWildPokemonFactory.HeldItem(owned, mon.Species, roll),
        { RockSmash: { } rock } => RockSmashWildBattleContract.HeldItem(rock, roll),
        { SourceTable: { } sourceTable } => SourceTableWildBattleContract.HeldItem(sourceTable, roll),
        { Seafoam: { Kind: SeafoamEncounterKind.OldRod or SeafoamEncounterKind.GoodRod } seafoam, FishingTables: { } tables } =>
            tables.HeldItem(new(seafoam.Kind == SeafoamEncounterKind.GoodRod ? FishingRod.Good : FishingRod.Old,
                seafoam.Map, seafoam.Slot, mon.Species, checked((byte)mon.Level)), roll),
        { Seafoam: { } } => SeafoamPokemonFactory.HeldItem(mon.Species, roll),
        { Fishing: { } fishing } => FishingBattleContract.HeldItem(fishing, roll, context.FishingTables),
        { PowerPlant: not null } => PowerPlantPokemonFactory.HeldItem(mon.Species, roll),
        { SevenIsland: not null } => SevenIslandWildPokemonFactory.HeldItem(mon.Species, roll),
        { CeruleanCave: not null } => CeruleanCaveWildPokemonFactory.HeldItem(mon.Species, roll),
        { ScriptedSnorlax: true } => SnorlaxBattleContract.WildHeldItem(roll),
        { TowerBattleKind: not null } => PokemonTowerBattleContract.WildHeldItem(mon.SourceSpeciesId, roll),
        { FiveIsland: not null } => CanonicalWildPokemonFactory.HeldItem(owned, mon.Species, roll),
        { SixIsland: not null } => CanonicalWildPokemonFactory.HeldItem(owned, mon.Species, roll),
        { FourIsland: not null } => CanonicalWildPokemonFactory.HeldItem(owned, mon.Species, roll),
        { SeviiRuby: not null } => CanonicalWildPokemonFactory.HeldItem(owned, mon.Species, roll),
        { VictoryRoad: not null } => CanonicalWildPokemonFactory.HeldItem(owned, mon.Species, roll),
        { FirstSeviiWild: true } => FirstSeviiWildPokemonFactory.HeldItem(mon.Species, roll),
        _ => throw new ArgumentException("source_wild_encounter_origin_required")
    };

    internal static string? ValidateFrame(SourceBattleFrame frame)
    {
        if (frame.WildEncounter is not { } context) return null;
        if (context.RulesVersion != SourceWildEncounterContext.Version || frame.Layout != SourceBattleLayout.Singles ||
            frame.SourceMechanicsRulesVersion != SourceBattleKernel.Version || frame.BattleKind != WildBattleCalledBattleKind.Wild ||
            frame.Party(BattleSide.Opponent).Members.Length != 1)
            return "source_wild_policy_identity";
        if (context.SourceOutcome is { } sourceOutcome && (!Enum.IsDefined(sourceOutcome) ||
            frame.Outcome != (sourceOutcome switch {
                SourceWildBattleOutcome.Won => WildBattleOutcome.Won,
                SourceWildBattleOutcome.Lost => WildBattleOutcome.Lost,
                SourceWildBattleOutcome.Drew => WildBattleOutcome.Draw,
                SourceWildBattleOutcome.Caught => WildBattleOutcome.Caught,
                _ => WildBattleOutcome.Escaped }))) return "source_wild_terminal_outcome";
        int origins = new object?[] { context.OrdinaryLand, context.OrdinaryWater, context.RockSmash, context.SourceTable, context.Seafoam, context.Fishing, context.CelioRoamer, context.VictoryRoad, context.SeviiRuby, context.FourIsland,
            context.SixIsland, context.FiveIsland, context.PowerPlant, context.EventIsland, context.SevenIsland, context.CeruleanCave, context.TowerBattleKind }.Count(x => x is not null) +
            (context.ScriptedSnorlax ? 1 : 0) + (context.FirstSeviiWild ? 1 : 0);
        if (origins > 1) return "source_wild_conflicting_origins";
        if (origins == 0 && frame.Campaign is not null) return "source_wild_encounter_origin_required";
        var mon = frame.Party(BattleSide.Opponent).Members[0].Pokemon;
        if (context.LostelleHypno && (!context.FirstSeviiWild || mon.SourceSpeciesId != 97 || mon.Level != 30)) return "first_sevii_hypno_source_identity";
        if (context.ScriptedSnorlax && (mon.SourceSpeciesId != 143 || mon.Level != 30)) return "snorlax_scripted_identity";
        if (ValidateTower(context, mon, frame.Outcome) is { } tower) return tower;
        if (context.OrdinaryLand is { } ordinary && (!ordinary.Matches(mon) ||
            frame.Campaign?.Catalog.Species.ContainsKey(mon.Species) != true))
            return "source_wild_ordinary_land_origin";
        if (context.OrdinaryWater is { } water && (!water.Matches(mon) ||
            frame.Campaign?.Catalog.Species.ContainsKey(mon.Species) != true))
            return "source_wild_ordinary_water_origin";
        if (context.SourceTable is { } sourceTable && (!SourceTableWildBattleContract.Matches(sourceTable, mon) ||
            !SourceTableWildBattleContract.IsAdmittedProfile(frame.SourceCampaignRulesVersion)))
            return "source_wild_table_origin";
        if (context.RockSmash is { } rock && (!RockSmashWildBattleContract.Matches(rock, mon) ||
            frame.Campaign?.Catalog.Species.ContainsKey(mon.Species) != true))
            return "source_wild_rock_smash_origin";
        if (context.Fishing is { } fishing && (!FishingBattleContract.Matches(fishing, mon, context.FishingTables) ||
            frame.Campaign?.Catalog.Species.ContainsKey(mon.Species) != true))
            return "source_wild_fishing_origin";
        if (context.Seafoam is {} seafoam && (frame.Campaign is null ||
            !SeafoamWildBattleContract.IsRuntimeKind(seafoam.Kind) || !SeafoamWildBattleContract.Matches(seafoam,mon,context.FishingTables)))
            return "source_wild_seafoam_origin";
        if (context.CelioRoamer is { } roamer && (frame.Campaign is null ||
            !CelioRoamerBattleContract.MatchesIdentity(roamer, mon) || !CelioRoamerBattleContract.MatchesOutcome(roamer, frame.Outcome)))
            return "source_wild_roamer_origin";
        if (context.PowerPlant is { } plant && (frame.Campaign is null || !PowerPlantWildBattleContract.Matches(plant, mon)) ||
            context.EventIsland is { } eventOrigin && (frame.Campaign is null || !EventIslandWildBattleContract.Matches(eventOrigin, mon)) ||
            context.SevenIsland is { } seven && (frame.Campaign is null || !SevenIslandWildBattleContract.Matches(seven, mon)) ||
            context.CeruleanCave is { } cave && (frame.Campaign is null || !CeruleanCaveWildBattleContract.Matches(cave, mon)) ||
            context.VictoryRoad is { } victory && !VictoryRoadWildBattleContract.Matches(victory, mon) ||
            context.SeviiRuby is { } ruby && (ruby.Kind == SeviiRubyEncounterKind.RockSmash || !SeviiRubyWildBattleContract.Matches(ruby, mon)) ||
            context.FourIsland is { } four && !FourIslandWildBattleContract.Matches(four, mon) ||
            context.SixIsland is { } six && (!SixIslandWildBattleContract.IsRuntimeKind(six.Kind) || !SixIslandWildBattleContract.Matches(six, mon)) ||
            context.FiveIsland is { } five && (!FiveIslandWildBattleContract.IsRuntimeKind(five.Kind) || !FiveIslandWildBattleContract.Matches(five, mon)) ||
            context.FirstSeviiWild && !FirstSeviiWildPokemonFactory.WildSpecies.Contains(mon.Species)) return "source_wild_origin";
        foreach (var action in frame.Queue)
        {
            if (action.Kind == SourceActionKind.Run && action.Slot.Side == BattleSide.Opponent && context.CelioRoamer is null)
                return "source_wild_run_actor";
            if (action.Capture is { } capture && (action.Slot.Side != BattleSide.Player || capture.Context?.CaughtSpecies is null ||
                !Enum.IsDefined(capture.Ball) || capture.Ball == CaptureBall.Safari ||
                capture.Context.Encounter != (context.CelioRoamer is null ? CaptureEncounterKind.OrdinaryWild : CaptureEncounterKind.Roamer)))
                return "source_capture_context";
        }
        return null;
    }

    public static WildBattleCalledPartyMember CapturedMember(SourceBattleFrame frame) =>
        SourceBattleIdentityRules.Validate(frame) is null && frame.WildEncounter is not null && frame.Outcome == WildBattleOutcome.Caught
            ? frame.Party(BattleSide.Opponent).Members[0] : throw new ArgumentException("source_capture_delivery_phase");

    public static RoamerBattleResult SettlementRoamer(PokemonCatalog catalog, SourceBattleFrame frame)
    {
        if (SourceFourActionTurns.ValidateCapability(catalog, frame) is not null || frame.Outcome == WildBattleOutcome.Ongoing ||
            frame.WildEncounter?.CelioRoamer is not { SourceOutcome: { } outcome } origin)
            throw new ArgumentException("celio_roamer_settlement_phase");
        var actual = frame.Party(BattleSide.Opponent).Members[0];
        return new(origin.Individual, actual.Pokemon.CurrentHp, actual.Status, outcome);
    }

    private static SourceWildEntryResult EntryFail(string fault) => new(null, [], [], fault);
    private static SourceFourActionResult Fail(SourceBattleFrame frame, string fault) => new(frame, [], [], fault);
}
