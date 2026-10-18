using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class OverlayMaterialTests
{
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static JsonDocument Vectors()=>JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/overlay-material-source-vectors.json")));
    private static int[] Ints(JsonElement a)=>a.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static string Hash(byte[] bytes)=>Convert.ToHexStringLower(SHA256.HashData(bytes));
    private static ushort Pixel(byte[] rgba,int x,int y)
    {int i=(y*240+x)*4;return (ushort)(((rgba[i]*31+127)/255)|(((rgba[i+1]*31+127)/255)<<5)|(((rgba[i+2]*31+127)/255)<<10));}
    private static SourceAnimationBackgroundSurface Surface(JsonElement row,ushort target)
    {
        var baseColors=row.GetProperty("control_palette_rgb555").EnumerateArray().Take(2).SelectMany(Ints).Select(x=>(ushort)x).ToArray();
        byte[] map=new byte[4096];for(int i=0;i<map.Length;i+=2)BinaryPrimitives.WriteUInt16LittleEndian(map.AsSpan(i),0x2000);
        return new(new(Enumerable.Repeat((byte)0x11,32).ToArray(),map,Enumerable.Repeat(target,48).ToArray()),baseColors);
    }
    private static Dictionary<string,SourcePixelAsset> Assets(ushort target)=>new(){["background"]=new(240,112,Enumerable.Repeat((ushort)(target|0x8000),240*112).ToArray()),["impact"]=new(32,32,new ushort[1024])};
    [TestMethod]
    public void SourceTileNibblesBanksFlipsWrappedScrollAndAllFifteenViewportHashesMatch()
    {
        using var catalog=new SourceAnimationBackgroundCatalog(Path.Combine(Folder,"backgrounds"));using var doc=Vectors();ushort target=doc.RootElement.GetProperty("transparent_second_target_rgb555").GetUInt16();
        foreach(var row in doc.RootElement.GetProperty("materials").EnumerateArray())
        {
            string name=row.GetProperty("name").GetString()!;var material=catalog.Overlay(name);var surface=Surface(row,target);
            CollectionAssert.AreEqual(Ints(row.GetProperty("palette_rgb555")).Select(x=>(ushort)x).ToArray(),material.Palette.ToArray());
            foreach(var sample in row.GetProperty("samples").EnumerateArray())
            {
                int[] p=Ints(sample.GetProperty("point")),v=Ints(sample.GetProperty("value"));
                Assert.AreEqual(new SourceBackgroundPixel(v[2],v[5]),material.Sample(p[0],p[1]),name);
                ushort[] actual=new ushort[1];material.Render(actual,1,1,p[0],p[1],surface,[],[]);
                Assert.AreEqual(v[7]==0?(ushort)0:(ushort)(0x8000|v[6]),actual[0],name);
            }
            foreach(var render in row.GetProperty("renders").EnumerateArray())
            {
                int[] scroll=Ints(render.GetProperty("scroll")),size=Ints(render.GetProperty("size"));int count=size[0]*size[1];byte[] indices=new byte[count*2],colors=new byte[count*2];ushort[] pixels=new ushort[count];
                material.Render(pixels,size[0],size[1],scroll[0],scroll[1],surface,[],[]);
                for(int y=0;y<size[1];y++)for(int x=0;x<size[0];x++)
                {
                    int i=y*size[0]+x;var p=material.Sample(x+scroll[0],y+scroll[1]);indices[i*2]=(byte)p.Bank;indices[i*2+1]=(byte)p.Index;
                    BinaryPrimitives.WriteUInt16LittleEndian(colors.AsSpan(i*2),(pixels[i]&0x8000)==0?target:(ushort)(pixels[i]&0x7fff));
                }
                Assert.AreEqual(render.GetProperty("bank_index_sha256").GetString(),Hash(indices),name);Assert.AreEqual(render.GetProperty("rgb555_sha256").GetString(),Hash(colors),name);
            }
        }
    }
    [TestMethod]
    public void FiveHundredThirtyTwoIndependentRgb555AlphaVectorsMatchLayerComposition()
    {
        using var catalog=new SourceAnimationBackgroundCatalog(Path.Combine(Folder,"backgrounds"));using var doc=Vectors();int count=0;
        foreach(var row in doc.RootElement.GetProperty("materials").EnumerateArray())
        {
            string name=row.GetProperty("name").GetString()!;ushort target=doc.RootElement.GetProperty("transparent_second_target_rgb555").GetUInt16();
            var composer=new SourceBattleFrameComposer(Assets(target),[],backgroundSurface:Surface(row,target),overlays:new Dictionary<string,SourceOverlayAsset>{{name,catalog.Overlay(name)}});
            foreach(var sample in row.GetProperty("blends").EnumerateArray())
            {
                int[] v=Ints(sample);var frame=new SourceAnimationFrame(0,true,new PixelPoint[4],[],v[2],v[3],[],[])
                {Overlay=new(name,v[0],v[1]),BlendEnabled=true,BlendFirstTargets=2,BlendSecondTargets=63,BattlerBackgroundPriorities=[1,1]};
                Assert.AreEqual((ushort)v[6],Pixel(composer.ComposeField(frame),0,0),$"{name} {v[0]},{v[1]} {v[2]}/{v[3]}");count++;
            }
        }
        Assert.AreEqual(532,count);
    }
    [TestMethod]
    public void BothDummySpriteFrameZeroMaterialsMatchAllSourceFlipCombinations()
    {
        using var resources=new SourceAnimationResources(Folder);using var doc=Vectors();const ushort target=0x6cbf;
        foreach(var row in doc.RootElement.GetProperty("sprites").EnumerateArray())
        {
            bool glare=row.GetProperty("name").GetString()=="glare-eye-dot";string name=glare?"small-red-eye":"small-bubbles";var resolved=resources.Resolve(glare?137:296);
            int[] size=Ints(row.GetProperty("size"));var assets=Assets(target);assets.Add(name,resolved.Assets[name]);var composer=new SourceBattleFrameComposer(assets,[]);
            foreach(var expected in row.GetProperty("frames").EnumerateArray())
            {
                int[] flip=Ints(expected.GetProperty("flip"));var sprite=new SourceVisualSpriteFrame(0,"source",new(32,32),default,0,true,flip[0]!=0,false,256,name,0){Width=size[0],Height=size[1],VFlip=flip[1]!=0};
                var rendered=composer.ComposeField(new(0,true,new PixelPoint[4],[],0,0,[],[]){Sprites=[sprite]});
                var colors=expected.GetProperty("rgb555").EnumerateArray().ToArray();
                for(int y=0;y<size[1];y++)for(int x=0;x<size[0];x++)Assert.AreEqual(colors[y*size[0]+x].ValueKind==JsonValueKind.Null?target:colors[y*size[0]+x].GetUInt16(),Pixel(rendered,32-size[0]/2+x,32-size[1]/2+y),name);
            }
        }
    }
}
