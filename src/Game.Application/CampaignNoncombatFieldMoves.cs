using System.Collections.Immutable;
using Game.Domain.Encounters;
using Game.Domain.Randomness;
using Game.Domain.Roaming;
using Game.Domain.World;

namespace Game.Application;

/// <summary>One party-owned field effect. Correlated callbacks cannot repeat a warp or encounter.</summary>
public sealed record CampaignNoncombatFieldEffect(string Move, int PartyIndex, long InstanceId, int SourceSpeciesId,
    string Nickname, TilePosition Origin, TilePosition? Destination, string Phase, string WaitId);

public sealed partial class CampaignSession
{
    public bool CanUseNoncombatFieldMove(int partyIndex, string move) => NoncombatFieldMoveError(State, partyIndex, move) is null;

    private string? NoncombatFieldMoveError(CampaignState state, int index, string move)
    {
        if (!NoncombatFieldMoveRules.IsSupported(move)) return "field_move_unsupported";
        if (!IsStableFieldForParty(state)) return "field_move_event_active";
        if (index < 0 || index >= state.Party.Length) return "field_move_party_index_invalid";
        if (!NoncombatFieldMoveRules.Knows(state.Party[index], move)) return "field_move_missing";
        if (NoncombatFieldMoveRules.IsHealing(move) && !NoncombatFieldMoveRules.CanDonate(state.Party[index], move)) return "field_heal_not_enough_hp";
        if (move == NoncombatFieldMoveRules.Teleport && !FieldFlySourceRules.MapTypeAllowsFly(_content.MapDefinition(state.World.Player.MapId).MapType))
            return "field_move_cannot_use_here";
        if (move is NoncombatFieldMoveRules.Dig or NoncombatFieldMoveRules.Teleport && NoncombatDestination(state, move) is null)
            return "field_move_destination_unavailable";
        return null;
    }

    private TilePosition? NoncombatDestination(CampaignState state, string move)
    {
        TilePosition? arrival;
        if (move == NoncombatFieldMoveRules.Dig)
        {
            if (_content.EscapeRoutes.ResolveArrival(state.World.Player.MapId, state.EscapeDestination, out arrival) is not null) return null;
        }
        else
        {
            // lastHealLocation is the exterior HealLocation warp, not the whiteout nurse coordinate.
            var destination = _content.FieldFly?.Destinations.SingleOrDefault(row => row.HealLocation == state.LastHealingLocation);
            arrival = destination is null ? null : FieldFlyArrival(destination);
        }
        return arrival is not null && ValidFieldTravelArrival(arrival) ? arrival : null;
    }

    private bool ValidFieldTravelArrival(TilePosition arrival) => _content.Maps.TryGetValue(arrival.MapId, out var map) &&
        map.GetTile(arrival.X, arrival.Y) is { Blocked: false } tile && tile.Elevation == arrival.Elevation;

    private bool TryDecideNoncombatFieldMove(CampaignState state, CampaignCommand command, out Decision<CampaignState> decision)
    {
        decision = default!;
        if (state.NoncombatFieldEffect is { } effect)
        {
            decision = AdvanceNoncombatFieldMove(state, command, effect); return true;
        }
        if (command.Kind is not ("field_move" or "field_heal" or "field_move_ack" or "field_move_confirm")) return false;
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (command.Kind is "field_move_ack" or "field_move_confirm") { decision = No("field_move_not_active"); return true; }
        string move = command.Text ?? "";
        if (NoncombatFieldMoveError(state, command.PartyIndex, move) is { } error) { decision = No(error); return true; }
        var owner = state.Party[command.PartyIndex];
        if (command.Kind == "field_heal")
        {
            if (command.Choice is not { } target || target < 0 || target >= state.Party.Length)
            { decision = No("field_heal_party_index_invalid"); return true; }
            if (command.TargetInstanceId != state.Party[target].InstanceId)
            { decision = No("field_heal_target_stale"); return true; }
            var result = NoncombatFieldMoveRules.Heal(state.Party, command.PartyIndex, target, move);
            decision = result.Succeeded ? new(CommandStatus.Accepted, state with { Party = result.Party }) : No(result.Error!);
            return true;
        }
        if (command.TargetInstanceId is { } instance && instance != owner.InstanceId) { decision = No("field_move_owner_mismatch"); return true; }
        if (NoncombatFieldMoveRules.IsHealing(move)) { decision = No("field_heal_target_required"); return true; }
        string phase = move == NoncombatFieldMoveRules.SweetScent ? "show-mon" : "confirm";
        decision = new(CommandStatus.Accepted, state with { NoncombatFieldEffect = new(move, command.PartyIndex, owner.InstanceId,
            owner.Pokemon.SourceSpeciesId, PokemonName(owner), state.World.Player,
            move == NoncombatFieldMoveRules.SweetScent ? null : NoncombatDestination(state, move), phase, NextNoncombatWait(phase)) });
        return true;
    }

