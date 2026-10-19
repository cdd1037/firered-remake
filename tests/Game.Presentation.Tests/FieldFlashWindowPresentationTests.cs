using Game.Application;
using System.Text.Json;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FieldFlashWindowPresentationTests
{
    private const string Fixture="tests/Game.Presentation.Tests/Fixtures/field-flash-window-source-vectors.json";
    private const string Generate="python tests/Reference/generate_field_flash_window_vectors.py --reference content/raw/pokefirered/037335f4 --output "+Fixture;

    [TestMethod]
    public void EverySupportedRadiusMatchesAll160ExtractedSourceScanlines()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile(Fixture,Generate)));
        var radii=source.RootElement.GetProperty("radii");
        Assert.AreEqual(FieldFlashWindowPresentation.MaximumRadius+1,radii.GetArrayLength());
        CollectionAssert.AreEqual(new[]{120,80},Numbers(source.RootElement,"center"));
        CollectionAssert.AreEqual(new[]{240,160},Numbers(source.RootElement,"visibleSize"));
        int[] left=new int[160],right=new int[160];
        int radius=0;
        foreach(var row in radii.EnumerateArray())
        {
            Assert.AreEqual(radius,row.GetProperty("radius").GetInt32(),"Source radii must be complete and ordered.");
            int[] expectedLeft=Numbers(row,"left"),expectedRight=Numbers(row,"right");
            Assert.AreEqual(160,expectedLeft.Length);Assert.AreEqual(160,expectedRight.Length);
            Array.Fill(left,-1);Array.Fill(right,-1);
            FieldFlashWindowPresentation.FillBoundaries(radius,left,right);
            CollectionAssert.AreEqual(expectedLeft,left,$"left radius {radius}");
            CollectionAssert.AreEqual(expectedRight,right,$"right-exclusive radius {radius}");
            for(int y=0;y<160;y++)
            {
                Assert.IsTrue(left[y] is >=0 and <=255,$"left radius {radius}, line {y}");
                Assert.IsTrue(right[y]>=left[y]&&right[y]<=255,$"right radius {radius}, line {y}");
            }
            radius++;
        }
        // Exhaustive 0..200 includes every source level and all 89 even
        // illumination radii from 24 to 200, as well as their odd neighbors.
        Assert.AreEqual(201,radius);
    }

    [TestMethod]
    [DataRow(24,96,144)]
    [DataRow(40,80,160)]
    [DataRow(56,64,176)]
    [DataRow(72,48,192)]
    [DataRow(119,1,239)]
    [DataRow(120,0,240)]
    [DataRow(121,0,241)]
    [DataRow(134,0,254)]
    [DataRow(135,0,255)]
    [DataRow(136,0,255)]
    [DataRow(200,0,255)]
    public void SourceLevelsAndHardwareClampsKeepRightExclusiveEndpoints(int radius,int start,int end)
    {
        int[] left=new int[160],right=new int[160];
        FieldFlashWindowPresentation.FillBoundaries(radius,left,right);
        Assert.AreEqual(start,left[80]);Assert.AreEqual(end,right[80]);
        // Keep values beyond visible x=239: screen clipping is the renderer's
        // job, and adding one to the right edge would widen the source window.
        if(radius==200)
        {
            Assert.IsTrue(left.All(value=>value==0));
            Assert.IsTrue(right.All(value=>value==255));
        }
    }

    [TestMethod]
    public void RepeatedSourceWritesRetainFlatPolarEdgesRatherThanARecomputedCircle()
    {
        int[] left=new int[160],right=new int[160];
        FieldFlashWindowPresentation.FillBoundaries(24,left,right);
        Assert.AreEqual(0,left[55]);Assert.AreEqual(0,right[55]);
        Assert.AreEqual(114,left[56]);Assert.AreEqual(126,right[56]);
        Assert.AreEqual(114,left[104]);Assert.AreEqual(126,right[104]);
        Assert.AreEqual(0,left[105]);Assert.AreEqual(0,right[105]);

        FieldFlashWindowPresentation.FillBoundaries(80,left,right);
        Assert.AreEqual(110,left[0]);Assert.AreEqual(130,right[0]);
        Assert.AreEqual(104,left[159]);Assert.AreEqual(136,right[159]);
    }

    [TestMethod]
    public void ZeroRadiusIsAnEmptyWindowAndReplacesEveryPreviouslyWrittenLine()
    {
        int[] left=new int[160],right=new int[160];
        FieldFlashWindowPresentation.FillBoundaries(200,left,right);
        FieldFlashWindowPresentation.FillBoundaries(0,left,right);
        for(int y=0;y<160;y++)
        {
            Assert.AreEqual(y==80?120:0,left[y],$"left line {y}");
            Assert.AreEqual(left[y],right[y],$"Empty right-exclusive line {y}");
        }
    }

    [TestMethod]
    public void SourceLine160AndLargerDestinationTailsStayOutsideTheVisibleFrame()
    {
        using var source=JsonDocument.Parse(File.ReadAllText(TestAssets.GeneratedFile(Fixture,Generate)));
        var sourceRadius80=source.RootElement.GetProperty("radii")[80];
        CollectionAssert.AreEqual(new[]{110,130},Numbers(sourceRadius80,"sourceLine160"));
        int[] left=Enumerable.Repeat(-7,162).ToArray(),right=Enumerable.Repeat(-9,163).ToArray();
        FieldFlashWindowPresentation.FillBoundaries(80,left,right);
        CollectionAssert.AreEqual(Numbers(sourceRadius80,"left"),left[..160]);
        CollectionAssert.AreEqual(Numbers(sourceRadius80,"right"),right[..160]);
        CollectionAssert.AreEqual(new[]{-7,-7},left[160..]);
        CollectionAssert.AreEqual(new[]{-9,-9,-9},right[160..]);
    }

    [TestMethod]
    public void IndependentAndDisjointBuffersNeverRetainSamplerOwnedState()
    {
        int[] firstLeft=new int[160],firstRight=new int[160];
        FieldFlashWindowPresentation.FillBoundaries(24,firstLeft,firstRight);
        int[] expectedLeft=firstLeft.ToArray(),expectedRight=firstRight.ToArray();
        int[] second=new int[320];
        FieldFlashWindowPresentation.FillBoundaries(200,second.AsSpan(0,160),second.AsSpan(160,160));
        CollectionAssert.AreEqual(expectedLeft,firstLeft);CollectionAssert.AreEqual(expectedRight,firstRight);
        Assert.IsTrue(second[..160].All(value=>value==0));
        Assert.IsTrue(second[160..].All(value=>value==255));
        Array.Fill(firstLeft,-1);Array.Fill(firstRight,-1);
        FieldFlashWindowPresentation.FillBoundaries(24,firstLeft,firstRight);
        CollectionAssert.AreEqual(expectedLeft,firstLeft);CollectionAssert.AreEqual(expectedRight,firstRight);
    }

    [TestMethod]
    [DataRow(-1)]
    [DataRow(201)]
    [DataRow(int.MinValue)]
    [DataRow(int.MaxValue)]
    public void InvalidRadiusFailsBeforeChangingEitherDestination(int radius)
    {
        int[] left=Enumerable.Repeat(-7,160).ToArray(),right=Enumerable.Repeat(-9,160).ToArray();
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(()=>FieldFlashWindowPresentation.FillBoundaries(radius,left,right));
        Assert.IsTrue(left.All(value=>value==-7));Assert.IsTrue(right.All(value=>value==-9));
    }

    [TestMethod]
    public void ShortAndOverlappingDestinationsFailBeforeChangingEitherDestination()
    {
        int[] left=Enumerable.Repeat(-7,160).ToArray(),right=Enumerable.Repeat(-9,160).ToArray();
        Assert.ThrowsExactly<ArgumentException>(()=>FieldFlashWindowPresentation.FillBoundaries(24,left.AsSpan(0,159),right));
        Assert.ThrowsExactly<ArgumentException>(()=>FieldFlashWindowPresentation.FillBoundaries(24,left,right.AsSpan(0,159)));
        Assert.ThrowsExactly<ArgumentException>(()=>FieldFlashWindowPresentation.FillBoundaries(24,Array.Empty<int>(),right));
        Assert.ThrowsExactly<ArgumentException>(()=>FieldFlashWindowPresentation.FillBoundaries(24,left,Array.Empty<int>()));
        Assert.IsTrue(left.All(value=>value==-7));Assert.IsTrue(right.All(value=>value==-9));
        int[] overlapping=Enumerable.Repeat(-5,161).ToArray();
        Assert.ThrowsExactly<ArgumentException>(()=>FieldFlashWindowPresentation.FillBoundaries(24,overlapping,overlapping));
        Assert.ThrowsExactly<ArgumentException>(()=>FieldFlashWindowPresentation.FillBoundaries(24,overlapping.AsSpan(0,160),overlapping.AsSpan(1,160)));
        Assert.IsTrue(overlapping.All(value=>value==-5));
    }

    private static int[] Numbers(JsonElement element,string property)=>
        element.GetProperty(property).EnumerateArray().Select(value=>value.GetInt32()).ToArray();
}
