using System.Text.Json;
using System.Reflection;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class MusicAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio{public bool IsPlaying=>false;public void Play(SourceAnimationSound c){}public void SetPan(int p,int f){}public void StopAnimationEffects(){}}
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    [TestMethod]
    public void OriginalCMusicTrajectoriesPreserveSignedAccumulationPaletteCyclingAndHandHandoff()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));using var json=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/music-trajectory-source-vectors.json")));int count=0;
        string[] tags=["ANIM_TAG_MUSIC_NOTES","ANIM_TAG_BENT_SPOON","ANIM_TAG_SPHERE_TO_CUBE","ANIM_TAG_LARGE_FRESH_EGG"];
        foreach(var entry in json.RootElement.GetProperty("cases").EnumerateObject())
        {
            var key=entry.Name.Split(':').Select(int.Parse).ToArray();int kind=key[0],actor=key[1],target=key[2],cfg=key[3];if(kind==3)continue;var original=resources.Resolve(kind==0?47:187).Script;
            string template=kind==0?"gWavyMusicNotesSpriteTemplate":kind==1?"gSlowFlyingMusicNotesSpriteTemplate":"gBellyDrumHandSpriteTemplate";
            string[] args=kind==0?[template,"ANIM_TARGET","2",cfg.ToString(),(cfg%4).ToString(),(cfg%2==0?0:12).ToString()]:kind==1?[template,"ANIM_ATTACKER","2",(cfg%2).ToString(),(cfg%4).ToString(),(cfg%4).ToString(),(cfg<4?0:128).ToString()]:[template,"ANIM_ATTACKER","3",cfg.ToString()];
            var script=original with{Commands=[C("loadspritegfx",kind==2?"ANIM_TAG_PURPLE_HAND_OUTLINE":"ANIM_TAG_MUSIC_NOTES"),C("createvisualtask","AnimTask_MusicNotesRainbowBlend","2"),C("createsprite",args),C("waitforvisualfinish"),C("createvisualtask","AnimTask_MusicNotesClearRainbowBlend","2"),C("end")],Labels=new Dictionary<string,int>()};
            var runtime=new SourceBattleAnimation(script,new("music-vector",1,script.MoveId,new(actor),new(target)),Battlers(true),true,new Audio());
            var rows=entry.Value.EnumerateArray().Select(r=>r.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).Where(r=>r[0]>=0&&r[8]==0).ToArray();int seen=0,ticks=0;
            while(runtime.Active&&ticks++<600)
            {
                var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;Assert.IsTrue(seen<rows.Length,entry.Name);var r=rows[seen++];var sprite=frame.Sprites.Single();Assert.AreEqual(new PixelPoint(r[1],r[2]),sprite.Position,entry.Name);Assert.AreEqual(new PixelPoint(r[3],r[4]),sprite.Offset,entry.Name);
                if(kind==2)Assert.AreEqual(r[6]==8,sprite.HFlip,entry.Name);
                else{Assert.IsNotNull(sprite.PaletteSlot);CollectionAssert.AreEqual(frame.SpritePalettes[script.SpriteTags[tags[r[5]-4]]],frame.SpritePaletteSlots[sprite.PaletteSlot.Value]);}
            }
            Assert.IsFalse(runtime.Active,entry.Name);Assert.AreEqual(rows.Length,seen,entry.Name);count++;
        }
        Assert.AreEqual(256,count);
    }
    [TestMethod][DataRow(47)][DataRow(187)][DataRow(320)]
    public void CompleteMusicRootsComposeAllActorsWithSourcePaletteCleanup(int id)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(id);
        foreach(bool doubles in new[]{false,true})foreach(int actor in Enumerable.Range(0,doubles?4:2))
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8421,240*112).ToArray());foreach(var b in battlers)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0xFFFF,4096).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var runtime=new SourceBattleAnimation(resolved.Script,new("music-root",1,id,new(actor),new(id==187?actor:actor^1)),battlers,doubles,new Audio());int ticks=0,sprites=0;
            while(runtime.Active&&ticks++<1500){var frame=runtime.Tick();sprites+=frame.Sprites.Length;Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length);}
            Assert.IsFalse(runtime.Active);Assert.IsTrue(sprites>0);Assert.AreEqual(0,runtime.Snapshot().SpritePalettes.Count);
        }
    }
    [TestMethod]
    public void MusicPaletteCycleCounterWrapsAtSignedSixteenBitBoundary()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var source=resources.Resolve(47).Script;
        var script=source with{Commands=[C("loadspritegfx","ANIM_TAG_MUSIC_NOTES"),C("createvisualtask","AnimTask_MusicNotesRainbowBlend","2"),C("createsprite","gWavyMusicNotesSpriteTemplate","ANIM_TARGET","2","0","0","32767"),C("waitforvisualfinish"),C("end")],Labels=new Dictionary<string,int>()};
        var runtime=new SourceBattleAnimation(script,new("music-wrap",1,47,new(0),new(1)),Battlers(true),true,new Audio());SourceAnimationFrame frame;do{frame=runtime.Tick();}while(frame.Sprites.Length==0);
        var sprites=(System.Collections.IEnumerable)typeof(SourceBattleAnimation).GetField("_sprites",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(runtime)!;var instance=sprites.Cast<object>().Single();var counter=instance.GetType().GetField("_blendTimer",BindingFlags.NonPublic|BindingFlags.Instance)!;counter.SetValue(instance,32767);
        using var json=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/music-trajectory-source-vectors.json")));
        foreach(var item in json.RootElement.GetProperty("cases").GetProperty("3:0:1:0").EnumerateArray().Skip(2))
        {
            var row=item.EnumerateArray().Select(v=>v.GetInt32()).ToArray();frame=runtime.Tick();var sprite=frame.Sprites.Single();Assert.AreEqual(row[11],(int)counter.GetValue(instance)!);Assert.AreEqual(new PixelPoint(row[1],row[2]),sprite.Position);Assert.AreEqual(new PixelPoint(row[3],row[4]),sprite.Offset);Assert.AreEqual(4,sprite.PaletteSlot);
        }
        runtime.Cancel();
    }
    [TestMethod]
    public void MusicTagDependenciesAndSignedAffineLiteralsAreExact()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var script=resources.Resolve(47).Script;var recipe=script.SpriteRecipes["gWavyMusicNotesSpriteTemplate"];
        Assert.AreEqual(-12,recipe.AffineAnimations[0][1].X);Assert.AreEqual(-12,recipe.AffineAnimations[0][1].Y);
        var tags=new Dictionary<string,int>(script.SpriteTags);tags.Remove("ANIM_TAG_BENT_SPOON");Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script with{SpriteTags=tags},new("bad-music",1,47,new(0),new(1)),Battlers(false),false,new Audio()));
    }
}
