using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Randomness;

namespace Game.Domain.World;

public enum SeafoamMovementPhase { EntryStep, Departure, Arrival, CurrentStep, SurfDismount }
/// <summary>Transient presentation capability, never a player-save payload.</summary>
public sealed class SeafoamMovementState
{
    public WorldSnapshot AwaitingWorld { get; }
    public CommandId PendingPresentationId { get; }
    public SeafoamMovementPhase Phase { get; }
    public SeafoamHole? Hole { get; }
    public int CompletedTiles { get; }
    public bool ControlsLocked => true;
    public bool CanSave => false;
    public bool PreventStep => Phase is SeafoamMovementPhase.Arrival or SeafoamMovementPhase.SurfDismount;
    internal SeafoamMovementState(WorldSnapshot world,CommandId command,SeafoamMovementPhase phase,SeafoamHole? hole = null,int tiles = 0)
    { AwaitingWorld = world; PendingPresentationId = command; Phase = phase; Hole = hole; CompletedTiles = tiles; }
}
/// <summary>MapLoad uses the existing ordinary-fall reset contract. Surfing is null when unchanged.
/// SetFallingVariable is emitted only after landing presentation, before the original on-frame current script.</summary>
public sealed record SeafoamFieldTransition(TransitionStatus Status,WorldSnapshot State,SeafoamMovementState? Active,
    ImmutableArray<FourIslandFieldEvent> Events,FourIslandFieldCallbacks Callbacks,DottedHoleMapLoadEffects? MapLoad,
    bool? Surfing,bool SetFallingVariable,Diagnostic? Diagnostic)
{
    public bool ControlsLocked => Active is not null;
    public bool CanSave => Active is null;
}

/// <summary>Eight original hole entries and terrain-driven Seafoam water currents. Scripted held current routes remain
/// owned by the original story interpreter; do not dispatch automatic currents during a script lock.</summary>
public static class SeafoamMovementRules
{
    public static SeafoamFieldTransition StepOntoHole(WorldSnapshot world,FourIslandFieldIntent intent,
        IReadOnlyDictionary<MapId,IFieldMap> maps,Direction direction,FourIslandTravelMode mode,
        SeafoamMovementState? active = null,Func<int,int,int,bool>? occupied = null)
    {
        if (active is not null) return Fail(world,active,"seafoam-controls-locked");
        if (!maps.TryGetValue(world.Player.MapId,out var map)) return Fail(world,null,"seafoam-map-missing");
        var invalid = Validate(world,intent,map);
        if (invalid is not null) return Fail(world,null,invalid);
        if (intent.CompletedPresentationId is not null || !Enum.IsDefined(direction) || !Enum.IsDefined(mode) || mode == FourIslandTravelMode.Surf)
            return Fail(world,null,"invalid-seafoam-fall-step");
        var (dx,dy) = FourIslandFieldSource.Delta(direction);
        int x = world.Player.X + dx,y = world.Player.Y + dy;
        var hole = SeafoamFieldRules.Holes.SingleOrDefault(h => h.Map == map.Id && h.X == x && h.Y == y);
        if (hole is null || !ValidateHole(hole,maps)) return Fail(world,null,"seafoam-source-hole-mismatch");
        if (map.SourceBehaviorAt(world.Player.X,world.Player.Y) is not (0x00 or 0x08 or 0x2A or >=0x30 and <=0x37))
            return Fail(world,null,"seafoam-fall-current-handler-required");
        var collision = FourIslandFieldSource.Collision(map,world.Player,x,y,direction,occupied);
        if (collision is not null) return Blocked(world,intent.CommandId,collision,direction);
        var position = new TilePosition(map.Id,x,y,3,direction);
        var next = world with { Revision = world.Revision + 1,Player = position };
        var pixels = mode == FourIslandTravelMode.Bicycle ? FourIslandForcedMovementRules.Fast2Pixels :
            mode == FourIslandTravelMode.Run ? FourIslandForcedMovementRules.Fast1Pixels : Enumerable.Repeat(1,16).ToImmutableArray();
        return new(TransitionStatus.Accepted,next,new(next,intent.CommandId,SeafoamMovementPhase.EntryStep,hole),
            [new(intent.CommandId,FourIslandFieldEventKind.Moved,world.Player,position,"seafoam-hole-entry",
                new(mode == FourIslandTravelMode.Bicycle ? "ride_water_current" : mode == FourIslandTravelMode.Run ? "player_run" : "walk_normal",direction,pixels))],
            FourIslandFieldCallbacks.None,null,null,false,null);
    }

