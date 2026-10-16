using System.Collections.Immutable;
using Game.Domain.Ids;

namespace Game.Domain.World;

public enum DottedHoleFallPhase { EntryStep, Departure, Arrival }
/// <summary>Transient capability created only by a real adjacent movement. Never serialize as a player save.</summary>
public sealed class DottedHoleFallState
{
    public WorldSnapshot AwaitingWorld { get; }
    public CommandId PendingPresentationId { get; }
    public DottedHoleFallPhase Phase { get; }
    public MapId SourceMap { get; }
    public FieldWarp SourceWarp { get; }
    public TilePosition Landing { get; }
    internal ImmutableHashSet<CommandId> UsedCommands {get;}
    public bool ControlsLocked => true;
    public bool CanSave => false;
    public bool PreventStep => Phase == DottedHoleFallPhase.Arrival;
    internal DottedHoleFallState(WorldSnapshot world, CommandId id, DottedHoleFallPhase phase,
        MapId sourceMap, FieldWarp warp, TilePosition landing,ImmutableHashSet<CommandId>? usedCommands=null)
    { AwaitingWorld=world; PendingPresentationId=id; Phase=phase; SourceMap=sourceMap; SourceWarp=warp; Landing=landing;
      UsedCommands=(usedCommands??ImmutableHashSet<CommandId>.Empty).Add(id); }
}
/// <summary>Application applies this exactly once, atomically with map loading. Preserve EscapeWarp and permanent door flag.</summary>
public sealed record DottedHoleMapLoadEffects(bool ClearTemporaryFieldData, bool ClearStrengthAndFlutes,
    bool RestartEncounterImmunity, bool ResetAvatarOnFoot, bool PreserveEscapeWarp);
public sealed record DottedHoleFallTransition(TransitionStatus Status, WorldSnapshot State, DottedHoleFallState? Active,
    ImmutableArray<FourIslandFieldEvent> Events, FourIslandFieldCallbacks Callbacks,
    DottedHoleMapLoadEffects? MapLoad, Diagnostic? Diagnostic)
{
    public bool ControlsLocked => Active is not null;
    public bool CanSave => Active is null;
}

/// <summary>Ordinary MB_FALL_WARP dispatch. No STEP_CB_ICE state, cracking, warphole coordinate fallback, or puzzle-progress counter.</summary>
public static class DottedHoleFallRules
{
    public static ImmutableArray<string> StepCounterOrder {get;} = ["GAME_STAT_STEPS", "WonderNews_IncrementStepCounter",
        "IncrementRenewableHiddenItemStepCounter", "RunMassageCooldownStepCounter", "IncrementResortGorgeousStepCounter", "IncrementBirthIslandRockStepCount"];
    /// <summary>Only the 17 authenticated hole cells are accepted. Ordinary ground, ladders and arrows retain their existing handlers.</summary>
    public static DottedHoleFallTransition StepOntoHole(WorldSnapshot world, FourIslandFieldIntent intent,
        IReadOnlyDictionary<MapId,IFieldMap> maps, Direction direction, FourIslandTravelMode mode,
        bool roamerActive, DottedHoleFallState? active = null, Func<int,int,int,bool>? occupied = null)
    {
        if (active is not null) return Fail(world,active,"dotted-hole-controls-locked");
        if (!maps.TryGetValue(world.Player.MapId,out var map)) return Fail(world,null,"dotted-hole-map-missing");
        var error=DottedHoleSource.ValidateWorld(world,intent,map);
        if(error is not null) return Fail(world,null,error.Code);
        if (intent.CompletedPresentationId is not null || !Enum.IsDefined(direction) || !Enum.IsDefined(mode) || mode==FourIslandTravelMode.Surf)
            return Fail(world,null,"invalid-dotted-hole-step");
        if(roamerActive) return Fail(world,null,"active-roamer-warp-handler-required");
        if(!DottedHoleSource.ValidateMap(map)) return Fail(world,null,"dotted-hole-source-map-mismatch");
        if(map.SourceBehaviorAt(world.Player.X,world.Player.Y) is not (0x00 or 0x08))
            return Fail(world,null,"dotted-hole-current-special-handler-required");
        var (dx,dy)=direction switch { Direction.North=>(0,-1), Direction.East=>(1,0), Direction.South=>(0,1), _=>(-1,0) };
        int x=world.Player.X+dx,y=world.Player.Y+dy;
        if(map.SourceBehaviorAt(x,y)!=DottedHoleSource.FallBehavior || map.GetTile(x,y) is not {Blocked:false,Elevation:3})
            return Fail(world,null,"dotted-hole-target-not-fall");
        var warp=map.FieldWarps.FirstOrDefault(w=>w.X==x&&w.Y==y&&(w.Elevation==0||w.Elevation==3));
        if(warp is null || !maps.TryGetValue(warp.DestinationMapId,out var destination) || !DottedHoleSource.ValidateMap(destination))
            return Fail(world,null,"dotted-hole-destination-mismatch");
        var arrival=destination.FieldWarps[warp.DestinationWarpId];
        if(destination.SourceBehaviorAt(arrival.X,arrival.Y)!=0x08 || destination.GetTile(arrival.X,arrival.Y) is not {Blocked:false,Elevation:3})
            return Fail(world,null,"dotted-hole-landing-mismatch");
        if(world.Player.Elevation is not (0 or 3) || occupied?.Invoke(x,y,3)==true)
        {
            var turned=world.Player with {Facing=direction};
            return new(TransitionStatus.Accepted,world with {Revision=world.Revision+1,Player=turned},null,
                [new(intent.CommandId,FourIslandFieldEventKind.Blocked,world.Player,turned,"dotted-hole-collision")],FourIslandFieldCallbacks.None,null,null);
        }
        var position=new TilePosition(map.Id,x,y,3,direction);
        var landing=new TilePosition(destination.Id,arrival.X,arrival.Y,3,Direction.South);
        var next=world with {Revision=world.Revision+1,Player=position};
        var pixels=mode==FourIslandTravelMode.Bicycle?FourIslandForcedMovementRules.Fast2Pixels:
            mode==FourIslandTravelMode.Run?FourIslandForcedMovementRules.Fast1Pixels:Enumerable.Repeat(1,16).ToImmutableArray();
        var state=new DottedHoleFallState(next,intent.CommandId,DottedHoleFallPhase.EntryStep,map.Id,warp,landing);
        return new(TransitionStatus.Accepted,next,state,
            [new(intent.CommandId,FourIslandFieldEventKind.Moved,world.Player,position,"dotted-hole-entry-step",
                new(mode==FourIslandTravelMode.Bicycle?"ride_water_current":mode==FourIslandTravelMode.Run?"player_run":"walk_normal",direction,pixels))],
            FourIslandFieldCallbacks.None,null,null);
    }

