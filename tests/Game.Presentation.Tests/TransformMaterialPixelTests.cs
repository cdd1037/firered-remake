using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class TransformMaterialPixelTests
{
    [TestMethod]
    public void WhiteSixUsesOriginalRgb555RoundingPreservesOpacityAndDetachesEveryPixel()
    {
        ushort[] values=Enumerable.Range(0,65536).Select(i=>(ushort)i).ToArray();
        byte[] indices=[0,1,2];ushort[] palette=[0,0x1234,0x7fff];
        var input=new SourcePixelAsset(256,256,values){PaletteIndices=indices,Palette=palette,PaletteTag=77};
        var result=SourceBattleFormCatalog.TransformPixels(input);
        Assert.AreEqual(input.Width,result.Width);Assert.AreEqual(input.Height,result.Height);Assert.AreNotSame(values,result.Pixels);
        for(int i=0;i<values.Length;i++)
        {
            int expected=i&0x8000;
            for(int channel=0;channel<3;channel++)
            {int component=(i>>(5*channel))&31;expected|=(component+((31-component)*6>>4))<<(5*channel);}
            Assert.AreEqual((ushort)expected,result.Pixels[i]);Assert.AreEqual((ushort)i,values[i]);
        }
        // Source has copied the blended colors back into the normal bank. The
        // immutable material must not also retain unblended indexed ownership.
        Assert.AreEqual(0,result.PaletteIndices.Length);Assert.AreEqual(0,result.Palette.Length);
        CollectionAssert.AreEqual(new byte[]{0,1,2},indices);CollectionAssert.AreEqual(new ushort[]{0,0x1234,0x7fff},palette);
        values[100]=0;Assert.AreNotEqual(values[100],result.Pixels[100]);
    }
}