    private string NextNoncombatWait(string phase) => "field-move:" + checked(Revision + 1) + ":" + phase;

    private Decision<CampaignState> AdvanceNoncombatFieldMove(CampaignState state, CampaignCommand command, CampaignNoncombatFieldEffect effect)
    {
        Decision<CampaignState> No(string error) => new(CommandStatus.Rejected, state, error);
        if (effect.PartyIndex < 0 || effect.PartyIndex >= state.Party.Length || state.Party[effect.PartyIndex].InstanceId != effect.InstanceId ||
            !NoncombatFieldMoveRules.Knows(state.Party[effect.PartyIndex], effect.Move)) return No("field_move_owner_changed");
        if (state.World.Player != (effect.Phase == "in" ? effect.Destination : effect.Origin)) return No("field_move_origin_changed");
        CampaignState Phase(CampaignState next, string phase) => next with { NoncombatFieldEffect = effect with { Phase = phase, WaitId = NextNoncombatWait(phase) } };
        if (effect.Phase == "confirm")
        {
            if (command.Kind != "field_move_confirm" || command.Choice is not (0 or 1)) return No("field_move_confirmation_pending");
            if (command.Choice == 1) return new(CommandStatus.Accepted, state with { NoncombatFieldEffect = null });
            if (NoncombatDestination(state, effect.Move) != effect.Destination) return No("field_move_destination_changed");
            return new(CommandStatus.Accepted, Phase(ResetFieldTravelState(state), "show-mon"));
        }
        if (command.Kind != "field_move_ack") return No("field_move_event_active");
        if (command.Text != effect.WaitId) return No("field_move_callback_mismatch");
        if (effect.Phase == "show-mon") return new(CommandStatus.Accepted, Phase(state, effect.Move == NoncombatFieldMoveRules.SweetScent ? "scent" : "out"));
        if (effect.Phase == "scent")
        {
            var next = TrySweetScentEncounter(state with { NoncombatFieldEffect = null }, out bool started);
            return new(CommandStatus.Accepted, started ? next : Phase(next, "fail"));
        }
        if (effect.Phase == "fail") return new(CommandStatus.Accepted, Pump(StartStory(state with { NoncombatFieldEffect = null }, "EventScript_FailSweetScent")));
        if (effect.Phase == "out")
        {
            if (effect.Destination is not { } arrival || !ValidFieldTravelArrival(arrival)) return No("field_move_destination_changed");
            return new(CommandStatus.Accepted, Pump(EnterMap(WarpFieldTravel(Phase(state, "in"), arrival), arrival.Facing)));
        }
        if (effect.Phase == "in") return new(CommandStatus.Accepted, Pump(state with { NoncombatFieldEffect = null }));
        return No("field_move_phase_invalid");
    }

