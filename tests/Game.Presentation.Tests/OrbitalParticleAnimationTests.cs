using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class OrbitalParticleAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],64,new[]{64,48,32,56}[i])).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor=0,int target=1)=>new("orbital-root",1,move,new(actor),new(target)){VisualSeed=0x12345678};
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static void Clean(SourceBattleAnimation runtime)
    {
        Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);var f=runtime.Snapshot();Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);
        Assert.IsTrue(f.Offsets.All(p=>p==default));Assert.IsFalse(f.BlendEnabled);Assert.IsTrue(f.BattlerAffine.All(a=>!a.DoubleSize&&a.XScale==256&&a.YScale==256));
        Assert.AreEqual(0,f.MoveBackground!.ScrollX);Assert.AreEqual(0,f.MoveBackground.ScrollY);Assert.AreEqual(1,f.MoveBackground.ScreenSize);
    }
    [TestMethod][DataRow(237)][DataRow(310)][DataRow(327)][DataRow(345)]
    public void CompleteRootsResolveRenderReplayAndReleaseTheirSourceResources(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        string[] expectedAssets=move switch{237=>["impact","red-orb"],310=>["impact","sweat-bead"],327=>["impact"],_=>["leaf","razor-leaf","impact"]};
        CollectionAssert.AreEquivalent(expectedAssets,resolved.Assets.Keys.ToArray());
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x801f,240*112).ToArray()));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();var cue=Cue(move,actor,target);var scene=new SourceBattleAnimation.SpriteScene();
            var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio,spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio(),spriteMaterials:assets);
            int ticks=0,peak=0;bool visible=false,pixels=false,scroll=false,palette=false,flip=false;var templates=new HashSet<string>();
            while(runtime.Active&&ticks++<500)
            {
                var f=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));peak=Math.Max(peak,f.Sprites.Count(s=>s.Affine));
                foreach(var s in f.Sprites){templates.Add(s.Template);if(s.Asset is not null)Assert.IsNotNull(s.Material);}
                visible|=f.Sprites.Any(s=>s.Visible);flip|=f.Sprites.Any(s=>s.Template=="gSprayWaterDropletSpriteTemplate"&&s.HFlip);
                scroll|=f.MoveBackground!.Id==17&&f.MoveBackground.ScrollY!=0;
                if(move==345)foreach(string tag in new[]{"ANIM_TAG_LEAF","ANIM_TAG_RAZOR_LEAF"})if(f.SpritePalettes.TryGetValue(resolved.Script.SpriteTags[tag],out var colors))palette|=!colors.SequenceEqual(resolved.Script.SpritePalettes[resolved.Script.SpriteTags[tag]]);
                if((ticks&3)==0&&f.Sprites.Any(s=>s.Visible))
                {var before=JsonSerializer.Serialize(runtime.Snapshot());var rendered=composer.ComposeField(f);pixels|=!rendered.SequenceEqual(composer.ComposeField(f with{Sprites=[]}));Assert.AreEqual(before,JsonSerializer.Serialize(runtime.Snapshot()));}
            }
            Clean(runtime);Clean(replay);Assert.AreEqual(0,scene.Count);Assert.IsTrue(visible);Assert.IsTrue(pixels);Assert.IsTrue(audio.Cues.Count>0);
            if(move==237){Assert.IsTrue(templates.Contains("gHiddenPowerOrbSpriteTemplate"));Assert.IsTrue(templates.Contains("gHiddenPowerOrbScatterSpriteTemplate"));Assert.IsTrue(peak<=14);}
            if(move==310)Assert.IsTrue(flip);if(move==327)Assert.IsTrue(scroll);if(move==345)Assert.IsTrue(palette);
        }
    }
    [TestMethod][DataRow(237)][DataRow(310)][DataRow(327)][DataRow(345)]
    public void CancellationAtEverySourceTickClearsSpritesMatricesPaletteAndBackground(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);var battlers=Battlers(true);var length=new SourceBattleAnimation(resolved.Script,Cue(move),battlers,true,new Audio(),spriteMaterials:resolved.Assets);int ticks=0;while(length.Active&&ticks++<500)length.Tick();Assert.IsFalse(length.Active);
        for(int stop=0;stop<=ticks;stop++)
        {
            var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(resolved.Script,Cue(move),battlers,true,new Audio(),spriteScene:scene,spriteMaterials:resolved.Assets);
            for(int t=0;t<stop&&runtime.Active;t++)runtime.Tick();runtime.Cancel();runtime.Cancel();Clean(runtime);Assert.AreEqual(0,scene.Count);
            Assert.IsTrue(runtime.Snapshot().SpritePaletteSlots.All(p=>p.All(c=>c==0)));
        }
    }
    [TestMethod]
    public void SharedLeavesHaveOneIndexedOwnerAndMissingPaletteFailsBeforeInvocation()
    {
        using var resources=new SourceAnimationResources(Folder);var magical=resources.Resolve(345);var razor=resources.Resolve(75);
        foreach(string name in new[]{"leaf","razor-leaf"})
        {
            var asset=magical.Assets[name];Assert.AreEqual(asset.Width*asset.Height,asset.PaletteIndices!.Length);Assert.AreEqual(16,asset.Palette!.Length);
            if(razor.Assets.TryGetValue(name,out var shared)){CollectionAssert.AreEqual(asset.Pixels,shared.Pixels);CollectionAssert.AreEqual(asset.PaletteIndices,shared.PaletteIndices!);CollectionAssert.AreEqual(asset.Palette,shared.Palette!);Assert.AreEqual(asset.PaletteTag,shared.PaletteTag);}
        }
        int leaf=magical.Script.SpriteTags["ANIM_TAG_LEAF"];var palettes=magical.Script.SpritePalettes.Where(p=>p.Key!=leaf).ToDictionary(p=>p.Key,p=>p.Value);
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(magical.Script with{SpritePalettes=palettes},Cue(345),Battlers(false),false,new Audio()));
        var physical=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Folder,"physical.json")));Assert.IsFalse(physical.RootElement.GetProperty("assets").EnumerateArray().Any(a=>a.GetProperty("id").GetString() is "leaf" or "razor-leaf"));
    }
    [TestMethod]
    public void UnsafeSourceAbiSeedRecipeAndStopClosureFailClosed()
    {
        var hidden=AnimationCompositionFixture.Script(237);var astonish=AnimationCompositionFixture.Script(310);var magical=AnimationCompositionFixture.Script(345);
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(astonish,Cue(310) with{VisualSeed=null},Battlers(false),false,new Audio()));
        foreach(var source in new[]{hidden,magical})Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{Commands=source.Commands.Where(c=>c.Op!="setarg").ToArray()},Cue(source.MoveId),Battlers(false),false,new Audio()));
        var recipes=hidden.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes["gHiddenPowerOrbSpriteTemplate"]=recipes["gHiddenPowerOrbSpriteTemplate"] with{AffineDoubleSize=true};
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(hidden with{SpriteRecipes=recipes},Cue(237),Battlers(false),false,new Audio()));
        var sky=AnimationCompositionFixture.Script(327);var commands=sky.Commands.Select(c=>c.Op=="createvisualtask"&&c.Args[0]=="AnimTask_MoveSkyUppercutBg"?c with{Args=[c.Args[0],"5","32768"]}:c).ToArray();
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(sky with{Commands=commands},Cue(327),Battlers(false),false,new Audio()));
    }
    [TestMethod]
    public void UnusedCatalogHelpersNeedOnlyAbiWhileInvokedCyclesAndRepeatedEmittersFailClosed()
    {
        var source=AnimationCompositionFixture.Script(237);var orbit=source.Commands.First(c=>c.Op=="createsprite"&&c.Args[0]=="gHiddenPowerOrbSpriteTemplate");
        var helper=new Dictionary<string,SourceAnimationCommand[]>{{"UnusedOrbit",[orbit,C("return")]}};
        var unused=source with{Commands=[C("end")],Subroutines=helper};var runtime=new SourceBattleAnimation(unused,Cue(237),Battlers(false),false,new Audio());runtime.Tick();Assert.IsFalse(runtime.Active);
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(unused with{Commands=[C("call","UnusedOrbit"),C("end")]},Cue(237),Battlers(false),false,new Audio()));
        var repeated=source.Commands.ToList();repeated.Insert(0,orbit);
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{Commands=repeated.ToArray()},Cue(237),Battlers(false),false,new Audio()));
        var leaf=AnimationCompositionFixture.Script(345);var cycle=leaf.Commands.First(c=>c.Op=="createvisualtask"&&c.Args[0]=="AnimTask_CycleMagicalLeafPal");
        var leafUnused=leaf with{Commands=[C("end")],Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"UnusedLeaf",[cycle,C("return")]}}};
        var leafRuntime=new SourceBattleAnimation(leafUnused,Cue(345),Battlers(false),false,new Audio());leafRuntime.Tick();Assert.IsFalse(leafRuntime.Active);
    }
    [TestMethod]
    public void RetainedNonAffineOwnerSurvivesOrbitalCancellation()
    {
        var bolt=AnimationCompositionFixture.Script(84);var battlers=Battlers(true);var scene=new SourceBattleAnimation.SpriteScene();
        var script=bolt with{Commands=[C("loadspritegfx","ANIM_TAG_SPARK"),C("createvisualtask","AnimTask_ElectricBolt","5","0","-44","0"),C("waitforvisualfinish"),C("end")],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        var old=new SourceBattleAnimation(script,Cue(84),battlers,true,new Audio(),spriteScene:scene);int ticks=0;while(old.Active&&ticks++<40)old.Tick();Assert.IsTrue(old.HasResidualSprites);
        var hidden=AnimationCompositionFixture.Script(237);var current=new SourceBattleAnimation(hidden,Cue(237),battlers,true,new Audio(),spriteScene:scene);
        current.Tick();current.Cancel();Assert.IsTrue(old.HasResidualSprites);Assert.AreEqual(0,current.ActiveVisualCount);
        ticks=0;while(old.HasResidualSprites&&ticks++<40)scene.TickIdle(battlers,true);Assert.AreEqual(0,scene.Count);
    }

}
