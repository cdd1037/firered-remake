namespace Game.Content;

public enum IntroMovieStage { Copyright, OpenWindow, Star, RevealName, RevealLogo, Grass, WideShot, CloseUp, Entrance, WaitCry, Cry, WaitSwipe, Swipe, Recoil, WaitHop, HopOne, HopTwo, WaitCharge, Charge, Zoom, Fade, Black, Complete }

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
    // SpriteCB_NidorinoCry: nine crouch updates, then case 1 plays the cry.
    public const int NidorinoCryFrame = 10;
    public IntroMovieStage Stage => index < Timeline.Length ? Timeline[index].Stage : IntroMovieStage.Complete;
    public int Frame { get; private set; }
    public int TotalFrames { get; private set; }
    public bool Complete => Stage == IntroMovieStage.Complete;
    public bool CanSkip => Stage != IntroMovieStage.Copyright && !Complete;
    public static int DurationFrames => Timeline.Sum(t => t.Frames);
    public void Advance(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) throw new ArgumentOutOfRangeException(nameof(seconds));
        remainder += seconds * 60;
        while (remainder + 1e-9 >= 1 && !Complete)
        {
            remainder -= 1; TotalFrames++; Frame++;
            if (Stage == IntroMovieStage.Cry && Frame == NidorinoCryFrame) nidorinoCryPending = true;
            if (Frame >= Timeline[index].Frames) { index++; Frame = 0; }
        }
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
