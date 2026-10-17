using System.Buffers.Binary;
using FireRedAudio;

namespace FireRed.Tests.Audio;

[TestClass, TestCategory("Audio")]
public sealed class HighPitchCryTests
{
    private static Resources Resources => TestAssets.Cached(() => FireRedAudio.Resources.FromPack(Path.Combine(TestAssets.Root, "content/generated/audio/synth/firered.pack")));
    private static NormalCryCatalog Catalog => TestAssets.Cached(() => NormalCryCatalog.FromFile(Path.Combine(TestAssets.Root, "content/generated/audio/synth/normal-cries.bin")));

    [TestMethod]
    public void HighPitchPresetMatchesPinnedSourceSetterFields()
    {
        var (song, sequence) = SourceAnimationCryPreset.Build(Resources, Catalog, 4, SourceHighPitchCry.Mode, -64);
        Assert.AreEqual(2, song.Tracks.Length);
        Assert.AreEqual(Catalog.Get(4).ToneAddress, song.Voices);
        Assert.AreEqual(10, song.Priority);
        Assert.AreEqual(2, sequence[0]);
        Assert.AreEqual(28, sequence[0x12]);
        Assert.AreEqual(48, sequence[0x19]);
        Assert.AreEqual(90, sequence[0x1d]);
        Assert.AreEqual(200, sequence[0x26]);
        Assert.AreEqual(0, sequence[0x28]);
        Assert.AreEqual(62, sequence[0x2a]);
        Assert.AreEqual(50, BinaryPrimitives.ReadUInt16LittleEndian(sequence.AsSpan(0x2e)));
        Assert.AreEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(sequence.AsSpan(0x20)));
        Assert.AreEqual((byte)0x20, Resources.U8(song.Voices));
    }

    [TestMethod]
    [DataRow(-128, 64), DataRow(-64, 0), DataRow(0, 64), DataRow(63, 127), DataRow(127, 63)]
    public void HighPitchUsesSourceSignedPanWrapping(int pan, int encoded)
    {
        Assert.AreEqual(encoded, SourceAnimationCryPreset.Build(Resources, Catalog, 33, SourceHighPitchCry.Mode, pan).Sequence[0x28]);
    }

    [TestMethod]
    [DataRow(4), DataRow(16), DataRow(143), DataRow(161), DataRow(410)]
    public void HighPitchRendersSourceVoicesAndEnds(int species)
    {
        var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog);
        synth.StartAnimationCry(species, SourceHighPitchCry.Mode, -64);
        var output = new float[4 * Synth.Rate * 2];
        synth.Render(output.AsSpan(0, 2048));
        var voices = synth.Voices.Where(v => v.Active && v.Player?.Id == 2).OrderBy(v => v.Track!.Index).ToArray();
        Assert.AreEqual(2, voices.Length);
        Assert.AreEqual(62, voices[0].MidiKey);
        Assert.AreEqual(-36, voices[0].Track!.Tune);
        Assert.AreEqual(-16, voices[1].Track!.Tune);
        Assert.IsTrue(voices.All(v => v.Track!.Pan == -64 && v.Track.ReleaseOverride == 200));
        synth.Render(output.AsSpan(2048));
        Assert.IsTrue(output.Any(v => v != 0));
        Assert.IsTrue(output.All(float.IsFinite));
        Assert.AreEqual(0L, synth.Clipped);
        Assert.AreEqual(2, synth.MaxPcm);
        Assert.IsFalse(synth.CryPlaying);
        Assert.IsFalse(synth.BgmDucked);
        Assert.IsTrue(synth.Ended);
        Assert.AreEqual(2, synth.Trace.Count(t => t.Contains("tick=50 cry-sequence-fine", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void HighPitchSharesOrdinaryDecodedCacheAndIsPartitionInvariant()
    {
        float[] Render(int block)
        {
            var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog) { EnableTrace = false };
            synth.PrepareNormalCry(33);
            int count = synth.PreparedCryCount, bytes = synth.PreparedCryBytes;
            synth.StartAnimationCry(33, SourceHighPitchCry.Mode, 63, duckBgm: false);
            Assert.AreEqual(count, synth.PreparedCryCount);
            Assert.AreEqual(bytes, synth.PreparedCryBytes);
            var result = new float[3 * Synth.Rate * 2];
            for (int frame = 0; frame < result.Length / 2;)
            {
                int frames = Math.Min(block, result.Length / 2 - frame);
                synth.Render(result.AsSpan(frame * 2, frames * 2));
                frame += frames;
            }
            Assert.IsFalse(synth.BgmDucked);
            return result;
        }
        CollectionAssert.AreEqual(Render(int.MaxValue), Render(257));
    }

    [TestMethod]
    public void InvalidHighPitchRequestsDoNotReplaceCurrentCry()
    {
        var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog);
        synth.StartNormalCry(4);
        synth.Render(new float[2048]);
        var current = synth.Voices.Single(v => v.Active && v.Player?.Id == 2).Player;
        Assert.Throws<NotSupportedException>(() => synth.StartAnimationCry(0, SourceHighPitchCry.Mode));
        Assert.Throws<NotSupportedException>(() => synth.StartAnimationCry(252, SourceHighPitchCry.Mode));
        Assert.Throws<ArgumentOutOfRangeException>(() => synth.StartAnimationCry(4, SourceHighPitchCry.Mode, -129));
        Assert.Throws<ArgumentOutOfRangeException>(() => synth.StartAnimationCry(4, SourceHighPitchCry.Mode, 128));
        Assert.Throws<NotSupportedException>(() => new Synth(Resources).StartAnimationCry(4, SourceHighPitchCry.Mode));
        Assert.Throws<NotSupportedException>(() => synth.StartNormalCry(4, SourceHighPitchCry.Mode));
        Assert.AreSame(current, synth.Voices.Single(v => v.Active && v.Player?.Id == 2).Player);
    }
}
