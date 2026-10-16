using Game.Domain.Pokemon;
namespace FireRed.Tests.Domain;

[TestClass]
public sealed class PokemonSizeRecordRulesTests
{
    [TestMethod]
    public void SourceThresholdsAndUpperUnsignedWrapScaleByDexHeight()
    {
        // Independent source factors at each threshold, including the skipped index-14 branch.
        (ushort Hash, uint Factor)[] vectors = [(0,290),(9,299),(10,300),(110,400),(310,500),
            (710,600),(2710,700),(7710,800),(17710,900),(32710,1000),(47710,1100),
            (57710,1200),(62710,1300),(64710,1400),(65210,1500),(65409,1599),
            (65410,1600),(65509,1699),(65510,1700),(65535,1725)];
        foreach (var (hash, factor) in vectors)
        {
            Assert.AreEqual(9 * factor / 10, PokemonSizeRecordRules.SizeTenthsCentimeters(hash, 9));
            Assert.AreEqual(15 * factor / 10, PokemonSizeRecordRules.SizeTenthsCentimeters(hash, 15));
            Assert.AreEqual(MagikarpSizeRecordRules.SizeTenthsCentimeters(hash), PokemonSizeRecordRules.SizeTenthsCentimeters(hash, 9));
        }
    }

    [TestMethod]
    public void PersonalityAndIvHashUsesOnlySourceLowBitsAndImperialDisplayTruncates()
    {
        var zero = new SixStats(0,0,0,0,0,0);
        Assert.AreEqual((ushort)0x3412, PokemonSizeRecordRules.Hash(0xabcd1234, zero));
        Assert.AreEqual(PokemonSizeRecordRules.Hash(0x1234, zero), PokemonSizeRecordRules.Hash(0xabcd1234, new(16,16,16,16,16,16)));
        Assert.AreEqual("17.1", PokemonSizeRecordRules.FormatInches(435));
        Assert.AreEqual("10.2", PokemonSizeRecordRules.FormatInches(261));
        Assert.AreEqual("43.5", PokemonSizeRecordRules.FormatCentimeters(435));
    }
}
