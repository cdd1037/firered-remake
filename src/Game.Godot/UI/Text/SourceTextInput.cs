/// <summary>
/// One new A/B edge belongs to the next printer frame, not every frame in a
/// host update. Held state is sampled from the host's combined physical owners.
/// Clearing a page's pending edge never releases a physical button.
/// </summary>
public sealed class SourceTextInput
{
    private bool _pressed;
    public void Press() => _pressed = true;
    public void Clear() => _pressed = false;
    public void Advance(SourceTextProgress printer, int frames, bool held)
    {
        ArgumentNullException.ThrowIfNull(printer);
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        if (frames == 0) return;
        printer.AdvanceFrames(frames, held, _pressed);
        _pressed = false;
    }
}
