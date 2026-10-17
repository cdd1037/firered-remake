using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SpriteFrameLoopTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static SourceAnimationScript Script()=>AnimationCompositionFixture.Script(111);
    private static SourceSpriteRecipe Recipe(string name,SourceSpriteRecipe source)=>name switch
    {
        "orb"=>source,
        "marked"=>source with{Animations=[[new(0,2,false,false),new(16,1,true,false),new(32,0,false,true),new(48,2,false,false)]],FrameLoops=[[new(3,1,2)]],LoopStarts=[-1]},
        "sequential"=>source with{Animations=[[new(0,1,false,false),new(16,1,false,false)]],FrameLoops=[[new(1,0,1),new(2,1,2)]],LoopStarts=[-1]},
        "limits"=>source with{Animations=[[new(0,0,false,false),new(16,63,false,false)]],FrameLoops=[[new(2,0,1)]],LoopStarts=[-1]},
        "jump"=>source with{Animations=[[new(0,1,false,false),new(16,2,false,false),new(32,1,false,false)]],FrameLoops=[[new(2,0,1)]],LoopStarts=[2]},
        _=>throw new ArgumentOutOfRangeException(nameof(name))
    };
    private static SourceBattleAnimation Runtime(SourceAnimationScript source,SourceSpriteRecipe recipe)
    {
        var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[recipe.Template]=recipe;
        var script=source with{SpriteRecipes=recipes,Commands=[source.Commands[0],source.Commands.Single(c=>c.Op=="createsprite"),new("waitforvisualfinish",[],3),new("end",[],4)]};
        return new(script,new("loops",1,111,new(0),new(0)),[new(new(0),"a",12,0,64,64),new(new(1),"b",12,0,64,64)],false,new Audio());
    }
    [TestMethod]
    [DataRow("orb")][DataRow("marked")][DataRow("sequential")][DataRow("jump")][DataRow("limits")]
    public void SharedFrameEngineMatchesExtractedFiniteLoopZeroDurationFlipAndJumpCadence(string name)
    {
        using var vectors=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/sprite-frame-loop-vectors.json")));
        var source=Script();var runtime=Runtime(source,Recipe(name,source.SpriteRecipes["gEclipsingOrbSpriteTemplate"]));
        for(int i=0;i<3;i++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
        foreach(var row in vectors.RootElement.GetProperty(name).EnumerateArray())
        {
            var v=row.EnumerateArray().Select(n=>n.GetInt32()).ToArray();var frame=runtime.Tick();
            if(v[6]==0){Assert.AreEqual(0,frame.Sprites.Length);continue;}
            var sprite=frame.Sprites.Single();Assert.AreEqual(v[1],sprite.Tile,$"{name} tick {v[0]}");
            Assert.AreEqual(v[2]!=0,sprite.HFlip);Assert.AreEqual(v[3]!=0,sprite.VFlip);
        }
        if(name=="jump"){Assert.IsTrue(runtime.Active);runtime.Cancel();}
        else runtime.Tick();
        Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);
    }
    [TestMethod]
    [DataRow("orb")][DataRow("marked")][DataRow("sequential")][DataRow("jump")][DataRow("limits")]
    public void SharedPlayerCountersAndEndFlagMatchExtractedOriginalFrameInterpreter(string name)
    {
        using var vectors=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/sprite-frame-loop-vectors.json")));
        var source=Script();var player=new SourceSpriteFrameProgram(Recipe(name,source.SpriteRecipes["gEclipsingOrbSpriteTemplate"]));
        foreach(var row in vectors.RootElement.GetProperty(name).EnumerateArray())
        {
            var v=row.EnumerateArray().Select(n=>n.GetInt32()).ToArray();player.Step();
            Assert.AreEqual(v[1],player.Frame.Tile,$"{name} tick {v[0]}");Assert.AreEqual(v[2]!=0,player.Frame.HFlip);Assert.AreEqual(v[3]!=0,player.Frame.VFlip);
            Assert.AreEqual(v[4],player.DelayCounter);Assert.AreEqual(v[5]!=0,player.Ended);Assert.AreEqual(v[7],player.LoopCounter);
        }
    }
    [TestMethod]
    [DataRow(8,0,1)][DataRow(1,1,1)][DataRow(7,0,0)][DataRow(7,0,64)]
    public void InvalidFiniteLoopMetadataFailsBeforeAFrameCanBeRendered(int at,int start,int count)
    {
        var source=Script();var recipe=source.SpriteRecipes["gEclipsingOrbSpriteTemplate"] with{FrameLoops=[[new(at,start,count)]]};
        Assert.Throws<InvalidDataException>(()=>{var runtime=Runtime(source,recipe);for(int i=0;i<4;i++)runtime.Tick();});
    }
    [TestMethod]
    public void DefenseCurlKeepsGrayscaleDuringDeformationAndBothOrbPassesThenRestoresPalette()
    {
        var source=Script();var audio=new Audio();
        foreach(int actor in new[]{0,1,2,3})
        {
            var battlers=Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,12,0,64,64));
            var runtime=new SourceBattleAnimation(source,new("defense",1,111,new(actor),new(actor)),battlers,true,audio);
            int ticks=0,orbFrames=0;bool affine=false,gray=false;
            while(runtime.Active&&ticks++<200)
            {
                var frame=runtime.Tick();gray|=frame.GreyscaleSlots.Contains(actor);
                if(frame.BattlerAffine[actor].DoubleSize){affine=true;CollectionAssert.AreEqual(new[]{actor},frame.GreyscaleSlots);}
                if(frame.Sprites.Any(s=>s.Asset=="eclipsing-orb")){orbFrames++;CollectionAssert.AreEqual(new[]{actor},frame.GreyscaleSlots);}
            }
            Assert.IsFalse(runtime.Active);Assert.IsTrue(affine&&gray);Assert.AreEqual(42,orbFrames);
            Assert.AreEqual(0,runtime.Snapshot().GreyscaleSlots.Length);Assert.IsTrue(runtime.Snapshot().BattlerAffine.All(a=>!a.DoubleSize));
        }
    }
}
