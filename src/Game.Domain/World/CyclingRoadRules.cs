namespace Game.Domain.World;

public enum CyclingRoadAction { Idle, Turn, Move }
public sealed record CyclingRoadInputResult(CyclingRoadAction Action, Direction Direction, bool SlopeState, int Ticks);

/// <summary>FireRed bike.c Normal/Slope input transitions, including DIR_NONE's face-down action.</summary>
public static class CyclingRoadRules
{
    public static bool IsSlope(int? behavior) => behavior is 0xd0 or 0xd1;

    public static CyclingRoadInputResult Resolve(bool slopeState, Direction facing, Direction? input, bool brake)
    {
        if (!Enum.IsDefined(facing) || input is { } direction && !Enum.IsDefined(direction))
            throw new ArgumentException("cycling_road_direction");
        // The source Slope handler turns once and resets to Normal whenever input changes.
        // DIR_NONE maps to MOVEMENT_ACTION_FACE_DOWN. B is tested by Normal, not Slope.
        if (slopeState && input != facing)
            return new(CyclingRoadAction.Turn, input ?? Direction.South, false, 1);
        if (!slopeState && brake && input is null)
            return new(CyclingRoadAction.Idle, facing, false, 1);
        bool downhill = input is null or Direction.South && (slopeState || !brake);
        return new(CyclingRoadAction.Move, downhill ? Direction.South : input!.Value, true, downhill ? 4 : 16);
    }
}
