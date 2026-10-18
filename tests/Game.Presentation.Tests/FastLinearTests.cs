using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class FastLinearTests
{
    [TestMethod]
    public void SourceFourBitTranslationRetainsNumeratorTruncationSignCarryAndReverseReset()
    {
        int[][] args=[[168,-48,72],[-168,48,72],[5000,-4000,72],[-5000,4000,72],[3,17,48],[1,-17,16]];
        using var vectors=SourceVectorFixture.Read("fast-linear-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_snow_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures");
        var rows=vectors.RootElement.Deserialize<int[][]>()!;
        foreach(var group in rows.GroupBy(r=>r[0]))
        {
            var a=args[group.Key];var linear=new SourceFastLinear(a[0],a[1],a[2]);Assert.AreEqual(group.First()[2],(int)linear.Remaining);
            foreach(var row in group.Skip(1))
            {
                int tick=row[1];if(tick==400){linear=linear.ReverseCopy();linear.ResetAccumulators();}
                PixelPoint offset;if(tick>=20)linear.StepContinuing(out offset);else linear.Step(out offset);
                Assert.AreEqual(row[2],(int)linear.Remaining);Assert.AreEqual(new PixelPoint(row[5],row[6]),offset);
            }
        }
    }
    [TestMethod]
    public void UndefinedSourceDivisionIsRejectedBeforeTranslation()
    {
        Assert.ThrowsExactly<InvalidDataException>(()=>new SourceFastLinear(10,10,0));
        Assert.ThrowsExactly<InvalidDataException>(()=>new SourceFastLinear(0,10,72));
        Assert.ThrowsExactly<InvalidDataException>(()=>new SourceFastLinear(1,10,72));
    }
}
