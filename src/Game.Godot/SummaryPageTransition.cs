/// <summary>A visible Summary visit owns its fade and portrait clocks. A slow
/// redraw is not an entry. Page motion is the source's four 60-pixel steps.</summary>
internal readonly record struct SummaryPageKey(string Owner, long InstanceId, int Page, bool Details);

internal sealed class SummaryPageTransition
{
    private bool _presented;
    public SummaryPageKey? Current { get; private set; }
    public SummaryPageKey? Outgoing { get; private set; }
    public double EntryStart { get; private set; }
    public double PortraitStart { get; private set; }
    public double SlideStart { get; private set; }
    public int Direction { get; private set; }

    // The host calls this once per actual non-Help draw. A different screen
    // being rendered ends the visit; elapsed wall/render time never does.
    public void BeginFrame()
    {
        if (!_presented) Reset();
        _presented = false;
    }

    public void Present() => _presented = true;

    public void Observe(SummaryPageKey page, double now)
    {
        if (string.IsNullOrEmpty(page.Owner)) throw new ArgumentException("Summary owner required.", nameof(page));
        if (page.Page is < 0 or > 2) throw new ArgumentOutOfRangeException(nameof(page));
        if (Current is not { } previous || previous.Owner != page.Owner)
        {
            EntryStart = PortraitStart = now;
            Outgoing = null;
        }
        else if (previous.InstanceId != page.InstanceId)
        {
            // Browsing changes the portrait, not the lifetime of the whole screen.
            PortraitStart = now;
            Outgoing = null;
        }
        else if (previous.Page != page.Page || previous.Details != page.Details)
        {
            Outgoing = previous;
            SlideStart = now;
            Direction = page.Page > previous.Page || page.Details ? 1 : -1;
        }
        Current = page;
        if (!IsSliding(now)) Outgoing = null;
    }

    public int Offset(double now) => Math.Min(3, ElapsedFrames(now, SlideStart)) * 60 + 60;
    public bool IsSliding(double now) => Outgoing is not null && Offset(now) < 240;
    public bool IsEntering(double now) => Current is not null && ElapsedFrames(now, EntryStart) < 16;
    public float EntryAlpha(double now) => Math.Max(0f, 1f - (float)(Elapsed(now, EntryStart) * 60 / 16000));
    public int PortraitFrame(double now) => ElapsedFrames(now, PortraitStart);
    // Preserve fractional milliseconds until this sole source-frame quantization.
    // The tolerance absorbs floating-point addition error, not a real 16ms tick.
    public static int ElapsedFrames(double now, double start) =>
        (int)Math.Min(int.MaxValue, Math.Floor(Elapsed(now, start) * 60 / 1000 + 1e-7));
    private static double Elapsed(double now, double start) => Math.Max(0, now - start);

    public void Reset()
    {
        Current = Outgoing = null;
        EntryStart = PortraitStart = SlideStart = 0;
        Direction = 0;
        _presented = false;
    }
}
