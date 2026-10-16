namespace FireRedAudio;

// Selected original wave/noise voices rendered by host oscillators, not an APU.
// Parameter/data provenance and documented timing limits: PHASE5-PSG-WAVE-NOISE.md.
public sealed partial class Synth
{
    internal const uint FirstWaveAddress = 0x084a31ec;
    internal const int WaveAssetCount = 11;
    readonly Dictionary<uint, byte[]> waveSamples = new();
    internal static readonly byte[] NoisePitchTable =
    {
        0xd7, 0xd6, 0xd5, 0xd4, 0xc7, 0xc6, 0xc5, 0xc4,
        0xb7, 0xb6, 0xb5, 0xb4, 0xa7, 0xa6, 0xa5, 0xa4,
        0x97, 0x96, 0x95, 0x94, 0x87, 0x86, 0x85, 0x84,
        0x77, 0x76, 0x75, 0x74, 0x67, 0x66, 0x65, 0x64,
        0x57, 0x56, 0x55, 0x54, 0x47, 0x46, 0x45, 0x44,
        0x37, 0x36, 0x35, 0x34, 0x27, 0x26, 0x25, 0x24,
        0x17, 0x16, 0x15, 0x14, 0x07, 0x06, 0x05, 0x04,
        0x03, 0x02, 0x01, 0x00
    };
    internal static readonly byte[] WaveVolumeTable =
    {
        0, 0, 0x60, 0x60, 0x60, 0x60, 0x40, 0x40,
        0x40, 0x40, 0x80, 0x80, 0x80, 0x80, 0x20, 0x20
    };

    byte[] PrepareWave(uint address)
    {
        // A fixed-source whitelist provides physical16-byte extents, not a
        // guessed PCM header or an arbitrary span to the next ROM symbol.
        Resources.Offset(address, 16);
        if (address < FirstWaveAddress || address >= FirstWaveAddress + WaveAssetCount * 16 ||
            (address - FirstWaveAddress) % 16 != 0)
            throw new InvalidDataException("Unsupported fixed programmable-wave address");
        if (waveSamples.TryGetValue(address, out var cached)) return cached;
        var samples = new byte[32];
        for (int n = 0; n < 16; n++)
        {
            byte packed = Resources.U8(address + (uint)n);
            samples[n * 2] = (byte)(packed >> 4);
            samples[n * 2 + 1] = (byte)(packed & 15);
        }
        waveSamples.Add(address, samples);
        return samples;
    }

    internal static double WaveGain(int envelope) => WaveVolumeTable[Math.Clamp(envelope, 0, 15)] switch
    {
        0x60 => 0.25,
        0x40 => 0.5,
        0x80 => 0.75,
        0x20 => 1,
        _ => 0
    };

    internal static double WaveClock(int key, Track track, Instrument instrument) =>
        2097152.0 / (2048 - SquareRegister(key, track, instrument));

    internal static int NoiseRegister(int key, Track track, int width)
    {
        // The original noise path ignores the fractional fine-adjust byte.
        int fixedKey = key * 256 + (track.Tune + track.Bend * track.BendRange) * 4 + (int)(track.PitchMod * 256);
        int index = Math.Clamp((fixedKey >> 8) - 21, 0, NoisePitchTable.Length - 1);
        return NoisePitchTable[index] | (width << 3);
    }

    internal static double NoiseClock(int register)
    {
        int shift = (register >> 4) & 15;
        if (shift >= 14) return 0;
        int divider = register & 7;
        return 262144.0 / (divider == 0 ? 0.5 : divider) / (1 << shift);
    }

    internal static int StepNoise(int state, bool shortMode, out int output)
    {
        // GBATEK's documented GBA Galois recurrence and deterministic seeds.
        output = state & 1;
        state >>= 1;
        if (output != 0) state ^= shortMode ? 0x60 : 0x6000;
        return state;
    }

    internal static double RenderWave(Voice voice, double step)
    {
        var samples = voice.WaveSamples!;
        double position = voice.Position;
        if (step == 0) return samples[(int)position] / 7.5 - 1;
        double remaining = step, area = 0;
        // Area-average original piecewise-constant nibble values over one host
        // sample. This resampling policy is modern; no waveform is substituted.
        while (remaining > 0)
        {
            int index = (int)position;
            double span = Math.Min(remaining, 1 - (position - index));
            area += (samples[index] / 7.5 - 1) * span;
            remaining -= span;
            position += span;
            if (position >= 32) position -= 32;
        }
        voice.Position = position;
        return area / step;
    }

    internal static double RenderNoise(Voice voice, double step)
    {
        if (step == 0) return voice.NoiseOutput * 2 - 1;
        double remaining = step, area = 0;
        bool shortMode = voice.Instrument!.Data == 1;
        while (remaining > 0)
        {
            double span = Math.Min(remaining, 1 - voice.Phase);
            area += (voice.NoiseOutput * 2 - 1) * span;
            remaining -= span;
            voice.Phase += span;
            if (voice.Phase >= 1)
            {
                voice.Phase = 0;
                voice.NoiseState = StepNoise(voice.NoiseState, shortMode, out voice.NoiseOutput);
            }
        }
        return area / step;
    }
}
