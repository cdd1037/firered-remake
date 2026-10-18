using System.Collections.Immutable;
using Game.Presentation.BattleAnimations;

/// <summary>Read-only 60 Hz evolution scene. Never owns a Pokemon, inventory,
/// gameplay RNG or an evolution decision.</summary>
public enum EvolutionScenePhase { Intro, Spiral, Arc, Morph, Circle, Reveal, Complete, Cancelled }
public enum EvolutionIntroStage { Text, Cry, Sound, BackgroundFade, Complete }
public enum EvolutionIntroCue { Cry, Sound, Music }

/// <summary>Current playback liveness, read again after every dispatched cue.
/// Unavailable audio is explicit and never substitutes a guessed clip duration.</summary>
public readonly record struct EvolutionIntroSignals(bool AudioAvailable, bool CryPlaying, bool SoundEffectsPlaying)
{
    public static EvolutionIntroSignals Unavailable => new(false, false, false);
}
public readonly record struct EvolutionSparkle(int X, int Y, int MatrixIndex, int Subpriority)
{
    // evolution_graphics.c: reciprocal OAM matrices 0x3C0 through 0x100.
    public float Scale => 256f / (960 - 64 * MatrixIndex);
}

/// <summary>palette.c normal fade: separate BG/OBJ passes, delta 2, delay only
/// before BG, then five finishing callbacks. Coefficients are GBA 0..16.</summary>
public sealed class EvolutionPaletteFade
{
    public int BackgroundBlend { get; private set; }
    public int ObjectBlend { get; private set; }
    public bool Active { get; private set; } = true;
    private readonly int _target, _delay;
    private int _value, _timer, _finish = -1;
    private bool _objects;
    public EvolutionPaletteFade(int start, int target, int delay)
    {
        if (start is < 0 or > 16 || target is < 0 or > 16 || delay < 0)
            throw new ArgumentOutOfRangeException(nameof(start));
        BackgroundBlend = ObjectBlend = _value = start; _target = target; _timer = _delay = delay;
        AdvanceFrame(); // BeginNormalPaletteFade calls UpdatePaletteFade immediately.
    }
    public void AdvanceFrame()
    {
        if (!Active) return;
        if (_finish >= 0) { if (_finish++ == 4) Active = false; return; }
        if (!_objects)
        {
            if (_timer < _delay) { _timer++; return; }
            _timer = 0; BackgroundBlend = _value;
        }
        else ObjectBlend = _value;
        _objects = !_objects;
        if (_objects) return;
        if (_value == _target) _finish = 0;
        else _value += Math.Sign(_target - _value) * Math.Min(2, Math.Abs(_target - _value));
    }
}

