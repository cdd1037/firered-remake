namespace Game.Domain.World;

// Accepted movement facts only. No animation clock or gameplay continuation is owned here.
public enum FieldTransitionCause { Unknown, ConnectedEdge, PlainWarp, AnimatedDoor, NonAnimatedDoor, StairWarp }
public enum FieldTransitionDeparture { Immediate, StepOntoWarp }
public enum FieldTransitionArrival { Plain, AnimatedDoor, NonAnimatedDoor, Stair }
public sealed record FieldTransitionReceipt(FieldTransitionCause Cause, FieldTransitionDeparture Departure,
    FieldTransitionArrival Arrival, TilePosition From, TilePosition To, TilePosition ArrivalAnchor)
{
    // Shared destination classification for static and resolved dynamic warps.
    public static FieldTransitionArrival ArrivalAt(int? sourceBehavior)=>sourceBehavior switch
    {
        0x69=>FieldTransitionArrival.AnimatedDoor,
        0x60=>FieldTransitionArrival.NonAnimatedDoor,
        >=0x6c and <=0x6f=>FieldTransitionArrival.Stair,
        _=>FieldTransitionArrival.Plain
    };

    // Source metatile facts, captured by the movement producer for stair offsets.
    public int? DepartureStairBehavior { get; init; }
    public int? ArrivalStairBehavior { get; init; }

    // Script warp captures this flag after its final source commands, before
    // map entry can change it. Null retains ordinary movement's origin policy.
    public bool? SuppressMusicFade { get; init; }

    // The accepted source step can differ from the old or arrival facing.
    public TilePosition? DepartureStep { get; init; }
}
