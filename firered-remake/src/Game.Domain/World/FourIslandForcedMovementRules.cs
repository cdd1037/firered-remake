using System.Collections.Immutable;
using Game.Domain.Ids;
using Game.Domain.Pokemon;
using Game.Domain.Randomness;

namespace Game.Domain.World;

public enum FourIslandTravelMode { Walk, Run, Bicycle, Surf }

/// <summary>Bounded cave slip and Entrance waterfall. Every pending presentation requires a new correlated command.</summary>
public static class FourIslandForcedMovementRules
{
    public static ImmutableArray<int> Fast1Pixels { get; } = [2,2,2,2,2,2,2,2];
    public static ImmutableArray<int> Fast2Pixels { get; } = [2,3,3,2,3,3];
    public static ImmutableArray<int> SlowerPixels { get; } = Enumerable.Range(0,31).Select(i => i % 2 == 0 ? 1 : 0).ToImmutableArray();

    /// <summary>A completed player-initiated step onto/off ice. Invoke StartSlip only after its presentation and callbacks complete.</summary>
    public static FourIslandFieldTransition CompletePlayerStep(WorldSnapshot world, FourIslandFieldIntent intent, IFieldMap map,
        Direction direction, FourIslandTravelMode mode, FourIslandMovementState? active = null, Func<int,int,int,bool>? occupied = null)
    {
        var invalid = ValidateStart(world, intent, map, active);
        if (invalid is not null) return Fail(world, active, invalid);
        if (!Enum.IsDefined(direction) || !Enum.IsDefined(mode)) return Fail(world, active, new("invalid-ice-step", "A cardinal direction and travel mode are required."));
        if ((map.Id != FourIslandFieldSource.FirstFloor && map.Id != FourIslandFieldSource.Basement) || map.Height != 20 || map.Width != (map.Id == FourIslandFieldSource.FirstFloor ? 20 : 24))
            return Fail(world, active, new("unsupported-ice-step-map", "Only Icefall 1F/B1F ground is admitted."));
        if (mode == FourIslandTravelMode.Surf) return Fail(world, active, new("unsupported-ice-surf", "Icefall floor movement is on land."));
        var (dx,dy) = FourIslandFieldSource.Delta(direction); int x = world.Player.X + dx, y = world.Player.Y + dy;
        int? here = map.SourceBehaviorAt(world.Player.X,world.Player.Y), behavior = map.SourceBehaviorAt(x,y);
        if (!Ground(here)) return Fail(world, active, new("unsupported-ice-current-behavior", "Dispatch the current source special tile explicitly."));
        if (behavior is >=0x38 and <=0x3b or >=0x60 and <=0x71)
            return Fail(world, active, new("ice-step-special-handler-required", "Ledge/warp/hole movement requires its source handler."));
        string? collision = FourIslandFieldSource.Collision(map,world.Player,x,y,direction,occupied);
        // GetBikeCollisionAt returns COLLISION_COUNT for cracked ice before handling collisions 0..4.
        bool bicycleCrack = mode == FourIslandTravelMode.Bicycle && behavior == FourIslandFieldSource.CrackedIce && map.GetTile(x,y) is not null;
        if (collision is not null && !bicycleCrack) return Blocked(world,intent.CommandId,active,collision,direction);
        if (!Ground(behavior)) return Fail(world,active,new("unsupported-ice-target-behavior", "The target behavior requires its own handler."));
        var tile = map.GetTile(x,y)!;
        if (tile.Elevation is <0 or >15) return Fail(world,active,new("invalid-ice-target-cell", "Target source elevation is invalid."));
        var p = new TilePosition(map.Id,x,y,tile.Elevation == 15 ? world.Player.Elevation : tile.Elevation,direction);
        var callbacks = FourIslandFieldSource.CompletionCallbacks(behavior!.Value,true,false);
        var next = world with { Revision = world.Revision + 1, StepCount = world.StepCount + (callbacks.TookStep ? 1 : 0), Player = p };
        ImmutableArray<int> pixels = mode == FourIslandTravelMode.Bicycle && !bicycleCrack ? Fast2Pixels :
            mode is FourIslandTravelMode.Run or FourIslandTravelMode.Bicycle ? Fast1Pixels : Enumerable.Repeat(1,16).ToImmutableArray();
        string action = mode == FourIslandTravelMode.Run ? "player_run" : pixels.Length == 16 ? "walk_normal" : pixels.Length == 8 ? "walk_fast" : "ride_water_current";
        return new(TransitionStatus.Accepted,next,null,[new(intent.CommandId,FourIslandFieldEventKind.Moved,world.Player,p,"ice-player-step",new(action,direction,pixels))],callbacks,null);
    }

