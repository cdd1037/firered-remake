using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class MeteorStarAnimationTests
{
    private const string Parent="gMeteorMashStarSpriteTemplate",Child="gMiniTwinklingStarSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public List<(int Pan,int Frame)> Pans{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles=true)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static SourceAnimationScript Script(){using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(309).Script;return script with{SpriteRecipes=script.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value)};}
    private static CommittedMoveAnimation Cue(int actor=0,int target=1,uint? seed=0)=>new("meteor",1,309,new(actor),new(target)){VisualSeed=seed};
    private static JsonDocument Vectors()=>JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/meteor-mash-source-vectors.json")));
    private static uint? RandomState(SourceBattleAnimation runtime)
    {var f=typeof(SourceBattleAnimation).GetField("_visualRandom",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(f);if(f.GetValue(runtime) is not SourceVisualRandom rng)return null;var state=typeof(SourceVisualRandom).GetField("_state",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(state);return (uint)state.GetValue(rng)!;}
    private static uint RandomAfter(uint seed,int draws){var rng=new SourceVisualRandom(seed);for(int i=0;i<draws;i++)rng.Next();var field=typeof(SourceVisualRandom).GetField("_state",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(field);return (uint)field.GetValue(rng)!;}
    private static SourceAnimationScript Isolated(bool schedule=false,string[]? args=null)
    {
        string[][] parameters=[args??["-48","-64","72","32","30"],["-112","-64","8","32","30"],["-80","-64","40","32","30"]];
        var list=new List<SourceAnimationCommand>{new("loadspritegfx",["ANIM_TAG_GOLD_STARS"],1)};
        for(int i=0;i<(schedule?3:1);i++){if(i!=0)list.Add(new("delay",[i==1?"10":"40"],2));list.Add(new("createsprite",[Parent,"ANIM_TARGET","3",..parameters[i]],3));}
        list.Add(new("waitforvisualfinish",[],4));list.Add(new("end",[],5));return Script() with{Commands=list.ToArray(),Labels=new Dictionary<string,int>()};
    }
    private static SourceAnimationFrame FirstSprite(SourceBattleAnimation runtime)
    {for(int i=0;i<20;i++){var frame=runtime.Tick();if(frame.Sprites.Length>0)return frame;}throw new InvalidOperationException("Source shooting star did not appear.");}
    private static SourceAnimationFrame Advance(SourceBattleAnimation runtime)=>runtime.Active?runtime.Tick():runtime.TickResidualSprites();

    [TestMethod]
    public void ParentAndRawChildSourceVectorsIncludeBlinkingRngAndNaturalLowerSlotReuse()
    {
        using var doc=Vectors();var seeds=doc.RootElement.GetProperty("seeds").EnumerateArray().Select(x=>x.GetUInt32()).ToArray();
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateObject())
        {
            string kind=entry.Value.GetProperty("kind").GetString()!;var config=entry.Value.GetProperty("config").EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            if(kind!="single"&&(kind!="schedule"||config[2]!=0))continue;
            int target=config[0];var runtime=new SourceBattleAnimation(Isolated(kind=="schedule"),Cue(target^1,target,seeds[config[1]]),Battlers(),true,new Audio());
            var frames=new List<SourceAnimationFrame>{FirstSprite(runtime)};
            while((runtime.Active||runtime.HasResidualSprites)&&frames.Count<180)frames.Add(Advance(runtime));
            Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.HasResidualSprites);
            var allocations=entry.Value.GetProperty("allocations").EnumerateArray().Select(x=>x.EnumerateArray().Select(y=>y.GetInt32()).ToArray()).ToDictionary(x=>x[0]);
            foreach(var row in entry.Value.GetProperty("states").EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[1]<0||v[4]!=1)continue;int spriteKind=allocations[v[2]][2];if(spriteKind==1&&v[5]==1)continue;
                var actual=frames[v[1]].Sprites.Single(x=>x.Slot==v[3]+4);string message=$"{entry.Name} tick{v[1]} slot{v[3]}";
                Assert.AreEqual(new PixelPoint(v[6],v[7]),actual.Position,message);Assert.AreEqual(new PixelPoint(v[8],v[9]),actual.Offset,message);Assert.AreEqual(v[16],actual.Tile,message);Assert.AreEqual(v[21]==0,actual.Visible,message);Assert.AreEqual(v[22],actual.Subpriority,message);Assert.IsFalse(actual.Affine,message);Assert.AreEqual(spriteKind==1?16:8,actual.Width,message);
            }
            foreach(var rows in entry.Value.GetProperty("rendered").EnumerateArray().Select(x=>x.EnumerateArray().Select(y=>y.GetInt32()).ToArray()).Where(x=>x[0]>=0).GroupBy(x=>x[0]))
            {
                var actual=frames[rows.Key].Sprites.OrderBy(x=>x.Slot).ToArray();var expected=rows.OrderBy(x=>x[1]).ToArray();Assert.AreEqual(expected.Length,actual.Length,$"{entry.Name} tick{rows.Key}");
                for(int i=0;i<expected.Length;i++){var v=expected[i];var sprite=actual[i];Assert.AreEqual(v[1]+4,sprite.Slot);Assert.AreEqual(new PixelPoint(v[4],v[5]),sprite.Position);Assert.AreEqual(new PixelPoint(v[6],v[7]),sprite.Offset);Assert.AreEqual(v[8],sprite.Tile);Assert.AreEqual(v[9]==0,sprite.Visible);}
            }
            Assert.AreEqual(entry.Value.GetProperty("end")[3].GetUInt32(),RandomState(runtime));Assert.AreEqual(kind=="schedule"?96:32,entry.Value.GetProperty("rng").GetArrayLength());
            var draw=typeof(SourceBattleAnimation).GetMethod("NextVisualRandom",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(draw);var suffix=entry.Value.GetProperty("suffix");
            Assert.AreEqual(suffix[0].GetUInt16(),(ushort)draw.Invoke(runtime,null)!);Assert.AreEqual(suffix[1].GetUInt16(),(ushort)draw.Invoke(runtime,null)!);Assert.AreEqual(suffix[2].GetUInt32(),RandomState(runtime));
            if(kind=="schedule")Assert.IsTrue(frames.Zip(frames.Skip(1)).Any(pair=>pair.First.Sprites.Any(x=>x.Template==Child&&x.Tile==0&&pair.Second.Sprites.Any(y=>y.Slot==x.Slot&&y.Position==x.Position&&y.Tile is 4 or 5))),"A lower raw slot exposes tile0 before its first random initializer, then retains tile4/5 through BeginAnim.");
        }
    }

    [TestMethod][DataRow(false)][DataRow(true)]
    public void CompleteRootCreatesFortyEightChildrenDrainsBeforeScriptEndAndRestoresBackground(bool doubles)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(309);Assert.AreEqual(3,resolved.Script.Commands.Count(c=>c.Op=="createsprite"&&c.Args[0]==Parent));
        for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var cue=Cue(actor,target,null);cue=cue with{VisualSeed=SourceAnimationVisualSeed.For(cue)};var other=cue with{Sequence=2};other=other with{VisualSeed=SourceAnimationVisualSeed.For(other)};
            var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio);var repeat=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio());var changed=new SourceBattleAnimation(resolved.Script,other,battlers,doubles,new Audio());
            var births=new HashSet<(int,PixelPoint)>();bool pixels=false,different=false,sawBackground=false;int ticks=0,peak=0;
            while(runtime.Active&&ticks++<300)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(repeat.Tick()));var contrast=changed.Tick();uint? rng=RandomState(runtime);runtime.Snapshot();runtime.Snapshot();
                var children=frame.Sprites.Where(s=>s.Template==Child).ToArray();foreach(var c in children)births.Add((c.Slot,c.Position));peak=Math.Max(peak,frame.Sprites.Length);sawBackground|=frame.MoveBackground?.Id==16;
                different|=!children.Select(c=>(c.Tile,c.Offset)).SequenceEqual(contrast.Sprites.Where(s=>s.Template==Child).Select(c=>(c.Tile,c.Offset)));
                var rgba=composer.ComposeField(frame);composer.ComposeField(frame);Assert.AreEqual(rng,RandomState(runtime));
                if(children.Any(c=>c.Visible))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Template!=Child).ToArray()}));
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(repeat.Active);Assert.IsFalse(changed.Active);Assert.AreEqual(48,births.Count);Assert.IsTrue(peak<=39);Assert.IsTrue(pixels);Assert.IsTrue(different);Assert.IsTrue(sawBackground);
            Assert.AreEqual(RandomAfter(cue.VisualSeed!.Value,96),RandomState(runtime));Assert.AreEqual(2,audio.Cues.Count);Assert.IsTrue(audio.Pans.Count>0);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.HasResidualSprites);Assert.IsFalse(runtime.GraphicsLoaded);
            var final=runtime.Snapshot();Assert.AreEqual(0,final.Sprites.Length);Assert.IsNull(final.MoveBackground?.Id);Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.IsFalse(final.BlendEnabled);
        }
    }

    [TestMethod]
    public void IsolatedParentLeavesRawStarsAndCancellationReleasesStreamAndEveryCell()
    {
        var source=Isolated();var cue=Cue(seed:42);var runtime=new SourceBattleAnimation(source,cue,Battlers(),true,new Audio());int ticks=0;
        while(runtime.Active&&ticks++<100)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.IsTrue(runtime.HasResidualSprites);Assert.AreEqual(0,runtime.ActiveVisualCount);
        uint? state=RandomState(runtime);int residual=0;while(runtime.HasResidualSprites&&residual++<70){runtime.TickResidualSprites();Assert.AreEqual(state,RandomState(runtime));}Assert.IsFalse(runtime.HasResidualSprites);Assert.IsTrue(residual>0);
        // Completed-state cancellation is separate from active/residual stops.
        runtime.Cancel();runtime.Cancel();Assert.IsNull(RandomState(runtime));Assert.AreEqual(0,runtime.Snapshot().Sprites.Length);
        foreach(int stop in new[]{1,5,20,32,40,80,100})
        {
            var audio=new Audio();var cancelled=new SourceBattleAnimation(source,cue,Battlers(),true,audio);int reached=0;
            while((cancelled.Active||cancelled.HasResidualSprites)&&reached<stop){Advance(cancelled);reached++;}
            Assert.IsTrue(reached==stop||!cancelled.Active&&!cancelled.HasResidualSprites);if(stop==1)Assert.IsNull(RandomState(cancelled));
            cancelled.Cancel();cancelled.Cancel();Assert.IsFalse(cancelled.Active);Assert.IsFalse(cancelled.HasResidualSprites);Assert.IsNull(RandomState(cancelled));Assert.AreEqual(0,cancelled.Snapshot().Sprites.Length);Assert.AreEqual(0,audio.Cues.Count);
        }
    }

    private static void AddSprite(SourceBattleAnimation runtime,object sprite)
    {var field=typeof(SourceBattleAnimation).GetField("_sprites",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(field);var registry=field.GetValue(runtime);Assert.IsNotNull(registry);var add=registry.GetType().GetMethod("Add");Assert.IsNotNull(add);add.Invoke(registry,[sprite]);}
    private static object NewParent(SourceBattleAnimation runtime,int slot,PixelPoint position,bool player,short[] args)
    {var type=typeof(SourceBattleAnimation).GetNestedType("MeteorStarSprite",BindingFlags.NonPublic);Assert.IsNotNull(type);var sprite=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[slot,3,Script().SpriteRecipes[Parent],position,player,args],CultureInfo.InvariantCulture);Assert.IsNotNull(sprite);AddSprite(runtime,sprite);return sprite;}

    [TestMethod]
    public void SignedWordWrapAndTruncationMatchExtractedBoundaryVectors()
    {
        using var doc=Vectors();var probes=doc.RootElement.GetProperty("boundaryProbes").EnumerateArray().Select(x=>x.EnumerateArray().Select(y=>y.GetInt32()).ToArray()).ToArray();
        var seeds=doc.RootElement.GetProperty("seeds").EnumerateArray().Select(x=>x.GetUInt32()).ToArray();
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateObject())
        {
            if(entry.Value.GetProperty("kind").GetString()!="boundary")continue;var config=entry.Value.GetProperty("config");int target=config[0].GetInt32();var p=probes[config[2].GetInt32()];
            var source=Script() with{Commands=[new("end",[],1)],Labels=new Dictionary<string,int>()};var runtime=new SourceBattleAnimation(source,Cue(target^1,target,seeds[config[1].GetInt32()]),Battlers(),true,new Audio());
            NewParent(runtime,4,new(p[0],p[1]),(target&1)==0,p.Skip(2).Select(x=>unchecked((short)x)).ToArray());
            var frames=new List<SourceAnimationFrame>();do{frames.Add(Advance(runtime));}while((runtime.Active||runtime.HasResidualSprites)&&frames.Count<110);
            Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.HasResidualSprites);
            foreach(var row in entry.Value.GetProperty("states").EnumerateArray())
            {var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[1]<0||v[4]!=1)continue;var actual=frames[v[1]].Sprites.Single(x=>x.Slot==v[3]+4);Assert.AreEqual(new PixelPoint(v[6],v[7]),actual.Position);Assert.AreEqual(new PixelPoint(v[8],v[9]),actual.Offset);Assert.AreEqual(v[16],actual.Tile);Assert.AreEqual(v[21]==0,actual.Visible);}
            Assert.AreEqual(entry.Value.GetProperty("end")[3].GetUInt32(),RandomState(runtime));
        }
    }

    [TestMethod]
    public void FullRegistrySuppressesOnlyFailedRawAllocationsWithoutConsumingRandomValues()
    {
        var source=Script() with{Commands=[new("end",[],1)],Labels=new Dictionary<string,int>()};var runtime=new SourceBattleAnimation(source,Cue(seed:0x12345678),Battlers(),true,new Audio());
        var helper=typeof(SourceBattleAnimation).GetNestedType("BubbleHelper",BindingFlags.NonPublic);Assert.IsNotNull(helper);
        for(int slot=4;slot<63;slot++){var sprite=Activator.CreateInstance(helper,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[slot],CultureInfo.InvariantCulture);Assert.IsNotNull(sprite);AddSprite(runtime,sprite);}
        NewParent(runtime,63,new(200,40),false,[-48,-64,72,32,30]);
        for(int tick=0;tick<=30;tick++){var frame=Advance(runtime);Assert.IsFalse(frame.Sprites.Any(s=>s.Template==Child));Assert.IsNull(RandomState(runtime));}
        Assert.AreEqual(0,runtime.ActiveVisualCount);runtime.Cancel();Assert.IsFalse(runtime.HasResidualSprites);Assert.AreEqual(0,runtime.Snapshot().Sprites.Length);
    }

    [TestMethod]
    public void RecipeSeedSignedWordsDurationAndUnknownMaterialsFailClosed()
    {
        var source=Isolated();void Reject(SourceAnimationScript s,uint? seed=0)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(seed:seed),Battlers(),true,new Audio()));
        Reject(source,null);foreach(var args in new[]{new[]{"0","0","1","1","0"},new[]{"0","0","1","1","29"},new[]{"32768","0","1","1","30"}})Reject(Isolated(args:args));
        foreach(var name in new[]{Parent,Child})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes.Remove(name);Reject(source with{SpriteRecipes=recipes});recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[name]=recipes[name] with{Width=32};Reject(source with{SpriteRecipes=recipes});}
        Reject(source with{Commands=source.Commands.Select(c=>c.Op=="loadspritegfx"?c with{Args=["ANIM_TAG_UNKNOWN_METEOR"]}:c).ToArray()});
        var helper=source.Commands.Where(c=>c.Op=="createsprite").Append(new("return",[],99)).ToArray();var unused=source with{Commands=[new("end",[],1)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"UnusedStars",helper}}};
        _=new SourceBattleAnimation(unused,Cue(seed:null),Battlers(),true,new Audio());Reject(unused with{Commands=[new("call",["UnusedStars"],1),new("end",[],2)]},null);
        Reject(unused with{Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"UnusedStars",[helper[0] with{Args=[Parent,"ANIM_TARGET","3","0","0","1","1","0"]},helper[1]]}}});
    }
}