    /// <summary>Call after completed movement onto a current, after step scripts have declined. It cannot redirect an active route.</summary>
    public static SeafoamFieldTransition StartCurrent(WorldSnapshot world,FourIslandFieldIntent intent,IFieldMap map,
        bool surfing,SeafoamMovementState? active = null,Func<int,int,int,bool>? occupied = null)
    {
        if (active is not null) return Fail(world,active,"seafoam-controls-locked");
        var invalid = Validate(world,intent,map);
        if (invalid is not null) return Fail(world,null,invalid);
        if (intent.CompletedPresentationId is not null || !surfing || !SeafoamFieldRules.IsWaterFloor(map.Id) ||
            SeafoamFieldRules.CurrentDirection(map.SourceBehaviorAt(world.Player.X,world.Player.Y) ?? -1) is not {} direction)
            return Fail(world,null,"seafoam-current-not-ready");
        return MoveCurrent(world,intent.CommandId,map,direction,0,null,occupied);
    }

    /// <summary>The original B4F special starts the same asynchronous StopSurfing task.
    /// Its preventStep lifetime survives releaseall; completing the cue owns the unlock.</summary>
    public static SeafoamFieldTransition StartScriptDismount(WorldSnapshot world, FourIslandFieldIntent intent,
        IFieldMap map, bool surfing, Func<int,int,int,bool>? occupied = null)
    {
        var invalid = Validate(world,intent,map);
        if(invalid is not null)return Fail(world,null,invalid);
        if(intent.CompletedPresentationId is not null || !surfing || map.Id!=SeafoamFieldRules.B4F ||
            world.Player.X!=12 || world.Player.Y!=13 || world.Player.Elevation!=1)
            return Fail(world,null,"seafoam-script-dismount-context");
        return MoveCurrent(world,intent.CommandId,map,Direction.North,0,null,occupied);
    }