public sealed class EvolutionScenePresentation
{
    public EvolutionScenePhase Phase { get; private set; } = EvolutionScenePhase.Intro;
    public int PhaseFrame { get; private set; }
    public EvolutionIntroStage IntroStage { get; private set; } = EvolutionIntroStage.Text;
    public string IntroVisibleText => _introText.VisibleText;
    public bool IntroTextComplete => _introText.Complete;
    public int BackgroundFrame { get; private set; }
    public int PreviousScale { get; private set; } = 256;
    public int TargetScale { get; private set; } = 16;
    public bool CanCancel => _allowCancellation && Phase == EvolutionScenePhase.Morph;
    public bool Animating => Phase is not (EvolutionScenePhase.Complete or EvolutionScenePhase.Cancelled);
    public bool MovingBackgroundVisible => Phase is EvolutionScenePhase.Spiral or EvolutionScenePhase.Arc or EvolutionScenePhase.Morph or EvolutionScenePhase.Circle;
    public int PortraitWhiteCoefficient => Phase switch
    {
        EvolutionScenePhase.Spiral => _fade?.ObjectBlend ?? 0,
        EvolutionScenePhase.Arc or EvolutionScenePhase.Morph or EvolutionScenePhase.Circle => 16,
        EvolutionScenePhase.Reveal => PhaseFrame < 33 ? 16 : _fade?.ObjectBlend ?? 0,
        _ => 0
    };
    public int BackgroundWhiteCoefficient => Phase == EvolutionScenePhase.Reveal ? _fade?.BackgroundBlend ?? 0 : 0;
    // The up-flash mask 0xFFF90F1C excludes portrait banks 17/18 but includes
    // sparkle bank 20. The return mask 0xFFFF0F1C includes all three OBJ banks.
    public int SparkleWhiteCoefficient => Phase == EvolutionScenePhase.Reveal ? _fade?.ObjectBlend ?? 0 : 0;
    private readonly SourceTextProgress _introText = new();
    private readonly bool _allowCancellation;
    private readonly ImmutableArray<(int Spawn, int Velocity, int Height)> _spray;
    private EvolutionPaletteFade? _fade;
    private double _fraction;
    private int _speed = 8;
    private bool _direction, _choose = true;
    public EvolutionScenePresentation(bool canCancel, string introText, int textSpeed, uint visualSeed = 0x45564F)
    {
        _allowCancellation = canCancel;
        _introText.Reset(introText, textSpeed);
        // Source Random() recurrence, privately seeded: no domain RNG reads or writes.
        int Random() { visualSeed = unchecked(visualSeed * 1103515245 + 24691); return (int)(visualSeed >> 16); }
        var spray = ImmutableArray.CreateBuilder<(int, int, int)>();
        for (int frame = 0; frame < 50; frame++)
        {
            if (frame == 32) continue; // The fade callback replaces this frame's spawn.
            if (frame != 0) Random(); // Unused argument in CreatePostEvoSparkleSet2 still consumes a draw.
            for (int i = 0; i < (frame == 0 ? 8 : 1); i++) spray.Add((frame, 3 - Random() % 7, 48 + (Random() & 63)));
        }
        _spray = spray.ToImmutable();
    }
    public bool Cancel()
    {
        // Task_EvolutionScene accepts B only during WAIT_CYCLE_MON_SPRITE.
        if (!CanCancel) return false;
        Phase = EvolutionScenePhase.Cancelled; PhaseFrame = 0;
        PreviousScale = 256; TargetScale = 0;
        return true;
    }
    public int Advance(double seconds, Func<EvolutionIntroSignals> readIntroSignals,
        Action<EvolutionIntroCue> dispatchIntroCue, bool speedButtonHeld = false, bool speedButtonPressed = false)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        _fraction += Math.Min(seconds, .25) * 60;
        int frames = (int)(_fraction + 1e-9); _fraction -= frames;
        AdvanceFrames(frames, readIntroSignals, dispatchIntroCue, speedButtonHeld, speedButtonPressed);
        return frames;
    }
    public void AdvanceFrames(int frames, Func<EvolutionIntroSignals> readIntroSignals,
        Action<EvolutionIntroCue> dispatchIntroCue, bool speedButtonHeld = false, bool speedButtonPressed = false)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frames);
        ArgumentNullException.ThrowIfNull(readIntroSignals);
        ArgumentNullException.ThrowIfNull(dispatchIntroCue);
        for (int i = 0; i < frames && Animating; i++)
        {
            PhaseFrame++;
            if (MovingBackgroundVisible) BackgroundFrame++;
            _fade?.AdvanceFrame();
            switch (Phase)
            {
                case EvolutionScenePhase.Intro:
                    // Dispatch synchronously before the next snapshot. A multi-frame
                    // host update must not reuse pre-cry/pre-SE "not playing" evidence.
                    if (AdvanceIntro(readIntroSignals(), speedButtonHeld, speedButtonPressed && i == 0) is {} cue)
                        dispatchIntroCue(cue);
                    break;
                case EvolutionScenePhase.Spiral: if (PhaseFrame >= 162) Enter(EvolutionScenePhase.Arc); break;
                case EvolutionScenePhase.Arc: if (PhaseFrame >= 99) Enter(EvolutionScenePhase.Morph); break;
                case EvolutionScenePhase.Morph:
                    // One choose/shrink callback per frame, reciprocal scales 256/16.
                    if (_choose)
                    {
                        if (_speed == 128) { PreviousScale = 0; TargetScale = 256; Enter(EvolutionScenePhase.Circle); }
                        else { _speed += 2; _direction = !_direction; _choose = false; }
                    }
                    else
                    {
                        PreviousScale = Math.Clamp(PreviousScale + (_direction ? -_speed : _speed), 16, 256);
                        TargetScale = Math.Clamp(TargetScale + (_direction ? _speed : -_speed), 16, 256);
                        _choose = _direction ? PreviousScale == 16 && TargetScale == 256 : PreviousScale == 256 && TargetScale == 16;
                    }
                    break;
                case EvolutionScenePhase.Circle: if (PhaseFrame >= 51) Enter(EvolutionScenePhase.Reveal); break;
                case EvolutionScenePhase.Reveal:
                    if (PhaseFrame == 33) _fade = new(16, 0, 16);
                    if (PhaseFrame >= 130 && _fade is { Active: false }) Enter(EvolutionScenePhase.Complete);
                    break;
            }
        }
    }
    private EvolutionIntroCue? AdvanceIntro(EvolutionIntroSignals signals, bool held, bool pressed)
    {
        // evolution_scene.c:675-709: printer EOS -> cry -> SE1/SE2 idle ->
        // music + normal fade -> spiral. The initial terrain palette is not modeled.
        switch (IntroStage)
        {
            case EvolutionIntroStage.Text:
                _introText.AdvanceFrames(1, held, pressed);
                if (!_introText.Complete) break;
                IntroStage = EvolutionIntroStage.Cry;
                return EvolutionIntroCue.Cry;
            case EvolutionIntroStage.Cry:
                if (signals.AudioAvailable && signals.CryPlaying) break;
                IntroStage = EvolutionIntroStage.Sound;
                return EvolutionIntroCue.Sound;
            case EvolutionIntroStage.Sound:
                if (signals.AudioAvailable && signals.SoundEffectsPlaying) break;
                IntroStage = EvolutionIntroStage.BackgroundFade;
                _fade = new(0, 16, 4);
                return EvolutionIntroCue.Music;
            case EvolutionIntroStage.BackgroundFade:
                if (_fade is { Active: false })
                { IntroStage = EvolutionIntroStage.Complete; Enter(EvolutionScenePhase.Spiral); }
                break;
        }
        return null;
    }
    private void Enter(EvolutionScenePhase phase)
    {
        Phase = phase; PhaseFrame = 0;
        if (phase == EvolutionScenePhase.Spiral) _fade = new(0, 16, 10);
        if (phase == EvolutionScenePhase.Reveal) _fade = new(0, 16, 0);
    }
    public ImmutableArray<EvolutionSparkle> Sparkles(ImmutableArray<int> sine)
    {
        if (sine.Length != 320) throw new ArgumentException("Native sine table required.", nameof(sine));
        int Sin(int angle, int radius) => sine[angle & 255] * radius >> 8;
        int Cos(int angle, int radius) => sine[(angle & 255) + 64] * radius >> 8;
        var result = ImmutableArray.CreateBuilder<EvolutionSparkle>();
        if (Phase == EvolutionScenePhase.Reveal)
        {
            foreach (var (spawn, velocity, height) in _spray)
            {
                int age = PhaseFrame - 1 - spawn;
                if (age is < 0 or >= 128) continue;
                result.Add(new(120 + velocity * age / 3, 57 + age / 4 - Sin(age, height),
                    Math.Max(0, 11 - (age + 1) * 12 / 128), age + 1 > 64 ? 1 : 20));
            }
        }
        else if (Phase is EvolutionScenePhase.Spiral or EvolutionScenePhase.Arc or EvolutionScenePhase.Circle)
        {
            int count = Phase == EvolutionScenePhase.Circle ? 16 : Phase == EvolutionScenePhase.Arc ? 9 : 4;
            int step = Phase == EvolutionScenePhase.Circle ? 32 : Phase == EvolutionScenePhase.Spiral ? 8 : 1;
            int last = Phase == EvolutionScenePhase.Spiral ? 56 : Phase == EvolutionScenePhase.Arc ? 5 : 32;
            for (int spawn = 0; spawn <= last; spawn += step)
            for (int i = 0; i < count; i++)
            {
                int age = PhaseFrame - 1 - spawn;
                if (age < 0) continue;
                if (Phase == EvolutionScenePhase.Spiral)
                {
                    if (age > 80) continue;
                    int radius = 48 - age / 2, angle = spawn * 2 + i * 64 + age * 4;
                    int y2 = Sin(angle, radius) / 4;
                    result.Add(new(120 + Cos(angle, radius), 88 - age * age / 80 + y2,
                        Math.Min(11, (48 - (age + 1) / 2) / 4), y2 > 0 ? 1 : 20));
                }
                else if (Phase == EvolutionScenePhase.Arc)
                {
                    if (age > 20) continue;
                    int radius = age == 0 ? 8 : Sin((age - 1) * 4, 40) + 8;
                    result.Add(new(120 + Cos(i * 16, radius), 8 + age * age / 5 + Sin(i * 16, radius) / 4, 5, 1));
                }
                else
                {
                    int radius = 120 - age * (spawn == 0 ? 4 : 8), angle = i * 16 + age * 4;
                    if (radius <= 8) continue;
                    result.Add(new(120 + Cos(angle, radius), 56 + Sin(angle, radius), 11, 1));
                }
            }
        }
        return result.ToImmutable();
    }
}

