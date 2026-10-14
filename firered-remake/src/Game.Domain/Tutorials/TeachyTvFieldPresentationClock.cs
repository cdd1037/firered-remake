namespace Game.Domain.Tutorials;

/// <summary>Bounded source normal-palette clock. A renderer calls Update after
/// its controller tasks each frame, including the frame that calls Begin.
/// TransferPending models source UpdatePaletteFade's early loading return.</summary>
public sealed record TeachyTvNormalPaletteClock(uint PaletteMask, int Delay, int Delta,
    int Coefficient, int Target, bool Active, bool ObjectHalf, int DelayCounter,
    bool Finishing, int FinishingCounter, int BackgroundCoefficient, int ObjectCoefficient);

public static class TeachyTvFieldPresentationClock
{
    public const int BagPocketSwitchFrames = 13; // 12 revealed tile rows, then rebuild/switch task
    public const int BagCloseWindowFrames = 11; // -16 + 16*n reaches 160
    public const int BagOpenWindowFrames = 12; // 192 - 16*n reaches 0

    public static TeachyTvNormalPaletteClock Begin(uint paletteMask, int delay, int from, int to)
    {
        if (from is < 0 or > 16 || to is < 0 or > 16 || delay is < -13 or > 63)
            throw new ArgumentOutOfRangeException(nameof(delay), "source_palette_arguments");
        int normalDelay = Math.Max(0, delay), delta = 2 + Math.Max(0, -delay);
        // BeginNormalPaletteFade itself performs the first BG update and forces
        // its copy. The subsequent per-frame Update is still required.
        return Update(new(paletteMask, normalDelay, delta, from, to, true, false,
            normalDelay, false, 0, from, from));
    }

    public static TeachyTvNormalPaletteClock Update(TeachyTvNormalPaletteClock state,
        bool transferPending = false)
    {
        if (transferPending || !state.Active) return state;
        if (state.Finishing)
            return state.FinishingCounter == 4
                ? state with { Active = false, Finishing = false, FinishingCounter = 0 }
                : state with { FinishingCounter = state.FinishingCounter + 1 };
        if (!state.ObjectHalf && state.DelayCounter < state.Delay)
            return state with { DelayCounter = state.DelayCounter + 1 };
        if (!state.ObjectHalf)
            return state with { DelayCounter = 0, BackgroundCoefficient = state.Coefficient, ObjectHalf = true };
        state = state with { ObjectCoefficient = state.Coefficient, ObjectHalf = false };
        if (state.Coefficient == state.Target) return state with { Finishing = true };
        int next = state.Coefficient < state.Target ? Math.Min(state.Coefficient + state.Delta, state.Target)
            : Math.Max(state.Coefficient - state.Delta, state.Target);
        return state with { Coefficient = next };
    }

    /// <summary>Updates after Begin, including the same frame's regular update,
    /// with one palette transfer per frame. An untransferred buffer extends this.</summary>
    public static int UpdatesUntilInactive(int delay, int from, int to)
    {
        var state = Begin(uint.MaxValue, delay, from, to);
        int levelsAfterStart = (Math.Abs(to - from) + state.Delta - 1) / state.Delta;
        return 1 + levelsAfterStart * (state.Delay + 2) + 5;
    }
}
