using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class CastformMaterialTests
{
    private static SourceBattleFormCatalog Catalog()=>new(Path.Combine(TestAssets.Root,"content/generated/battle/art/campaign-hit-art"));
    [TestMethod,DataRow(0,32,32,17,13),DataRow(1,48,48,9,14),DataRow(2,32,48,9,13),DataRow(3,64,48,8,13)]
    public void SourceFormDimensionsAndCoordinates(int form,int width,int height,int y,int elevation)
    {
        var catalog=Catalog();
        for(int slot=0;slot<4;slot++)
        {
            var material=catalog.Resolve(385,new(slot),form,false);var g=material.Geometry;
            Assert.AreEqual((slot&1)==0?32:width,g.SourceWidth);Assert.AreEqual((slot&1)==0?40:height,g.SourceHeight);
            Assert.AreEqual((slot&1)==0?0:y,g.YOffset);Assert.AreEqual((slot&1)==0?0:elevation,g.Elevation);
            foreach(bool doubles in new[]{false,true})
            {
                int sourceY=(g.Base(doubles).Y+((slot&1)==0?0:y-elevation))&255;
                Assert.AreEqual(sourceY,g.Center(doubles).Y);
                Assert.AreEqual(Math.Min(104,sourceY+((slot&1)==0?8:0)),g.Center(doubles,true).Y);
                if(doubles)
                {
                    // Ordinary battle artwork uses canonical singles anchors plus source doubles offsets.
                    PixelPoint[] offsets=[new(-40,0),new(24,0),new(18,8),new(-24,-8)];
                    var face=catalog.Resolve(385,new(slot&1),form,false).Geometry.Center(false);
                    Assert.AreEqual(new PixelPoint(face.X+offsets[slot].X,face.Y+offsets[slot].Y),g.Center(true));
                }
            }
        }
        Assert.ThrowsExactly<InvalidDataException>(()=>catalog.Resolve(385,new(0),4,false));
    }
    [TestMethod]
    public void TransformWhiteSixUsesChosenPaletteAndPreservesTransparency()
    {
        var catalog=Catalog();
        for(int form=0;form<4;form++)foreach(bool shiny in new[]{false,true})foreach(int slot in new[]{0,1})
        {
            var original=catalog.Resolve(385,new(slot),form,shiny).Pixels.Pixels;
            var transformed=catalog.Resolve(385,new(slot),form,shiny,true).Pixels.Pixels;
            for(int i=0;i<4096;i++)
            {
                int expected=original[i]&0x8000;
                for(int shift=0;shift<=10;shift+=5)
                {int c=(original[i]>>shift)&31;expected|=(c+(31-c)*6/16)<<shift;}
                Assert.AreEqual((ushort)expected,transformed[i]);
            }
            CollectionAssert.AreEqual(original,catalog.Resolve(385,new(slot),form,shiny).Pixels.Pixels);
        }
    }
}
