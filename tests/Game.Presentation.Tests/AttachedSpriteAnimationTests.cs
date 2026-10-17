using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class AttachedSpriteAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static SourceAnimationScript Script(int move)=>AnimationCompositionFixture.Script(move);
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static JsonDocument Vectors()=>JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/attached-physical-original-vectors.json")));
    [TestMethod]
    [DataRow("EyeSparkle",0)][DataRow("EyeSparkle",1)][DataRow("EyeSparkle",2)][DataRow("EyeSparkle",3)]
    [DataRow("AngerMark",0)][DataRow("AngerMark",1)][DataRow("AngerMark",2)][DataRow("AngerMark",3)]
    public void AttachedCallbacksMatchOriginalPositionsArgumentMutationFramesAffineAndRetirement(string kind,int actor)
    {
        bool anger=kind=="AngerMark";int move=anger?207:321;var source=Script(move);using var vectors=Vectors();
        foreach(int target in new[]{actor^1,actor^2})
        for(int participant=0;participant<(anger?2:1);participant++)
        {
            string template=anger?"gAngerMarkSpriteTemplate":"gEyeSparkleSpriteTemplate";
            string[] args=anger?[template,"ANIM_TARGET","2",participant.ToString(),"20","-28"]:[template,"ANIM_ATTACKER","0","-16","-8"];
            var script=source with{Commands=[new("loadspritegfx",[anger?"ANIM_TAG_ANGER":"ANIM_TAG_EYE_SPARKLE"],1),new("createsprite",args,2),new("waitforvisualfinish",[],3),new("end",[],4)]};
            var runtime=new SourceBattleAnimation(script,new("attached",1,move,new(actor),new(target)),Battlers(true),true,new Audio());
            for(int i=0;i<3;i++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            string key=anger?$"{actor}:{target}:{participant}":$"{actor}:{target}";
            foreach(var row in vectors.RootElement.GetProperty(kind).GetProperty(key).EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[0]<0)continue;
                var frame=runtime.Tick();Assert.AreEqual(v[3],(int)frame.AnimationArguments[1],$"{kind} {key} tick {v[0]}");
                if(v[11]==0){Assert.AreEqual(0,frame.Sprites.Length);continue;}
                var sprite=frame.Sprites.Single();Assert.AreEqual(new PixelPoint(v[1],v[2]),sprite.Position);Assert.AreEqual(default,sprite.Offset);
                Assert.AreEqual(v[4],sprite.Tile);Assert.AreEqual(v[5]!=0,sprite.HFlip);Assert.AreEqual(v[6]!=0,sprite.VFlip);
                Assert.AreEqual(v[9],sprite.MatrixA);Assert.AreEqual(v[10],sprite.MatrixD);Assert.AreEqual(0,sprite.MatrixB);Assert.AreEqual(0,sprite.MatrixC);
                Assert.AreEqual(anger,sprite.Affine);Assert.IsFalse(sprite.DoubleSize);Assert.AreEqual(16,sprite.Width);Assert.AreEqual(16,sprite.Height);
            }
            runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);
        }
    }
    [TestMethod]
    public void AngerMarkReusesSpriteAffineProgramIncludingBeginPhaseAndEndBeforeCallbackRetirement()
    {
        var recipe=Script(207).SpriteRecipes["gAngerMarkSpriteTemplate"];var player=new SourceSpriteAffineProgram(recipe.AffineAnimations.Single());using var vectors=Vectors();
        foreach(var row in vectors.RootElement.GetProperty("AngerMark").GetProperty("0:1:0").EnumerateArray())
        {
            var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[0]>=0)player.Step();
            Assert.AreEqual(v[7],player.XScale);Assert.AreEqual(v[8],player.YScale);Assert.AreEqual(v[9],player.Matrix.A);Assert.AreEqual(v[10],player.Matrix.D);Assert.AreEqual(v[13]!=0,player.Ended);
        }
    }
    [TestMethod]
    [DataRow(207)][DataRow(321)]
    public void CompleteAttachedRootsFinishAcrossEveryPositionAndTargetSideWithGeneratedAssets(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})
        for(int actor=0;actor<(doubles?4:2);actor++)
        foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,new("full",1,move,new(actor),new(target)),Battlers(doubles),doubles,audio);
            int ticks=0;bool sawAttached=false;
            while(runtime.Active&&ticks++<500)
            {
                var frame=runtime.Tick();
                foreach(var sprite in frame.Sprites)
                {
                    if(sprite.Asset is {} asset){Assert.IsTrue(resolved.Assets.ContainsKey(asset));sawAttached|=asset is "anger" or "eye-sparkle";}
                    else Assert.IsFalse(sprite.Visible,"Source palette/controller sprites do not draw pixels.");
                }
            }
            Assert.IsFalse(runtime.Active);Assert.IsTrue(sawAttached);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);
            Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));Assert.IsTrue(runtime.Snapshot().BattlerAffine.All(a=>!a.DoubleSize));
            Assert.AreEqual(move==207?5:9,audio.Cues.Count);
        }
    }
    [TestMethod]
    [DataRow(207,0)][DataRow(207,20)][DataRow(207,145)][DataRow(321,15)][DataRow(321,70)]
    public void CancellationRetiresAttachedSpritesAndRestoresPaletteAndBattlerState(int move,int ticks)
    {
        var runtime=new SourceBattleAnimation(Script(move),new("cancel",1,move,new(0),new(1)),Battlers(false),false,new Audio());
        for(int i=0;i<ticks;i++)runtime.Tick();runtime.Cancel();runtime.Cancel();var frame=runtime.Snapshot();
        Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,frame.Sprites.Length);
        Assert.AreEqual(0,frame.PaletteBlends.Length);Assert.IsTrue(frame.Offsets.All(p=>p==default));Assert.IsTrue(frame.BattlerAffine.All(a=>!a.DoubleSize));
    }
    [TestMethod]
    public void AttachedRecipeValidationRejectsUnknownCallbackOrMissingAffineProgram()
    {
        var source=Script(207);var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);
        recipes["gAngerMarkSpriteTemplate"]=recipes["gAngerMarkSpriteTemplate"] with{AffineAnimations=[]};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpriteRecipes=recipes},new("bad",1,207,new(0),new(1)),Battlers(false),false,new Audio()));
        source=Script(321);recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes["gEyeSparkleSpriteTemplate"]=recipes["gEyeSparkleSpriteTemplate"] with{Callback="UnknownEye"};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpriteRecipes=recipes},new("bad",1,321,new(0),new(1)),Battlers(false),false,new Audio()));
    }
}
