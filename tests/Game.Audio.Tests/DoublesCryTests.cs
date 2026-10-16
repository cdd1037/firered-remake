using System.Buffers.Binary;
using FireRedAudio;

namespace FireRed.Tests.Audio;

[TestClass, TestCategory("Audio")]
public sealed class DoublesCryTests
{
    private static Resources Resources => TestAssets.Cached(() => FireRedAudio.Resources.FromPack(Path.Combine(TestAssets.Root, "content/generated/audio/synth/firered.pack")));
    private static NormalCryCatalog Catalog => TestAssets.Cached(() => NormalCryCatalog.FromFile(Path.Combine(TestAssets.Root, "content/generated/audio/synth/normal-cries.bin")));

    [TestMethod]
    public void DoublesPresetChangesOnlySourceLengthReleaseAndPan()
    {
        var (normalSong, normal) = NormalCharmanderCry.Build(Resources, 4, 0);
        var (song, sequence) = SourceDoublesCry.Build(Resources, Catalog, 4, pan: 63);
        Assert.AreEqual(normalSong.Address, song.Address);
        Assert.AreEqual(normalSong.Priority, song.Priority);
        Assert.AreEqual(normalSong.Voices, song.Voices);
        CollectionAssert.AreEqual(normalSong.Tracks, song.Tracks);
        Assert.AreEqual(1, sequence[0]);
        Assert.AreEqual(225, sequence[0x26]);
        Assert.AreEqual(127, sequence[0x28]);
        Assert.AreEqual(20, BinaryPrimitives.ReadUInt16LittleEndian(sequence.AsSpan(0x2e)));
        for (int i = 0; i < normal.Length; i++)
            if (i is not (0x26 or 0x28 or 0x2e or 0x2f)) Assert.AreEqual(normal[i], sequence[i], $"Cry byte {i:x}");
        Assert.AreEqual(64, SourceDoublesCry.Build(Resources, Catalog, 4).Sequence[0x28]);
    }

    [TestMethod]
    [DataRow(-128, 64), DataRow(-64, 0), DataRow(0, 64), DataRow(63, 127), DataRow(127, 63)]
    public void PanUsesSourceSignedByteWrapping(int pan, int expected)
    {
        Assert.AreEqual(expected, SourceDoublesCry.Build(Resources, Catalog, 33, pan).Sequence[0x28]);
    }

    [TestMethod]
    [DataRow(4, 0), DataRow(33, 63), DataRow(143, -64), DataRow(410, 0)]
    public void CatalogDoublesCriesRenderWithOneVoiceAndFinishAtTick20(int species, int pan)
    {
        var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog);
        synth.StartDoublesCry(species, pan);
        var samples = new float[3 * Synth.Rate * 2];
        synth.Render(samples.AsSpan(0, 2048));
        var voice = synth.Voices.Single(v => v.Active && v.Player?.Id == 2);
        Assert.AreEqual(60, voice.MidiKey);
        Assert.AreEqual(0, voice.Track!.Tune);
        Assert.AreEqual(pan, voice.Track.Pan);
        Assert.AreEqual(225, voice.Track.ReleaseOverride);
        synth.Render(samples.AsSpan(2048));
        Assert.IsTrue(samples.Any(s => s != 0));
        Assert.IsTrue(samples.All(float.IsFinite));
        Assert.AreEqual(0L, synth.Clipped);
        Assert.AreEqual(1, synth.MaxPcm);
        Assert.IsFalse(synth.CryPlaying);
        Assert.IsFalse(synth.BgmDucked);
        Assert.IsTrue(synth.Ended);
        Assert.AreEqual(1, synth.Trace.Count(t => t.Contains("tick=20 cry-sequence-fine", StringComparison.Ordinal)));
        Assert.IsFalse(synth.Trace.Any(t => t.Contains("duck=on", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void DoublesCryLeavesBgmUnduckedAndIsRenderPartitionInvariant()
    {
        float[] Render(int block)
        {
            var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog) { EnableTrace = false };
            synth.Start(0x086dd844, 0);
            synth.Render(new float[2048]);
            synth.StartDoublesCry(33, pan: 63);
            Assert.IsFalse(synth.BgmDucked);
            var result = new float[2 * Synth.Rate * 2];
            for (int frame = 0; frame < result.Length / 2;)
            {
                int count = Math.Min(block, result.Length / 2 - frame);
                synth.Render(result.AsSpan(frame * 2, count * 2));
                Assert.IsFalse(synth.BgmDucked);
                Assert.IsTrue(synth.Voices.Where(v => v.Active && v.Player?.Id == 0).All(v => v.Track!.ExternalVolume == 64));
                frame += count;
            }
            Assert.IsFalse(synth.PlayerPlaying(2));
            return result;
        }
        CollectionAssert.AreEqual(Render(int.MaxValue), Render(257));
    }

    [TestMethod]
    public void InvalidDoublesRequestsPreserveExistingCryAndHistoricalModeContract()
    {
        var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog);
        synth.StartNormalCry(33);
        synth.Render(new float[2048]);
        var current = synth.Voices.Single(v => v.Active && v.Player?.Id == 2).Player;
        Assert.Throws<NotSupportedException>(() => synth.StartDoublesCry(0));
        Assert.Throws<NotSupportedException>(() => synth.StartDoublesCry(252));
        Assert.Throws<ArgumentOutOfRangeException>(() => synth.StartDoublesCry(33, pan: 128));
        Assert.Throws<ArgumentOutOfRangeException>(() => synth.StartDoublesCry(33, pan: -129));
        Assert.Throws<NotSupportedException>(() => synth.StartNormalCry(33, mode: 1));
        Assert.Throws<NotSupportedException>(() => new Synth(Resources).StartDoublesCry(33));
        Assert.AreSame(current, synth.Voices.Single(v => v.Active && v.Player?.Id == 2).Player);
        Assert.IsTrue(synth.BgmDucked);
    }

    [TestMethod]
    public void NormalAndEncounterParametersRemainIndependentOfDoublesPreset()
    {
        var (_, normal) = NormalCharmanderCry.Build(Resources, 4, 0);
        var (encounterSong, encounter) = SourceEncounterCry.Build(Resources, Catalog, 4);
        Assert.AreEqual(140, BinaryPrimitives.ReadUInt16LittleEndian(normal.AsSpan(0x2e)));
        Assert.AreEqual(0, normal[0x26]);
        Assert.AreEqual(120, normal[0x1d]);
        Assert.AreEqual(60, normal[0x2a]);
        Assert.AreEqual(64, normal[0x12]);
        Assert.AreEqual(1, normal[0]);
        Assert.AreEqual(140, BinaryPrimitives.ReadUInt16LittleEndian(encounter.AsSpan(0x2e)));
        Assert.AreEqual(225, encounter[0x26]);
        Assert.AreEqual(90, encounter[0x1d]);
        Assert.AreEqual(61, encounter[0x2a]);
        Assert.AreEqual(56, encounter[0x12]);
        Assert.AreEqual(76, encounter[0x19]);
        Assert.AreEqual(2, encounterSong.Tracks.Length);
        Assert.AreEqual(2, encounter[0]);
    }
}
