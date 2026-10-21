/// <summary>Counts delivered logical callbacks, never elapsed time or frame differences.</summary>
public sealed class CampaignPlayTimeTicks
{
    private ulong _lastProcessFrame;
    private bool _hasProcessFrame;
    public int PendingTicks { get; private set; }

    public void Observe(ulong processFrame, bool running)
    {
        // Physics catch-up callbacks share the same process frame. Only the first
        // delivered callback can count; a large stamp jump is still just one tick.
        if (_hasProcessFrame && _lastProcessFrame == processFrame) return;
        _hasProcessFrame = true;
        _lastProcessFrame = processFrame;
        if (running) PendingTicks = checked(PendingTicks + 1);
    }

    public int PeekBatch(bool flush) => flush || PendingTicks >= 60 ? Math.Min(60, PendingTicks) : 0;

    public void Acknowledge(int ticks)
    {
        if (ticks is < 1 or > 60 || ticks > PendingTicks) throw new ArgumentOutOfRangeException(nameof(ticks));
        PendingTicks -= ticks;
    }

    public void Reset(ulong processFrame)
    {
        PendingTicks = 0;
        _lastProcessFrame = processFrame;
        _hasProcessFrame = true;
    }

    public static long PreserveMatchingRevision(long guard, long before, long after) => guard == before ? after : guard;
}
