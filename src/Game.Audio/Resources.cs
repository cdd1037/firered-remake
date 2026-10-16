using System.Buffers.Binary;
namespace FireRedAudio;
// Immutable bounded image: source addresses are never CLR pointers.
public sealed class Resources
{
    public const uint Base = 0x08489968, End = 0x086fbea4;
    readonly byte[] bytes;
    public Resources(byte[] audioData)
    {
        if (audioData.Length != End - Base)
            throw new InvalidDataException("Incorrect audio resource length");
        bytes = (byte[])audioData.Clone();
    }
    public static Resources FromPack(string path)
    {
        using var file = File.OpenRead(path);
        if (file.Length != End - Base)
            throw new InvalidDataException("Incorrect audio resource length");
        var data = new byte[checked((int)file.Length)];
        file.ReadExactly(data);
        return new Resources(data);
    }
    public int Offset(uint address, int count)
    {
        if (count < 0 || address < Base || address > End || (ulong)address + (uint)count > End)
            throw new InvalidDataException($"Audio address out of bounds: 0x{address:X8} + {count}");
        return checked((int)(address - Base));
    }
    public byte U8(uint p) => bytes[Offset(p, 1)];
    public sbyte I8(uint p) => unchecked((sbyte)U8(p));
    public ushort U16(uint p) => BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(Offset(p, 2), 2));
    public uint U32(uint p) => BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(Offset(p, 4), 4));
    public uint Pointer(uint p)
    {
        var value = U32(p);
        Offset(value, 1);
        return value;
    }
}
public sealed record Song(uint Address, byte Priority, uint Voices, uint[] Tracks)
{
    public static Song Read(Resources r, uint p)
    {
        int n = r.U8(p);
        if (n is < 1 or > 16)
            throw new InvalidDataException("Invalid track count");
        if (r.U8(p + 1) != 0)
            throw new NotSupportedException("Blocks not supported");
        var tracks = new uint[n];
        for (int i = 0; i < n; i++)
            tracks[i] = r.Pointer(p + 8 + (uint)i * 4);
        return new(p, r.U8(p + 2), r.Pointer(p + 4), tracks);
    }
}