    private CampaignState ResetFieldTravelState(CampaignState state)
    {
        var flags = state.Script.Flags; var variables = state.Script.Variables;
        foreach (string flag in FieldFlySourceRules.ClearedFlags) flags = flags.SetItem(flag, false);
        foreach (string variable in FieldFlySourceRules.ResetVariables) variables = variables.SetItem(variable, 0);
        return state with { Script = state.Script with { Flags = flags, Variables = variables },
            IsSurfing = false, IsCycling = false, SafariVisit = null, SafariExitPending = false };
    }

    private CampaignState WarpFieldTravel(CampaignState state, TilePosition arrival) => ResetFieldTravelState(state) with
    {
        World = state.World with { Revision = checked(state.World.Revision + 1), Player = arrival },
        Music = _content.MapDefinition(arrival.MapId).Music, SavedMusic = "MUS_DUMMY",
        Exploration = state.Exploration is null ? null : ResetEncounterModifiers(state.Exploration)
    };

    private CampaignState TrySweetScentEncounter(CampaignState state, out bool started)
    {
        started = false;
        var p = state.World.Player;
        int type = FieldEncounterTypeAt(state,p.X,p.Y) ?? 0;
        if (type is not (1 or 2) || SevenIslandEncounterTables.IsChamber(p.MapId) && !state.Script.Flags.GetValueOrDefault("FLAG_SYS_UNLOCKED_TANOBY_RUINS")) return state;
        _content.LandEncounters!.TryGetValue(p.MapId, out var land);
        _content.WaterEncounters!.TryGetValue(p.MapId, out var water);
        // All source headers in the current composition have at least one of these table families.
        bool header = land is not null || water is not null || _content.OldRodEncounters!.ContainsKey(p.MapId) ||
            _content.GoodRodEncounters.ContainsKey(p.MapId) || _content.SuperRodEncounters.ContainsKey(p.MapId) || _content.FieldRockSmash?.EncountersFor(p.MapId) is not null;
        if (!header) return state;
        var method = type == 1 ? RoamerEncounterMethod.SweetScentLand : RoamerEncounterMethod.SweetScentWater;
        var roamer = SourceRoamerRules.TryEncounter(state.Roamer ?? RoamerState.Empty,
            new(CelioSourceMap(p.MapId), method, false, type == 1 ? land is not null : water is not null, false,
                state.Party.Select(mon => new RoamerPartyMember(mon.Pokemon.Level, mon.Pokemon.CurrentHp, mon.IsEgg)).ToImmutableArray()), state.World.Rng);
        state = state with { World = state.World with { Rng = roamer.Rng } };
        if (roamer.Candidate is { } candidate)
        {
            started = true;
            return BeginCelioRoamerBattle(state with { Exploration = ResetEncounterModifiers(state.Exploration!) }, candidate, method,
                roamer.Trace.Select(draw => new EncounterRandomTrace("global", draw)).ToImmutableArray());
        }
        if (type == 1 && land is not null)
        {
            var shared = state.Exploration!.EarlyEncounter!;
            var result = SweetScentEncounterRules.Land(shared, land, (byte)shared.PreviousMetatileBehavior, state.World.Rng, OrdinaryLandDefinition(p.MapId));
            var next = state with { Exploration = state.Exploration with { EarlyEncounter = result.State }, World = state.World with { Rng = result.GlobalRng } };
            next = state.SafariVisit is { Active: true } ? GenerateSafariBattle(next) : BeginWildBattle(next);
            // Task_BattleStart resets cooldown for every battle, including Sweet Scent.
            started = true;
            return next;
        }
        if (type == 2 && water is not null)
        {
            var shared = state.Exploration!.EarlyEncounter!;
            var result = SweetScentEncounterRules.Water(WaterProcess(shared), water, (byte)shared.PreviousMetatileBehavior, state.World.Rng);
            var next = CompleteWaterEncounter(state, shared, water, result);
            started = true;
            return next;
        }
        // A failed roamer test can consume a draw even if the current terrain has no table.
        return state;
    }
}
