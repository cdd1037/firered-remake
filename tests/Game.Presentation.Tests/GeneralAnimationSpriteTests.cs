using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class GeneralAnimationSpriteTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    [TestMethod]
    public void GrantingStarsUseSignedFixedPointAndTwoStepCallbackRetirement()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"content/generated/battle/animations/general.json")));
        var recipes=SourceHitAnimationCatalog.Recipes(doc.RootElement.GetProperty("spriteRecipes"));
        foreach(int actor in new[]{0,1,2,3})
        {
            var battlers=Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
            var script=resources.Scripts[52] with{MoveId=0,Label="GeneralStarVector",SpriteRecipes=recipes,Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),Commands=[new("loadspritegfx",["ANIM_TAG_SPARKLE_2"],1),new("createsprite",["gGrantingStarsSpriteTemplate","ANIM_ATTACKER","2","-15","0","0","-192","32","60"],2),new("waitforvisualfinish",[],3),new("end",[],4)]};
            var runtime=new SourceBattleAnimation(script,new("stars",1,0,new(actor),new(actor^1)),battlers,true,new Audio());
            for(int i=0;i<3;i++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            for(int tick=1;tick<=60;tick++)
            {var s=runtime.Tick().Sprites.Single();var center=battlers[actor].Center(true,true);Assert.AreEqual(new PixelPoint(center.X+(actor%2==0?-15:15),center.Y),s.Position);Assert.AreEqual(new PixelPoint((-192*tick)>>8,(32*tick)>>8),s.Offset);}
            Assert.AreEqual(new PixelPoint(-45,7),runtime.Tick().Sprites.Single().Offset);
            Assert.AreEqual(0,runtime.Tick().Sprites.Length);
        }
    }
    [TestMethod]
    public void HeldItemGeneralCompletesAndComposesForEveryCanonicalActor()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var resolved=resources.ResolveGeneral("General_HeldItemEffect");Assert.AreEqual(0,resolved.Script.MoveId);
        CollectionAssert.AreEquivalent(new[]{"sparkle-2","thin-ring","impact"},resolved.Assets.Keys.ToArray());
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var battlers=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
            var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));
            foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x801f,4096).ToArray()));
            var cue=new CommittedMoveAnimation("held-item",1,0,new(actor),new(actor)){GeneralAnimationLabel="General_HeldItemEffect"};
            var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio());var composer=new SourceBattleFrameComposer(assets,battlers);
            int ticks=0;var seen=new HashSet<string>();
            while(runtime.Active&&ticks++<1024)
            {var frame=runtime.Tick();foreach(var sprite in frame.Sprites)if(sprite.Asset is string asset)seen.Add(asset);Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length);}
            Assert.IsFalse(runtime.Active);CollectionAssert.AreEquivalent(new[]{"sparkle-2","thin-ring"},seen.ToArray());
            var final=runtime.Snapshot();Assert.AreEqual(0,final.Sprites.Length);Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.AreEqual(0,final.PaletteBlends.Length);Assert.IsFalse(runtime.GraphicsLoaded);
        }
    }
    [TestMethod]
    public void NamedGeneralResolvesWithOnlyGeneratedResources()
    {
        string source=Path.Combine(TestAssets.Root,"content/generated/battle/animations"),folder=Path.Combine(Path.GetTempPath(),"general-generated-"+Guid.NewGuid().ToString("N"));
        try
        {
            foreach(string file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories))
            {string target=Path.Combine(folder,Path.GetRelativePath(source,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);}
            using var resources=new SourceAnimationResources(folder);Assert.IsTrue(resources.GeneralScripts.ContainsKey("General_HeldItemEffect"));
            var result=resources.ResolveGeneral("General_HeldItemEffect");Assert.AreEqual(3,result.Assets.Count);Assert.IsFalse(Directory.Exists(Path.Combine(folder,"content/raw")));
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
}
