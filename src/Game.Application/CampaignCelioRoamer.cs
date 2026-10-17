using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Battle;
using Game.Domain.Encounters;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;
using Game.Domain.Roaming;
using Game.Domain.WildBattle;

namespace Game.Application;

public sealed record CampaignCelioRoamerEncounter(MapId Map, RoamerEncounterMethod Method,
    RoamerIndividual Individual, uint OriginalTrainerId);
public sealed record CampaignCelioRoamerSettlement(CampaignCelioRoamerEncounter Encounter,
    RoamerBattleResult Battle, RoamerState Before, RoamerState After,
    RngState RngBefore, RngState RngAfter, ImmutableArray<RandomTrace> Trace);

public sealed partial class CampaignSession
{
    private RoamerMap CelioSourceMap(MapId map) =>
        _content.CelioRoamerMaps is {} maps && maps.TryGetValue(map, out var source)
            ? source : throw new InvalidDataException("celio_roamer_source_map_missing");

    private CampaignState ApplyCelioRoamerTransition(CampaignState state, RoamerTransition transition)
    {
        var result = SourceRoamerRules.Transition(state.Roamer ?? RoamerState.Empty,
            transition, CelioSourceMap(state.World.Player.MapId), state.World.Rng);
        return state with { Roamer = result.State, World = state.World with { Rng = result.Rng } };
    }

    private CampaignState BeginCelioRoamerBattle(CampaignState state,
        RoamerEncounterCandidate candidate, RoamerEncounterMethod method, ImmutableArray<EncounterRandomTrace> gateTrace)
    {
        if (!CelioCompleted(state) || state.Roamer is not {} roamer ||
            candidate.Individual != roamer.Individual || roamer.Runtime.Location != CelioSourceMap(state.World.Player.MapId) ||
            method is not (RoamerEncounterMethod.Land or RoamerEncounterMethod.Surfing or RoamerEncounterMethod.SweetScentLand or RoamerEncounterMethod.SweetScentWater) ||
            state.SafariVisit is { Active: true } || long.MaxValue - Revision < 4096)
            throw new InvalidDataException("celio_roamer_field_origin");
        state = ResetFieldPoisonCounterForBattle(state);
        var exploration = state.Exploration ?? throw new InvalidDataException("exploration_missing");
        if (exploration.NextWildBattleId is < 1 or long.MaxValue || state.WildBattle is not null ||
            state.Battle is not null || state.Party.IsDefaultOrEmpty || state.Party.Length > 6)
            throw new InvalidDataException("wild_battle_context_invalid");
        int active = Enumerable.Range(0, state.Party.Length).First(i => !state.Party[i].IsEgg && state.Party[i].Pokemon.CurrentHp > 0);
        var owned = state.Party[active];
        var started = SourceWildBattlePolicy.StartCelioRoamer(SafariOrdinaryOwnership!, candidate.Individual,
            state.TrainerId, SourceTrainerEntryParty(state), active, state.World.Rng, CampaignBattleTerrain(state));
        if (!started.Succeeded) throw new InvalidDataException(started.Fault);
        var frame = PrepareCanonicalWildFrame(state,started.State!);
        var battle = CampaignWildReadProjection.From(frame);
        var origin = new CampaignCelioRoamerEncounter(state.World.Player.MapId, method, candidate.Individual, state.TrainerId);
        var presentation = new CampaignWildBattle("wild-battle:" + exploration.NextWildBattleId, owned,
            frame, ProjectCanonicalWildEvents(started.Events), 0, battle.Player.Pokemon, battle.Opponent.Pokemon,
            CampaignBattlePhase.Events, [], 0, started.Trace)
        {
            DisplayBatch = new(CampaignBattleDisplayBodies.FromSlots(started.EntrySlots)), SourceEvents=started.Events,EntryLevels=state.Party.Select(p=>p.Pokemon.Level).ToImmutableArray(),
            CelioRoamer = origin, CelioRoamerEncounterTrace = gateTrace, ActivePartyIndex = active,
            Origin = new(_content.MapDefinition(origin.Map).RegionMapSection, SourceRoamerRules.Level,
                _content.PokemonContent!.SpeciesNames[battle.Opponent.Pokemon.SourceSpeciesId]),
            FaintedMembers = state.Party.Where(p => p.Pokemon.CurrentHp == 0).Select(p => p.InstanceId).ToImmutableSortedSet(),
            OpponentHpAtLastSwitch = battle.Opponent.Pokemon.CurrentHp
        };
        return PrepareWildPresentation(state with { WildBattle = presentation,
            Exploration = exploration with { NextWildBattleId = checked(exploration.NextWildBattleId + 1) },
            World = state.World with { Rng = battle.Rng }, SeenSpecies = state.SeenSpecies.Add(battle.Opponent.Pokemon.SourceSpeciesId),
            Motions = [], PresentationTicks = 0 });
    }

    // Runs once after all battle events, before capture delivery/reward or blackout. A later
    // presentation ACK sees the persisted receipt and never repeats the relocation draws.
    private CampaignState SettleCelioRoamer(CampaignState state)
    {
        var battle = state.WildBattle!;
        if (battle.CelioRoamer is not {} origin) return state;
        if (battle.CelioRoamerSettlement is not null) return state;
        if (state.Roamer is not {} before || state.World.Rng != battle.Frame!.Rng ||
            origin.Map != state.World.Player.MapId || origin.Individual != before.Individual ||
            battle.Frame!.WildEncounter?.CelioRoamer?.Individual != origin.Individual)
            throw new InvalidDataException("celio_roamer_settlement_context");
        var result = SourceWildBattlePolicy.SettlementRoamer(CanonicalWildPokemon,battle.Frame!);
        var settled = SourceRoamerRules.Settle(before, result, state.World.Rng);
        var receipt = new CampaignCelioRoamerSettlement(origin, result, before, settled.State,
            state.World.Rng, settled.Rng, settled.Trace);
        return state with { Roamer = settled.State, World = state.World with { Rng = settled.Rng },
            WildBattle = battle with { CelioRoamerSettlement = receipt } };
    }