/// <summary>Pure RGB555 evolution pixels. Source assets remain unchanged; bit 15
/// is retained opacity. Field composition is bounded to the existing 240x112 crop.</summary>
public static class EvolutionScenePixels
{
    public static SourcePixelAsset Whiten(SourcePixelAsset source, int coefficient)
    {
        if (coefficient is < 0 or > 16) throw new ArgumentOutOfRangeException(nameof(coefficient));
        if (source.Width <= 0 || source.Height <= 0 || source.Pixels.Length != source.Width * source.Height)
            throw new ArgumentException("Invalid evolution pixel asset.", nameof(source));
        var pixels = new ushort[source.Pixels.Length];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = SourcePaletteMath.Blend(source.Pixels[i], coefficient, 0x7fff);
        return new(source.Width, source.Height, pixels);
    }

    public static SourcePixelAsset MovingBackground(SourcePixelAsset inner, SourcePixelAsset outer,
        int innerScrollX, int innerScrollY, int outerScrollX, int outerScrollY)
    {
        if (inner.Width != 256 || inner.Height != 256 || inner.Pixels.Length != 256 * 256 ||
            outer.Width != 256 || outer.Height != 256 || outer.Pixels.Length != 256 * 256)
            throw new ArgumentException("Evolution backgrounds must be 256x256 source crops.");
        var pixels = new ushort[240 * 112];
        for (int y = 0; y < 112; y++)
        for (int x = 0; x < 240; x++)
        {
            ushort first = inner.Pixels[((y + innerScrollY) & 255) * 256 + ((x + innerScrollX) & 255)];
            ushort second = outer.Pixels[((y + outerScrollY) & 255) * 256 + ((x + outerScrollX) & 255)];
            pixels[y * 240 + x] = (first & 0x8000) == 0 ? ((second & 0x8000) == 0 ? (ushort)0x8000 : second)
                : (second & 0x8000) == 0 ? first : SourcePaletteMath.AlphaBlend(first, second, 8, 8);
        }
        return new(240, 112, pixels);
    }
}
