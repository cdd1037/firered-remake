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
        var indexed=resources.Asset("impact");var rgb=SourceHitAnimationCatalog.Asset(manifest,Path.Combine(Folder,"hit"),"impact");
        Assert.AreEqual(1024,indexed.PaletteIndices.Length);Assert.AreEqual(16,indexed.Palette.Length);Assert.AreEqual(resources.Resolve(1).Script.SpriteTags["ANIM_TAG_IMPACT"],indexed.PaletteTag);
        for(int i=0;i<1024;i++){int index=indexed.PaletteIndices[i];Assert.AreEqual(index==0?0:0x8000|indexed.Palette[index],(int)indexed.Pixels[i]);Assert.AreEqual((rgb.Pixels[i]&0x8000)==0?0:(int)rgb.Pixels[i],(int)indexed.Pixels[i]);}
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
