using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class WishStarAnimationTests
{
    private const string Parent="gWishStarSpriteTemplate",Child="gMiniTwinklingStarSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public List<(int Pan,int Frame)> Pans{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles=true)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static SourceAnimationScript Script(){using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(273).Script;return script with{SpriteRecipes=script.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value)};}
    private static CommittedMoveAnimation Cue(int actor=0,uint? seed=0)=>new("wish",1,273,new(actor),new(actor)){VisualSeed=seed};
    private static uint? RandomState(SourceBattleAnimation runtime)
    {var f=typeof(SourceBattleAnimation).GetField("_visualRandom",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(f);if(f.GetValue(runtime) is not SourceVisualRandom rng)return null;var state=typeof(SourceVisualRandom).GetField("_state",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(state);return (uint)state.GetValue(rng)!;}
    private static uint RandomAfter(uint seed,int draws){var rng=new SourceVisualRandom(seed);for(int i=0;i<draws;i++)rng.Next();var f=typeof(SourceVisualRandom).GetField("_state",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(f);return (uint)f.GetValue(rng)!;}
    private static SourceAnimationFrame Advance(SourceBattleAnimation runtime)=>runtime.Active?runtime.Tick():runtime.TickResidualSprites();
    private static object Registry(SourceBattleAnimation runtime){var f=typeof(SourceBattleAnimation).GetField("_sprites",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(f);var registry=f.GetValue(runtime);Assert.IsNotNull(registry);return registry;}
    private static void AddSprite(SourceBattleAnimation runtime,object sprite){var registry=Registry(runtime);var add=registry.GetType().GetMethod("Add");Assert.IsNotNull(add);add.Invoke(registry,[sprite]);}
    private static void AddParent(SourceBattleAnimation runtime,int slot,int priority=3,bool player=true)
    {var type=typeof(SourceBattleAnimation).GetNestedType("WishStarSprite",BindingFlags.NonPublic);Assert.IsNotNull(type);var sprite=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[slot,priority,Script().SpriteRecipes[Parent],player],CultureInfo.InvariantCulture);Assert.IsNotNull(sprite);AddSprite(runtime,sprite);}
    private static SourceBattleAnimation Bare(uint seed=0)=>new(Script() with{Commands=[new("end",[],1)],Labels=new Dictionary<string,int>()},Cue(seed:seed),Battlers(),true,new Audio());
    private static void FillHelpers(SourceBattleAnimation runtime,int start,int end)
    {var helper=typeof(SourceBattleAnimation).GetNestedType("BubbleHelper",BindingFlags.NonPublic);Assert.IsNotNull(helper);for(int slot=start;slot<=end;slot++){var sprite=Activator.CreateInstance(helper,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[slot],CultureInfo.InvariantCulture);Assert.IsNotNull(sprite);AddSprite(runtime,sprite);}}

    [TestMethod]
    public void ExtractedSourceMotionBlinkingTileAndRngVectorsMatchAllActorPositionsAndSeeds()
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/wish-star-source-vectors.json")));
        var seeds=doc.RootElement.GetProperty("seeds").EnumerateArray().Select(v=>v.GetUInt32()).ToArray();
        var source=Script() with{Commands=[new("loadspritegfx",["ANIM_TAG_GOLD_STARS"],1),new("createsprite",[Parent,"ANIM_ATTACKER","40"],2),new("waitforvisualfinish",[],3),new("end",[],4)],Labels=new Dictionary<string,int>()};
        int cases=0;
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateObject())
        {
            if(entry.Value.GetProperty("kind").GetString()!="single")continue;cases++;var config=entry.Value.GetProperty("config");int actor=config[0].GetInt32();
            var runtime=new SourceBattleAnimation(source,Cue(actor,seeds[config[1].GetInt32()]),Battlers(),true,new Audio());SourceAnimationFrame first;
            do{first=runtime.Tick();}while(!first.Sprites.Any(s=>s.Template==Parent));
            var frames=new List<SourceAnimationFrame>{first};while((runtime.Active||runtime.HasResidualSprites)&&frames.Count<160)frames.Add(Advance(runtime));Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.HasResidualSprites);
            var allocations=entry.Value.GetProperty("allocations").EnumerateArray().Select(v=>v.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).ToDictionary(v=>v[0]);
            var states=entry.Value.GetProperty("states").EnumerateArray().Select(v=>v.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).Where(v=>v[1]>=0&&v[4]==1).GroupBy(v=>(Tick:v[1],Slot:v[3])).Select(g=>g.Last());
            foreach(var v in states)
            {
                var actual=frames[v[1]].Sprites.Single(s=>s.Slot==v[3]+4);string message=$"case{entry.Name} tick{v[1]} slot{v[3]}";
                Assert.AreEqual(new PixelPoint(v[6],v[7]),actual.Position,message);Assert.AreEqual(new PixelPoint(v[8],v[9]),actual.Offset,message);Assert.AreEqual(v[16],actual.Tile,message);Assert.AreEqual(v[21]==0,actual.Visible,message);Assert.AreEqual(v[22],actual.Subpriority,message);Assert.IsFalse(actual.Affine,message);Assert.AreEqual(allocations[v[2]][2]==1?16:8,actual.Width,message);
            }
            foreach(var group in entry.Value.GetProperty("rendered").EnumerateArray().Select(v=>v.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).Where(v=>v[0]>=0).GroupBy(v=>v[0]))
            {
                var actual=frames[group.Key].Sprites.OrderBy(s=>s.Slot).ToArray();var expected=group.OrderBy(v=>v[1]).ToArray();Assert.AreEqual(expected.Length,actual.Length);
                for(int i=0;i<expected.Length;i++){var v=expected[i];Assert.AreEqual(v[1]+4,actual[i].Slot);Assert.AreEqual(new PixelPoint(v[4],v[5]),actual[i].Position);Assert.AreEqual(new PixelPoint(v[6],v[7]),actual[i].Offset);Assert.AreEqual(v[8],actual[i].Tile);Assert.AreEqual(v[9]==0,actual[i].Visible);}
            }
            Assert.AreEqual(42,entry.Value.GetProperty("rng").GetArrayLength());Assert.AreEqual(entry.Value.GetProperty("end")[3].GetUInt32(),RandomState(runtime));
            var draw=typeof(SourceBattleAnimation).GetMethod("NextVisualRandom",BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(draw);var suffix=entry.Value.GetProperty("suffix");Assert.AreEqual(suffix[0].GetUInt16(),(ushort)draw.Invoke(runtime,null)!);Assert.AreEqual(suffix[1].GetUInt16(),(ushort)draw.Invoke(runtime,null)!);Assert.AreEqual(suffix[2].GetUInt32(),RandomState(runtime));
        }
        Assert.AreEqual(24,cases);
    }

    [TestMethod][DataRow(false)][DataRow(true)]
    public void ImmediateRawInitializationRunsBeforeLowerOrHigherSlotTraversal(bool lower)
    {
        var runtime=Bare(42);if(lower)FillHelpers(runtime,4,62);AddParent(runtime,lower?63:4);
        runtime.Tick();runtime.Tick();Assert.IsNull(RandomState(runtime));
        if(lower){var registry=Registry(runtime);var remove=registry.GetType().GetMethod("RemoveAt");Assert.IsNotNull(remove);remove.Invoke(registry,[4]);}
        var birth=runtime.Tick().Sprites.Single(s=>s.Template==Child);Assert.AreEqual(lower?4:5,birth.Slot);Assert.AreEqual(4,birth.Subpriority);
        Assert.IsTrue(birth.Tile is 4 or 5);Assert.IsTrue(birth.Visible);Assert.AreEqual(RandomAfter(42,2),RandomState(runtime));
        var next=runtime.Tick().Sprites.Single(s=>s.Template==Child);Assert.AreEqual(lower,next.Visible,"Higher child ages once on birth frame; lower child initializes immediately but ages next pass.");Assert.AreEqual(birth.Tile,next.Tile);Assert.AreEqual(birth.Offset,next.Offset);
        runtime.Cancel();Assert.IsNull(RandomState(runtime));Assert.IsFalse(runtime.HasResidualSprites);
    }

    [TestMethod][DataRow(false)][DataRow(true)]
    public void CompleteWishReplaysTwentyOneStarsAndExactAdjustedPanWithoutResidual(bool doubles)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(273);
        Assert.AreEqual(1,resolved.Script.Commands.Count(c=>c.Op=="createsprite"&&c.Args[0]==Parent));
        for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var cue=Cue(actor,null);cue=cue with{VisualSeed=SourceAnimationVisualSeed.For(cue)};var other=cue with{Sequence=2};other=other with{VisualSeed=SourceAnimationVisualSeed.For(other)};
            var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio);var repeat=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio());var contrast=new SourceBattleAnimation(resolved.Script,other,battlers,doubles,new Audio());
            var births=new HashSet<PixelPoint>();bool pixels=false,different=false,sawDark=false;int ticks=0,peak=0;
            while(runtime.Active&&ticks++<400)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(repeat.Tick()));var changed=contrast.Tick();var rng=RandomState(runtime);runtime.Snapshot();runtime.Snapshot();
                var children=frame.Sprites.Where(s=>s.Template==Child).ToArray();foreach(var c in children){births.Add(c.Position);Assert.IsTrue(c.Tile is 4 or 5);}
                different|=!children.Select(c=>(c.Tile,c.Offset)).SequenceEqual(changed.Sprites.Where(s=>s.Template==Child).Select(c=>(c.Tile,c.Offset)));
                peak=Math.Max(peak,frame.Sprites.Length);sawDark|=frame.PaletteBlends.Any(p=>p.Palette is >=1 and <=3&&p.Coefficient==10);
                var rgba=composer.ComposeField(frame);composer.ComposeField(frame);Assert.AreEqual(rng,RandomState(runtime));
                if(children.Any(c=>c.Visible))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Template!=Child).ToArray()}));
                if(frame.Sprites.Any(s=>s.Template=="gGrantingStarsSpriteTemplate"))Assert.AreEqual(0,children.Length,"The authored delay drains raw children before the granting stars.");
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(repeat.Active);Assert.IsFalse(contrast.Active);Assert.AreEqual(21,births.Count);Assert.IsTrue(peak<=22);Assert.IsTrue(pixels);Assert.IsTrue(different);Assert.IsTrue(sawDark);
            Assert.AreEqual(RandomAfter(cue.VisualSeed!.Value,42),RandomState(runtime));Assert.AreEqual(4,audio.Cues.Count);Assert.AreEqual("SE_M_REFLECT",audio.Cues[0].Id);Assert.AreEqual(actor%2==0?63:-63,audio.Cues[0].Pan);
            Assert.AreEqual(43,audio.Pans.Count);Assert.AreEqual(actor%2==0?-64:64,audio.Pans[^1].Pan);
            Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.HasResidualSprites);Assert.IsFalse(runtime.GraphicsLoaded);var final=runtime.Snapshot();Assert.AreEqual(0,final.Sprites.Length);Assert.AreEqual(0,final.PaletteBlends.Length);Assert.IsTrue(final.Offsets.All(p=>p==default));
            runtime.Cancel();Assert.IsNull(RandomState(runtime));
        }
    }

    [TestMethod]
    public void ExhaustedImmediateAllocationConsumesNoRandomAndCancellationClearsAllCells()
    {
        var runtime=Bare(42);FillHelpers(runtime,4,62);AddParent(runtime,63);
        for(int tick=0;tick<65;tick++){var frame=Advance(runtime);Assert.IsFalse(frame.Sprites.Any(s=>s.Template==Child));Assert.IsNull(RandomState(runtime));}
        Assert.AreEqual(0,runtime.ActiveVisualCount);runtime.Cancel();Assert.IsFalse(runtime.HasResidualSprites);Assert.AreEqual(0,runtime.Snapshot().Sprites.Length);
        foreach(int stop in new[]{1,20,40,100,170,260,400})
        {
            var audio=new Audio();var cancelled=new SourceBattleAnimation(Script(),Cue(seed:42),Battlers(),true,audio);int reached=0;
            while((cancelled.Active||cancelled.HasResidualSprites)&&reached<stop){Advance(cancelled);reached++;}
            Assert.IsTrue(reached==stop||!cancelled.Active&&!cancelled.HasResidualSprites);if(stop<=20)Assert.IsNull(RandomState(cancelled));
            int sounds=audio.Cues.Count;cancelled.Cancel();cancelled.Cancel();Assert.AreEqual(sounds,audio.Cues.Count);Assert.IsFalse(cancelled.Active);Assert.IsFalse(cancelled.HasResidualSprites);Assert.IsNull(RandomState(cancelled));Assert.AreEqual(0,cancelled.Snapshot().Sprites.Length);
        }
    }

    [TestMethod]
    public void MissingSeedRecipesUnknownSongAndUnreviewedParentOperandsFailClosed()
    {
        var source=Script();void Reject(SourceAnimationScript s,uint? seed=0)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(seed:seed),Battlers(),true,new Audio()));
        Reject(source,null);foreach(var name in new[]{Parent,Child}){var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes.Remove(name);Reject(source with{SpriteRecipes=recipes});}
        Reject(source with{Commands=source.Commands.Select(c=>c.Op=="createsprite"&&c.Args[0]==Parent?c with{Args=[Parent,"ANIM_TARGET","40"]}:c).ToArray()});
        Reject(source with{Commands=source.Commands.Select(c=>c.Op=="panse_adjustall"?c with{Args=["SE_UNKNOWN_WISH","63","-64","-3","0"]}:c).ToArray()});
        var parent=source.Commands.Single(c=>c.Op=="createsprite"&&c.Args[0]==Parent);var unused=source with{Commands=[new("end",[],1)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"UnusedWish",[parent,new("return",[],2)]}}};
        _=new SourceBattleAnimation(unused,Cue(seed:null),Battlers(),true,new Audio());Reject(unused with{Commands=[new("call",["UnusedWish"],1),new("end",[],2)]},null);
        Reject(unused with{Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"UnusedWish",[parent with{Args=[Parent,"ANIM_ATTACKER","39"]},new("return",[],2)]}}});
    }
}
