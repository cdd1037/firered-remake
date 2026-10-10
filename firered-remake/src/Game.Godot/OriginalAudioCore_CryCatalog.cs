using System.Buffers.Binary;
using System.Security.Cryptography;
namespace FireRedAudio;

public readonly record struct NormalCryEntry(int Species, int CryIndex, int SampleCount, int PayloadBytes, bool GuardStored)
{
    public uint ToneAddress => 0x0848c914 + (uint)CryIndex * 12;
}

// Small external metadata authenticated independently of the immutable original audio pack.
// Construction is restricted to the audited 386-species snapshot; no arbitrary asset admission.
public sealed class NormalCryCatalog
{
    public const int FileBytes = 6188;
    public const string Sha256 = "623fe59bb7fc0b8d3907dd67bc677a7985037e0086698636f35db82e113d7f2e";
    readonly Dictionary<int, NormalCryEntry> entries = new();
    public int Count => entries.Count;
    NormalCryCatalog() { }

    public static NormalCryCatalog FromFile(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length != FileBytes) throw new InvalidDataException("Unexpected cry catalog length");
        var bytes = new byte[FileBytes];
        file.ReadExactly(bytes);
        return FromBytes(bytes);
    }

    public static NormalCryCatalog FromBytes(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != FileBytes || Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant() != Sha256)
            throw new InvalidDataException("Unsupported cry catalog identity");
        if (!bytes[..8].SequenceEqual("FRCN0001"u8) || BinaryPrimitives.ReadUInt32LittleEndian(bytes[8..]) != 386)
            throw new InvalidDataException("Invalid cry catalog header");
        var result = new NormalCryCatalog();
        for (int offset = 12; offset < bytes.Length; offset += 16)
        {
            var row = bytes.Slice(offset, 16);
            int species = BinaryPrimitives.ReadUInt16LittleEndian(row);
            int index = BinaryPrimitives.ReadUInt16LittleEndian(row[2..]);
            int count = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(row[4..]));
            int payload = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(row[8..]));
            bool guard = row[12] == 1;
            if (!(species is >= 1 and <= 251 or >= 277 and <= 411) || index >= 388 || index is 265 or 268 ||
                row[12] > 1 || payload < DpcmDecoder.RequiredBytes(guard ? count + 1 : count))
                throw new InvalidDataException("Invalid cry catalog row");
            result.entries.Add(species, new(species, index, count, payload, guard));
        }
        return result;
    }

    public NormalCryEntry Get(int species) => entries.TryGetValue(species, out var entry) ? entry :
        throw new NotSupportedException("Species is outside the fixed ordinary-cry catalog");
}