    public static SeafoamFieldTransition CompletePresentation(WorldSnapshot world,SeafoamMovementState active,
        FourIslandFieldIntent intent,IReadOnlyDictionary<MapId,IFieldMap> maps,Func<int,int,int,bool>? occupied = null)
    {
        if (world != active.AwaitingWorld || intent.ExpectedRevision != world.Revision || !StableId.IsValid(intent.CommandId.Value) ||
            intent.CommandId == active.PendingPresentationId || intent.CompletedPresentationId != active.PendingPresentationId)
            return Fail(world,active,"seafoam-presentation-mismatch");
        if (!maps.TryGetValue(world.Player.MapId,out var map)) return Fail(world,active,"seafoam-map-missing");
        var invalid = Validate(world,intent,map);
        if (invalid is not null) return Fail(world,active,invalid);
        if (active.Hole is {} hole)
        {
            if (!ValidateHole(hole,maps)) return Fail(world,active,"seafoam-source-hole-mismatch");
            var next = world with { Revision = world.Revision + 1 };
            if (active.Phase == SeafoamMovementPhase.Arrival)
            {
                bool surf = SeafoamFieldRules.FallStartsSurf(world.Player,map.SourceBehaviorAt(world.Player.X,world.Player.Y) ?? -1);
                return new(TransitionStatus.Accepted,next,null,
                    [new(intent.CommandId,FourIslandFieldEventKind.Stopped,world.Player,world.Player,"fall-unlock-unfreeze-restore-camera")],
                    FourIslandFieldCallbacks.None,null,surf,surf,null);
            }
            bool entry = active.Phase == SeafoamMovementPhase.EntryStep;
            if (entry) next = next with { StepCount = world.StepCount + 1 };
            else next = next with { Player = new(hole.DestinationMap,hole.LandingX,hole.LandingY,hole.LandingElevation,Direction.South) };
            var phase = entry ? SeafoamMovementPhase.Departure : SeafoamMovementPhase.Arrival;
            return new(TransitionStatus.Accepted,next,new(next,intent.CommandId,phase,hole),
                [new(intent.CommandId,entry ? FourIslandFieldEventKind.Moved : FourIslandFieldEventKind.Fell,world.Player,next.Player,
                    entry ? "EventScript_DoFallWarp" : "FieldCB_FallWarpExit")],
                entry ? new(true,false,false,false,false) : FourIslandFieldCallbacks.None,
                entry ? null : new(true,true,true,true,true),entry ? null : false,false,null);
        }
        if (active.Phase == SeafoamMovementPhase.SurfDismount || SeafoamFieldRules.CurrentDirection(map.SourceBehaviorAt(world.Player.X,world.Player.Y) ?? -1) is not {} current)
            return new(TransitionStatus.Accepted,world with { Revision = world.Revision + 1 },null,
                [new(intent.CommandId,FourIslandFieldEventKind.Stopped,world.Player,world.Player,"clear-forced-restore-animation")],
                FourIslandFieldCallbacks.None,null,active.Phase == SeafoamMovementPhase.SurfDismount ? false : null,false,null);
        return MoveCurrent(world,intent.CommandId,map,current,active.CompletedTiles,active,occupied);
    }

    /// <summary>Only the authenticated forward RNG changes from original step/encounter callbacks may rebase a pending cue.</summary>
    public static SeafoamMovementState ReconcileCallbackRng(SeafoamMovementState active,WorldSnapshot world)
    {
        if (world.Rng.AlgorithmId != Gen3Rng.AlgorithmId || world.Rng.Cursor < active.AwaitingWorld.Rng.Cursor ||
            (world with { Rng = active.AwaitingWorld.Rng }) != active.AwaitingWorld)
            throw new ArgumentException("Only callback RNG may change the awaiting world.",nameof(world));
        return new(world,active.PendingPresentationId,active.Phase,active.Hole,active.CompletedTiles);
    }
    public static FourIslandFieldCallbacks CurrentCallbacks(int behavior) => SeafoamFieldRules.CurrentDirection(behavior) is not null
        ? FourIslandFieldCallbacks.None : new(true,true,false,false,true);