    private bool IsCelioRoamerHistory(CampaignState state, CampaignWildHistory history)
    {
        if (history.CelioRoamer is not {} receipt ||
            state.CelioHandoff?.Initialization is not {} initialization || history.FiveIsland is not null ||
            history.SixIsland is not null || history.FourIsland is not null || history.SeviiRuby is not null ||
            history.VictoryRoad is not null || history.LostelleHypno is not null || history.Safari ||
            history.ScriptedMarowak || history.ScriptedSnorlaxRoute != 0 || receipt.Trace.IsDefault ||
            receipt.Encounter?.Individual is null || receipt.Before?.Runtime is null || receipt.After?.Runtime is null ||
            receipt.Battle is null || initialization.InitialState?.Individual is null)
            return false;
        try
        {
            var origin = receipt.Encounter;
            var initial = initialization.InitialState.Individual!;
            if (origin.Individual with { CurrentHp = initial.CurrentHp, Status = initial.Status, Active = initial.Active } != initial ||
                origin.OriginalTrainerId != state.TrainerId || origin.Method is not (RoamerEncounterMethod.Land or RoamerEncounterMethod.Surfing or RoamerEncounterMethod.SweetScentLand or RoamerEncounterMethod.SweetScentWater) ||
                (origin.Method is RoamerEncounterMethod.Land or RoamerEncounterMethod.SweetScentLand ? !_content.LandEncounters!.ContainsKey(origin.Map) : !_content.WaterEncounters!.ContainsKey(origin.Map)) ||
                receipt.Before.Individual != origin.Individual || receipt.Before.Runtime.Location != CelioSourceMap(origin.Map) ||
                receipt.Battle.EncounteredIndividual != origin.Individual || history.Level != SourceRoamerRules.Level ||
                history.SourceSpeciesId != SourceRoamerRules.SourceSpeciesId(origin.Individual.Species) ||
                receipt.RngBefore.Cursor < initialization.RngAfter.Cursor || receipt.RngAfter.Cursor > state.World.Rng.Cursor ||
                history.Outcome != (receipt.Battle.Outcome switch {
                    RoamerBattleOutcome.Won => WildBattleOutcome.Won, RoamerBattleOutcome.Lost => WildBattleOutcome.Lost,
                    RoamerBattleOutcome.Drew => WildBattleOutcome.Draw, RoamerBattleOutcome.Caught => WildBattleOutcome.Caught,
                    RoamerBattleOutcome.Ran or RoamerBattleOutcome.MonFled or RoamerBattleOutcome.PlayerTeleported => WildBattleOutcome.Escaped,
                    _ => WildBattleOutcome.Ongoing }) ||
                history.Outcome != WildBattleOutcome.Won && history.ExperienceGained != 0) return false;
            var expected = SourceRoamerRules.Settle(receipt.Before, receipt.Battle, receipt.RngBefore);
            return expected.Rng == receipt.RngAfter && expected.Trace.SequenceEqual(receipt.Trace) &&
                expected.State.Individual == receipt.After.Individual &&
                expected.State.Runtime.Location == receipt.After.Runtime.Location &&
                !receipt.After.Runtime.PlayerHistory.IsDefault &&
                expected.State.Runtime.PlayerHistory.SequenceEqual(receipt.After.Runtime.PlayerHistory);
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or InvalidDataException or OverflowException)
        { return false; }
    }

    private string? ValidateCelioRoamerProgress(CampaignState state)
    {
        var latest = state.Exploration!.Battles.LastOrDefault(h => h.CelioRoamer is not null);
        // Roamer is the authoritative live individual. Movement changes its runtime route
        // without changing HP/status/identity, so only the latest settlement's individual
        // must agree. Old encounters are not a proof chain from initialization to now.
        if (latest is not null && !IsCelioRoamerHistory(state, latest))
            return "save_celio_roamer_progress";
        var individual = latest?.CelioRoamer?.After.Individual ??
            state.CelioHandoff?.Initialization?.InitialState.Individual;
        return state.Roamer?.Individual == individual ? null : "save_celio_roamer_live_settlement";
    }

    private bool IsCelioRoamerCapture(CampaignState state, CampaignCapturedPokemon capture)
    {
        if (capture.CelioRoamer is not {} origin || capture.FiveIsland is not null || capture.SixIsland is not null || capture.FourIsland is not null)
            return false;
        var history = state.Exploration!.Battles.SingleOrDefault(h => h.Id == capture.BattleId);
        if (history?.CelioRoamer is not {} receipt || origin != receipt.Encounter || !IsCelioRoamerHistory(state, history) ||
            history.Outcome != WildBattleOutcome.Caught || history.SourceSpeciesId != capture.SourceSpeciesId ||
            capture.MetLevel != SourceRoamerRules.Level || capture.MetLocation != _content.MapDefinition(origin.Map).RegionMapSection ||
            capture.Personality != origin.Individual.Personality) return false;
        var iv = SourceRoamerRules.UnpackIvs(origin.Individual.PackedIvs);
        return capture.Ivs == new SixStats(iv[0], iv[1], iv[2], iv[3], iv[4], iv[5]);
    }

}
