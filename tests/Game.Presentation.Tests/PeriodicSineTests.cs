using System.Globalization;
using System.Text.RegularExpressions;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass]
public sealed class PeriodicSineTests
{
    [TestMethod]
    public void PinnedSourceExtensionIsPeriodicAndEveryDefinedEntryRemainsExact()
    {
        string source=File.ReadAllText(Path.Combine(TestAssets.Root,"content/raw/pokefirered/037335f4/src/trig.c"));
        source=source[..source.IndexOf("};",StringComparison.Ordinal)];
        int[] native=Regex.Matches(source,@"Q_8_8\((-?[0-9.]+)\)").Select(m=>(int)(decimal.Parse(m.Groups[1].Value,CultureInfo.InvariantCulture)*256)).ToArray();
        Assert.AreEqual(320,native.Length);
        var imported=AnimationCompositionFixture.Script(1).SineTable;CollectionAssert.AreEqual(native,imported);
        for(int index=0;index<320;index++)Assert.AreEqual(native[index],SourceBattleAnimation.PeriodicSineSample(imported,index),$"defined index {index}");
        for(int index=256;index<320;index++)Assert.AreEqual(native[index-256],native[index],$"source extension {index}");
    }
    [TestMethod]
    public void ApprovedOutOfStoragePolicySamplesThePeriodicBoundaryWithoutAnAdjacentRead()
    {
        var table=AnimationCompositionFixture.Script(1).SineTable;
        Assert.AreEqual(256,SourceBattleAnimation.PeriodicSineSample(table,320));
        foreach(int index in new[]{-1,-256,-257,320,321,511,512,1024,int.MinValue,int.MaxValue})
            Assert.AreEqual(table[index&255],SourceBattleAnimation.PeriodicSineSample(table,index),$"periodic policy {index}");
        Assert.ThrowsExactly<InvalidDataException>(()=>SourceBattleAnimation.PeriodicSineSample(new int[319],320));
    }
}
