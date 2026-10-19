namespace Game.Domain.World;

// Accepted movement facts only. No animation clock or gameplay continuation is owned here.
public enum FieldTransitionCause { Unknown, ConnectedEdge, PlainWarp, AnimatedDoor, NonAnimatedDoor, StairWarp }
public enum FieldTransitionDeparture { Immediate, StepOntoWarp }
public enum FieldTransitionArrival { Plain, AnimatedDoor, NonAnimatedDoor, Stair }
public sealed record FieldTransitionReceipt(FieldTransitionCause Cause, FieldTransitionDeparture Departure,
    FieldTransitionArrival Arrival, TilePosition From, TilePosition To, TilePosition ArrivalAnchor)
{
    // The accepted source step can differ from the old or arrival facing.
    public TilePosition? DepartureStep { get; init; }
}
