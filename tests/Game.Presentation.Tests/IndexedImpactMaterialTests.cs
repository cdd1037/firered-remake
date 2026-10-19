using System.Buffers.Binary;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class IndexedImpactMaterialTests
{
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private sealed class Audio:ISourceAnimationAudio{public bool IsPlaying=>false;public void Play(SourceAnimationSound sound){}public void StopAnimationEffects(){}}
    [TestMethod]
    public void CanonicalImpactRetainsNativeIndicesPaletteAndAllVisibleRgbPixels()
    {
        using var resources=new SourceAnimationResources(Folder);using var manifest=SourceHitAnimationCatalog.Read(Path.Combine(Folder,"hit"));
        var indexed=resources.Asset("impact");var row=manifest.RootElement.GetProperty("indexedAssets").EnumerateArray().Single(a=>a.GetProperty("id").GetString()=="impact");
        Assert.IsFalse(manifest.RootElement.GetProperty("assets").EnumerateArray().Any(a=>a.GetProperty("id").GetString()=="impact"));
        IndexedSpriteExpectations.AssertMatches(row,Path.Combine(Folder,"hit"),indexed);
        Assert.AreEqual(1024,indexed.PaletteIndices.Length);Assert.AreEqual(16,indexed.Palette.Length);Assert.AreEqual(resources.Resolve(1).Script.SpriteTags["ANIM_TAG_IMPACT"],indexed.PaletteTag);
        Assert.AreSame(indexed,resources.Resolve(1).Assets["impact"]);Assert.AreSame(indexed,resources.Resolve(295).Assets["impact"]);
    }
    [TestMethod,DataRow(1),DataRow(295)]
    public void SharedLoadCommandBindsCanonicalImpactPaletteForBasicHitAndLusterPurge(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var full=resources.Resolve(move);var surface=resources.CreateBackgroundSurface(0);int tag=full.Script.SpriteTags["ANIM_TAG_IMPACT"];
        var bodies=new[]{new AnimationBattler(new(0),"player",0,0,64,64),new AnimationBattler(new(1),"opponent",8,0,64,64)};
        var runtime=new SourceBattleAnimation(full.Script,new("impact",1,move,new(0),new(1)){VisualSeed=0x12345678},bodies,false,new Audio(),Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray(),spriteMaterials:full.Assets);
        int ticks=0;while(runtime.Active&&!runtime.Snapshot().SpritePalettes.ContainsKey(tag)&&ticks++<10)runtime.Tick();
        Assert.IsTrue(runtime.Snapshot().SpritePalettes.ContainsKey(tag));CollectionAssert.AreEqual(full.Assets["impact"].Palette,runtime.Snapshot().SpritePalettes[tag]);
        runtime.Cancel();Assert.AreEqual(0,runtime.Snapshot().SpritePalettes.Count);
    }
}

// This test-only decoder is independent of the production reader. Import checks
// separately compare these packed bytes with original PNG/JASC pixels.
internal static class IndexedSpriteExpectations
{
    public static void AssertMatches(JsonElement row,string folder,SourcePixelAsset loaded)
    {
        int width=row.GetProperty("width").GetInt32(),height=row.GetProperty("height").GetInt32();
        byte[] packed=File.ReadAllBytes(Path.Combine(folder,row.GetProperty("indicesPath").GetString()!));
        byte[] colors=File.ReadAllBytes(Path.Combine(folder,row.GetProperty("palettePath").GetString()!));
        Assert.AreEqual(width,loaded.Width);Assert.AreEqual(height,loaded.Height);Assert.AreEqual(width*height/2,packed.Length);Assert.AreEqual(32,colors.Length);
        var palette=Enumerable.Range(0,16).Select(i=>BinaryPrimitives.ReadUInt16LittleEndian(colors.AsSpan(i*2))).ToArray();
        var indices=new byte[width*height];var pixels=new ushort[indices.Length];
        for(int y=0;y<height;y++)for(int x=0;x<width;x++)
        {
            int offset=((y/8)*(width/8)+x/8)*32+(y%8)*4+(x%8)/2;
            int index=(packed[offset]>>((x&1)*4))&15;
            indices[y*width+x]=(byte)index;pixels[y*width+x]=index==0?(ushort)0:(ushort)(palette[index]|32768);
        }
        CollectionAssert.AreEqual(palette,loaded.Palette);CollectionAssert.AreEqual(indices,loaded.PaletteIndices);CollectionAssert.AreEqual(pixels,loaded.Pixels);
    }
}
