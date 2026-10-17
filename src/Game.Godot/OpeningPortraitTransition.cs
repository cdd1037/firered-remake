/// <summary>Oak's separate outgoing/incoming trainer alpha tasks. Both start
/// with a two-frame wait; outgoing decreases by one, incoming increases by two,
/// with the source delay between callbacks. Platforms toggle at coefficient 8.</summary>
public sealed class OpeningPortraitTransition(string? previous, string? next, int holdFrames = 0)
{
    public string? Previous { get; } = previous;
    public string? Next { get; } = next;
    public int Frame { get; private set; }
    private int OutFrames => Previous is null ? 0 : 49;
    public bool Complete => Frame >= OutFrames + holdFrames + (Next is null ? 0 : 25);
    public string? Portrait => Frame < OutFrames ? Previous : Next;
    public float Alpha => Frame < OutFrames ? Math.Max(0, 16 - Frame / 3) / 16f
        : Math.Clamp((Frame - OutFrames - holdFrames) / 3 * 2, 0, 16) / 16f;
    public bool PlatformVisible => Frame < OutFrames ? Alpha > .5f : Alpha >= .5f;
    public void AdvanceFrames(int frames)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        Frame = Math.Min(OutFrames + holdFrames + (Next is null ? 0 : 25), Frame + frames);
    }
}
