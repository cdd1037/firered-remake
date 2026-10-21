using Game.Content;

/// <summary>Source title-screen run-state presentation. Has its own fixed seed and never touches campaign RNG.</summary>
public sealed class OpeningTitlePresentation
{
    public sealed record Flame(int X16, int Y16, int SpeedX, int SpeedY, int Age, bool Visible);
    private readonly List<Flame> flames = [];
    private uint seed = 30840;
    private int spawnTimer, offset, exitSlashUntil;
    public bool Exiting { get; private set; }
    public int Frame { get; private set; }
    public IReadOnlyList<Flame> Flames => flames;
    public bool PressStartVisible => Frame % 90 < 60;
    public bool RestartDue => !Exiting && Frame >= 2700;
    public int SlashX => -32 + Math.Max(0, Frame % 574 - 540) * 9;
    public int SlashY => SlashX is >= 67 and < 148 ? 20 : 27;
    public bool SlashVisible => Frame % 574 >= 540 && (!Exiting || Frame < exitSlashUntil);
    public void Reset() { Frame = spawnTimer = offset = exitSlashUntil = 0; Exiting = false; seed = 30840; flames.Clear(); }
    public void BeginExit()
    {
        exitSlashUntil = SlashVisible ? (Frame / 574 + 1) * 574 : Frame;
        Exiting = true;
    }
    private int Random() { seed = unchecked(seed * 1103515245 + 24691); return (int)(seed >> 16); }
    public void AdvanceFrames(int count)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        for (int frame = 0; frame < count; frame++)
        {
            Frame++;
            for (int i = flames.Count - 1; i >= 0; i--)
            {
                var f = flames[i];
                f = f with { X16 = f.X16 - f.SpeedX, Y16 = f.Y16 + f.SpeedY, Age = f.Age + 1 };
                if (f.Age >= 57 || f.X16 >> 4 < -8 || f.Y16 >> 4 < 16 || f.Y16 >> 4 > 200) flames.RemoveAt(i);
                else flames[i] = f;
            }
            // Initial task state seeds on its first frame; first spawn on the next.
            if (Frame == 1) continue;
            if (++spawnTimer < (Frame == 2 ? 0 : 18)) continue;
            spawnTimer = 0; Random();
            int sx = Random() % 4 - 2, sy = Random() % 8 - 16;
            int y = Random() % 3 + 116, x = Random() % 240;
            flames.Add(new(x * 16, y * 16, sx, sy, 0, Random() % 16 >= 8));
            foreach (int pos in new[] { 4, 16, 26, 32, 48, 200, 216, 224, 232, 60, 76, 92, 108, 128, 144 })
            {
                flames.Add(new((offset + pos) * 16, y * 16, sx, sy, 0, true));
                sx = Random() % 4 - 2; sy = Random() % 8 - 16;
            }
            offset = (offset + 1) % 4;
        }
    }
    public static int AnimationFrame(IReadOnlyList<OpeningAnimationFrame> track, int elapsed, bool loop = true)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(elapsed);
        if (track.Count == 0 || track.Any(f => f.Duration <= 0)) throw new ArgumentException("Empty or invalid animation.", nameof(track));
        int duration = track.Sum(f => f.Duration);
        if (loop) elapsed %= duration;
        foreach (var item in track) { if (elapsed < item.Duration) return item.Frame; elapsed -= item.Duration; }
        return -1;
    }
}

/// <summary>Title FLASHSPRITE/FADEIN scene scheduler, separate from the run-state flame task.</summary>
public sealed class OpeningTitleRevealPresentation
{
    private int state, timer, coefficient = 16, blendTimer, flash, flashDelta;
    public int Frame { get; private set; }
    public bool Complete => state == 10;
    public int Scanline => state == 0 ? 128 - timer * 4 : -100;
    public float GrayBrightness => state < 2 ? 0 : 1 - coefficient / 16f;
    public float WhiteBlend => state >= 9 ? coefficient / 16f : flash / 16f;
    public bool ShowLogo => state >= 9;
    public bool UseColor => state >= 9;
    public int WindowFrame { get; private set; } = -1;
    public void Reset() { state = timer = Frame = blendTimer = flash = flashDelta = 0; coefficient = 16; WindowFrame = -1; }
    public void Skip() => state = 10;
    public void AdvanceFrame()
    {
        if (Complete) return;
        Frame++;
        if (WindowFrame >= 0) WindowFrame++;
        if (flashDelta != 0)
        {
            flash = Math.Clamp(flash + flashDelta, 0, 16);
            if (flash is 0 or 16) flashDelta = 0;
        }
        switch (state)
        {
            case 0: if (++timer > 32) { state = 1; timer = 0; } break;
            case 1: if (++timer > 10) { state = 2; timer = 0; } break;
            case 2:
                if (++blendTimer >= 10) { blendTimer = 0; if (--coefficient == 0) { state = 3; timer = 0; } }
                break;
            case 3: if (++timer > 36) { WindowFrame = 0; flash = 1; flashDelta = 5; state = 4; } break;
            case 4: if (flashDelta == 0) { flash = 15; flashDelta = -5; state = 5; timer = 0; } break;
            case 5: if (++timer > 20) { flash = 1; flashDelta = 5; state = 6; } break;
            case 6: if (flashDelta == 0) { flash = 15; flashDelta = -5; state = 7; timer = 0; } break;
            case 7: if (++timer > 20) { flash = 0; flashDelta = 4; state = 8; } break;
            case 8: if (flashDelta == 0) { coefficient = 16; blendTimer = 0; state = 9; } break;
            case 9: if (++blendTimer >= 2) { blendTimer = 0; if (--coefficient == 0) state = 10; } break;
        }
    }
}

/// <summary>Oak exit affine step sequence, entirely presentation-only.</summary>
public sealed class OpeningExitPresentation
{
    private int whiteTimer = 8, whiteDelay = 8, whiteCoefficient;
    public int Frame { get; private set; }
    public float ScaleX => Frame < 20 ? 1 : (256 - Math.Min(5, Frame / 20) * 32 + 24) / 256f;
    public float ScaleY => Frame < 20 ? 1 : (256 - Math.Min(5, Frame / 20) * 32 - 16) / 256f;
    public float WhiteBlend => Math.Min(16, whiteCoefficient) / 16f;
    public float BackgroundFade => Math.Clamp(Frame / 80f, 0, 1);
    public float PlayerBlackFade => Math.Clamp((Frame - 136) / 48f, 0, 1);
    public bool Complete => Frame >= 184;
    public void AdvanceFrame()
    {
        if (Complete) return;
        Frame++;
        if (whiteTimer > 0) whiteTimer--;
        else if (whiteCoefficient < 16)
        {
            if (whiteDelay > 0) whiteDelay--;
            whiteCoefficient++;
            if (whiteCoefficient > 14) whiteCoefficient = 16;
            whiteTimer = whiteDelay;
        }
    }
}
