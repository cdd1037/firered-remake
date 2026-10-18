using System.Buffers.Binary;
using FireRedAudio;

namespace FireRed.Tests.Audio;

[TestClass, TestCategory("Audio")]
public sealed class AnimationCryTests
{
    private static Resources Resources => TestAssets.Cached(() => FireRedAudio.Resources.FromPack(Path.Combine(TestAssets.Root, "content/generated/audio/synth/firered.pack")));
    private static NormalCryCatalog Catalog => TestAssets.Cached(() => NormalCryCatalog.FromFile(Path.Combine(TestAssets.Root, "content/generated/audio/synth/normal-cries.bin")));
    public static IEnumerable<object[]> ModesAndSpecies => new[] { 3, 4, 6, 7, 8, 9, 10 }
        .SelectMany(mode => new[] { 4, 16, 39, 161, 410 }.Select(species => new object[] { mode, species }));

    [TestMethod]
    [DataRow(3, 50, 200, 90, 62, 28, 48, false)]
    [DataRow(4, 25, 100, 90, 61, 56, 120, true)]
    [DataRow(6, 140, 220, 90, 61, 33, 97, false)]
    [DataRow(7, 10, 100, 120, 58, 64, 80, false)]
    [DataRow(8, 60, 225, 120, 61, 64, 80, false)]
    [DataRow(9, 15, 125, 120, 59, 112, 0, true)]
    [DataRow(10, 100, 225, 120, 59, 112, 0, false)]
    public void SourceModeFieldsMatchPinnedSetterVectors(int mode, int length, int release, int volume, int key, int tune1, int tune2, bool reverse)
    {
        var (song, sequence) = SourceAnimationCryPreset.Build(Resources, Catalog, 4, mode, -64);
        int tracks = mode is 7 or 8 or 9 or 10 ? 1 : 2;
        Assert.AreEqual(tracks, sequence[0]);
        Assert.AreEqual(tracks, song.Tracks.Length);
        Assert.AreEqual(reverse ? Catalog.Get(4).ReverseToneAddress : Catalog.Get(4).ToneAddress, song.Voices);
        Assert.AreEqual(length, BinaryPrimitives.ReadUInt16LittleEndian(sequence.AsSpan(0x2e)));
        Assert.AreEqual(release, sequence[0x26]);
        Assert.AreEqual(volume, sequence[0x1d]);
        Assert.AreEqual(key, sequence[0x2a]);
        Assert.AreEqual(tune1, sequence[0x12]);
        Assert.AreEqual(tune2, sequence[0x19]);
        Assert.AreEqual(0, sequence[0x28]);
        Assert.AreEqual(0u, BinaryPrimitives.ReadUInt32LittleEndian(sequence.AsSpan(0x20)));
    }