    private static SeafoamFieldTransition MoveCurrent(WorldSnapshot world,CommandId command,IFieldMap map,Direction direction,
        int completed,SeafoamMovementState? previous,Func<int,int,int,bool>? occupied)
    {
        if (completed >= map.Width * map.Height) return Fail(world,previous,"seafoam-current-limit");
        var (dx,dy) = FourIslandFieldSource.Delta(direction);
        int x = world.Player.X + dx,y = world.Player.Y + dy;
        var tile = map.GetTile(x,y);
        int behavior = map.SourceBehaviorAt(x,y) ?? -1;
        string? collision = FourIslandFieldSource.Collision(map,world.Player,x,y,direction,occupied);
        // Source CheckForPlayerAvatarCollision intercepts elevation mismatch with the shared Surf dismount.
        bool dismount = collision == "elevation" && tile is { Blocked:false,Elevation:3 } && occupied?.Invoke(x,y,3) != true;
        if (collision is not null && !dismount) return Blocked(world,command,collision);
        if (tile is null || tile.Elevation is <0 or >15 || behavior is <0 or >255)
            return Fail(world,previous,"seafoam-current-target-invalid");
        if (dismount ? behavior is not (0x00 or 0x08 or 0x2A or >=0x30 and <=0x37) : !SeafoamFieldRules.Surfable(behavior) && behavior != 0x17)
            return Fail(world,previous,"seafoam-current-target-handler-required");
        var p = new TilePosition(map.Id,x,y,tile.Elevation == 15 ? world.Player.Elevation : tile.Elevation,direction);
        var callbacks = dismount ? FourIslandFieldCallbacks.None : CurrentCallbacks(behavior);
        var next = world with { Revision = world.Revision + 1,StepCount = world.StepCount + (callbacks.TookStep ? 1 : 0),Player = p };
        var pending = new SeafoamMovementState(next,command,dismount ? SeafoamMovementPhase.SurfDismount : SeafoamMovementPhase.CurrentStep,null,completed + 1);
        return new(TransitionStatus.Accepted,next,pending,[new(command,FourIslandFieldEventKind.Moved,world.Player,p,
            dismount ? "source-current-stop-surfing" : "source-water-current",new(dismount ? "jump_special_with_effect" : "ride_water_current",direction,
                dismount ? Enumerable.Repeat(1,16).ToImmutableArray() : FourIslandForcedMovementRules.Fast2Pixels))],callbacks,null,dismount ? false : null,false,null);
    }
    private static string? Validate(WorldSnapshot world,FourIslandFieldIntent intent,IFieldMap map)
    {
        if (!SeafoamFieldRules.ValidDimensions(map) || world.Revision >= long.MaxValue - 4 || world.StepCount >= long.MaxValue - 1)
            return "seafoam-source-map-or-counter-invalid";
        return FourIslandFieldSource.Validate(world,intent,map)?.Code;
    }
    private static bool ValidateHole(SeafoamHole hole,IReadOnlyDictionary<MapId,IFieldMap> maps)
    {
        if (!maps.TryGetValue(hole.Map,out var source) || !SeafoamFieldRules.ValidDimensions(source) ||
            source.SourceBehaviorAt(hole.X,hole.Y) != SeafoamFieldRules.FallBehavior || source.GetTile(hole.X,hole.Y) is not { Blocked:false,Elevation:3 } ||
            source.FieldWarps.IsDefault || hole.WarpId >= source.FieldWarps.Length ||
            source.FieldWarps[hole.WarpId] != new FieldWarp(hole.WarpId,hole.X,hole.Y,3,hole.DestinationMap,hole.DestinationWarpId) ||
            !maps.TryGetValue(hole.DestinationMap,out var destination) || !SeafoamFieldRules.ValidDimensions(destination) ||
            destination.FieldWarps.IsDefault || hole.DestinationWarpId >= destination.FieldWarps.Length) return false;
        var arrival = destination.FieldWarps[hole.DestinationWarpId];
        return (arrival.X,arrival.Y,arrival.Elevation) == (hole.LandingX,hole.LandingY,hole.LandingElevation) &&
            destination.GetTile(arrival.X,arrival.Y) is { Blocked:false } tile && tile.Elevation == hole.LandingElevation &&
            (hole.LandingElevation == 3 ? destination.SourceBehaviorAt(arrival.X,arrival.Y) == 0x08 :
                SeafoamFieldRules.Surfable(destination.SourceBehaviorAt(arrival.X,arrival.Y) ?? -1));
    }
    private static SeafoamFieldTransition Fail(WorldSnapshot world,SeafoamMovementState? active,string code) =>
        new(TransitionStatus.Rejected,world,active,[],FourIslandFieldCallbacks.None,null,null,false,new(code,code));
    private static SeafoamFieldTransition Blocked(WorldSnapshot world,CommandId command,string reason,Direction? attempted = null)
    {
        var p = attempted is {} direction ? world.Player with { Facing = direction } : world.Player;
        return new(TransitionStatus.Accepted,world with { Revision = world.Revision + 1,Player = p },null,
            [new(command,FourIslandFieldEventKind.Blocked,world.Player,p,reason)],FourIslandFieldCallbacks.None,null,null,false,null);
    }
}
