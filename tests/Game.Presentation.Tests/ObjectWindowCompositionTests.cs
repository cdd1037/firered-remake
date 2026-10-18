using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ObjectWindowCompositionTests
{
    private static readonly AnimationBattler[] Bodies=[new(new(0),"body",0,0,64,64){Visible=false},new(new(1),"body",0,0,64,64){Visible=false}];
    private static SourceVisualSpriteFrame Window(int slot,int x,int y,SourcePixelAsset material)=>new(slot,"window",new(x,y),default,0,true,false,false,256,"mask",0)
        {Width=8,Height=8,ObjectWindow=true,Material=material};
    private static SourceAnimationFrame Frame(params SourceVisualSpriteFrame[] sprites)=>new(0,true,new PixelPoint[4],[],8,8,[],[])
        {Sprites=sprites,Overlay=new("test",0,0),ObjectWindow=new(true,63,61),BlendEnabled=true,BlendFirstTargets=2,BlendSecondTargets=63};
    private static SourceBattleFrameComposer Composer()
    {
        var map=new byte[2048];for(int i=1;i<map.Length;i+=2)map[i]=128;
        ushort[] palette=new ushort[16];palette[1]=0x03e0;
        var overlay=new SourceOverlayAsset(Enumerable.Repeat((byte)0x11,32).ToArray(),map,palette);
        return new(new Dictionary<string,SourcePixelAsset>{{"impact",new(32,32,new ushort[1024])},{"background",new(240,112,Enumerable.Repeat((ushort)0xfc00,240*112).ToArray())},{"body",new(64,64,new ushort[4096])}},Bodies,overlays:new Dictionary<string,SourceOverlayAsset>{{"test",overlay}});
    }
    private static void Pixel(byte[] rgba,int x,int y,int r,int g,int b)
    {int at=(y*240+x)*4;CollectionAssert.AreEqual(new[]{r,g,b,255},rgba.AsSpan(at,4).ToArray().Select(v=>(int)v).ToArray(),$"({x},{y})");}
    [TestMethod]
    public void ObjectWindowsUnionOpaquePixelsWithoutPaintingOrOccludingObjects()
    {
        ushort[] pixels=Enumerable.Repeat((ushort)0x801f,64).ToArray();pixels[0]=0;
        var material=new SourcePixelAsset(8,8,pixels);var a=Window(4,20,20,material);var b=Window(5,25,20,material);var hidden=Window(6,40,20,material) with{Visible=false};
        var composer=Composer();var frame=Frame(a,b,hidden);byte[] result=composer.ComposeField(frame);
        Pixel(result,16,16,0,0,255);Pixel(result,17,16,0,123,123);Pixel(result,28,20,0,123,123);Pixel(result,40,20,0,0,255);
        var front=Window(7,20,20,new(8,8,Enumerable.Repeat((ushort)0x801f,64).ToArray())) with{ObjectWindow=false,OamPriority=0};
        var covered=composer.ComposeField(frame with{Sprites=[a,b,front]});Pixel(covered,20,20,255,0,0);
        CollectionAssert.AreEqual(result,composer.ComposeField(frame));
    }
    [TestMethod,DataRow(256,0,0,256,false),DataRow(0,256,-256,0,false),DataRow(128,64,-64,128,true)]
    public void CopiedAffineMaskUsesFrozenMatrixOffsetAndClippedSourcePixels(int a,int b,int c,int d,bool doubled)
    {
        ushort[] source=Enumerable.Range(0,64).Select(i=>(ushort)((i%8<3&&i/8>=2)?0x801f:0)).ToArray();
        var sprite=Window(4,2,4,new(8,8,source)) with{Affine=true,DoubleSize=doubled,MatrixA=a,MatrixB=b,MatrixC=c,MatrixD=d,Offset=new(1,-1)};
        byte[] result=Composer().ComposeField(Frame(sprite));int half=doubled?8:4;
        for(int y=0;y<20;y++)for(int x=0;x<20;x++)
        {
            int dx=x-3,dy=y-3,sx=((dx*a+dy*b)>>8)+4,sy=((dx*c+dy*d)>>8)+4;
            bool mask=dx>=-half&&dx<half&&dy>=-half&&dy<half&&sx>=0&&sx<8&&sy>=0&&sy<8&&source[sy*8+sx]!=0;
            Pixel(result,x,y,0,mask?123:0,mask?123:255);
        }
    }
    [TestMethod]
    public void ObjectWindowUsesCopiedFrameFlipAndOffsetWithoutReadingPaletteColors()
    {
        var material=new SourcePixelAsset(8,8,Enumerable.Range(0,64).Select(i=>(ushort)(i==0?0x8000:0)).ToArray());
        var sprite=Window(4,20,20,material) with{HFlip=true,VFlip=true,Offset=new(2,3)};
        var result=Composer().ComposeField(Frame(sprite));
        Pixel(result,25,26,0,123,123);Pixel(result,18,19,0,0,255);
    }
    [TestMethod]
    public void DisabledWindowRestoresBgAndUnsupportedConcurrentMasksFailClosed()
    {
        var composer=Composer();var material=new SourcePixelAsset(8,8,Enumerable.Repeat((ushort)0x801f,64).ToArray());var frame=Frame(Window(4,20,20,material));
        Pixel(composer.ComposeField(frame with{ObjectWindow=null}),100,50,0,123,123);
        Pixel(composer.ComposeField(frame with{ObjectWindow=new(false,63,61)}),20,20,0,123,123);
        Assert.Throws<InvalidDataException>(()=>composer.ComposeField(frame with{ObjectWindow=new(true,27,61)}));
        CollectionAssert.AreEqual(composer.ComposeField(frame),composer.ComposeField(frame with{Window0=new(true,0,0,63,63)}));
        Assert.Throws<InvalidDataException>(()=>composer.ComposeField(frame with{Sprites=[frame.Sprites[0] with{Subsprites=[new(0,0,8,8,0,0)]}]}));
    }
}
