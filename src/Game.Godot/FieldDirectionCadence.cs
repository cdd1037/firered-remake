using Game.Domain.World;

// A field action owns its cooldown. Input edges change intent, not the time
// remaining in an already accepted walk/turn/bump. No gameplay state is stored.
public sealed class FieldDirectionCadence
{
    private readonly HashSet<Direction> _held = [];
    private bool _moving;
    public double RemainingFrames { get; private set; }
    public bool IdlePullFailed { get; private set; }
    public bool Ready => RemainingFrames <= 1e-7;
    public Direction? Held => _held.Contains(Direction.North) ? Direction.North :
        _held.Contains(Direction.South) ? Direction.South :
        _held.Contains(Direction.West) ? Direction.West :
        _held.Contains(Direction.East) ? Direction.East : null;

    public void SetHeld(Direction direction, bool held)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        IdlePullFailed = false;
        if (held) _held.Add(direction); else _held.Remove(direction);
    }
    public void ClearHeld() => _held.Clear();
    public void ResetIdlePullFailure() => IdlePullFailed = false;
    public void Reset() { ClearHeld(); RemainingFrames = 0; _moving = false; IdlePullFailed = false; }
    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        RemainingFrames = Math.Max(0, RemainingFrames - seconds * 60);
        if (Ready && Held is null) _moving = false;
    }
    public bool NeedsTurn(Direction facing) => !_moving && Held is {} direction && direction != facing;
    public void Accept(int frames, bool moving)
    {
        if (frames < 1) throw new ArgumentOutOfRangeException(nameof(frames));
        RemainingFrames = frames;
        _moving = moving;
    }
    public void Complete(bool accepted, int frames, bool moving, bool idle = false)
    {
        if (accepted) Accept(frames, moving);
        else { ClearHeld(); if (idle) IdlePullFailed = true; }
    }
    public static int ActionFrames(bool cycling, bool moved, bool turn, int stepFrames, CyclingRoadAction? slope) =>
        moved ? stepFrames : slope is not null ? 1 : turn ? TurnFrames(cycling) : BumpFrames(cycling);
    public static int TurnFrames(bool cycling) => cycling ? 1 : 8;
    public static int BumpFrames(bool cycling) => cycling ? 16 : 32;
}
