using System.Buffers.Binary;
namespace FireRedAudio;

// This phase intentionally accepts one species/mode/parameter set, not an arbitrary cry API.
public static class NormalCharmanderCry
{
    public const uint ToneAddress = 0x0848c938;
    public const uint WaveAddress = 0x08513038;
    public const uint TemplateAddress = 0x08489c20;
    public const uint SequenceBase = 0x00010000;
    public const int DecodedLength = 7133;
    public const int EncodedPayloadLength = 3679;

    public static (Song Song, byte[] Sequence) Build(Resources resources, int species, int mode)
    {
        if (species != 4 || mode != 0)
            throw new NotSupportedException("Only Charmander normal cry is implemented");
        return BuildForTone(resources, ToneAddress);
    }

    internal static (Song Song, byte[] Sequence) BuildForTone(Resources resources, uint toneAddress)
    {
        var bytes = new byte[52];
        for (int n = 0; n < bytes.Length; n++)
            bytes[n] = resources.U8(TemplateAddress + (uint)n);
        bytes[0] = 1;
        bytes[2] = 10;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(4), toneAddress);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), SequenceBase + 0x11);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x14), SequenceBase + 0x1a);
        bytes[0x12] = 64;
        bytes[0x1d] = 120;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x20), 0);
        bytes[0x26] = 0;
        bytes[0x28] = 64;
        bytes[0x2a] = 60;
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x2e), 140);
        return (new Song(SequenceBase, 10, toneAddress, new[] { SequenceBase + 0x11 }), bytes);
    }
}

// pret sound.c CRY_MODE_DOUBLES shortens the ordinary cry without changing pitch,
// chorus or volume. Pan is the signed source argument to SetPokemonCryPanpot.
public static class SourceDoublesCry
{
    public const int Mode = 1;
    public const int Length = 20, Release = 225, Pitch = 15360, Chorus = 0, Volume = 120;

    public static (Song Song, byte[] Sequence) Build(Resources resources, NormalCryCatalog catalog, int species, int pan = 0)
    {
        if (pan is < -128 or > 127) throw new ArgumentOutOfRangeException(nameof(pan));
        var (song, bytes) = NormalCharmanderCry.BuildForTone(resources, catalog.Get(species).ToneAddress);
        bytes[0x26] = Release;
        bytes[0x28] = (byte)((pan + 64) & 0x7f);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x2e), Length);
        return (song, bytes);
    }
}

// pret sound.c CRY_MODE_ENCOUNTER uses the same fixed parameters for each
// species with a loaded canonical cry. Normal mode stays intact.
public static class SourceEncounterCry
{
    public const int Mode = 2;
    public const int Length = 140, Release = 225, Pitch = 15600, Chorus = 20, Volume = 90;

    public static (Song Song, byte[] Sequence) Build(Resources resources, NormalCryCatalog catalog, int species) =>
        SourceCrySequence.Build(resources, catalog.Get(species).ToneAddress, Length, Release, Pitch, Chorus, Volume, 0);
}

// pret sound.c CRY_MODE_HIGH_PITCH, used by Howl.
public static class SourceHighPitchCry
{
    public const int Mode = 3;
    public const int Length = 50, Release = 200, Pitch = 15800, Chorus = 20, Volume = 90;


}

// Source modes used by Howl, Growl, Roar and Hyper Voice.
// Reverse modes select the original alternate tone table, not another sample.
public static class SourceAnimationCryPreset
{
    public const int EchoStart = 4, EchoEnd = 6, Roar1 = 7, Roar2 = 8, Growl1 = 9, Growl2 = 10;
    public static bool IsSupported(int mode) => mode is SourceHighPitchCry.Mode or EchoStart or EchoEnd or Roar1 or Roar2 or Growl1 or Growl2;
    public static bool UsesReverse(int mode)
    {
        if (!IsSupported(mode)) throw new NotSupportedException("Unsupported animation cry mode");
        return mode is EchoStart or Growl1;
    }
    public static (Song Song, byte[] Sequence) Build(Resources resources, NormalCryCatalog catalog, int species, int mode, int pan = 0)
    {
        if (pan is < -128 or > 127) throw new ArgumentOutOfRangeException(nameof(pan));
        bool reverse = UsesReverse(mode);
        var entry = catalog.Get(species);
        var parameters = mode switch
        {
            SourceHighPitchCry.Mode => (Length: SourceHighPitchCry.Length, Release: SourceHighPitchCry.Release,
                Pitch: SourceHighPitchCry.Pitch, Chorus: SourceHighPitchCry.Chorus, Volume: SourceHighPitchCry.Volume),
            EchoStart => (Length: 25, Release: 100, Pitch: 15600, Chorus: 192, Volume: 90),
            EchoEnd => (Length: 140, Release: 220, Pitch: 15555, Chorus: 192, Volume: 90),
            Roar1 => (Length: 10, Release: 100, Pitch: 14848, Chorus: 0, Volume: 120),
            Roar2 => (Length: 60, Release: 225, Pitch: 15616, Chorus: 0, Volume: 120),
            Growl1 => (Length: 15, Release: 125, Pitch: 15200, Chorus: 0, Volume: 120),
            Growl2 => (Length: 100, Release: 225, Pitch: 15200, Chorus: 0, Volume: 120),
            _ => throw new NotSupportedException("Unsupported animation cry mode")
        };
        return SourceCrySequence.Build(resources, reverse ? entry.ReverseToneAddress : entry.ToneAddress,
            parameters.Length, parameters.Release, parameters.Pitch, parameters.Chorus, parameters.Volume, pan);
    }
}

