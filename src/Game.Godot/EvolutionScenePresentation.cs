/// <summary>Read-only 60 Hz evolution scene. Ports the source reciprocal sprite scaling;
/// never owns a Pokemon, inventory, RNG or an evolution decision.</summary>
public enum EvolutionScenePhase { Intro, Spiral, Arc, Morph, Circle, Reveal, Complete, Cancelled }
public sealed class EvolutionScenePresentation
{
    public EvolutionScenePhase Phase { get; private set; } = EvolutionScenePhase.Intro;
    public int PhaseFrame { get; private set; }
    public int BackgroundFrame { get; private set; }
    public int PreviousScale { get; private set; } = 256;
    public int TargetScale { get; private set; } = 16;
    public bool CanCancel { get; }
    public bool Animating => Phase is not (EvolutionScenePhase.Complete or EvolutionScenePhase.Cancelled);
    public bool Silhouette => Phase is EvolutionScenePhase.Arc or EvolutionScenePhase.Morph or EvolutionScenePhase.Circle;
    private double _fraction;
    private int _speed = 8;
    private bool _direction, _choose = true;
    public EvolutionScenePresentation(bool canCancel) => CanCancel = canCancel;

    public bool Cancel()
    {
        if (!CanCancel || !Animating) return false;
        Phase = EvolutionScenePhase.Cancelled; PhaseFrame = 0;
        PreviousScale = 256; TargetScale = 0;
        return true;
    }
    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        // A stalled host never skips the user's whole cancellation window.
        _fraction += Math.Min(seconds, .25) * 60;
        int frames = (int)(_fraction + 1e-9); _fraction -= frames;
        AdvanceFrames(frames);
    }
    public void AdvanceFrames(int frames)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        for (int i=0; i<frames && Animating; i++)
        {
            PhaseFrame++;
            if (Phase is not EvolutionScenePhase.Intro) BackgroundFrame++;
            switch (Phase)
            {
                case EvolutionScenePhase.Intro: if (PhaseFrame >= 90) Enter(EvolutionScenePhase.Spiral); break;
                case EvolutionScenePhase.Spiral: if (PhaseFrame >= 162) Enter(EvolutionScenePhase.Arc); break;
                case EvolutionScenePhase.Arc: if (PhaseFrame >= 99) Enter(EvolutionScenePhase.Morph); break;
                case EvolutionScenePhase.Morph:
                    // evolution_graphics.c EvoTask_ChooseNextEvoSpriteAnim and
                    // EvoTask_ShrinkOrExpandEvoSprites. One callback per frame.
                    if (_choose)
                    {
                        if (_speed == 128) { PreviousScale=0; TargetScale=256; Enter(EvolutionScenePhase.Circle); }
                        else { _speed += 2; _direction = !_direction; _choose=false; }
                    }
                    else
                    {
                        PreviousScale = Math.Clamp(PreviousScale + (_direction ? -_speed : _speed),16,256);
                        TargetScale = Math.Clamp(TargetScale + (_direction ? _speed : -_speed),16,256);
                        _choose = _direction ? PreviousScale==16 && TargetScale==256 : PreviousScale==256 && TargetScale==16;
                    }
                    break;
                case EvolutionScenePhase.Circle: if (PhaseFrame>=51) Enter(EvolutionScenePhase.Reveal); break;
                case EvolutionScenePhase.Reveal: if (PhaseFrame>=306) Enter(EvolutionScenePhase.Complete); break;
            }
        }
    }
    private void Enter(EvolutionScenePhase phase) { Phase=phase; PhaseFrame=0; }
    public float WhiteAmount => Phase switch {
        EvolutionScenePhase.Spiral => Math.Clamp(PhaseFrame / 176f,0,1),
        EvolutionScenePhase.Arc or EvolutionScenePhase.Morph or EvolutionScenePhase.Circle => 1,
        EvolutionScenePhase.Reveal => Math.Clamp(1-(PhaseFrame-32)/272f,0,1),
        _ => 0
    };
}
