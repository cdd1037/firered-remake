using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class WaterPulseAnimationTests
{
    private const string Bubble="gWaterPulseBubbleSpriteTemplate",Ring="gWaterPulseRingSpriteTemplate",Child="gWaterPulseRingBubbleSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles=true)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static SourceAnimationScript Script(){using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(352).Script;return script with{SpriteRecipes=script.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value)};}
    private static CommittedMoveAnimation Cue(int actor=0,int target=1,uint? seed=0)=>new("water-pulse",1,352,new(actor),new(target)){VisualSeed=seed};
    private static JsonDocument Vectors()=>JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/water-pulse-source-vectors.json")));
    private static uint? RandomState(SourceBattleAnimation runtime)
    {
        var f=typeof(SourceBattleAnimation).GetField("_visualRandom",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(f);
        if(f.GetValue(runtime) is not SourceVisualRandom rng)return null;
        var state=typeof(SourceVisualRandom).GetField("_state",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(state);return (uint)state.GetValue(rng)!;
    }
    private static SourceAnimationScript Isolated(string template,string[] args,int count=1,int delay=5)
    {
        var list=new List<SourceAnimationCommand>{new("loadspritegfx",["ANIM_TAG_SMALL_BUBBLES"],1),new("loadspritegfx",["ANIM_TAG_BLUE_RING_2"],2)};
        for(int i=0;i<count;i++){if(i!=0)list.Add(new("delay",[delay.ToString(CultureInfo.InvariantCulture)],3));list.Add(new("createsprite",[template,"ANIM_TARGET","2",..args],4));}
        list.Add(new("waitforvisualfinish",[],5));list.Add(new("end",[],6));return Script() with{Commands=list.ToArray(),Labels=new Dictionary<string,int>()};
    }
    private static SourceAnimationFrame FirstSprite(SourceBattleAnimation runtime)
    {for(int i=0;i<20;i++){var frame=runtime.Tick();if(frame.Sprites.Length>0)return frame;}throw new InvalidOperationException("Source sprite did not appear.");}
    private static SourceAnimationFrame Advance(SourceBattleAnimation runtime,SourceBattleAnimation.SpriteScene scene)=>runtime.Active?runtime.Tick():scene.TickIdle(Battlers(),true);

    [TestMethod]
    public void FiveAuthoredFloatTrajectoriesIgnoreTheUnusedSeventhArgument()
    {
        using var doc=Vectors();var configurations=doc.RootElement.GetProperty("floatArgs").EnumerateArray().ToArray();
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateObject())
        {
            if(entry.Value.GetProperty("kind").GetString()!="float")continue;
            int index=entry.Value.GetProperty("config")[0].GetInt32();var args=configurations[index].EnumerateArray().Select(x=>x.GetInt32().ToString(CultureInfo.InvariantCulture)).ToArray();
            var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(Isolated(Bubble,args),Cue(seed:null),Battlers(),true,new Audio(),spriteScene:scene);var frame=FirstSprite(runtime);int tick=0;
            foreach(var row in entry.Value.GetProperty("states").EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[1]<0||v[4]!=1)continue;
                while(tick<v[1]){frame=Advance(runtime,scene);tick++;}
                var sprite=frame.Sprites.Single();Assert.AreEqual(new PixelPoint(v[6],v[7]),sprite.Position);Assert.AreEqual(new PixelPoint(v[8],v[9]),sprite.Offset);Assert.AreEqual(v[16],sprite.Tile);Assert.IsFalse(sprite.Affine);Assert.AreEqual(8,sprite.Width);Assert.AreEqual(8,sprite.Height);
                Assert.IsNull(RandomState(runtime));
            }
            while(runtime.Active)runtime.Tick();Assert.IsFalse(runtime.HasResidualSprites);
        }
    }

    [TestMethod]
    public void RingsRawChildrenAndRngOrderMatchExtractedSourceIncludingLowerSlotReuse()
    {
        using var doc=Vectors();var seeds=doc.RootElement.GetProperty("seeds").EnumerateArray().Select(x=>x.GetUInt32()).ToArray();
        var geometry=doc.RootElement.GetProperty("geometries").EnumerateArray().Select(x=>x.EnumerateArray().Select(y=>y.GetInt32()).ToArray()).ToArray();
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateObject())
        {
            string kind=entry.Value.GetProperty("kind").GetString()!;if(kind is not ("ring" or "schedule" or "pair"))continue;
            var config=entry.Value.GetProperty("config").EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(kind=="schedule"&&config[2]!=0)continue;
            int actor=geometry[config[0]][0],target=geometry[config[0]][1];uint seed=seeds[config[1]];
            var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(Isolated(Ring,["0","0","40","15"],kind=="schedule"?3:kind=="pair"?2:1,kind=="pair"?18:5),Cue(actor,target,seed),Battlers(),true,new Audio(),spriteScene:scene);
            var frames=new List<SourceAnimationFrame>{FirstSprite(runtime)};
            while((runtime.Active||runtime.HasResidualSprites)&&frames.Count<100)frames.Add(Advance(runtime,scene));
            Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.HasResidualSprites);
            var allocations=entry.Value.GetProperty("allocations").EnumerateArray().Select(x=>x.EnumerateArray().Select(y=>y.GetInt32()).ToArray()).ToDictionary(x=>x[0]);
            foreach(var row in entry.Value.GetProperty("states").EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[1]<0||v[4]!=1)continue;
                int spriteKind=allocations[v[2]][2];if(spriteKind==2&&v[5]==1)continue; // Eager Begin precedes this tick's normal callback/player pass.
                var actual=frames[v[1]].Sprites.Single(x=>x.Slot==v[3]+4);string message=$"{entry.Name} tick{v[1]} slot{v[3]}";
                Assert.AreEqual(new PixelPoint(v[6],v[7]),actual.Position,message);Assert.AreEqual(new PixelPoint(v[8],v[9]),actual.Offset,message);Assert.AreEqual(v[16],actual.Tile,message);Assert.AreEqual(v[22],actual.MatrixA,message);Assert.AreEqual(v[22],actual.MatrixD,message);Assert.AreEqual(0,actual.MatrixB,message);Assert.AreEqual(0,actual.MatrixC,message);Assert.AreEqual(v[26],actual.Subpriority,message);
                Assert.AreEqual(spriteKind==2,actual.DoubleSize,message);Assert.IsTrue(actual.Affine,message);
            }
            var rendered=entry.Value.GetProperty("rendered").EnumerateArray().Select(x=>x.EnumerateArray().Select(y=>y.GetInt32()).ToArray()).Where(x=>x[0]>=0).GroupBy(x=>x[0]);
            foreach(var rows in rendered)
            {
                var actual=frames[rows.Key].Sprites.OrderBy(x=>x.Slot).ToArray();var expected=rows.OrderBy(x=>x[1]).ToArray();Assert.AreEqual(expected.Length,actual.Length,$"{entry.Name} tick{rows.Key}");
                for(int i=0;i<expected.Length;i++)
                {
                    var v=expected[i];var sprite=actual[i];Assert.AreEqual(v[1]+4,sprite.Slot);Assert.AreEqual(new PixelPoint(v[3],v[4]),sprite.Position);Assert.AreEqual(new PixelPoint(v[5],v[6]),sprite.Offset);Assert.AreEqual(v[7],sprite.Tile);Assert.AreEqual(v[8],sprite.MatrixA);Assert.AreEqual(v[9],sprite.Subpriority);
                }
            }
            Assert.AreEqual(entry.Value.GetProperty("end")[3].GetUInt32(),RandomState(runtime));
            Assert.AreEqual(kind=="schedule"?12:kind=="pair"?8:4,entry.Value.GetProperty("rng").GetArrayLength());
            if(kind=="schedule"||kind=="pair")
            {
                bool sawDeferred=frames.Zip(frames.Skip(1)).Any(pair=>pair.First.Sprites.Any(x=>x.Template==Child&&x.Offset==default&&x.MatrixA==256&&pair.Second.Sprites.Any(y=>y.Slot==x.Slot&&y.Position==x.Position&&y.Offset!=default)));
                Assert.IsTrue(sawDeferred,"Reused lower cells must expose their raw pre-Begin frame before their next callback.");
            }
        }
    }

    [TestMethod][DataRow(false)][DataRow(true)]
    public void CompleteRootCreatesTwelveRawChildrenDrainsThemAndReplaysStableIdentity(bool doubles)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(352);
        Assert.AreEqual(5,resolved.Script.Commands.Count(c=>c.Op=="createsprite"&&c.Args[0]==Bubble));Assert.AreEqual(3,resolved.Script.Commands.Count(c=>c.Op=="createsprite"&&c.Args[0]==Ring));
        for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var cue=Cue(actor,target,null);cue=cue with{VisualSeed=SourceAnimationVisualSeed.For(cue)};var other=cue with{Sequence=2};other=other with{VisualSeed=SourceAnimationVisualSeed.For(other)};
            var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio);var repeat=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio());var changed=new SourceBattleAnimation(resolved.Script,other,battlers,doubles,new Audio());
            var births=new HashSet<(int,PixelPoint)>();bool pixels=false,different=false;int ticks=0,peak=0;
            while(runtime.Active&&ticks++<240)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(repeat.Tick()));var contrast=changed.Tick();runtime.Snapshot();runtime.Snapshot();
                var children=frame.Sprites.Where(s=>s.Template==Child).ToArray();foreach(var c in children)births.Add((c.Slot,c.Position));peak=Math.Max(peak,frame.Sprites.Count(s=>s.Affine));
                different|=!children.Select(c=>c.Offset).SequenceEqual(contrast.Sprites.Where(s=>s.Template==Child).Select(c=>c.Offset));
                var rgba=composer.ComposeField(frame);composer.ComposeField(frame);if(children.Length>0)pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Template!=Child).ToArray()}));
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(repeat.Active);Assert.IsFalse(changed.Active);Assert.AreEqual(12,births.Count);Assert.IsTrue(peak<=15);Assert.IsTrue(pixels);Assert.IsTrue(different);
            var rng=new SourceVisualRandom(cue.VisualSeed!.Value);for(int i=0;i<12;i++)rng.Next();var state=typeof(SourceVisualRandom).GetField("_state",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(state);Assert.AreEqual((uint)state.GetValue(rng)!,RandomState(runtime));
            Assert.AreEqual(5,audio.Cues.Count);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.HasResidualSprites);Assert.IsFalse(runtime.GraphicsLoaded);
            var final=runtime.Snapshot();Assert.AreEqual(0,final.Sprites.Length);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.IsFalse(final.BlendEnabled);
        }
    }

    [TestMethod]
    public void IsolatedRingReleasesScriptCountWhileRawChildrenContinueAndCancelReleasesBoth()
    {
        var source=Isolated(Ring,["0","0","40","15"]);var cue=Cue(seed:42);var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(source,cue,Battlers(),true,new Audio(),spriteScene:scene);int ticks=0;bool sawChildren=false;
        while(runtime.Active&&ticks++<100)
        {
            var frame=runtime.Tick();sawChildren|=frame.Sprites.Any(s=>s.Template==Child);
            Assert.AreEqual(frame.Sprites.Count(s=>s.Template==Ring),runtime.ActiveVisualCount);
        }
        Assert.IsFalse(runtime.Active);Assert.IsTrue(sawChildren);Assert.IsTrue(runtime.HasResidualSprites);Assert.AreEqual(0,runtime.ActiveVisualCount);uint? state=RandomState(runtime);int residual=0;
        while(runtime.HasResidualSprites&&residual++<25){var frame=scene.TickIdle(Battlers(),true);Assert.IsTrue(frame.Sprites.All(s=>s.Template==Child));Assert.AreEqual(state,RandomState(runtime));}
        Assert.IsFalse(runtime.HasResidualSprites);Assert.IsTrue(residual>0);
        foreach(int stop in new[]{1,10,20,25,40,48,55})
        {
            var audio=new Audio();var cancelledScene=new SourceBattleAnimation.SpriteScene();var cancelled=new SourceBattleAnimation(source,cue,Battlers(),true,audio,spriteScene:cancelledScene);int reached=0;
            while((cancelled.Active||cancelled.HasResidualSprites)&&reached<stop){Advance(cancelled,cancelledScene);reached++;}
            Assert.IsTrue(reached==stop||!cancelled.Active&&!cancelled.HasResidualSprites);if(stop<=20)Assert.IsNull(RandomState(cancelled));
            int sound=audio.Cues.Count;cancelled.Cancel();cancelled.Cancel();Assert.IsFalse(cancelled.Active);Assert.IsFalse(cancelled.HasResidualSprites);Assert.AreEqual(0,cancelled.ActiveVisualCount);Assert.AreEqual(0,cancelled.Snapshot().Sprites.Length);Assert.IsNull(RandomState(cancelled));Assert.AreEqual(sound,audio.Cues.Count);
        }
    }

    [TestMethod]
    public void RawChildSignedWordsAndDestroyBeforePlayerMatchOverflowVectors()
    {
        using var doc=Vectors();var probes=doc.RootElement.GetProperty("signedAccumulatorProbes").EnumerateArray().Select(x=>x.EnumerateArray().Select(y=>y.GetInt32()).ToArray()).ToArray();
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateObject())
        {
            if(entry.Value.GetProperty("kind").GetString()!="probe")continue;var data=probes[entry.Value.GetProperty("config")[0].GetInt32()];
            var script=Script() with{Commands=[new("end",[],1)],Labels=new Dictionary<string,int>()};var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(script,Cue(),Battlers(),true,new Audio(),spriteScene:scene);
            var type=typeof(SourceBattleAnimation).GetNestedType("WaterPulseSprite",BindingFlags.NonPublic);Assert.IsNotNull(type);
            var sprite=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[4,19,script.SpriteRecipes[Child],new PixelPoint(100,80),data[1],data[2]],CultureInfo.InvariantCulture);Assert.IsNotNull(sprite);
            var words=type.GetField("_data",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(words);var actualWords=(short[])words.GetValue(sprite)!;for(int i=0;i<5;i++)actualWords[i]=unchecked((short)data[i]);
            var field=typeof(SourceBattleAnimation).GetField("_sprites",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(field);var registry=field.GetValue(runtime);Assert.IsNotNull(registry);var add=registry.GetType().GetMethod("Add");Assert.IsNotNull(add);add.Invoke(registry,[sprite]);
            var frames=new List<SourceAnimationFrame>();do{frames.Add(Advance(runtime,scene));}while(runtime.Active||runtime.HasResidualSprites);
            Assert.AreEqual(data[0],frames.Count);Assert.AreEqual(0,frames[^1].Sprites.Length);Assert.IsNull(RandomState(runtime));
            foreach(var row in entry.Value.GetProperty("states").EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[1]<0||v[4]!=1)continue;var actual=frames[v[1]].Sprites.Single();Assert.AreEqual(new PixelPoint(v[8],v[9]),actual.Offset);Assert.AreEqual(v[22],actual.MatrixA);Assert.AreEqual(v[16],actual.Tile);
            }
        }
    }

    [TestMethod]
    public void SourceRecipeSeedAndBoundedRingOperandsFailClosed()
    {
        var source=Isolated(Ring,["0","0","40","15"]);void Reject(SourceAnimationScript script,uint? seed=0)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(seed:seed),Battlers(),true,new Audio()));
        Reject(source,null);Reject(source with{SineTable=[]});
        foreach(var operand in new[]{new[]{"0","0","0","15"},new[]{"0","0","40","0"},new[]{"128","0","40","15"}})Reject(Isolated(Ring,operand));
        foreach(var name in new[]{Ring,Child})
        {
            var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes.Remove(name);Reject(source with{SpriteRecipes=recipes});
            recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[name]=recipes[name] with{Width=64};Reject(source with{SpriteRecipes=recipes});
        }
        var unknown=source.Commands.Select(c=>c.Op=="loadspritegfx"&&c.Args[0]=="ANIM_TAG_BLUE_RING_2"?c with{Args=["ANIM_TAG_UNKNOWN_PULSE"]}:c).ToArray();Reject(source with{Commands=unknown});
    }
}