// Shared m4a SetPokemonCry* field encoding for verified source presets.
internal static class SourceCrySequence
{
    internal static (Song Song, byte[] Sequence) Build(Resources resources, uint tone,
        int length, int release, int pitch, int chorus, int volume, int pan)
    {
        var (_, bytes) = NormalCharmanderCry.BuildForTone(resources, tone);
        int adjustedPitch = pitch + 0x80;
        int previousChorus = bytes[0x19] - bytes[0x12];
        bytes[0] = (byte)(chorus == 0 ? 1 : 2);
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(0x0c), NormalCharmanderCry.SequenceBase + 0x18);
        bytes[0x12] = (byte)((adjustedPitch >> 1) & 0x7f);
        bytes[0x19] = (byte)((bytes[0x12] + (chorus == 0 ? previousChorus : chorus)) & 0x7f);
        bytes[0x1d] = (byte)(volume & 0x7f);
        bytes[0x26] = (byte)release;
        bytes[0x28] = (byte)((pan + 64) & 0x7f);
        bytes[0x2a] = (byte)((adjustedPitch >> 8) & 0x7f);
        BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(0x2e), (ushort)length);
        uint[] tracks = chorus == 0 ? new[] { NormalCharmanderCry.SequenceBase + 0x11 } :
            new[] { NormalCharmanderCry.SequenceBase + 0x11, NormalCharmanderCry.SequenceBase + 0x18 };
        return (new Song(NormalCharmanderCry.SequenceBase, 10, tone, tracks), bytes);
    }
}

// Keep the historical public entrypoint and all parameter constants intact.
public static class EncounterSnorlaxCry
{
    public const int Species = 143, Mode = SourceEncounterCry.Mode;
    public const int Length = SourceEncounterCry.Length, Release = SourceEncounterCry.Release,
        Pitch = SourceEncounterCry.Pitch, Chorus = SourceEncounterCry.Chorus, Volume = SourceEncounterCry.Volume;
    public static (Song Song, byte[] Sequence) Build(Resources resources, NormalCryCatalog catalog) => SourceEncounterCry.Build(resources, catalog, Species);
}

public static class DpcmDecoder
{
    static readonly int[] Delta = { 0, 1, 4, 9, 16, 25, 36, 49, -64, -49, -36, -25, -16, -9, -4, -1 };

    public static int RequiredBytes(int samples)
    {
        if (samples is < 1 or > 1_000_000)
            throw new InvalidDataException("DPCM sample budget exceeded");
        int full = samples / 64, tail = samples % 64;
        return checked(full * 33 + (tail == 0 ? 0 : tail == 1 ? 1 : 2 + (tail - 1) / 2));
    }

    // Block-local decoding from original compressed data; this is not a rendered recording.
    public static sbyte[] Decode(Resources resources, uint data, int samples, int payloadBytes)
    {
        int needed = RequiredBytes(samples);
        if (payloadBytes < needed)
            throw new InvalidDataException("Truncated DPCM payload");
        resources.Offset(data, payloadBytes);
        var decoded = new sbyte[samples];
        for (int block = 0, output = 0; output < samples; block++)
        {
            uint start = data + (uint)(block * 33);
            int count = Math.Min(64, samples - output);
            sbyte value = resources.I8(start);
            decoded[output++] = value;
            for (int index = 1; index < count; index++)
            {
                byte packed = resources.U8(start + (uint)(index == 1 ? 1 : index / 2 + 1));
                int nibble = index == 1 || (index & 1) != 0 ? packed & 15 : packed >> 4;
                value = unchecked((sbyte)(value + Delta[nibble]));
                decoded[output++] = value;
            }
        }
        return decoded;
    }
}

// Dynamic tracks are a separate bounded address domain; jumps cannot escape into ROM or RAM.
public sealed class SequenceImage
{
    readonly Resources resources;
    readonly byte[]? dynamicBytes;
    public SequenceImage(Resources resources, byte[]? bytes = null)
    {
        this.resources = resources;
        dynamicBytes = bytes == null ? null : (byte[])bytes.Clone();
    }
    int Offset(uint address, int count)
    {
        if (dynamicBytes == null || address < NormalCharmanderCry.SequenceBase ||
            (ulong)address + (uint)count > NormalCharmanderCry.SequenceBase + (ulong)dynamicBytes.Length)
            throw new InvalidDataException("Dynamic sequence address out of bounds");
        return checked((int)(address - NormalCharmanderCry.SequenceBase));
    }
    public byte U8(uint address) => dynamicBytes == null ? resources.U8(address) : dynamicBytes[Offset(address, 1)];
    public ushort U16(uint address) => (ushort)(U8(address) | U8(address + 1) << 8);
    public uint U32(uint address) => (uint)(U8(address) | U8(address + 1) << 8 | U8(address + 2) << 16 | U8(address + 3) << 24);
    public uint Pointer(uint address)
    {
        uint target = U32(address);
        if (dynamicBytes == null)
            resources.Offset(target, 1);
        else
            Offset(target, 1);
        return target;
    }
}
