using System.Buffers.Binary;
namespace FireRedAudio;

public readonly record struct NormalCryEntry(int Species, int CryIndex, int SampleCount, int PayloadBytes, bool GuardStored)
{
    public uint ToneAddress => 0x0848c914 + (uint)CryIndex * 12;
    public uint ReverseToneAddress => 0x0848db44 + (uint)CryIndex * 12;
}

// Cry metadata uses a versioned header followed by fixed-size sample records.
public sealed class NormalCryCatalog
{
    readonly Dictionary<int, NormalCryEntry> entries = new();
    public int Count => entries.Count;
    NormalCryCatalog() { }

    public static NormalCryCatalog FromFile(string path) => FromBytes(File.ReadAllBytes(path));

    public static NormalCryCatalog FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length <= 12 || (bytes.Length - 12) % 16 != 0 ||
            !bytes[..8].SequenceEqual("FRCN0001"u8) ||
            BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]) != (bytes.Length - 12) / 16)
            throw new InvalidDataException("Invalid cry catalog header");
        var result = new NormalCryCatalog();
        for (int offset = 12; offset < bytes.Length; offset += 16)
        {
            var row = bytes.Slice(offset, 16);
            int species = BinaryPrimitives.ReadUInt16LittleEndian(row);
            int index = BinaryPrimitives.ReadUInt16LittleEndian(row[2..]);
            uint sampleCount = BinaryPrimitives.ReadUInt32LittleEndian(row[4..]);
            uint payloadBytes = BinaryPrimitives.ReadUInt32LittleEndian(row[8..]);
            if (sampleCount is < 1 or > 1_000_000 || payloadBytes > Resources.End - Resources.Base)
                throw new InvalidDataException("Invalid cry catalog sample bounds");
            int count = (int)sampleCount;
            int payload = (int)payloadBytes;
            bool guard = row[12] == 1;
            if (!(species is >= 1 and <= 251 or >= 277 and <= 411) || index >= 388 || index is 265 or 268 ||
                row[12] > 1 || payload < DpcmDecoder.RequiredBytes(guard ? count + 1 : count))
                throw new InvalidDataException("Invalid cry catalog row");
            if (!result.entries.TryAdd(species, new(species, index, count, payload, guard)))
                throw new InvalidDataException("Duplicate cry catalog species");
        }
        return result;
    }

    public NormalCryEntry Get(int species) => entries.TryGetValue(species, out var entry) ? entry :
        throw new NotSupportedException("Species is outside the fixed ordinary-cry catalog");
}
