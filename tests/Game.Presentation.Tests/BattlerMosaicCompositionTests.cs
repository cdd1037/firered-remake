using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class BattlerMosaicCompositionTests
{
    private static readonly ushort Ground=0x8421;
    private static SourcePixelAsset Pattern()=>new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)((i%7==0)?0:0x8000|((i%64)&31)|(((i/64)&31)<<5))).ToArray());
    private static Dictionary<string,SourcePixelAsset> Assets()=>new()
    {
        ["background"]=new(240,112,Enumerable.Repeat(Ground,240*112).ToArray()),
        ["impact"]=new(32,32,new ushort[1024]),["old"]=Pattern(),
        ["new"]=new(64,64,Enumerable.Repeat((ushort)0xfc00,4096).ToArray())
    };
    private static ushort Pixel(byte[] rgba,int x,int y)
    {int i=(y*240+x)*4;return (ushort)(0x8000|((rgba[i]*31+127)/255)|(((rgba[i+1]*31+127)/255)<<5)|(((rgba[i+2]*31+127)/255)<<10));}
    private static SourceAnimationFrame Frame(int slot,int mosaic)=>new(1,true,new PixelPoint[4],[slot],0,0,[],[])
        {BattlerBackgroundMosaic=slot==0?[false,true]:[true,false],BackgroundMosaicHorizontal=mosaic,BackgroundMosaicVertical=mosaic};
    [TestMethod]
    [DataRow(0)] [DataRow(1)] [DataRow(7)] [DataRow(15)]
    public void MosaicSamplesScreenAlignedSourcePixelsIncludingTransparentAndOffscreenMargins(int mosaic)
    {
        foreach(int slot in new[]{0,1})foreach(var center in new[]{new PixelPoint(-9,-3),new PixelPoint(37,28),new PixelPoint(229,105)})
        {
            var assets=Assets();var battlers=Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"old",0,0,64,64){DisplayCenter=center,Visible=i==slot}).ToArray();
            var composer=new SourceBattleFrameComposer(assets,battlers);var frame=Frame(slot,mosaic);var image=composer.ComposeField(frame);
            for(int y=0;y<112;y++)for(int x=0;x<240;x++)
            {
                int block=mosaic+1,sx=x-x%block-center.X+32,sy=y-y%block-center.Y+32;
                ushort sampled=sx>=0&&sx<64&&sy>=0&&sy<64?assets["old"].Pixels[sy*64+sx]:(ushort)0;
                ushort expected=(sampled&0x8000)!=0?sampled:Ground;
                Assert.AreEqual(expected,Pixel(image,x,y),$"rank {slot}, block {block}, center {center}, ({x},{y})");
            }
            CollectionAssert.AreEqual(image,composer.ComposeField(frame));
        }
    }
    [TestMethod]
    public void SelectedMaterialAndGeometryAreFrameOwnedAndOtherBackgroundIsUnaffected()
    {
        var assets=Assets();var old=new AnimationBattler(new(0),"old",0,0,64,64){DisplayCenter=new(40,40)};
        var partner=new AnimationBattler(new(1),"old",0,0,64,64){DisplayCenter=new(190,40)};
        var composer=new SourceBattleFrameComposer(assets,[old,partner]);
        var before=Frame(0,7) with{BackgroundSlots=[0,1],Battlers=[old,partner]};
        var first=composer.ComposeField(before);
        var replacement=old with{Asset="new",DisplayCenter=new(40,56)};
        var after=before with{Battlers=[replacement,partner]};var second=composer.ComposeField(after);
        string json=System.Text.Json.JsonSerializer.Serialize(after);
        Assert.IsTrue(json.Contains("\"Asset\":\"new\"",StringComparison.Ordinal));
        Assert.IsFalse(json.Contains("\"Partner\"",StringComparison.Ordinal));
        Assert.AreNotEqual(Pixel(first,40,56),Pixel(second,40,56));
        for(int y=0;y<112;y++)for(int x=150;x<240;x++)Assert.AreEqual(Pixel(first,x,y),Pixel(second,x,y));
        CollectionAssert.AreEqual(first,composer.ComposeField(before));
        Assert.Throws<InvalidDataException>(()=>composer.ComposeField(after with{BackgroundMosaicHorizontal=16}));
        Assert.Throws<InvalidDataException>(()=>composer.ComposeField(after with{Battlers=[replacement,replacement]}));
    }
}