    /// <summary>Only call following actual movement onto ice, never after fall/warp initialization or a stationary save restore.</summary>
    public static FourIslandFieldTransition StartSlip(WorldSnapshot world, FourIslandFieldIntent intent, IFieldMap map,
        Direction movementDirection, FourIslandMovementState? active = null, Func<int,int,int,bool>? occupied = null)
    {
        var invalid = ValidateStart(world,intent,map,active);
        if (invalid is not null) return Fail(world,active,invalid);
        if (map.Id != FourIslandFieldSource.Basement || map.Width != 24 || map.Height != 20 || !Enum.IsDefined(movementDirection) ||
            map.SourceBehaviorAt(world.Player.X,world.Player.Y) != FourIslandFieldSource.Ice)
            return Fail(world,active,new("ice-slip-not-ready", "A completed B1F ice entry and the source movement direction are required."));
        return MoveOne(world,intent.CommandId,map,FourIslandMovementKind.IceSlip,movementDirection,0,null,null,occupied);
    }

    /// <summary>Waterfall field interaction/party menu has resolved its owner. No/Cancel returns an unchanged world.</summary>
    public static FourIslandFieldTransition StartWaterfall(WorldSnapshot world, FourIslandFieldIntent intent, IFieldMap map,
        PartyPokemon member, bool hasVolcanoBadge, bool surfing, Direction movementDirection, bool accepted,
        FourIslandMovementState? active = null, Func<int,int,int,bool>? occupied = null)
    {
        var invalid = ValidateStart(world,intent,map,active);
        if (invalid is not null) return Fail(world,active,invalid);
        var (dx,dy) = FourIslandFieldSource.Delta(world.Player.Facing);
        if (!WaterfallSourceRules.CanUse(member,hasVolcanoBadge,surfing,movementDirection,map.SourceBehaviorAt(world.Player.X+dx,world.Player.Y+dy) ?? -1) ||
            world.Player.Facing != Direction.North || world.Player.Y != 21 || !ValidLane(map,world.Player.X,occupied))
            return Fail(world,active,new("waterfall-cannot-use", "The source Badge7, Surf, north movement, move owner and facing waterfall gates are required."));
        if (!accepted) return new(TransitionStatus.Accepted,world,null,[new(intent.CommandId,FourIslandFieldEventKind.Cancelled,world.Player,world.Player,"waterfall-declined")],FourIslandFieldCallbacks.None,null);
        if (member.InstanceId <= 0) return Fail(world,active,new("invalid-waterfall-owner", "A valid owned Pokemon instance is required."));
        var next = world with { Revision = world.Revision + 1 };
        var movement = new FourIslandMovementState(next,intent.CommandId,FourIslandMovementKind.WaterfallAscent,FourIslandMovementPhase.ShowPokemon,Direction.North,0,member.InstanceId);
        return new(TransitionStatus.Accepted,next,movement,[new(intent.CommandId,FourIslandFieldEventKind.ShowPokemon,world.Player,world.Player,
            "lock-controls-prevent-step-show-mon",new("field_move_show_mon",Direction.North,[],member.InstanceId))],FourIslandFieldCallbacks.None,null);
    }

    /// <summary>Includes the ordinary eight-update Surf entry from y13 to y14, then six-update forced current tiles. No HM/badge/owner gate.</summary>
    public static FourIslandFieldTransition StartDescent(WorldSnapshot world, FourIslandFieldIntent intent, IFieldMap map,
        bool surfing, FourIslandMovementState? active = null, Func<int,int,int,bool>? occupied = null)
    {
        var invalid = ValidateStart(world,intent,map,active);
        if (invalid is not null) return Fail(world,active,invalid);
        if (!surfing || world.Player.Y != 13 || !ValidLane(map,world.Player.X,occupied))
            return Fail(world,active,new("waterfall-descent-not-ready", "Descend from the upper pond while surfing."));
        return MoveOne(world,intent.CommandId,map,FourIslandMovementKind.WaterfallDescent,Direction.South,0,null,null,occupied,true);
    }

