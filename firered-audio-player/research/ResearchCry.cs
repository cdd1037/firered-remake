using System.Buffers.Binary;
namespace FireRedAudio;

// Compiled only into the offline coverage executable, never the runtime library.
public sealed partial class Synth
{
    readonly Dictionary<uint, sbyte[]> researchSamples = new();

    public void StartResearchCry(uint toneAddress, int payloadBytes, bool guardStored)
    {
        var tone = ReadInstrument(toneAddress);
        if (tone.Type != 0x20 || tone.Root != 60 || tone.Attack != 255 ||
            tone.Decay != 0 || tone.Sustain != 255 || tone.Release != 0)
            throw new NotSupportedException("Research accepts only the audited normal cry tone format");
        uint wave = tone.Data;
        int count = checked((int)Resources.U32(wave + 12));
        if (Resources.U16(wave) != 1 || Resources.U16(wave + 2) != 0 ||
            Resources.U32(wave + 8) != 0 || Resources.U32(wave + 4) == 0)
            throw new NotSupportedException("Research accepts only nonlooping DPCM");
        var samples = DpcmDecoder.Decode(Resources, wave + 16,
            guardStored ? checked(count + 1) : count, payloadBytes);
        if (!guardStored)
        {
            // Fixed-source inventory verifies the omitted high nibble is delta zero.
            // Never read alignment padding or the next asset to obtain this guard.
            Array.Resize(ref samples, checked(count + 1));
            samples[count] = samples[count - 1];
        }
        var (_, sequence) = NormalCharmanderCry.Build(Resources, 4, 0);
        BinaryPrimitives.WriteUInt32LittleEndian(sequence.AsSpan(4), toneAddress);
        var song = new Song(NormalCharmanderCry.SequenceBase, 10, toneAddress,
            new[] { NormalCharmanderCry.SequenceBase + 0x11 });
        researchSamples.Add(wave, samples);
        players[2] = new Player(Resources, this, song, 2, sequence);
    }
}
