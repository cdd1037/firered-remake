using System.Collections.Immutable;
using Game.Application;
using Game.Domain.Ids;
using Game.Domain.World;
using Godot;

public partial class CampaignView
{
    // Both regions invoke the same original EventScript_DoFallWarp/FieldCB_FallWarpExit.
    // The shared renderer observes the command owner; it never decides a destination or Surf state.
    private readonly record struct CommonFallCue(DottedHoleFallPhase Phase, string PresentationId, string Command, MapId Destination);
    private static CommonFallCue? CommonFall(CampaignState? state)
    {
        if (state?.DottedHoleFall?.Movement is { } dotted)
            return new(dotted.Phase, dotted.PendingPresentationId.Value, "dotted_hole_fall_ack", dotted.Landing.MapId);
        if (state?.SeafoamMovement?.Movement is { Hole: { } hole } seafoam)
            return new(seafoam.Phase switch
            {
                SeafoamMovementPhase.EntryStep => DottedHoleFallPhase.EntryStep,
                SeafoamMovementPhase.Departure => DottedHoleFallPhase.Departure,
                SeafoamMovementPhase.Arrival => DottedHoleFallPhase.Arrival,
                _ => throw new InvalidDataException("Seafoam fall presentation phase differs.")
            }, seafoam.PendingPresentationId.Value, "seafoam_field_ack", hole.DestinationMap);
        return null;
    }

    private string? _seafoamMovementId;
    private double _seafoamMovementFrames;
    private bool SeafoamFieldLocked => _session?.State.SeafoamMovement is not null || SeafoamDismountOrigin is not null;
    private Vector2? SeafoamDismountOrigin => _session is not null && SeafoamFieldRules.IsCave(_session.State.World.Player.MapId)
        ? _tracks.FirstOrDefault(t => t.Actor == "LOCALID_PLAYER" && t.Time + 1e-9 < t.Duration &&
            t.Clips.Count == 1 && t.Clips[0].Action.StartsWith("jump_special_with_effect_", StringComparison.Ordinal))?.Clips[0].From : null;

    private void SynchronizeSeafoamPresentation(CampaignState after)
    {
        string? id = after.SeafoamMovement?.Movement.PendingPresentationId.Value;
        if (id != _seafoamMovementId) { _seafoamMovementId = id; _seafoamMovementFrames = 0; }
        if (SeafoamFieldLocked) { _held = null; _repeat = 0; }
    }
    private bool ProcessSeafoamCurrentPresentation(double delta)
    {
        if (_session.State.SeafoamMovement?.Movement is not { Phase: SeafoamMovementPhase.CurrentStep or SeafoamMovementPhase.SurfDismount } pending) return false;
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        int duration = pending.Phase == SeafoamMovementPhase.SurfDismount ? FieldSurfSourcePresentation.JumpCueFrames : _session.State.PresentationTicks;
        if (duration <= 0) throw new InvalidDataException("Seafoam current presentation duration missing.");
        _seafoamMovementFrames = Math.Min(duration, _seafoamMovementFrames + delta * 60);
        _held = null; _repeat = 0;
        ProcessFieldSurfPresentation(delta);
        if (!_busy && _seafoamMovementFrames + 1e-7 >= duration && _tracks.All(t => t.Time + 1e-9 >= t.Duration))
            Apply(new("seafoam_field_ack", Text: pending.PendingPresentationId.Value));
        return true;
    }
    private double? SeafoamMotionSeconds(CampaignState after, string actor, string action)
    {
        if (actor != "LOCALID_PLAYER" || !SeafoamFieldRules.IsCave(after.World.Player.MapId)) return null;
        if (action.StartsWith("jump_special_", StringComparison.Ordinal)) return FieldSurfSourcePresentation.JumpCueFrames / 60d;
        if (action.StartsWith("ride_water_current_", StringComparison.Ordinal)) return FourIslandForcedMovementRules.Fast2Pixels.Length / 60d;
        if (after.SeafoamMovement?.Movement.Phase == SeafoamMovementPhase.EntryStep) return after.PresentationTicks / 60d;
        return null;
    }
    private float? SeafoamStepFraction(string actor, string action, double elapsed)
    {
        if (actor != "LOCALID_PLAYER" || !SeafoamFieldRules.IsCave(_session.State.World.Player.MapId)) return null;
        ImmutableArray<int> pixels = action.StartsWith("ride_water_current_", StringComparison.Ordinal) ? FourIslandForcedMovementRules.Fast2Pixels :
            action.StartsWith("walk_fast_", StringComparison.Ordinal) || action.StartsWith("player_run_", StringComparison.Ordinal) ? FourIslandForcedMovementRules.Fast1Pixels : [];
        if (pixels.IsEmpty) return null;
        int updates = Math.Clamp((int)Math.Floor(elapsed * 60 + 1e-7), 0, pixels.Length);
        return pixels.Take(updates).Sum() / 16f;
    }
}