    /// <summary>Entry ack accounts the step; departure ack loads the map; arrival ack unlocks. Every cue has its own exact ID.</summary>
    public static DottedHoleFallTransition CompletePresentation(WorldSnapshot world,DottedHoleFallState active,
        FourIslandFieldIntent intent,IReadOnlyDictionary<MapId,IFieldMap> maps)
    {
        if(world!=active.AwaitingWorld || intent.ExpectedRevision!=world.Revision || !StableId.IsValid(intent.CommandId.Value) ||
            active.UsedCommands.Contains(intent.CommandId) || intent.CompletedPresentationId!=active.PendingPresentationId)
            return Fail(world,active,"dotted-hole-presentation-mismatch");
        if(!maps.TryGetValue(active.SourceMap,out var source) || !DottedHoleSource.ValidateMap(source) ||
            !maps.TryGetValue(active.Landing.MapId,out var destination) || !DottedHoleSource.ValidateMap(destination))
            return Fail(world,active,"dotted-hole-source-map-mismatch");
        var next=world with {Revision=world.Revision+1};
        if(active.Phase==DottedHoleFallPhase.Arrival)
            return new(TransitionStatus.Accepted,next,null,
                [new(intent.CommandId,FourIslandFieldEventKind.Stopped,world.Player,world.Player,"clear-prevent-step-unlock-unfreeze-restore-camera")],FourIslandFieldCallbacks.None,null,null);
        bool entry=active.Phase==DottedHoleFallPhase.EntryStep;
        if(entry) next=next with {StepCount=world.StepCount+1};
        else next=next with {Player=active.Landing};
        var phase=entry?DottedHoleFallPhase.Departure:DottedHoleFallPhase.Arrival;
        var pending=new DottedHoleFallState(next,intent.CommandId,phase,active.SourceMap,active.SourceWarp,active.Landing,active.UsedCommands);
        return new(TransitionStatus.Accepted,next,pending,
            [new(intent.CommandId,entry?FourIslandFieldEventKind.Moved:FourIslandFieldEventKind.Fell,world.Player,next.Player,
                entry?"EventScript_DoFallWarp":"FieldCB_FallWarpExit")],
            entry?new(true,false,false,false,false):FourIslandFieldCallbacks.None,
            entry?null:new(true,true,true,true,true),null);
    }

    // B/Menu/Save cannot cancel an already committed source fall. Before StepOntoHole there is no fall transaction to cancel.
    public static DottedHoleFallTransition Cancel(WorldSnapshot world,DottedHoleFallState active) => Fail(world,active,"dotted-hole-fall-not-cancellable");
    private static DottedHoleFallTransition Fail(WorldSnapshot world,DottedHoleFallState? active,string code) =>
        new(TransitionStatus.Rejected,world,active,[],FourIslandFieldCallbacks.None,null,new(code,code));
}

/// <summary>Source visual task parameters. Fade/music/weather completion are separate acknowledgements, never invented fixed frame totals.</summary>
public static class DottedHoleFallPresentation
{
    public const int BeforeInvisibleDelay=20, BeforeWarpDelay=60;
    public static ImmutableArray<int> CameraPans {get;}=[4,-4,4,-4,2,-2,2,-2,1,-1,1,-1];
    public static int StartingOffset(int spriteY,int centerToCornerY,int spriteCoordinateOffsetY) =>
        -(spriteY+centerToCornerY+spriteCoordinateOffsetY-(centerToCornerY<<1));
    public static ImmutableArray<int> FallingOffsets(int initialOffset)
    {
        if(initialOffset is >=0 or <-512) throw new ArgumentOutOfRangeException(nameof(initialOffset));
        var offsets=ImmutableArray.CreateBuilder<int>();int velocity=1,accumulator=0,y=initialOffset;
        while(y<0)
        {
            y+=velocity;
            if(velocity<8) {accumulator+=velocity;if((accumulator&15)!=0)velocity<<=1;}
            offsets.Add(Math.Min(0,y));
        }
        return offsets.ToImmutable();
    }
}
