using Game.Presentation.BattleAnimations;
using System.Text.Json;
using System.Security.Cryptography;
using System.Buffers.Binary;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class SubspriteCompositionTests
{
    private const ushort Red=0x801f,Green=0x83e0,Blue=0xfc00;
    private static Dictionary<string,SourcePixelAsset> Assets()
    {
        // Two contiguous 32x16 OBJ frames in a 64x16 storage sheet.
        ushort[] sheet=new ushort[1024];
        for(int y=0;y<16;y++)for(int x=0;x<64;x++)sheet[y*64+x]=y<8?Red:Blue;
        return new(){["background"]=new(240,112,Enumerable.Repeat(Green,240*112).ToArray()),
            ["impact"]=new(32,32,new ushort[1024]),["parts"]=new(64,16,sheet)};
    }
    private static SourceVisualSpriteFrame Parent()=>new(4,"subsprite-fixture",new(100,40),default,0,true,false,false,256,"parts",20)
        {Width=32,Height=16,OamRank=4,Subsprites=[new(-16,0,32,16,0,1),new(16,0,32,16,8,1)]};
    private static SourceAnimationFrame Frame(params SourceVisualSpriteFrame[] sprites)=>new(0,true,new PixelPoint[4],[],8,8,[],[]){Sprites=sprites};
    private static ushort Pixel(byte[] rgba,int x,int y)
    {int i=(y*240+x)*4;return (ushort)(0x8000|((rgba[i]*31+127)/255)|(((rgba[i+1]*31+127)/255)<<5)|(((rgba[i+2]*31+127)/255)<<10));}
    [TestMethod]
    public void TableOffsetsAreTopLeftAndFlipsReflectEachRectangleAroundParentCenter()
    {
        var composer=new SourceBattleFrameComposer(Assets(),[]);
        foreach(bool horizontal in new[]{false,true})foreach(bool vertical in new[]{false,true})
        {
            var parent=Parent() with{HFlip=horizontal,VFlip=vertical};var frame=Frame(parent);var rgba=composer.ComposeField(frame);
            int left=horizontal?52:84,top=vertical?24:40;
            for(int y=0;y<112;y++)for(int x=0;x<240;x++)
            {
                ushort expected=Green;
                if(y>=top&&y<top+16&&x>=left&&x<left+64)
                    expected=(x<left+32)^horizontal?Red:Blue;
                Assert.AreEqual(expected,Pixel(rgba,x,y),$"{horizontal}/{vertical} {x},{y}");
            }
            Assert.AreEqual(1,frame.Sprites.Length,"Fragments do not become logical sprite owners.");
        }
    }
    [TestMethod]
    public void FragmentOrderWinsTiesButHardwarePriorityPrecedesParentOamRank()
    {
        var composer=new SourceBattleFrameComposer(Assets(),[]);
        var overlap=Parent() with{Subsprites=[new(0,0,32,16,0,1),new(0,0,32,16,8,1)]};
        Assert.AreEqual(Red,Pixel(composer.ComposeField(Frame(overlap)),105,45));
        var higherPriority=overlap with{Subsprites=[new(0,0,32,16,0,2),new(0,0,32,16,8,1)]};
        Assert.AreEqual(Blue,Pixel(composer.ComposeField(Frame(higherPriority)),105,45));
        var earlier=overlap with{Slot=5,OamRank=3,Subsprites=[new(0,0,32,16,8,1)]};
        Assert.AreEqual(Blue,Pixel(composer.ComposeField(Frame(overlap,earlier)),105,45));
    }
    [TestMethod]
    public void FragmentOamCoordinatesWrapAndUnsupportedAffineTablesFailClosed()
    {
        var composer=new SourceBattleFrameComposer(Assets(),[]);
        var parent=Parent() with{Position=new(504,248),Subsprites=[new(0,0,32,16,0,1)]};
        var rgba=composer.ComposeField(Frame(parent));
        Assert.AreEqual(Red,Pixel(rgba,0,0));Assert.AreEqual(Red,Pixel(rgba,23,7));
        Assert.AreEqual(Green,Pixel(rgba,24,7));Assert.AreEqual(Green,Pixel(rgba,23,8));
        foreach(var invalid in new[]{parent with{Affine=true},parent with{DoubleSize=true},parent with{Subsprites=[new(0,0,24,16,0,1)]},parent with{Subsprites=[new(128,0,32,16,0,1)]},parent with{Subsprites=[new(0,0,32,16,0,4)]}})
            Assert.ThrowsExactly<InvalidDataException>(()=>composer.ComposeField(Frame(invalid)));
    }
    [TestMethod]
    public void SandCrescentFieldPixelsMatchIndependentNativeOamAndSourcePaletteHashes()
    {
        using var motion=SourceVectorFixture.Read("weather-motion-source-vectors.json", "python tests/Reference/Weather/generate_motion.py content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/weather-motion-source-vectors.json");
        using var material=SourceVectorFixture.Read("weather-material-source-vectors.json", "python tests/Reference/Weather/check_vectors.py --reference content/raw/pokefirered/037335f4 --prepare tests/Game.Presentation.Tests/Fixtures");
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var resolved=resources.Resolve(201);var recipe=resolved.Script.SpriteRecipes["gFlyingSandCrescentSpriteTemplate"];
        ushort backdrop=(ushort)material.RootElement.GetProperty("backdropRgb555").GetInt32();
        var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);
        assets["background"]=new(240,112,Enumerable.Repeat((ushort)(backdrop|0x8000),240*112).ToArray());
        assets["impact"]=new(32,32,new ushort[1024]);
        var composer=new SourceBattleFrameComposer(assets,[]);int checkedCases=0;
        foreach(var render in material.RootElement.GetProperty("fragmentRenders").EnumerateArray())
        {
            var c=motion.RootElement.GetProperty("abi")[render.GetProperty("abiCaseIndex").GetInt32()];
            if(c.GetProperty("mode").GetInt32()!=1||c.GetProperty("tile").GetInt32()!=0||c.GetProperty("initialIndex").GetInt32()!=0||c.GetProperty("nullTable").GetBoolean()||c.GetProperty("row").GetProperty("pieces").GetArrayLength()!=2)continue;
            int flip=c.GetProperty("flip").GetInt32();var center=c.GetProperty("center");
            var parent=Parent() with{Template="gFlyingSandCrescentSpriteTemplate",Position=new(center[0].GetInt32(),center[1].GetInt32()),Asset=recipe.Asset,HFlip=(flip&8)!=0,VFlip=(flip&16)!=0,Subsprites=recipe.Subsprites};
            var rgba=composer.ComposeField(Frame(parent));var rgb=new byte[240*112*2];
            for(int y=0;y<112;y++)for(int x=0;x<240;x++)BinaryPrimitives.WriteUInt16LittleEndian(rgb.AsSpan((y*240+x)*2,2),(ushort)(Pixel(rgba,x,y)&0x7fff));
            Assert.AreEqual(render.GetProperty("rgb555FieldSha256").GetString(),Convert.ToHexString(SHA256.HashData(rgb)).ToLowerInvariant(),$"Native ABI case {render.GetProperty("abiCaseIndex")}");
            checkedCases++;
        }
        Assert.IsTrue(checkedCases>=16,"Cover source wrapping and both flip axes without claiming truncated or ignore-priority tables.");
    }

}
