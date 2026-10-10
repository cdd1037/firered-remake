namespace FireRedAudio;

// Named experimental profiles prevent improvements from silently changing the frozen MVP.
public enum AudioProfile
{
    ModernDryV1,
    SourceGain,
    SourceLinear,
    SourcePitch,
    SourceEnvelope,
    SourceReverb,
    SourceBandlimited,
    SourceFiltered
}

// Source delay geometry at host rate, intentionally omitting signed8 DMA wrap/rounding.
// This is an explicit modern approximation, not a transplanted hardware mixer.
public sealed class SourceDelay
{
    readonly double[] left = new double[6000];
    readonly double[] right = new double[6000];
    int cursor;
    double energy;
    public bool IsSilent => energy < 1e-7;
    const double FrameSamples = Synth.Rate * (double)Synth.FrameCycles / Synth.Cpu;

    double Read(double[] ring, double delay)
    {
        int integral = (int)delay;
        double fraction = delay - integral;
        int newest = (cursor - integral + ring.Length) % ring.Length;
        int older = (newest - 1 + ring.Length) % ring.Length;
        return ring[newest] * (1 - fraction) + ring[older] * fraction;
    }

    public (double Left, double Right) Process(double inputLeft, double inputRight, int amount = 50)
    {
        double tail = (Read(left, 6 * FrameSamples) + Read(right, 6 * FrameSamples)
            + Read(left, 7 * FrameSamples) + Read(right, 7 * FrameSamples)) * (amount / 512.0);
        double l = inputLeft + tail, r = inputRight + tail;
        energy = Math.Max(0, energy - Math.Abs(left[cursor]) - Math.Abs(right[cursor]) + Math.Abs(l) + Math.Abs(r));
        left[cursor] = l;
        right[cursor] = r;
        cursor = (cursor + 1) % left.Length;
        return (l, r);
    }
}

// Modern DC blocker. The approximately 15 Hz corner is intentionally below musical fundamentals.
public sealed class DcBlocker
{
    const double Pole = 511.0 / 512;
    double previousLeft, previousRight, outputLeft, outputRight;
    public bool IsSilent => Math.Abs(previousLeft) + Math.Abs(previousRight) + Math.Abs(outputLeft) + Math.Abs(outputRight) < 1e-7;

    public (double Left, double Right) Process(double left, double right)
    {
        outputLeft = left - previousLeft + Pole * outputLeft;
        outputRight = right - previousRight + Pole * outputRight;
        previousLeft = left;
        previousRight = right;
        return (outputLeft, outputRight);
    }
}
