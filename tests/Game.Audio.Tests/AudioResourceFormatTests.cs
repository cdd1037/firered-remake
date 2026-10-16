using System.Buffers.Binary;
using FireRedAudio;

namespace FireRed.Tests.Audio;

[TestClass, TestCategory("Audio")]
public sealed class AudioResourceFormatTests
{
    private static byte[] CatalogBytes => File.ReadAllBytes(Path.Combine(TestAssets.Root, "content/generated/audio/synth/normal-cries.bin"));

    [TestMethod]
    public void CatalogAcceptsValidSubsetInsteadOfRequiringHistoricalFileIdentity()
    {
        byte[] bytes = CatalogBytes[..28];
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), 1);
        var catalog = NormalCryCatalog.FromBytes(bytes);
        Assert.AreEqual(1, catalog.Count);
        Assert.AreEqual(1, catalog.Get(1).Species);
    }

    [TestMethod]
    public void ReservedBytesDoNotChangeCatalogMeaning()
    {
        byte[] bytes = CatalogBytes;
        var original = NormalCryCatalog.FromBytes(bytes);
        bytes[^1] = 127;
        var edited = NormalCryCatalog.FromBytes(bytes);
        Assert.AreEqual(original.Count, edited.Count);
        Assert.AreEqual(original.Get(411), edited.Get(411));
    }

    [TestMethod]
    public void CatalogRecordCountMustMatchFileLength()
    {
        byte[] bytes = CatalogBytes;
        BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(8), uint.MaxValue);
        Assert.Throws<InvalidDataException>(() => NormalCryCatalog.FromBytes(bytes));
        Assert.Throws<InvalidDataException>(() => NormalCryCatalog.FromBytes("FRCN0001\0\0\0\0"u8));
    }

    [TestMethod]
    [DataRow(12, 0U, 2)] // Invalid species.
    [DataRow(14, 388U, 2)] // Cry table index beyond the supported table.
    [DataRow(14, 265U, 2)] // Unused cry table slot.
    [DataRow(16, 0U, 4)] // Empty sample.
    [DataRow(16, uint.MaxValue, 4)]
    [DataRow(20, 0U, 4)] // Truncated compressed payload.
    [DataRow(20, uint.MaxValue, 4)]
    [DataRow(24, 2U, 1)] // Guard flag must be boolean.
    public void InvalidCatalogFieldsAreRejected(int offset, uint value, int width)
    {
        byte[] bytes = CatalogBytes;
        if (width == 4) BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        else if (width == 2) BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(offset), (ushort)value);
        else bytes[offset] = (byte)value;
        Assert.Throws<InvalidDataException>(() => NormalCryCatalog.FromBytes(bytes));
    }

    [TestMethod]
    public void DuplicateCatalogSpeciesIsRejected()
    {
        byte[] bytes = CatalogBytes;
        bytes.AsSpan(12, 2).CopyTo(bytes.AsSpan(28, 2));
        Assert.Throws<InvalidDataException>(() => NormalCryCatalog.FromBytes(bytes));
    }

    [TestMethod]
    public void PackReaderChecksLayoutInsteadOfHistoricalContentFingerprint()
    {
        string path = Path.GetTempFileName();
        try
        {
            byte[] pack = File.ReadAllBytes(Path.Combine(TestAssets.Root, "content/generated/audio/synth/firered.pack"));
            pack[0] ^= 1;
            File.WriteAllBytes(path, pack);
            Assert.AreEqual(pack[0], Resources.FromPack(path).U8(Resources.Base));
            File.WriteAllBytes(path, pack[..^1]);
            Assert.Throws<InvalidDataException>(() => Resources.FromPack(path));
        }
        finally { File.Delete(path); }
    }
}