    [TestMethod]
    public void SourceReverseTablePairsReuseEveryOrdinarySample()
    {
        var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog);
        foreach (int species in Enumerable.Range(1, 251).Concat(Enumerable.Range(277, 135)))
        {
            var entry = Catalog.Get(species);
            synth.PrepareAnimationCry(species);
            Assert.AreEqual((byte)0x20, Resources.U8(entry.ToneAddress));
            Assert.AreEqual((byte)0x30, Resources.U8(entry.ReverseToneAddress));
            for (uint offset = 1; offset < 12; offset++)
                Assert.AreEqual(Resources.U8(entry.ToneAddress + offset), Resources.U8(entry.ReverseToneAddress + offset));
        }
        Assert.AreEqual(386, synth.PreparedCryCount);
        Assert.AreEqual(3136467, synth.PreparedCryBytes);
        var fresh = synth.CreateFreshPlayback();
        fresh.StartAnimationCry(410, SourceAnimationCryPreset.Growl1);
        fresh.Render(new float[1024]);
        Assert.IsTrue(fresh.CryPlaying);
        Assert.AreEqual(synth.PreparedCryBytes, fresh.PreparedCryBytes);
    }

    [TestMethod]
    public void ValidReverseSamplesExcludeForwardGuardAndUseOriginalSampleArray()
    {
        var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog);
        synth.StartNormalCry(4, duckBgm: false);
        synth.Render(new float[1024]);
        var forward = synth.Voices.Single(v => v.Active && v.Player?.Id == 2);
        sbyte[] decoded = forward.DecodedSamples!;
        synth.StartAnimationCry(4, SourceAnimationCryPreset.Growl1, duckBgm: false);
        synth.Render(new float[1024]);
        var reverse = synth.Voices.Single(v => v.Active && v.Player?.Id == 2);
        Assert.AreSame(decoded, reverse.DecodedSamples);
        // Extracted bounded SoundMainRAM_Unk2 vectors, independent of the live mapper.
        int[] first = [-6, -15, -6, -10, -10, -14, -15, -16, -20, -21, -20, -4, 12, 12, 16, 20];
        int[] last = [48, 32, -17, -42, -43, -59, -68, -84, -93, -109, -73, -37, -53, -28, 36, 0];
        for (int index = 0; index < first.Length; index++)
        {
            Assert.AreEqual((double)first[index], synth.ReadSample(reverse, index));
            Assert.AreEqual((double)last[index], synth.ReadSample(reverse, reverse.Length - last.Length + index));
        }
        for (int index = 0; index < reverse.Length; index++)
            Assert.AreEqual((double)decoded[reverse.Length - 1 - index], synth.ReadSample(reverse, index));
    }

    [TestMethod][DataRow(7)][DataRow(8)]
    public void RoarModesUseEveryForwardSampleAndReuseNormalDecodedResources(int mode)
    {
        var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog);
        synth.StartNormalCry(4, duckBgm: false);
        synth.Render(new float[1024]);
        var normal = synth.Voices.Single(v => v.Active && v.Player?.Id == 2);
        var normalInstrument = normal.Instrument;
        var samples = normal.DecodedSamples!;
        int prepared = synth.PreparedCryCount, bytes = synth.PreparedCryBytes;
        synth.StartAnimationCry(4, mode, -64, duckBgm: false);
        synth.Render(new float[1024]);
        var roar = synth.Voices.Single(v => v.Active && v.Player?.Id == 2);
        Assert.AreSame(samples, roar.DecodedSamples);
        Assert.IsNotNull(roar.Instrument);
        Assert.AreEqual(0x20, roar.Instrument!.Type);
        Assert.IsFalse(SourceAnimationCryPreset.UsesReverse(mode));
        for (int i = 0; i < roar.Length; i++) Assert.AreEqual((double)samples[i], synth.ReadSample(roar, i));
        Assert.AreEqual(prepared, synth.PreparedCryCount);Assert.AreEqual(bytes, synth.PreparedCryBytes);
        synth.StartNormalCry(4, duckBgm: false);
        // A replacement waits for the next m4a frame. The preceding 512-frame
        // blocks leave this restart between boundaries, so another 512 is too short.
        long beforeFrame = synth.GameFrames;
        int sourceFrameSamples = (int)((Synth.Rate * Synth.FrameCycles + Synth.Cpu - 1) / Synth.Cpu);
        synth.Render(new float[sourceFrameSamples * 2]);
        Assert.AreEqual(beforeFrame + 1, synth.GameFrames);
        var restored = synth.Voices.Single(v => v.Active && v.Player?.Id == 2);
        Assert.AreSame(samples, restored.DecodedSamples);Assert.AreEqual(normalInstrument, restored.Instrument);
        Assert.AreEqual(60, restored.MidiKey);Assert.IsNotNull(restored.Track);
        Assert.AreEqual(0, restored.Track!.Tune);Assert.AreEqual(0, restored.Track.Pan);
        Assert.AreEqual(0, restored.Track.ReleaseOverride);
    }

    // This is the explicitly approved safe boundary rule, not an original ARM
    // sample vector. The driver would request a wrapped out-of-pack address.
    [TestMethod]
    public void ApprovedReverseLookaheadPolicyHoldsBoundarySample()
    {
        var synth = new Synth(Resources);
        var voice = new Voice
        {
            Instrument = new Instrument(0x30, 60, 0, 0, 0, 255, 0, 255, 0),
            Length = 3, DecodedSamples = new sbyte[] { 12, -34, 56, 99 }
        };
        Assert.AreEqual(56d, synth.ReadSample(voice, 0));
        Assert.AreEqual(-34d, synth.ReadSample(voice, 1));
        Assert.AreEqual(12d, synth.ReadSample(voice, 2));
        Assert.AreEqual(12d, synth.ReadSample(voice, 3));
        foreach (double fraction in new[] { 0d, .25, .5, .75, .999 })
        {
            double sample = synth.ReadSample(voice, 2);
            sample += (synth.ReadSample(voice, 3) - sample) * fraction;
            Assert.AreEqual(12d, sample);
        }
    }

    [TestMethod]
    [DynamicData(nameof(ModesAndSpecies))]
    public void AnimationModesAreFiniteFinishAndDoNotDuplicateDecodedResources(int mode, int species)
    {
        var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog) { EnableTrace = false };
        synth.PrepareAnimationCry(species);
        int count = synth.PreparedCryCount, bytes = synth.PreparedCryBytes;
        synth.StartAnimationCry(species, mode, -64);
        var output = new float[4 * Synth.Rate * 2];
        synth.Render(output);
        Assert.IsTrue(output.Any(v => v != 0));
        Assert.IsTrue(output.All(float.IsFinite));
        Assert.AreEqual(0L, synth.Clipped);
        Assert.AreEqual(count, synth.PreparedCryCount);
        Assert.AreEqual(bytes, synth.PreparedCryBytes);
        Assert.IsFalse(synth.CryPlaying);
        Assert.IsFalse(synth.BgmDucked);
        Assert.IsTrue(synth.Ended);
    }

    [TestMethod]
    [DataRow(4), DataRow(9)]
    public void ReverseRenderingIsPartitionInvariant(int mode)
    {
        float[] Render(int block)
        {
            var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog) { EnableTrace = false };
            synth.StartAnimationCry(161, mode, 63, duckBgm: false);
            var result = new float[2 * Synth.Rate * 2];
            for (int frame = 0; frame < result.Length / 2;)
            {
                int count = Math.Min(block, result.Length / 2 - frame);
                synth.Render(result.AsSpan(frame * 2, count * 2));
                frame += count;
            }
            return result;
        }
        CollectionAssert.AreEqual(Render(int.MaxValue), Render(257));
    }

    [TestMethod]
    public void InvalidReverseMetadataAndUnsupportedModesPreserveCurrentCry()
    {
        var pack = File.ReadAllBytes(Path.Combine(TestAssets.Root, "content/generated/audio/synth/firered.pack"));
        pack[Catalog.Get(4).ReverseToneAddress - FireRedAudio.Resources.Base] = 0x20;
        var synth = new Synth(new Resources(pack), AudioProfile.SourceFiltered, Catalog);
        synth.StartNormalCry(4);
        synth.Render(new float[2048]);
        var current = synth.Voices.Single(v => v.Active && v.Player?.Id == 2).Player;
        Assert.Throws<InvalidDataException>(() => synth.StartAnimationCry(4, SourceAnimationCryPreset.Growl1));
        foreach (int mode in new[] { 0, 1, 2, 5, 11, 12, 255 })
            Assert.Throws<NotSupportedException>(() => synth.StartAnimationCry(4, mode));
        Assert.AreSame(current, synth.Voices.Single(v => v.Active && v.Player?.Id == 2).Player);
    }
}
