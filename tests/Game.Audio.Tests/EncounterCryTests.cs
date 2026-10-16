using FireRedAudio;

namespace FireRed.Tests.Audio;

[TestClass, TestCategory("Audio")]
public sealed class EncounterCryTests
{
    private static Resources Resources => TestAssets.Cached(() => FireRedAudio.Resources.FromPack(Path.Combine(TestAssets.Root, "content/generated/audio/synth/firered.pack")));
    private static NormalCryCatalog Catalog => TestAssets.Cached(() => NormalCryCatalog.FromFile(Path.Combine(TestAssets.Root, "content/generated/audio/synth/normal-cries.bin")));

    [TestMethod]
    [DataRow(4), DataRow(101), DataRow(131), DataRow(143), DataRow(144), DataRow(145), DataRow(146), DataRow(150), DataRow(249), DataRow(250), DataRow(410)]
    public void CatalogEncounterCriesRenderThroughSharedCore(int species)
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
    [DataRow(-1), DataRow(0), DataRow(252), DataRow(276), DataRow(412), DataRow(int.MaxValue)]
    public void UnsupportedEncounterDoesNotReplaceCurrentCry(int species)
    {
        var synth = new Synth(Resources, AudioProfile.SourceFiltered, Catalog) { EnableTrace = false };
        synth.StartNormalCry(4);
        synth.Render(new float[1024]);
        Assert.IsTrue(synth.CryPlaying);
        Assert.Throws<NotSupportedException>(() => synth.StartEncounterCry(species));
        Assert.IsTrue(synth.CryPlaying);
    }

    [TestMethod]
    public void EncounterCapabilityComesFromTheLoadedCatalog()
    {
        var bytes = File.ReadAllBytes(Path.Combine(TestAssets.Root, "content/generated/audio/synth/normal-cries.bin"));
        var entry = Enumerable.Range(0, Catalog.Count).Select(index => 12 + index * 16)
            .Single(offset => System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(offset)) == 146);
        var singleSpecies = new byte[28];
        bytes.AsSpan(0, 8).CopyTo(singleSpecies);
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(singleSpecies.AsSpan(8), 1);
        bytes.AsSpan(entry, 16).CopyTo(singleSpecies.AsSpan(12));
        var synth = new Synth(Resources, AudioProfile.SourceFiltered, NormalCryCatalog.FromBytes(singleSpecies));
        synth.StartEncounterCry(146);
        synth.Render(new float[1024]);
        Assert.IsTrue(synth.CryPlaying);
        Assert.Throws<NotSupportedException>(() => synth.StartEncounterCry(143));
        Assert.IsTrue(synth.CryPlaying, "Absent catalog species must preserve the current cry.");
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
