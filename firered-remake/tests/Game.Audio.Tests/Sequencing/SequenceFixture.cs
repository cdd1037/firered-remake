using System.Buffers.Binary;
using FireRed.Tests;
using FireRedAudio;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace FireRed.Tests.Audio;
[TestClass]
public abstract class SequenceFixture
{
    private protected FireRedAudio.Resources real = TestAssets.Cached(() => FireRedAudio.Resources.FromPack(Path.GetFullPath(Path.Combine(TestAssets.Root, "../firered-audio-player/resources/firered.pack"))));
    private protected static Resources Fixture(byte[] sequence, int parentType = 0, int leafType = 0, bool invalidChildAddress = false)
    {
        var bytes = new byte[Resources.End - Resources.Base];
        void Word(int offset, uint value) => BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(offset), value);
        void Tone(int offset, int type, int key, int pan)
        {
            bytes[offset] = (byte)type;
            bytes[offset + 1] = (byte)key;
            bytes[offset + 3] = (byte)pan;
            Word(offset + 4, (type & 7) == 0 ? Resources.Base + 4096 : 2);
            bytes[offset + 8] = 255;
            bytes[offset + 10] = 255;
        }

        bytes[0] = 1;
        Word(4, Resources.Base + 512);
        Word(8, Resources.Base + 256);
        sequence.CopyTo(bytes, 256);
        Tone(512, leafType, 60, 0);
        if (parentType != 0)
        {
            bytes[512] = (byte)parentType;
            Word(516, invalidChildAddress ? Resources.End - 4 : Resources.Base + 1024);
            Word(520, Resources.Base + 3072);
            for (int n = 0; n < 128; n++)
                Tone(1024 + n * 12, leafType, 64, 0xd0);
        }

        Word(4096, 0x40000000); // looped uncompressed PCM
        Word(4100, 10512 * 1024);
        Word(4104, 0);
        Word(4108, 16);
        Array.Fill(bytes, (byte)32, 4112, 16);
        return new Resources(bytes);
    }
}
