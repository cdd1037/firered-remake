namespace Game.Content;

public enum IntroMovieStage { Copyright, OpenWindow, Star, RevealName, RevealLogo, Grass, WideShot, CloseUp, Entrance, WaitCry, Cry, WaitSwipe, Swipe, Recoil, WaitHop, HopOne, HopTwo, WaitCharge, Charge, Zoom, Fade, Black, Complete }

public readonly record struct IntroMovieGengarSwipe(int SineIndex, int AnimationFrame, int AmplitudeX, int AmplitudeY);

/// <summary>60 Hz presentation clock. No gameplay state, game RNG, wall-clock randomness or save access.</summary>
public sealed class IntroMoviePresentation
{
    // Source callback waits, sprite callbacks and palette transitions; DMA setup waits are collapsed.
    private static readonly (IntroMovieStage Stage, int Frames)[] Timeline = [
        (IntroMovieStage.Copyright, 173), (IntroMovieStage.OpenWindow, 8),
        (IntroMovieStage.Star, 121), (IntroMovieStage.RevealName, 144), (IntroMovieStage.RevealLogo, 153),
        (IntroMovieStage.Grass, 42), (IntroMovieStage.WideShot, 72), (IntroMovieStage.CloseUp, 62),
        (IntroMovieStage.Entrance, 57), (IntroMovieStage.WaitCry, 31), (IntroMovieStage.Cry, 60),
        (IntroMovieStage.WaitSwipe, 31), (IntroMovieStage.Swipe, 47), (IntroMovieStage.Recoil, 62),
        (IntroMovieStage.WaitHop, 17), (IntroMovieStage.HopOne, 13), (IntroMovieStage.HopTwo, 13),
        (IntroMovieStage.WaitCharge, 21), (IntroMovieStage.Charge, 121), (IntroMovieStage.Zoom, 9),
        (IntroMovieStage.Fade, 8), (IntroMovieStage.Black, 61) ];
    private int index;
    private double remainder;
    private bool nidorinoCryPending;
    private int fightFrame, fightBackgroundX256, fightGrassX = 296;
    private bool fightScrollSlow, fightGrassFinished, gengarAsSprite;
    private int gengarBounceTimer, gengarBounceState, gengarFrame;
    // SpriteCB_NidorinoCry: nine crouch updates, then case 1 plays the cry.
    public const int NidorinoCryFrame = 10;
    public IntroMovieStage Stage => index < Timeline.Length ? Timeline[index].Stage : IntroMovieStage.Complete;
    public int Frame { get; private set; }
    public int TotalFrames { get; private set; }
    public float FightBackgroundScrollX => fightBackgroundX256 / 256f;
    public int FightBackgroundPixelX => fightBackgroundX256 >> 8;
    public int FightGrassX => fightGrassX;
    public bool FightGrassVisible => fightFrame >= 16 && !fightGrassFinished;
    public int GengarFrame => gengarFrame;
    public bool GengarAsSprite => gengarAsSprite;
    public bool Complete => Stage == IntroMovieStage.Complete;
    public bool CanSkip => Stage != IntroMovieStage.Copyright && !Complete;
    public static int DurationFrames => Timeline.Sum(t => t.Frames);
    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        remainder += seconds * 60;
        while (remainder + 1e-9 >= 1 && !Complete)
        {
            remainder -= 1;
            AdvanceFightFrame();
            TotalFrames++; Frame++;
            if (Stage == IntroMovieStage.Cry && Frame == NidorinoCryFrame) nidorinoCryPending = true;
            if (Frame >= Timeline[index].Frames)
            {
                index++; Frame = 0;
                // The attack task finishes in the normal pose. Its idle task
                // stays paused until Nidorino's recoil/return hop has finished.
                if (Stage == IntroMovieStage.Recoil) gengarFrame = 0;
                if (Stage == IntroMovieStage.Charge && gengarBounceState == 0) gengarAsSprite = true;
            }
        }
    }
    private void AdvanceFightFrame()
    {
        if (Stage is < IntroMovieStage.Entrance or > IntroMovieStage.Fade) return;
        fightFrame++;
        // Scene3_Task_BgScroll accumulates its position. SpriteCB_Grass changes
        // only its speed, after the grass reaches x <= 52; neither stage nor
        // speed changes rebase an already visible background.
        fightBackgroundX256 -= fightScrollSlow ? 0x20 : 0x400;
        if (fightFrame >= 16 && !fightGrassFinished)
        {
            fightGrassX -= fightScrollSlow ? 1 : 5;
            if (fightGrassX <= 52) fightScrollSlow = true;
            if (fightGrassX <= -32) fightGrassFinished = true;
        }
        if (Stage is not (IntroMovieStage.Swipe or IntroMovieStage.Recoil) && !gengarAsSprite)
        {
            if (++gengarBounceTimer >= 30)
            {
                gengarBounceTimer = 0;
                gengarBounceState ^= 1;
                gengarFrame = gengarBounceState;
            }
            // Source waits for the normal idle pose before replacing the BG
            // Gengar with the identically positioned zoomable sprites.
            if (Stage == IntroMovieStage.Charge && gengarBounceState == 0) gengarAsSprite = true;
        }
    }
    public static IntroMovieGengarSwipe GengarSwipeAt(int frame)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(frame);
        // Source callbacks: 16 backward steps, 16 held frames, 8 forward
        // steps, then 4 return steps. The final callback pins the rest pose.
        int sineIndex = frame <= 16 ? 64 - frame * 2
            : frame <= 32 ? 32
            : frame <= 40 ? 32 + (frame - 32) * 8
            : frame < 44 ? 96 - (frame - 40) * 8 : 64;
        return new(sineIndex, frame < 36 ? 2 : frame < 44 ? 3 : 0,
            frame < 36 ? 32 : 48, frame < 36 ? 6 : 32);
    }
    public bool ConsumeNidorinoCry()
    {
        bool pending = nidorinoCryPending;
        nidorinoCryPending = false;
        return pending;
    }
    public bool Skip(bool a, bool start, bool select)
    {
        if (!CanSkip || !(a || start || select)) return false;
        index = Timeline.Length; Frame = 0; nidorinoCryPending = false; return true;
    }
    public static uint SparkleRandom(int index)
    {
        uint seed = 354128453;
        for (int i = 0; i <= index; i++) seed = unchecked(seed * 1103515245 + 12345);
        return seed >> 16;
    }
}
