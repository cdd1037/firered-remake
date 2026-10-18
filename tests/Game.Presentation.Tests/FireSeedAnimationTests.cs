using System.Globalization;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FireSeedAnimationTests
{
    private static readonly (int Move,string Template,string Asset)[] Families=[
        (307,"gFirePlumeSpriteTemplate","fire-plume"),(221,"gLargeFlameSpriteTemplate","fire"),
        (221,"gLargeFlameScatterSpriteTemplate","fire"),(73,"gLeechSeedSpriteTemplate","seed")];
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public int Stops{get;private set;}public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects()=>Stops++;}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles,bool pictureOffsets=false)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,pictureOffsets?new[]{5,11,7,13}[i]:0,pictureOffsets?new[]{2,3,4,5}[i]:0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor,int target)=>new("fire-seed",1,move,new(actor),new(target));
    private static SourceAnimationScript Primitive(int kind,params int[] args)
    {
        var f=Families[kind];var source=AnimationCompositionFixture.Script(f.Move);
        return source with{Commands=[new("loadspritegfx",["ANIM_TAG_"+f.Asset.ToUpperInvariant().Replace('-','_')],1),
            new("createsprite",new[]{f.Template,"ANIM_TARGET","2"}.Concat(args.Select(v=>v.ToString(CultureInfo.InvariantCulture))).ToArray(),2),new("waitforvisualfinish",[],3),new("end",[],4)]};
    }
    [TestMethod][DataRow(0,160)][DataRow(1,140)][DataRow(2,240)][DataRow(3,140)]
    public void ExtractedSourceCallbacksMatchEveryTickWithSignedWordsAndAllLayouts(int kind,int expectedCases)
    {
        using var doc=SourceVectorFixture.Read("fire-seed-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_fire_seed_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/fire-seed-source-vectors.json");
        var root=doc.RootElement;var family=Families[kind];int count=0,rows=0;
        foreach(var item in root.GetProperty("cases").EnumerateObject())
        {
            var key=item.Name.Split(':').Select(int.Parse).ToArray();if(key[0]!=kind)continue;count++;
            int[] args=root.GetProperty("configs")[kind][key[4]].EnumerateArray().Select(v=>v.GetInt32()).ToArray();bool doubles=key[1]!=0;
            var runtime=new SourceBattleAnimation(Primitive(kind,args),Cue(family.Move,key[2],key[3]),Battlers(doubles,true),doubles,new Audio());
            for(int i=0;i<3;i++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            foreach(var row in item.Value.EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[0]<0)continue;rows++;
                var frame=runtime.Tick();string message=item.Name+" tick "+v[0];CollectionAssert.AreEqual(v[18..22],frame.AnimationArguments.Take(4).Select(x=>(int)x).ToArray(),message);
                if(v[1]==0){Assert.AreEqual(0,frame.Sprites.Length,message);continue;}
                var sprite=frame.Sprites.Single();Assert.AreEqual(family.Template,sprite.Template,message);Assert.AreEqual(family.Asset,sprite.Asset,message);
                Assert.AreEqual(new PixelPoint(v[2],v[3]),sprite.Position,message);Assert.AreEqual(new PixelPoint(v[4],v[5]),sprite.Offset,message);
                Assert.AreEqual(v[6]==0,sprite.Visible,message);Assert.AreEqual(v[7],sprite.Tile,message);Assert.AreEqual(v[8]!=0,sprite.HFlip,message);Assert.AreEqual(v[9]!=0,sprite.VFlip,message);
                Assert.AreEqual(v[10],sprite.Subpriority,message);CollectionAssert.AreEqual(v[11..15],new[]{sprite.MatrixA,sprite.MatrixB,sprite.MatrixC,sprite.MatrixD},message);
                Assert.AreEqual(kind==1,sprite.Affine);Assert.IsFalse(sprite.DoubleSize);Assert.IsFalse(sprite.Blend);Assert.AreEqual(kind==3?16:32,sprite.Width);Assert.AreEqual(sprite.Width,sprite.Height);
            }
            int ticks=0;while(runtime.Active&&ticks++<10)runtime.Tick();AssertClean(runtime);
        }
        Assert.AreEqual(expectedCases,count,"Empty or partial vector selections must fail.");Assert.IsTrue(rows>0);
    }
    [TestMethod][DataRow(73,3,327)][DataRow(221,19,841)][DataRow(307,24,552)]
    public void CompleteRootsRetainRealMaterialsSourceMultiplicityAudioReplayAndCleanup(int move,int expectedCreates,int expectedFrames)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);var templates=Families.Where(f=>f.Move==move||move==221&&f.Move==307).Select(f=>f.Template).ToHashSet();
        string[] expectedAssets=move==73?["impact","seed"]:move==221?["impact","fire","fire-plume"]:["impact","fire-plume"];
        CollectionAssert.AreEquivalent(expectedAssets,resolved.Assets.Keys.ToArray());Assert.IsNull(resolved.Metallic);Assert.IsNull(resolved.StatMask);
        var lines=resolved.Script.Commands.Where(c=>c.Op=="createsprite"&&templates.Contains(c.Args[0])).Select(c=>$"{c.Line}:createsprite").ToHashSet();
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x801f,240*112).ToArray()));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var cue=Cue(move,actor,target);var audio=new Audio();
            var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio(),spriteMaterials:assets);
            int ticks=0,particleFrames=0,created=0;bool pixels=false,inverted=false,background=false,blended=false,hidden=false,sprouted=false,affine=false;
            while(runtime.Active&&ticks++<600)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(replay.Tick()));created+=frame.Trace.Count(lines.Contains);
                var before=JsonSerializer.Serialize(runtime.Snapshot());runtime.Snapshot();var rgba=composer.ComposeField(frame);composer.ComposeField(frame);Assert.AreEqual(before,JsonSerializer.Serialize(runtime.Snapshot()));
                var sprites=frame.Sprites.Where(s=>templates.Contains(s.Template)).ToArray();particleFrames+=sprites.Length;hidden|=sprites.Any(s=>!s.Visible);sprouted|=sprites.Any(s=>s.Asset=="seed"&&s.Tile>0);affine|=sprites.Any(s=>s.Affine&&s.MatrixA!=256);
                inverted|=frame.InvertedPalettes.Length!=0;background|=frame.BackgroundSlots.Length!=0;blended|=frame.BlendEnabled;
                if(sprites.Any(s=>s.Visible))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>!templates.Contains(s.Template)).ToArray()}));
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.AreEqual(expectedCreates,created);Assert.AreEqual(expectedFrames,particleFrames);Assert.IsTrue(pixels);AssertClean(runtime);
            Assert.AreEqual(move!=73,inverted);Assert.AreEqual(move==307,background);Assert.AreEqual(move==307,blended);Assert.AreEqual(move==73,hidden);Assert.AreEqual(move==73,sprouted);Assert.AreEqual(move==221,affine);
            var expectedSounds=move==73?new Dictionary<string,int>{{"SE_M_POISON_POWDER",3},{"SE_M_TAIL_WHIP",8}}:move==221?new Dictionary<string,int>{{"SE_M_SACRED_FIRE",5},{"SE_M_SACRED_FIRE2",3},{"SE_M_FLAME_WHEEL2",1}}:new Dictionary<string,int>{{"SE_M_SACRED_FIRE",1},{"SE_M_FLAME_WHEEL2",2}};
            Assert.AreEqual(expectedSounds.Values.Sum(),audio.Cues.Count);foreach(var pair in expectedSounds)Assert.AreEqual(pair.Value,audio.Cues.Count(c=>c.Id==pair.Key));
            foreach(var sound in audio.Cues)
            {
                bool targetSound=move==73?sound.Id=="SE_M_TAIL_WHIP":move==221&&sound.Id!="SE_M_SACRED_FIRE";
                int pan=targetSound?(actor%2==0?(target%2==0?-64:63):(target%2==0?-63:63)):(actor%2==0?-64:63);
                Assert.AreEqual(pan,sound.Pan);
            }
        }
    }
    private static void AssertClean(SourceBattleAnimation runtime)
    {
        var f=runtime.Snapshot();Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.IsFalse(f.BlendEnabled);Assert.IsTrue(f.Offsets.All(p=>p==default));Assert.AreEqual(0,f.InvertedPalettes.Length);Assert.IsFalse(runtime.HasResidualSprites);
    }
    [TestMethod][DataRow(73)][DataRow(221)][DataRow(307)]
    public void EveryCancellationTickAndReplacementReleaseAllOwnedResources(int move)
    {
        var source=AnimationCompositionFixture.Script(move);var battlers=Battlers(true);var cue=Cue(move,3,1);var baseline=new SourceBattleAnimation(source,cue,battlers,true,new Audio());int duration=0;
        while(baseline.Active&&duration<600){baseline.Tick();duration++;}Assert.IsFalse(baseline.Active);Assert.IsTrue(duration>50);
        foreach(int requested in Enumerable.Range(0,duration+1).Append(duration+5))
        {
            var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(source,cue,battlers,true,audio,spriteScene:scene);int reached=0;
            while(runtime.Active&&reached<requested){runtime.Tick();reached++;}Assert.AreEqual(Math.Min(requested,duration),reached);int sounds=audio.Cues.Count;
            runtime.Cancel();runtime.Cancel();AssertClean(runtime);Assert.AreEqual(0,scene.Count);Assert.AreEqual(sounds,audio.Cues.Count);
            var next=new SourceBattleAnimation(source,cue,battlers,true,new Audio(),spriteScene:scene);int ticks=0;while(next.Active&&ticks++<600)next.Tick();AssertClean(next);Assert.AreEqual(0,scene.Count);
        }
    }
    [TestMethod]
    public void MissingFamilyMaterialsFailInsteadOfSilentlyDroppingSourceSprites()
    {
        int[][] args=[[-32,0,50,5,-2,0],[-16,0,70,16,0,1],[0,0,30,30,1,-2],[15,0,16,24,35,-37]];
        using var resources=new SourceAnimationResources(Folder);
        foreach(int kind in Enumerable.Range(0,4))
        {
            var family=Families[kind];var resolved=resources.Resolve(family.Move);var battlers=Battlers(false);
            var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Remove(family.Asset);
            assets.Add("background",new(240,112,new ushort[240*112]));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,new ushort[4096]));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);
            var runtime=new SourceBattleAnimation(Primitive(kind,args[kind]),Cue(family.Move,0,1),battlers,false,new Audio());
            SourceAnimationFrame frame=runtime.Snapshot();int ticks=0;while(frame.Sprites.Length==0&&ticks++<10)frame=runtime.Tick();
            Assert.AreEqual(1,frame.Sprites.Length);Assert.IsNull(frame.Sprites.Single().Material);
            Assert.Throws<KeyNotFoundException>(()=>composer.ComposeField(frame));runtime.Cancel();AssertClean(runtime);
        }
    }
    [TestMethod]
    public void MissingChangedRecipeAndOutOfWidthOrNonterminatingOperandsFailClosed()
    {
        void Reject(SourceAnimationScript source,int kind)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source,Cue(Families[kind].Move,0,1),Battlers(true),true,new Audio()));
        int[][] args=[[-32,0,50,5,-2,0],[-16,0,70,16,0,1],[0,0,30,30,1,-2],[15,0,16,24,35,-37]];
        foreach(int kind in Enumerable.Range(0,4))
        {
            var source=Primitive(kind,args[kind]);var recipe=source.SpriteRecipes[Families[kind].Template];
            foreach(var bad in new[]{recipe with{Callback="UnknownFireSeed"},recipe with{Asset="impact"},recipe with{Width=8},recipe with{Height=8},recipe with{Blend=true},recipe with{AffineDoubleSize=true},recipe with{LoopStarts=[99]},recipe with{Animations=[[new(1,1,false,false)]]},recipe with{AffineAnimations=[[new("end",0,0,0,0,0)]]},recipe with{FrameLoops=[[new(1,0,1)]]}})
            {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[recipe.Template]=bad;Reject(source with{SpriteRecipes=recipes},kind);}
            var missing=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);missing.Remove(recipe.Template);Reject(source with{SpriteRecipes=missing},kind);
            foreach(int i in Enumerable.Range(0,6))foreach(int value in new[]{-32769,32768}){var overflow=(int[])args[kind].Clone();overflow[i]=value;Reject(Primitive(kind,overflow),kind);}
            foreach(int value in new[]{0,-1}){var invalid=(int[])args[kind].Clone();invalid[kind==3?4:2]=value;Reject(Primitive(kind,invalid),kind);}
            if(kind==3)Reject(source with{SineTable=[]},kind);
            var unloaded=source with{Commands=source.Commands.Skip(1).ToArray()};var runtime=new SourceBattleAnimation(unloaded,Cue(Families[kind].Move,0,1),Battlers(true),true,new Audio());Assert.Throws<SourceAnimationException>(()=>runtime.Tick());runtime.Cancel();AssertClean(runtime);
        }
    }
}
