using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass]
public sealed class AffineBattlerCopyCompositionTests
{
    [TestMethod,DataRow(true,true,128,64,-32,256),DataRow(true,false,256,-128,64,128),DataRow(false,true,1,99,99,1)]
    public void CopiesUseTheSharedExplicitMatrixAndDoubleCanvasWithLegacyScaleFallback(bool affine,bool doubleSize,int a,int b,int c,int d)
    {
        var bodies=Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"body"+i,0,0,64,64){Visible=false}).ToArray();
        ushort[] tile=Enumerable.Range(0,4096).Select(i=>(ushort)(0x8000|((i%64)&31)|(((i/64)&31)<<5))).ToArray();
        var assets=new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,new ushort[240*112])},{"impact",new(32,32,new ushort[1024])}};
        foreach(var body in bodies)assets[body.Asset]=new(64,64,tile);
        var sprite=new SourceVisualSpriteFrame(4,"copy",new(100,56),default,0,true,false,false,512,"body0",20){BattlerCopySlot=0,Affine=affine,DoubleSize=doubleSize,MatrixA=a,MatrixB=b,MatrixC=c,MatrixD=d,Width=64,Height=64};
        var frame=new SourceAnimationFrame(0,true,new PixelPoint[4],[],16,0,[],[]){Battlers=bodies,Doubles=true,Sprites=[sprite]};
        var rgba=new SourceBattleFrameComposer(assets,bodies).ComposeField(frame);int half=affine&&doubleSize?64:32;
        if(!affine){a=d=512;b=c=0;}
        for(int y=0;y<112;y++)for(int x=0;x<240;x++)
        {
            int dx=x-100,dy=y-56,sx=((dx*a+dy*b)>>8)+32,sy=((dx*c+dy*d)>>8)+32;
            int expected=dx>=-half&&dx<half&&dy>=-half&&dy<half&&sx>=0&&sx<64&&sy>=0&&sy<64?tile[sy*64+sx]:0;
            int at=(y*240+x)*4;Assert.AreEqual((byte)((expected&31)*255/31),rgba[at]);Assert.AreEqual((byte)(((expected>>5)&31)*255/31),rgba[at+1]);Assert.AreEqual((byte)0,rgba[at+2]);Assert.AreEqual((byte)255,rgba[at+3]);
        }
    }
}
