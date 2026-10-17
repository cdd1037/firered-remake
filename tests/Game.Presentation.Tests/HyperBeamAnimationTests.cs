using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class HyperBeamAnimationTests
{
    private const string Template="gHyperBeamOrbSpriteTemplate";
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int actor,int target,uint? seed=1)=>new("hyper-beam",1,63,new(actor),new(target)){VisualSeed=seed};
    private static SourceAnimationScript Primitive()=>AnimationCompositionFixture.Script(63) with{Commands=[new("loadspritegfx",["ANIM_TAG_ORBS"],1),new("createsprite",[Template,"ANIM_TARGET","2"],2),new("waitforvisualfinish",[],3),new("end",[],4)]};
    private static uint? RandomState(SourceBattleAnimation runtime)
    {var stream=typeof(SourceBattleAnimation).GetField("_visualRandom",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(runtime);return stream is null?null:(uint)typeof(SourceVisualRandom).GetField("_state",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(stream)!;}
    private static uint StreamState(SourceVisualRandom random)=>(uint)typeof(SourceVisualRandom).GetField("_state",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(random)!;
    [TestMethod]
    public void ApprovedSevenFramePolicyHasExactEqualCountsForEveryAcceptedHalfword()
    {
        var counts=new int[7];const uint inverse=4005161829;
        for(uint word=0;word<65534;word++)
        {
            uint next=word<<16,seed=unchecked((next-24691)*inverse);var random=new SourceVisualRandom(seed);
            int choice=random.NextBounded(7);Assert.AreEqual((int)(word%7),choice);Assert.AreEqual(next,StreamState(random));counts[choice]++;
        }
        CollectionAssert.AreEqual(Enumerable.Repeat(9362,7).ToArray(),counts);
        Assert.Throws<ArgumentOutOfRangeException>(()=>new SourceVisualRandom(1).NextBounded(0));Assert.Throws<ArgumentOutOfRangeException>(()=>new SourceVisualRandom(1).NextBounded(65537));
        foreach(int bound in new[]{1,2,8,256,65536})
        {var direct=new SourceVisualRandom(42);var chosen=new SourceVisualRandom(42);Assert.AreEqual(direct.Next()%bound,chosen.NextBounded(bound));Assert.AreEqual(StreamState(direct),StreamState(chosen));}
    }
    [TestMethod]
    [DataRow(862675361u,0,1663459443u)]
    [DataRow(516972961u,1,2979225715u)]
    [DataRow(3328885925u,6,2296933694u)]
    [DataRow(3897561891u,6,959673428u)]
    public void ApprovedPolicyRejectsBothUpperValuesIncludingConsecutiveRejects(uint seed,int choice,uint state)
    {
        var random=new SourceVisualRandom(seed);Assert.AreEqual(choice,random.NextBounded(7));Assert.AreEqual(state,StreamState(random));
        var runtime=new SourceBattleAnimation(Primitive(),Cue(0,1,seed),Battlers(true),true,new Audio());for(int i=0;i<3;i++)runtime.Tick();var frame=runtime.Tick();Assert.AreEqual(choice,frame.Sprites.Single().Tile);
        random.Next();random.Next();Assert.AreEqual(StreamState(random),RandomState(runtime),"Only the visual stream advances for rejected animation choices, speed and wave phase.");
    }
    [TestMethod]
    public void SourceFastMotionWavePriorityAndThreeDrawsRemainSeparateFromApprovedFramePolicy()
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/hyper-beam-source-motion-vectors.json")));var root=doc.RootElement;int cases=0;bool originalUndefined=false;
        foreach(var item in root.GetProperty("cases").EnumerateObject())
        {
            cases++;var key=item.Name.Split(':').Select(int.Parse).ToArray();uint seed=root.GetProperty("seeds")[key[2]].GetUInt32();var battlers=Battlers(true);
            var runtime=new SourceBattleAnimation(Primitive(),Cue(key[0],key[1],seed),battlers,true,new Audio());for(int i=0;i<3;i++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            foreach(var row in item.Value.EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();Assert.IsTrue(v[9]<65534,"Motion comparison requires an unrejected first source draw.");originalUndefined|=v[6]==7;if(v[0]<0)continue;
                var frame=runtime.Tick();string message=item.Name+" tick "+v[0];if(v[5]==0){Assert.AreEqual(0,frame.Sprites.Length,message);continue;}
                var sprite=frame.Sprites.Single();Assert.AreEqual(new PixelPoint(v[1],v[2]),sprite.Position,message);Assert.AreEqual(new PixelPoint(v[3],v[4]),sprite.Offset,message);
                Assert.AreEqual(v[7]-10+Math.Max(3,battlers[key[1]].Subpriority-2),sprite.Subpriority,message);
                Assert.AreEqual(v[9]%7,sprite.Tile,"Approved valid-seven policy differs intentionally from source %8, without changing motion.");Assert.IsFalse(sprite.Affine);Assert.AreEqual(8,sprite.Width);Assert.AreEqual(8,sprite.Height);
            }
            Assert.AreEqual(root.GetProperty("finalStates").GetProperty(item.Name).GetUInt32(),RandomState(runtime));int ticks=0;while(runtime.Active&&ticks++<10)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);
        }
        Assert.AreEqual(288,cases);Assert.IsTrue(originalUndefined,"The source oracle must retain original out-of-range operand7 rather than hide it.");
    }
    [TestMethod][DataRow(false)][DataRow(true)]
    public void FullBeamUsesFiftyTwoOrbsSourcePaletteFlashAndLoopSoundWithStableReplay(bool doubles)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(63);int tag=resolved.Script.SpriteTags["ANIM_TAG_ORBS"];
        var orbLines=resolved.Script.Subroutines["HyperBeamOrbs"].Where(c=>c.Op=="createsprite").Select(c=>$"{c.Line}:createsprite").ToHashSet();Assert.AreEqual(2,orbLines.Count);
        Assert.AreEqual(26,resolved.Script.Commands.Count(c=>c.Op=="call"&&c.Args[0]=="HyperBeamOrbs"));
        for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x5294,240*112).ToArray()));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var cue=Cue(actor,target);cue=cue with{VisualSeed=SourceAnimationVisualSeed.For(cue)};
            var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio());int ticks=0,created=0;bool pixels=false,flash=false;
            while(runtime.Active&&ticks++<700)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(replay.Tick()));created+=frame.Trace.Count(orbLines.Contains);
                uint? before=RandomState(runtime);runtime.Snapshot();var rgba=composer.ComposeField(frame);composer.ComposeField(frame);Assert.AreEqual(before,RandomState(runtime));
                if(frame.Sprites.Any(s=>s.Template==Template)){pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));flash|=frame.SpritePalettes.TryGetValue(tag,out var palette)&&!palette.SequenceEqual(resolved.Assets["orbs"].Palette);}
                Assert.IsTrue(frame.Sprites.Where(s=>s.Template==Template).All(s=>s.Tile is >=0 and <7));
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.AreEqual(52,created);Assert.IsTrue(pixels);Assert.IsTrue(flash);Assert.AreEqual(15,audio.Cues.Count(c=>c.Id=="SE_M_HYPER_BEAM2"));
            Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.Snapshot().Sprites.Length);Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));
        }
    }
    [TestMethod]
    public void IndexedOrbsRenderExactlyLikeExistingFallbackWhenPaletteIsUnchanged()
    {
        using var resources=new SourceAnimationResources(Folder);var indexed=resources.Asset("orbs");var bytes=File.ReadAllBytes(Path.Combine(Folder,"elemental-orbs.rgb555"));
        int width=BitConverter.ToUInt16(bytes,0),height=BitConverter.ToUInt16(bytes,2);var fallback=new SourcePixelAsset(width,height,Enumerable.Range(0,width*height).Select(i=>BitConverter.ToUInt16(bytes,4+i*2)).ToArray());
        Assert.AreEqual((16,48),(indexed.Width,indexed.Height));Assert.AreEqual(768,indexed.PaletteIndices.Length);Assert.AreEqual(16,indexed.Palette.Length);
        for(int i=0;i<indexed.Pixels.Length;i++)Assert.AreEqual((fallback.Pixels[i]&32768)==0?0:fallback.Pixels[i],(int)indexed.Pixels[i],"Transparent RGB bits are not visible colors.");
        var battlers=Battlers(false);var resolved=resources.Resolve(63);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x5294,240*112).ToArray()));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,new ushort[4096]));
        var before=new Dictionary<string,SourcePixelAsset>(assets){["orbs"]=fallback};var oldComposer=AnimationCompositionFixture.Create(resources,resolved,before,battlers);var newComposer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);
        for(int tile=0;tile<9;tile++)
        {
            var frame=new SourceAnimationFrame(0,true,new PixelPoint[4],[],0,0,[],[]){Battlers=battlers,Sprites=[new(2,Template,new(120,56),default,tile,true,false,false,256,"orbs",10){Width=8,Height=8}]};
            CollectionAssert.AreEqual(oldComposer.ComposeField(frame),newComposer.ComposeField(frame));
        }
    }
    [TestMethod]
    public void MissingSeedIndexedPaletteOrAnInventedEighthProgramFailClosedAndCancelReleasesScene()
    {
        var source=AnimationCompositionFixture.Script(63);var battlers=Battlers(true);
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source,Cue(0,1,null),battlers,true,new Audio()));
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpritePalettes=new Dictionary<int,ushort[]>()},Cue(0,1),battlers,true,new Audio()));
        var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[Template]=recipes[Template] with{Animations=[..recipes[Template].Animations,[new(7,1,false,false)]],LoopStarts=Enumerable.Repeat(-1,8).ToArray()};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpriteRecipes=recipes},Cue(0,1),battlers,true,new Audio()));
        foreach(int cut in new[]{1,4,50,100,150,200,300})
        {
            var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(source,Cue(3,1),battlers,true,new Audio(),spriteScene:scene);for(int i=0;i<cut&&runtime.Active;i++)runtime.Tick();runtime.Cancel();runtime.Cancel();
            Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,scene.Count);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsNull(RandomState(runtime));Assert.AreEqual(0,runtime.Snapshot().Sprites.Length);
        }
    }
}
