namespace Game.Domain.Time;

/// <summary>The four saved fields in SaveBlock2, including the subsecond remainder.
/// The source counts 60 main-loop updates per displayed second, not wall time.</summary>
public readonly record struct SourcePlayTime(ushort Hours, byte Minutes, byte Seconds, byte VBlanks);

/// <summary>Runtime-only source state. This is not part of SaveBlock2.</summary>
public enum SourcePlayTimeCounterMode : byte { Stopped, Running, MaxedOut }

public readonly record struct SourcePlayTimeCounter(SourcePlayTime Time, SourcePlayTimeCounterMode Mode);

/// <summary>Pure translation of the pinned play_time.c. No host clock, elapsed-history
/// inference, RNG, rendering, save I/O or session policy lives here.</summary>
public static class SourcePlayTimeRules
{
    public const string SourceCommit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";
    public const string SourcePath = "src/play_time.c";
    public const string SourceSha256 = "6444dec627a94218a9ee72d4e8c0ebb1fe66262d8f93fd1cc5de66cb5c9e4baf";
    public const int FramesPerSecond = 60;
    public const int MaximumHours = 999;
    public static SourcePlayTime Maximum { get; } = new(999, 59, 59, 59);

    public static SourcePlayTimeCounter Reset() => new(default, SourcePlayTimeCounterMode.Stopped);

    public static SourcePlayTimeCounter Start(SourcePlayTimeCounter counter) =>
        counter.Time.Hours > MaximumHours ? SetToMax() : counter with { Mode = SourcePlayTimeCounterMode.Running };

    public static SourcePlayTimeCounter Stop(SourcePlayTimeCounter counter) =>
        counter with { Mode = SourcePlayTimeCounterMode.Stopped };

    public static SourcePlayTimeCounter SetToMax() => new(Maximum, SourcePlayTimeCounterMode.MaxedOut);

    /// <summary>Exactly one original main-loop call. Unchecked byte increments retain
    /// the C unsigned-field behavior even for raw noncanonical source input. The
    /// Application save boundary should reject that input with IsCanonicalSaveTime.</summary>
    public static SourcePlayTimeCounter Update(SourcePlayTimeCounter counter)
    {
        if (counter.Mode != SourcePlayTimeCounterMode.Running) return counter;
        var time = counter.Time with { VBlanks = unchecked((byte)(counter.Time.VBlanks + 1)) };
        if (time.VBlanks > 59)
        {
            time = time with { VBlanks = 0, Seconds = unchecked((byte)(time.Seconds + 1)) };
            if (time.Seconds > 59)
            {
                time = time with { Seconds = 0, Minutes = unchecked((byte)(time.Minutes + 1)) };
                if (time.Minutes > 59)
                {
                    time = time with { Minutes = 0, Hours = unchecked((ushort)(time.Hours + 1)) };
                    if (time.Hours > MaximumHours) return SetToMax();
                }
            }
        }
        return counter with { Time = time };
    }

    public static bool IsCanonicalSaveTime(SourcePlayTime time) =>
        time.Hours <= MaximumHours && time.Minutes < 60 && time.Seconds < 60 && time.VBlanks < 60;

    /// <summary>src/post_battle_event_funcs.c EnterHallOfFame. Frames are omitted.
    /// This is a packed u32, not a count of seconds or a 24-bit ordinary statistic.</summary>
    public static uint PackHallOfFameTime(SourcePlayTime time) =>
        ((uint)time.Hours << 16) | ((uint)time.Minutes << 8) | time.Seconds;

    /// <summary>The source tests the stored statistic for zero, not the game-clear
    /// flag. A first entry before one second leaves zero and can be written later.
    /// Unknown historical play time must not be passed as zero by a converter.</summary>
    public static uint RecordFirstHallOfFame(uint stored, SourcePlayTime time) =>
        stored == 0 ? PackHallOfFameTime(time) : stored;
}