    public static FourIslandFieldTransition CompletePresentation(WorldSnapshot world, FourIslandMovementState active,
        FourIslandFieldIntent intent, IFieldMap map, Func<int,int,int,bool>? occupied = null)
    {
        var invalid = FourIslandFieldSource.Validate(world,intent,map);
        if (invalid is not null) return Fail(world,active,invalid);
        if (!ValidActive(active) || world != active.AwaitingWorld ||
            (active.Kind == FourIslandMovementKind.IceSlip ? map.Width != 24 || map.Height != 20 : map.Width != 30 || map.Height != 36)) return Fail(world,active,new("invalid-four-island-movement", "Awaiting world or transient movement is inconsistent."));
        if (intent.CompletedPresentationId != active.PendingPresentationId || intent.CommandId == active.PendingPresentationId)
            return Fail(world,active,new("four-island-presentation-mismatch", "A new command must acknowledge the exact pending cue."));
        int behavior = map.SourceBehaviorAt(world.Player.X,world.Player.Y)!.Value;
        if (active.Phase == FourIslandMovementPhase.Moving &&
            (active.Kind == FourIslandMovementKind.IceSlip ? behavior != FourIslandFieldSource.Ice : behavior != FourIslandFieldSource.Waterfall))
        {
            var next = world with { Revision = world.Revision + 1 };
            return new(TransitionStatus.Accepted,next,null,[new(intent.CommandId,FourIslandFieldEventKind.Stopped,world.Player,world.Player,
                active.Kind == FourIslandMovementKind.WaterfallAscent ? "clear-held-movement-unlock-clear-prevent-step" : "clear-forced-restore-animation")],FourIslandFieldCallbacks.None,null);
        }
        return MoveOne(world,intent.CommandId,map,active.Kind,active.MovementDirection,active.CompletedTiles,active.MoveOwnerInstanceId,active,occupied);
    }

    public static FourIslandMovementState ReconcileCallbackRng(FourIslandMovementState active, WorldSnapshot world)
    {
        if (!ValidActive(active) || world.Rng.AlgorithmId != Gen3Rng.AlgorithmId || world.Rng.Cursor < active.AwaitingWorld.Rng.Cursor ||
            (world with { Rng = active.AwaitingWorld.Rng }) != active.AwaitingWorld)
            throw new ArgumentException("Only source callbacks' forward RNG state may change the awaiting world.",nameof(world));
        return active with { AwaitingWorld = world };
    }

