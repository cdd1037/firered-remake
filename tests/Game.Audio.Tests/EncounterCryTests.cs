using FireRedAudio;

namespace FireRed.Tests.Audio;

[TestClass, TestCategory("Audio")]
public sealed class EncounterCryTests
{
    private static Resources Resources => TestAssets.Cached(() => FireRedAudio.Resources.FromPack(Path.Combine(TestAssets.Root, "content/generated/audio/synth/firered.pack")));
    private static NormalCryCatalog Catalog => TestAssets.Cached(() => NormalCryCatalog.FromFile(Path.Combine(TestAssets.Root, "content/generated/audio/synth/normal-cries.bin")));

    [TestMethod]
    [DataRow(101), DataRow(131), DataRow(143), DataRow(144), DataRow(145), DataRow(150), DataRow(249), DataRow(250), DataRow(410)]
    public void ExistingGameEncounterCriesRenderThroughSharedCore(int species)
    {
        var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog) { EnableTrace = false };
        synth.StartEncounterCry(species);
        var buffer = new float[514];
        bool nonzero = false;
        for (int frame = 0; frame < 4 * Synth.Rate; frame += 257)
        {
            synth.Render(buffer);
            foreach (float sample in buffer)
            {
                Assert.IsTrue(float.IsFinite(sample));
                nonzero |= sample != 0;
            }
        }
        Assert.IsTrue(nonzero);
        Assert.AreEqual(0L, synth.Clipped);
        Assert.IsFalse(synth.CryPlaying);
        Assert.IsFalse(synth.BgmDucked);
    }

    [TestMethod]
    public void UnsupportedEncounterDoesNotReplaceCurrentCry()
    {
        var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog) { EnableTrace = false };
        synth.StartNormalCry(4);
        synth.Render(new float[1024]);
        Assert.IsTrue(synth.CryPlaying);
        Assert.Throws<NotSupportedException>(() => synth.StartEncounterCry(4));
        Assert.IsTrue(synth.CryPlaying);
    }

    [TestMethod]
    public void EncounterModeDoesNotChangeNormalCryModeContract()
    {
        var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog);
        Assert.Throws<NotSupportedException>(() => synth.StartNormalCry(143, mode: 2));
        var withoutCatalog = new Synth(Resources, AudioProfile.SourceFiltered);
        Assert.Throws<NotSupportedException>(() => withoutCatalog.StartEncounterCry(143));
    }
}
