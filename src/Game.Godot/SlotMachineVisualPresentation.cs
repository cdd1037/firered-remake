using System.Text.Json;

/// <summary>Read-only original slot task palette/animation projection; no game or wallet state.</summary>
public static class SlotMachineVisualPresentation
{
    public readonly record struct FlashState(int? LightFrame, int LineDarken);
    public readonly record struct FadeState(int Background, int Objects);

    // BlendPalette's signed shift rounds down. Hardware BLDY instead subtracts a rounded-down
    // reduction; these must not be merged, particularly when both effects are active.
    public static byte FadeChannel(byte channel, int coefficient)
    {
        int value = ((channel >> 3) * (16 - coefficient)) >> 4;
        return (byte)((value << 3) | (value >> 2));
    }

    public static FlashState Flash(JsonElement layout, long ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);
        if (ticks == 0) return new(null, 0); // Task state 0 only binds palette 6 to winning tiles.
        var flash = layout.GetProperty("winningFlash");
        var lights = flash.GetProperty("lightFrames");
        var shades = flash.GetProperty("darkenCoefficients");
        return new(lights[(int)((ticks - 1) / flash.GetProperty("lightHoldTicks").GetInt32() % lights.GetArrayLength())].GetInt32(),
            shades[(int)((ticks - 1) % shades.GetArrayLength())].GetInt32());
    }

    /// <summary>UpdateNormalPaletteFade advances Y after OBJ; Y itself can describe the next transfer.</summary>
    public static FadeState Fade(int y, int target, bool objectHalf, int finish)
    {
        if (y is < 0 or > 16 || target is not (0 or 16)) throw new ArgumentOutOfRangeException(nameof(y));
        if (finish >= 0) return new(target, target);
        int previous = target == 0 ? y == 0 ? 1 : Math.Min(16, y + 3)
            : y == 16 ? 15 : Math.Max(0, y - 3);
        return new(objectHalf ? y : previous, previous);
    }

    public static int AnimationTile(JsonElement animation, long ticks)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(ticks);
        var frames = animation.GetProperty("frames");
        int duration = 0;
        foreach (var frame in frames.EnumerateArray()) duration += frame.GetProperty("durationTicks").GetInt32();
        long time = animation.GetProperty("loop").GetBoolean() ? ticks % duration : Math.Min(ticks, duration - 1);
        foreach (var frame in frames.EnumerateArray())
        {
            int length = frame.GetProperty("durationTicks").GetInt32();
            if (time < length) return frame.GetProperty("tileOffset").GetInt32();
            time -= length;
        }
        throw new InvalidDataException("Slot animation has no frame.");
    }
}