    private static FourIslandFieldTransition MoveOne(WorldSnapshot world, CommandId command, IFieldMap map,
        FourIslandMovementKind kind, Direction direction, int completed, long? owner, FourIslandMovementState? previous,
        Func<int,int,int,bool>? occupied, bool ordinaryEntry = false)
    {
        var (dx,dy) = FourIslandFieldSource.Delta(direction); int x = world.Player.X+dx,y=world.Player.Y+dy;
        if (completed >= (kind == FourIslandMovementKind.IceSlip ? 480 : 8))
            return Fail(world,previous,new("four-island-movement-limit", "The source-bounded movement budget is exhausted."));
        int? behavior = map.SourceBehaviorAt(x,y);
        if (behavior is >=0x38 and <=0x3b or >=0x60 and <=0x71)
            return Fail(world,previous,new("four-island-forced-special-handler-required", "Forced ledge, warp or hole movement is outside this bounded route."));
        string? collision = FourIslandFieldSource.Collision(map,world.Player,x,y,direction,occupied);
        if (collision is not null)
        {
            if (kind == FourIslandMovementKind.WaterfallAscent)
                return Fail(world,previous,new("unsupported-waterfall-held-move-collision", "Source held Waterfall movement bypasses collision; a changed lane needs separate admission."));
            return Blocked(world,command,previous,collision);
        }
        bool supported = kind == FourIslandMovementKind.IceSlip ? Ground(behavior) && behavior is not (FourIslandFieldSource.ThinIce or FourIslandFieldSource.CrackedIce) :
            behavior is FourIslandFieldSource.Waterfall or 0x10;
        if (!supported) return Fail(world,previous,new("unsupported-four-island-forced-target", "Target source behavior needs a dedicated handler."));
        var tile = map.GetTile(x,y)!;
        if (tile.Elevation is <0 or >15) return Fail(world,previous,new("invalid-four-island-target-cell", "Target source elevation is invalid."));
        var p = new TilePosition(map.Id,x,y,tile.Elevation == 15 ? world.Player.Elevation : tile.Elevation,direction);
        var callbacks = FourIslandFieldSource.CompletionCallbacks(behavior!.Value,true,!ordinaryEntry,kind == FourIslandMovementKind.WaterfallAscent);
        var next = world with { Revision = world.Revision+1, StepCount = world.StepCount+(callbacks.TookStep?1:0), Player=p };
        var movement = new FourIslandMovementState(next,command,kind,FourIslandMovementPhase.Moving,direction,completed+1,owner);
        var pixels = kind == FourIslandMovementKind.WaterfallAscent ? SlowerPixels : kind == FourIslandMovementKind.IceSlip || ordinaryEntry ? Fast1Pixels : Fast2Pixels;
        string action = kind == FourIslandMovementKind.WaterfallAscent ? "walk_slower" : kind == FourIslandMovementKind.IceSlip || ordinaryEntry ? "walk_fast" : "ride_water_current";
        return new(TransitionStatus.Accepted,next,movement,[new(command,FourIslandFieldEventKind.Moved,world.Player,p,"source-forced-movement",new(action,direction,pixels,owner))],callbacks,null);
    }
    private static bool ValidLane(IFieldMap map,int x,Func<int,int,int,bool>? occupied)
    {
        if (map.Id != FourIslandFieldSource.Entrance || map.Width != 30 || map.Height != 36 || x is <15 or >19) return false;
        for(int y=13;y<=21;y++)
            if(map.GetTile(x,y) is not {Blocked:false,Elevation:1} || map.SourceBehaviorAt(x,y) != (y is 13 or 21?0x10:FourIslandFieldSource.Waterfall) || occupied?.Invoke(x,y,1)==true) return false;
        return true;
    }
    private static bool Ground(int? behavior) => behavior is 0x00 or 0x08 or >=0x30 and <=0x37 or FourIslandFieldSource.Ice or FourIslandFieldSource.ThinIce or FourIslandFieldSource.CrackedIce;
    private static bool ValidActive(FourIslandMovementState s) => s is not null && s.AwaitingWorld is not null && StableId.IsValid(s.PendingPresentationId.Value) &&
        Enum.IsDefined(s.Kind) && Enum.IsDefined(s.Phase) && Enum.IsDefined(s.MovementDirection) && s.CompletedTiles is >=0 and <=480 &&
        (s.Kind == FourIslandMovementKind.IceSlip ? s.AwaitingWorld.Player.MapId == FourIslandFieldSource.Basement && s.MoveOwnerInstanceId is null :
            s.AwaitingWorld.Player.MapId == FourIslandFieldSource.Entrance && s.CompletedTiles <=8 && s.MovementDirection == (s.Kind == FourIslandMovementKind.WaterfallAscent?Direction.North:Direction.South)) &&
        (s.Kind != FourIslandMovementKind.WaterfallAscent || s.MoveOwnerInstanceId > 0) &&
        (s.Phase == FourIslandMovementPhase.ShowPokemon ? s.Kind == FourIslandMovementKind.WaterfallAscent && s.CompletedTiles == 0 : s.CompletedTiles > 0) &&
        (s.Kind == FourIslandMovementKind.IceSlip || s.AwaitingWorld.Player.X is >=15 and <=19 && s.AwaitingWorld.Player.Y ==
            (s.Kind == FourIslandMovementKind.WaterfallAscent ? 21-s.CompletedTiles : 13+s.CompletedTiles));
    private static Diagnostic? ValidateStart(WorldSnapshot world,FourIslandFieldIntent intent,IFieldMap map,FourIslandMovementState? active) =>
        active is not null ? new("four-island-controls-locked","Complete the current presentation first.") :
        intent.CompletedPresentationId is not null ? new("unexpected-four-island-completion","There is no pending presentation.") : FourIslandFieldSource.Validate(world,intent,map);
    private static FourIslandFieldTransition Fail(WorldSnapshot w,FourIslandMovementState? a,Diagnostic d) => new(TransitionStatus.Rejected,w,a,[],FourIslandFieldCallbacks.None,d);
    private static FourIslandFieldTransition Blocked(WorldSnapshot w,CommandId c,FourIslandMovementState? a,string reason,Direction? playerAttempt = null)
    {
        var p = playerAttempt is {} direction ? w.Player with {Facing=direction} : w.Player;
        return new(TransitionStatus.Accepted,w with {Revision=w.Revision+1,Player=p},null,[new(c,FourIslandFieldEventKind.Blocked,w.Player,p,reason)],FourIslandFieldCallbacks.None,null);
    }
}
