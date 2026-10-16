using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class MixedSpriteSheetTests
{
    private static readonly ushort[] Colors=[0x8000,0x801f,0x83e0,0xfc00,0xffff,0x8000];
    private static SourcePixelAsset Sheet(int width)
    {
        var pixels=new ushort[6*64];int tilesWide=width/8;
        for(int tile=0;tile<6;tile++)for(int y=0;y<8;y++)for(int x=0;x<8;x++)
            pixels[((tile/tilesWide)*8+y)*width+(tile%tilesWide)*8+x]=Colors[tile];
        return new(width,pixels.Length/width,pixels);
    }
    private static byte[] Draw(SourceVisualSpriteFrame sprite,int sheetWidth)
    {
        var composer=new SourceBattleFrameComposer(new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,new ushort[240*112])},{"impact",new(32,32,new ushort[1024])},{"mixed",Sheet(sheetWidth)}},[]);
        return composer.ComposeField(new(0,true,new PixelPoint[4],[],0,0,[],[]){Sprites=[sprite]});
    }
    private static SourceVisualSpriteFrame Sprite()=>new(4,"shared-tile-sheet",new(32,32),default,1,true,false,false,256,"mixed",1){Width=16,Height=16};
    private static void Pixel(byte[] rgba,int x,int y,ushort color)
    {
        int p=(y*240+x)*4;
        CollectionAssert.AreEqual(new[]{(byte)((color&31)*255/31),(byte)(((color>>5)&31)*255/31),(byte)(((color>>10)&31)*255/31),(byte)255},rgba.Skip(p).Take(4).ToArray());
    }
    [TestMethod]
    [DataRow(false,false)][DataRow(true,false)][DataRow(false,true)][DataRow(true,true)]
    public void MixedSizeFramesUseSpriteRowStrideAndWholeFrameFlips(bool hFlip,bool vFlip)
    {
        var sprite=Sprite() with{HFlip=hFlip,VFlip=vFlip};
        byte[] narrow=Draw(sprite,8),grid=Draw(sprite,16);CollectionAssert.AreEqual(narrow,grid);
        for(int row=0;row<2;row++)for(int col=0;col<2;col++)
            Pixel(narrow,25+col*8,25+row*8,Colors[1+(vFlip?1-row:row)*2+(hFlip?1-col:col)]);
    }
    [TestMethod]
    public void AffineSamplingReadsMixedSizeTilesAfterTransformingFrameCoordinates()
    {
        var sprite=Sprite() with{Affine=true,MatrixA=0,MatrixB=-256,MatrixC=256,MatrixD=0};
        byte[] narrow=Draw(sprite,8),grid=Draw(sprite,16);CollectionAssert.AreEqual(narrow,grid);
        Pixel(narrow,25,25,Colors[2]);Pixel(narrow,33,25,Colors[4]);Pixel(narrow,25,33,Colors[1]);Pixel(narrow,33,33,Colors[3]);
    }
    [TestMethod]
    public void TileExtentIsCheckedInTilesRatherThanStoredPixelRows()
    {
        Assert.Throws<InvalidDataException>(()=>Draw(Sprite() with{Tile=3},8));
        Assert.Throws<InvalidDataException>(()=>Draw(Sprite() with{Tile=-1},16));
    }
}
