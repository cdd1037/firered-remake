using System.Collections.Immutable;
using Game.Domain;
using Game.Domain.Events;
using Game.Domain.Ids;
using Game.Domain.Items;
using Game.Domain.World;

namespace Game.Application;

public sealed record CampaignRuinValleyCut(RuinValleyCutState Movement);
public sealed record CampaignDottedHoleFall(DottedHoleFallState Movement);

public sealed partial class CampaignSession
{
    private static ImmutableSortedDictionary<string, FieldMetatileEdit> SixIslandDoorEdits(bool opened) => opened
        ? ImmutableSortedDictionary<string, FieldMetatileEdit>.Empty.Add("24:24", new(24, 24, RuinValleyCutRules.OpenMetatile, 0, 0))
        : ImmutableSortedDictionary<string, FieldMetatileEdit>.Empty;
    private string? ValidateSixIslandField(CampaignState state)
    {
        if (state.RuinValleyCut is not null || state.DottedHoleFall is not null) return "save_six_island_field_active";
        if (DottedHoleMap(state.World.Player.MapId) && !state.Script.Flags.GetValueOrDefault(RuinValleyCutRules.PermanentFlag))
            return "save_dotted_hole_door_required";
        if (state.World.Player.MapId.Value == SixIslandRuinMap &&
            !(state.MapEdits ?? ImmutableSortedDictionary<string, FieldMetatileEdit>.Empty).SequenceEqual(SixIslandDoorEdits(state.Script.Flags.GetValueOrDefault(RuinValleyCutRules.PermanentFlag))))
            return "save_ruin_valley_door_metatile";
        return null;
    }

    private RuinValleyDoorSnapshot RuinValleySnapshot(CampaignState state) => new(state.World,
        state.Script.Flags.GetValueOrDefault(RuinValleyCutRules.PermanentFlag), state.GameStats?.GetValueOrDefault(CutUsageStat) ?? 0, state.Party);
    private bool RuinValleyCutPosition(CampaignState state) => _content.SixIslandEnabled &&
        RuinValleyCutRules.SourcePositionMatches(state.World.Player, state.Script.Flags.GetValueOrDefault(RuinValleyCutRules.PermanentFlag));
    private Decision<CampaignState> DecideRuinValleyCut(CampaignState state, CampaignCommand command)
    {
        var intent = new FourIslandFieldIntent(new("six:cut:" + state.World.Revision), state.World.Revision);
        var result = RuinValleyCutRules.Begin(RuinValleySnapshot(state), intent, _content.Maps[state.World.Player.MapId],
            command.PartyIndex, state.Script.Flags.GetValueOrDefault("FLAG_BADGE02_GET"));
        if (result.Status != TransitionStatus.Accepted) return new(CommandStatus.Rejected, state, result.Diagnostic?.Code);
        var stats = state.GameStats ?? ImmutableSortedDictionary<string, int>.Empty.WithComparers(StringComparer.Ordinal);
        return new(CommandStatus.Accepted, state with { World = result.State.World,
            GameStats = stats.SetItem(CutUsageStat, result.State.CutUses), RuinValleyCut = new(result.Active!),
            Script = state.Script with { Locked = true }, PresentationTicks = FieldCutSourcePresentation.ShowMonCueFrames,
            PresentationEffects = [], Motions = [] });
    }
    private Decision<CampaignState> DecideRuinValleyCutAck(CampaignState state, CampaignCommand command)
    {
        var active = state.RuinValleyCut!.Movement;
        if (command.Kind != "six_island_cut_ack" || command.Text != active.PendingPresentationId.Value)
            return new(CommandStatus.Rejected, state, "ruin_valley_cut_pending");
        var result = RuinValleyCutRules.CompletePresentation(RuinValleySnapshot(state), active,
            new(new("six:cut:ack:" + state.World.Revision), state.World.Revision, active.PendingPresentationId), _content.Maps[state.World.Player.MapId]);
        if (result.Status != TransitionStatus.Accepted) return new(CommandStatus.Rejected, state, result.Diagnostic?.Code);
        long sequence=checked(state.Script.EffectSequence+1);
        return new(CommandStatus.Accepted, state with { World = result.State.World, RuinValleyCut = null,
            MapEdits = SixIslandDoorEdits(true), Script = state.Script with { Locked = false, EffectSequence=sequence,
                Flags = state.Script.Flags.SetItem(RuinValleyCutRules.PermanentFlag, true) }, PresentationTicks = 0,
            Motions = [], PresentationEffects = [new(sequence, "playse", state.World.Player.MapId, ["SE_BANG"])] });
    }

