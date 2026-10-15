using FireRedAudio;

namespace FireRed.Tests.Audio;
[TestClass, TestCategory("Audio")]
public sealed class NormalCryCatalogTests
{
    private static string CatalogPath => Path.GetFullPath(Path.Combine(TestAssets.Root, "content/generated/audio/synth/normal-cries.bin"));
    private static Resources Resources => TestAssets.Cached(() => FireRedAudio.Resources.FromPack(Path.GetFullPath(Path.Combine(TestAssets.Root, "content/generated/audio/synth/firered.pack"))));
    private static NormalCryCatalog Catalog => TestAssets.Cached(() => NormalCryCatalog.FromFile(CatalogPath));
    public static IEnumerable<object[]> Species => Enumerable.Range(1, 251).Concat(Enumerable.Range(277, 135)).Select(id => new object[] { id });

    [TestMethod]
    public void CatalogContainsAll386OrdinarySpecies()
    {
        Assert.AreEqual(386, Catalog.Count);
        Assert.AreEqual(386, Species.Count());
        foreach (object[] row in Species)
            Assert.AreEqual((int)row[0], Catalog.Get((int)row[0]).Species);
    }

    [TestMethod]
    public void MalformedCatalogsAreRejected()
    {
        var bad = File.ReadAllBytes(CatalogPath);
        bad[^1] ^= 1;
        Assert.Throws<InvalidDataException>(() => NormalCryCatalog.FromBytes(bad));
        Assert.Throws<InvalidDataException>(() => NormalCryCatalog.FromBytes(new byte[100]));
    }

    [TestMethod]
    [DataRow(-1), DataRow(0), DataRow(252), DataRow(276), DataRow(412), DataRow(413), DataRow(int.MaxValue)]
    public void InvalidSpeciesCannotReplacePlayingCry(int species)
    {
        var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog)
        {
            EnableTrace = false
        };
        synth.StartNormalCry(1);
        Assert.Throws<NotSupportedException>(() => synth.StartNormalCry(species));
        synth.Render(new float[1024]);
        Assert.IsTrue(synth.CryPlaying, "Rejected request must preserve the current cry.");
    }

    [TestMethod]
    public void UnsupportedModeIsRejected()
    {
        var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog);
        Assert.Throws<NotSupportedException>(() => synth.StartNormalCry(1, mode: 1));
    }

    [TestMethod]
    [DynamicData(nameof(Species))]
    public void OrdinaryCryRendersFiniteSamplesAndEndsWithoutClipping(int species)
    {
        var engine = new Synth(Resources, AudioProfile.SourceFiltered, Catalog)
        {
            EnableTrace = false
        };
        engine.PrepareNormalCry(species);
        engine.StartNormalCry(species, duckBgm: false);
        var buffer = new float[514];
        for (int done = 0; done < 4 * Synth.Rate;)
        {
            int frames = Math.Min(257, 4 * Synth.Rate - done);
            engine.Render(buffer.AsSpan(0, frames * 2));
            bool finite=true;foreach(float sample in buffer.AsSpan(0,frames*2))finite &= float.IsFinite(sample);
            Assert.IsTrue(finite);
            done += frames;
        }

        Assert.IsTrue(engine.Ended);
        Assert.AreEqual(0L, engine.Clipped);
        Assert.AreEqual(1, engine.MaxPcm);
    }

    [TestMethod]
    public void PreparingEverySpeciesHasExactCacheAccounting()
    {
        var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog);
        foreach (object[] row in Species)
            synth.PrepareNormalCry((int)row[0]);
        Assert.AreEqual(386, synth.PreparedCryCount);
        Assert.AreEqual(3136467, synth.PreparedCryBytes);
    }
}
