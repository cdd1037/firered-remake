using FireRedAudio;

namespace FireRed.Tests.Audio;

[TestClass, TestCategory("Audio")]
public sealed class PlaybackResetTests
{
    private static Resources Resources => TestAssets.Cached(() => FireRedAudio.Resources.FromPack(
        Path.Combine(TestAssets.Root, "content/generated/audio/synth/firered.pack")));
    private static NormalCryCatalog Catalog => TestAssets.Cached(() => NormalCryCatalog.FromFile(
        Path.Combine(TestAssets.Root, "content/generated/audio/synth/normal-cries.bin")));
    private static Synth New() => new(Resources, AudioProfile.SourceFiltered, Catalog) { EnableTrace = false };

    [TestMethod]
    public void FreshPlaybackRetainsDecodedSamplesWithoutCopyingSamplePayloads()
    {
        var old = New();
        foreach (int species in Enumerable.Range(1, 251).Concat(Enumerable.Range(277, 135))) old.PrepareNormalCry(species);
        old.StartNormalCry(4); old.Render(new float[1024]);
        var samples = old.Voices.First(v => v.Active && v.DecodedSamples is not null).DecodedSamples;
        long allocated = GC.GetAllocatedBytesForCurrentThread();
        var fresh = old.CreateFreshPlayback();
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;
        Assert.AreEqual(386, fresh.PreparedCryCount);
        Assert.AreEqual(3136467, fresh.PreparedCryBytes);
        Assert.IsTrue(allocated < 256_000L, "Reset must not reallocate the multi-megabyte decoded payloads.");
        fresh.StartNormalCry(4); fresh.Render(new float[1024]);
        Assert.AreSame(samples, fresh.Voices.First(v => v.Active && v.DecodedSamples is not null).DecodedSamples);
        Assert.AreNotSame(old.Voices[0], fresh.Voices[0]);
    }

    [TestMethod]
    public void FreshPlaybackClearsVoicesClocksDuckAndFeedbackTails()
    {
        var old = New();
        old.Start(0x086dd844, 0); old.StartNormalCry(4); old.Render(new float[Synth.Rate * 2]);
        old.Stop(0); old.Stop(2);
        Assert.IsFalse(old.Ended, "The fixture must leave actual feedback state to clear.");
        var fresh = old.CreateFreshPlayback();
        Assert.AreEqual(0L, fresh.OutputFrames); Assert.AreEqual(0L, fresh.GameFrames);
        Assert.AreEqual(0, fresh.ReverbAmount); Assert.IsFalse(fresh.BgmDucked);
        Assert.IsTrue(fresh.Ended); Assert.IsTrue(fresh.Voices.All(v => !v.Active));
        var silent = new float[4096]; fresh.Render(silent);
        Assert.IsTrue(silent.All(x => x == 0), "A reset cannot retain a previous song's tail.");
    }

    [TestMethod]
    [DataRow(1), DataRow(4), DataRow(143), DataRow(411)]
    public void FreshPlaybackProducesExactlyTheSamePcmAsIndependentMixer(int species)
    {
        var old = New(); old.PrepareNormalCry(species);
        old.StartNormalCry(4); old.Render(new float[1024]);
        var fresh = old.CreateFreshPlayback(); var independent = New();
        fresh.Start(0x086dd844, 0); independent.Start(0x086dd844, 0);
        fresh.StartNormalCry(species); independent.StartNormalCry(species);
        var actual = new float[4096]; var expected = new float[4096];
        for (int block = 0; block < 32; block++)
        {
            fresh.Render(actual); independent.Render(expected);
            CollectionAssert.AreEqual(expected, actual);
        }
    }

    [TestMethod]
    public void FreshPlaybackCachesRemainIndependentWhenMoreSpeciesArePrepared()
    {
        var old = New(); old.PrepareNormalCry(4);
        var fresh = old.CreateFreshPlayback(); fresh.PrepareNormalCry(1);
        Assert.AreEqual(1, old.PreparedCryCount); Assert.AreEqual(2, fresh.PreparedCryCount);
    }
}