    private Decision<CampaignState>? TryDottedHoleStep(CampaignState state, CampaignCommand command)
    {
        if (!_content.SixIslandEnabled || command.Kind != "move" || !DottedHoleMap(state.World.Player.MapId)) return null;
        var (dx, dy) = Delta(command.Direction); var p = state.World.Player;
        if (_content.Maps[p.MapId].SourceBehaviorAt(p.X + dx, p.Y + dy) != DottedHoleSource.FallBehavior) return null;
        var result = DottedHoleFallRules.StepOntoHole(state.World, new(new("six:fall:" + state.World.Revision), state.World.Revision),
            _content.Maps, command.Direction, state.IsCycling ? FourIslandTravelMode.Bicycle : FourIslandTravelMode.Walk,
            roamerActive: false, occupied: (x, y, e) => ActorOccupies(state, x, y, e));
        return AcceptDottedHoleFall(state, result);
    }
    private Decision<CampaignState> AcceptDottedHoleFall(CampaignState state, DottedHoleFallTransition result)
    {
        if (result.Status != TransitionStatus.Accepted) return new(CommandStatus.Rejected, state, result.Diagnostic?.Code);
        var next = state with { World = result.State, DottedHoleFall = result.Active is {} active ? new(active) : null,
            Motions = [], PresentationEffects = [], PresentationTicks = 0 };
        if (result.Callbacks.TookStep) next = ApplyRenewableHiddenItemWalkingStep(next);
        if (result.MapLoad is not null)
        {
            next = Pump(EnterMap(next with { IsCycling = false, IsSurfing = false, SurfEffect = null, PlayerVisible = true,
                Dialogue = null, Script = next.Script with { Stack = [], Wait = null, Completed = true, Locked = false },
                Exploration = ResetEncounterModifiers(next.Exploration!) }, Direction.South));
            if (next.World != result.State || next.Script.Wait is not null || !next.Script.Completed)
                throw new InvalidDataException("dotted_hole_map_entry_unexpected_continuation");
        }
        foreach (var e in result.Events)
            if (e.Presentation is {} cue)
            {
                string suffix = cue.Direction switch { Direction.North => "up", Direction.South => "down", Direction.East => "right", _ => "left" };
                next = next with { PresentationTicks = cue.MovementUpdates,
                    Motions = [new("LOCALID_PLAYER", [e.Before, e.After], [cue.SourceAction + "_" + suffix, "step_end"])] };
            }
        if (result.Active?.Phase == DottedHoleFallPhase.Departure)
            next = next with { PresentationTicks = DottedHoleFallPresentation.BeforeInvisibleDelay + DottedHoleFallPresentation.BeforeWarpDelay };
        next = next with { Script = next.Script with { Locked = result.Active is not null } };
        return new(CommandStatus.Accepted, next);
    }
    private Decision<CampaignState> DecideDottedHoleFallAck(CampaignState state, CampaignCommand command)
    {
        var active = state.DottedHoleFall!.Movement;
        if (command.Kind != "dotted_hole_fall_ack" || command.Text != active.PendingPresentationId.Value)
            return new(CommandStatus.Rejected, state, "dotted_hole_fall_pending");
        return AcceptDottedHoleFall(state, DottedHoleFallRules.CompletePresentation(state.World, active,
            new(new("six:fall:ack:" + state.World.Revision), state.World.Revision, active.PendingPresentationId), _content.Maps));
    }
}
