public enum DiplomaPhase { Closed, FadeIn, Fanfare, AwaitingConfirm, FadeOut, ReturnFadeIn, Complete }

/// <summary>Read-only native-screen timing. Early/held confirmation is never queued for the ready phase.</summary>
public sealed class DiplomaPresentation
{
    private readonly int _fadeFrames, _fanfareCounterFrames;
    private int _phaseFrames;
    private double _fraction;
    private bool _confirmHeld;
    public DiplomaPhase Phase { get; private set; } = DiplomaPhase.Closed;
    public bool CertificateVisible => Phase is DiplomaPhase.FadeIn or DiplomaPhase.Fanfare or DiplomaPhase.AwaitingConfirm or DiplomaPhase.FadeOut;
    public bool FieldVisible => Phase is DiplomaPhase.ReturnFadeIn or DiplomaPhase.Complete;
    public bool Active => Phase != DiplomaPhase.Closed;
    public int FadeCoefficient => Phase switch {
        DiplomaPhase.FadeIn or DiplomaPhase.ReturnFadeIn => Math.Clamp(_fadeFrames - _phaseFrames, 0, _fadeFrames),
        DiplomaPhase.FadeOut => Math.Clamp(_phaseFrames, 0, _fadeFrames),
        _ => 0
    };
    public float FadeAlpha => FadeCoefficient / (float)_fadeFrames;
    public event Action? FanfareRequested;

    public DiplomaPresentation(int fadeFrames, int fanfareCounterFrames)
    {
        if (fadeFrames is <= 0 or > 60) throw new ArgumentOutOfRangeException(nameof(fadeFrames));
        if (fanfareCounterFrames is <= 0 or > 3600) throw new ArgumentOutOfRangeException(nameof(fanfareCounterFrames));
        (_fadeFrames, _fanfareCounterFrames) = (fadeFrames, fanfareCounterFrames);
    }

    public void Open(bool confirmHeld = false)
    { _confirmHeld = confirmHeld; _fraction = 0; Enter(DiplomaPhase.FadeIn); }
    public void Close()
    { _confirmHeld = false; _fraction = 0; Enter(DiplomaPhase.Closed); }
    public void ReleaseInput() => _confirmHeld = false;

    public void Confirm(bool pressed, bool echo = false)
    {
        bool fresh = pressed && !_confirmHeld && !echo;
        _confirmHeld = pressed;
        if (fresh && Phase == DiplomaPhase.AwaitingConfirm) Enter(DiplomaPhase.FadeOut);
    }

    public void Advance(double delta, bool fanfareFinished)
    {
        if (!double.IsFinite(delta) || delta < 0) throw new ArgumentOutOfRangeException(nameof(delta));
        _fraction += Math.Min(delta, .25) * 60;
        int frames = (int)(_fraction + 1e-9); _fraction -= frames;
        AdvanceFrames(frames, fanfareFinished);
    }

    public void AdvanceFrames(int frames, bool fanfareFinished)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        for (int i = 0; i < frames; i++)
        {
            switch (Phase)
            {
                case DiplomaPhase.FadeIn:
                    if (++_phaseFrames >= _fadeFrames)
                    { Enter(DiplomaPhase.Fanfare); FanfareRequested?.Invoke(); }
                    break;
                case DiplomaPhase.Fanfare:
                    // The source fanfare task decrements its counter to zero, then retires on the next tick.
                    if (++_phaseFrames >= _fanfareCounterFrames + 1 && fanfareFinished) Enter(DiplomaPhase.AwaitingConfirm);
                    break;
                case DiplomaPhase.FadeOut:
                    if (++_phaseFrames >= _fadeFrames) Enter(DiplomaPhase.ReturnFadeIn);
                    break;
                case DiplomaPhase.ReturnFadeIn:
                    if (++_phaseFrames >= _fadeFrames) Enter(DiplomaPhase.Complete);
                    break;
            }
        }
    }

    private void Enter(DiplomaPhase phase) { Phase = phase; _phaseFrames = 0; }
}
