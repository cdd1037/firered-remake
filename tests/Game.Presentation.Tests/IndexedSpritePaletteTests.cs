using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class IndexedSpritePaletteTests
{
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    [TestMethod]
    public void WallAssetsShareExactIndicesButKeepDistinctSourcePaletteTags()
    {
        using var resources=new SourceAnimationResources(Folder);var green=resources.Resolve(113).Assets["green-light-wall"];var gray=resources.Resolve(112).Assets["gray-light-wall"];
        Assert.AreEqual(10166,green.PaletteTag);Assert.AreEqual(10169,gray.PaletteTag);CollectionAssert.AreEqual(green.PaletteIndices,gray.PaletteIndices);Assert.IsFalse(green.Palette.SequenceEqual(gray.Palette));
        var packed=File.ReadAllBytes(Path.Combine(Folder,"walls-light-wall.4bpp"));
        for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            int address=((y/8)*8+x/8)*32+(y%8)*4+(x%8)/2,index=packed[address]>>((x&1)*4)&15;
            Assert.AreEqual(index,(int)green.PaletteIndices[y*64+x]);Assert.AreEqual(index==0?0:0x8000|green.Palette[index],(int)green.Pixels[y*64+x]);
        }
    }
    [TestMethod]
    public void IndexedComposerUsesTagOwnedFadedColorsAndAlwaysKeepsIndexZeroTransparent()
    {
        using var resources=new SourceAnimationResources(Folder);var gray=resources.Resolve(112).Assets["gray-light-wall"];var green=resources.Resolve(113).Assets["green-light-wall"];
        var originalGray=gray.Palette.ToArray();var grayFaded=Enumerable.Range(0,16).Select(i=>(ushort)(i|((31-i)<<5))).ToArray();var greenFaded=Enumerable.Range(0,16).Select(i=>(ushort)((31-i)|(i<<10))).ToArray();grayFaded[0]=greenFaded[0]=0x7fff;
        var assets=new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,Enumerable.Repeat((ushort)0x8000,240*112).ToArray())},{"impact",new(32,32,new ushort[1024])},{"gray-light-wall",gray},{"green-light-wall",green}};
        var composer=new SourceBattleFrameComposer(assets,[]);
        SourceVisualSpriteFrame Sprite(int slot,string asset,int x)=>new(slot,"indexed-pixel-vector",new(x,48),default,0,true,false,false,256,asset,2){Width=64,Height=64,OamRank=slot};
        var frame=new SourceAnimationFrame(0,true,new PixelPoint[4],[],0,0,[],[]){Sprites=[Sprite(0,"gray-light-wall",64),Sprite(1,"green-light-wall",160)],SpritePalettes=new Dictionary<int,ushort[]>{{10169,grayFaded},{10166,greenFaded}}};
        var pixels=composer.ComposeField(frame);
        foreach(var (asset,palette,left) in new[]{(gray,grayFaded,32),(green,greenFaded,128)})for(int y=0;y<64;y++)for(int x=0;x<64;x++)
        {
            int index=asset.PaletteIndices[y*64+x],color=index==0?0:palette[index],at=((y+16)*240+x+left)*4;
            Assert.AreEqual((byte)((color&31)*255/31),pixels[at]);Assert.AreEqual((byte)(((color>>5)&31)*255/31),pixels[at+1]);Assert.AreEqual((byte)(((color>>10)&31)*255/31),pixels[at+2]);Assert.AreEqual((byte)255,pixels[at+3]);
        }
        CollectionAssert.AreEqual(originalGray,gray.Palette);
    }
}
