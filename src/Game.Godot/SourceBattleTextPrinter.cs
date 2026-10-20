/// <summary>
/// Presentation-only battle page clock. Owns reveal and one-shot speed input, not
/// semantic battle pages, animation gates, result choices, or event acknowledgments.
/// </summary>
public sealed class SourceBattleTextPrinter
{
    private readonly SourceTextProgress _progress = new();
    private readonly SourceTextInput _input = new();
    private double _frameRemainder;

    public bool Complete => _progress.Complete;
    public int VisibleCodepoints => _progress.VisibleCodepoints;
    public string VisibleText => _progress.VisibleText;

    public void Reset(string page, int speed)
    {
        _progress.Reset(page, speed);
        _frameRemainder = 0;
        _input.Clear();
    }

    public void ContinuePage(string page)
    {
        _progress.ContinuePage(page);
        _frameRemainder = 0;
        _input.Clear();
    }

    // A press which speeds printing must never also acknowledge this page, even
    // if its next source frame prints the last character or processes EOS.
    public bool Press()
    {
        if (Complete) return false;
        _input.Press();
        return true;
    }

    public void ReleaseInput() => _input.Clear();

    public void Advance(double delta, bool held)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        if (Complete) return;
        _frameRemainder += delta * 60;
        int frames = (int)Math.Min(int.MaxValue, Math.Floor(_frameRemainder));
        if (frames <= 0) return;
        _frameRemainder -= frames;
        _input.Advance(_progress, frames, held);
    }
}
