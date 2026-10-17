using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ImpactAdapterAnimationTests
{
    private static readonly (int Move,string Template,string Asset,int Width,int Height)[] Families=[
        (146,"gDizzyPunchDuckSpriteTemplate","duck",16,16),(132,"gConstrictBindingSpriteTemplate","tendrils",64,32),
        (282,"gKnockOffStrikeSpriteTemplate","slam-hit-2",64,64),(302,"gNeedleArmSpikeSpriteTemplate","green-spike",16,16),
        (338,"gFrenzyPlantRootSpriteTemplate","roots",32,32)];
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor,int target)=>new("impact-adapters",1,move,new(actor),new(target));
    private static SourceAnimationScript Primitive(int kind,params int[] args)
    {
        var family=Families[kind];var source=AnimationCompositionFixture.Script(family.Move);
        return source with{Commands=[new("loadspritegfx",["ANIM_TAG_"+family.Asset.ToUpperInvariant().Replace('-','_')],1),
            new("createsprite",new[]{family.Template,"ANIM_TARGET","2"}.Concat(args.Select(v=>v.ToString(CultureInfo.InvariantCulture))).ToArray(),2),new("waitforvisualfinish",[],3),new("end",[],4)]};
    }
    private static short[] Arguments(SourceBattleAnimation runtime)=>(short[])typeof(SourceBattleAnimation).GetField("_animationArgs",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(runtime)!;
    [TestMethod][DataRow(0,160)][DataRow(1,120)][DataRow(2,80)][DataRow(3,500)][DataRow(4,140)]
    public void ExtractedSourceCallbacksMatchEveryTickAcrossAllLayoutsAndSignedEdges(int kind,int expectedCases)
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/impact-adapter-source-vectors.json")));
        var root=doc.RootElement;var family=Families[kind];int count=0,rows=0;
        foreach(var item in root.GetProperty("cases").EnumerateObject())
        {
            var key=item.Name.Split(':').Select(int.Parse).ToArray();if(key[0]!=kind)continue;count++;
            int[] config=root.GetProperty("configs")[kind][key[4]].EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            int[] args=kind==1?config[..4]:config;bool doubles=key[1]!=0;
            var runtime=new SourceBattleAnimation(Primitive(kind,args),Cue(family.Move,key[2],key[3]),Battlers(doubles),doubles,new Audio());
            for(int i=0;i<3;i++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            foreach(var row in item.Value.EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[0]<0)continue;rows++;
                if(kind==1&&v[0]==config[4])Arguments(runtime)[7]=-1;
                var frame=runtime.Tick();string message=item.Name+" tick "+v[0];
                CollectionAssert.AreEqual(v[18..22],frame.AnimationArguments.Take(4).Select(x=>(int)x).ToArray(),message);
                if(v[1]==0){Assert.AreEqual(0,frame.Sprites.Length,message);continue;}
                var sprite=frame.Sprites.Single();Assert.AreEqual(family.Template,sprite.Template,message);Assert.AreEqual(family.Asset,sprite.Asset,message);
                Assert.AreEqual(new PixelPoint(v[2],v[3]),sprite.Position,message);Assert.AreEqual(new PixelPoint(v[4],v[5]),sprite.Offset,message);
                Assert.AreEqual(v[6]==0,sprite.Visible,message);Assert.AreEqual(v[7],sprite.Tile,message);Assert.AreEqual(v[8]!=0,sprite.HFlip,message);Assert.AreEqual(v[9]!=0,sprite.VFlip,message);
                Assert.AreEqual(v[10],sprite.Subpriority,message);CollectionAssert.AreEqual(v[11..15],new[]{sprite.MatrixA,sprite.MatrixB,sprite.MatrixC,sprite.MatrixD},message);
                Assert.AreEqual(kind is 1 or 2 or 3,sprite.Affine);Assert.IsFalse(sprite.DoubleSize);Assert.IsFalse(sprite.Blend);Assert.AreEqual(family.Width,sprite.Width);Assert.AreEqual(family.Height,sprite.Height);
            }
            int ticks=0;while(runtime.Active&&ticks++<10)runtime.Tick();AssertClean(runtime);
        }
        Assert.AreEqual(expectedCases,count,"Empty or partial vector selections must fail.");Assert.IsTrue(rows>0);
    }
    [TestMethod][DataRow(146,12,480)][DataRow(132,4,272)][DataRow(282,1,8)][DataRow(302,20,272)][DataRow(338,9,780)]
    public void CompleteRootsPreserveSourceMultiplicityRealPixelsReplayAndCleanup(int move,int expectedCreates,int expectedFrames)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);var family=Families.Single(f=>f.Move==move);
        var lines=resolved.Script.Commands.Where(c=>c.Op=="createsprite"&&c.Args[0]==family.Template).Select(c=>$"{c.Line}:createsprite").ToHashSet();
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));
            foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var cue=Cue(move,actor,target);var audio=new Audio();
            var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio());
            int ticks=0,particleFrames=0,created=0;bool pixels=false;
            while(runtime.Active&&ticks++<600)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(replay.Tick()));created+=frame.Trace.Count(lines.Contains);
                var before=JsonSerializer.Serialize(runtime.Snapshot());runtime.Snapshot();var rgba=composer.ComposeField(frame);composer.ComposeField(frame);Assert.AreEqual(before,JsonSerializer.Serialize(runtime.Snapshot()));
                particleFrames+=frame.Sprites.Count(s=>s.Template==family.Template);
                if(frame.Sprites.Any(s=>s.Template==family.Template&&s.Visible))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Template!=family.Template).ToArray()}));
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.AreEqual(expectedCreates,created);Assert.AreEqual(expectedFrames,particleFrames);Assert.IsTrue(pixels);AssertClean(runtime);
            if(move==132){Assert.AreEqual(4,audio.Cues.Count(c=>c.Id=="SE_M_SCRATCH"));Assert.AreEqual(1,audio.Cues.Count(c=>c.Id=="SE_M_BIND"));}
            if(move==146){Assert.AreEqual(1,audio.Cues.Count(c=>c.Id=="SE_M_COMET_PUNCH"));Assert.AreEqual(1,audio.Cues.Count(c=>c.Id=="SE_M_VITAL_THROW2"));}
            if(move==282){Assert.AreEqual(1,audio.Cues.Count(c=>c.Id=="SE_M_VITAL_THROW"));Assert.AreEqual(1,audio.Cues.Count(c=>c.Id=="SE_M_COMET_PUNCH"));Assert.IsFalse(resolved.Script.Commands.Any(c=>c.Args.FirstOrDefault()=="gItemBagKnockOffSpriteTemplate"));}
            if(move==302){Assert.AreEqual(16,audio.Cues.Count(c=>c.Id=="SE_M_HORN_ATTACK"));Assert.AreEqual(1,audio.Cues.Count(c=>c.Id=="SE_M_VITAL_THROW2"));}
            if(move==338){Assert.AreEqual(9,audio.Cues.Count(c=>c.Id=="SE_M_SCRATCH"));Assert.AreEqual(6,audio.Cues.Count(c=>c.Id=="SE_M_DOUBLE_SLAP"));}
        }
    }
    private static void AssertClean(SourceBattleAnimation runtime)
    {
        var frame=runtime.Snapshot();Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.IsFalse(frame.BlendEnabled);Assert.IsTrue(frame.Offsets.All(p=>p==default));Assert.IsFalse(runtime.HasResidualSprites);
    }
    [TestMethod][DataRow(146)][DataRow(132)][DataRow(282)][DataRow(302)][DataRow(338)]
    public void CancellationAtEveryLifetimePhaseAndReplacementReleaseOwnedResources(int move)
    {
        var source=AnimationCompositionFixture.Script(move);var battlers=Battlers(true);var cue=Cue(move,3,1);var baseline=new SourceBattleAnimation(source,cue,battlers,true,new Audio());int duration=0;
        while(baseline.Active&&duration<600){baseline.Tick();duration++;}Assert.IsFalse(baseline.Active);
        foreach(int requested in new[]{1,4,10,20,40,53,67,80,100,duration-1,duration,duration+5}.Distinct())
        {
            var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(source,cue,battlers,true,audio,spriteScene:scene);
            int reached=0;while(runtime.Active&&reached<requested){runtime.Tick();reached++;}Assert.AreEqual(Math.Min(requested,duration),reached);
            int sounds=audio.Cues.Count;runtime.Cancel();runtime.Cancel();AssertClean(runtime);Assert.AreEqual(0,scene.Count);Assert.AreEqual(sounds,audio.Cues.Count);
            var next=new SourceBattleAnimation(source,cue,battlers,true,new Audio(),spriteScene:scene);int ticks=0;while(next.Active&&ticks++<600)next.Tick();AssertClean(next);Assert.AreEqual(0,scene.Count);
        }
    }
    [TestMethod]
    public void MissingOrChangedRecipeAndOutOfRangeOperandsFailClosed()
    {
        void Reject(SourceAnimationScript source,int kind)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source,Cue(Families[kind].Move,0,1),Battlers(true),true,new Audio()));
        int[][] args=[[16,8,160,-32],[0,16,0,2],[-16,-16],[1,0,0,-32,16],[10,8,2,0,0,100]];
        foreach(int kind in Enumerable.Range(0,5))
        {
            var source=Primitive(kind,args[kind]);var recipe=source.SpriteRecipes[Families[kind].Template];
            foreach(var bad in new[]{recipe with{Callback="UnknownImpact"},recipe with{Width=8},recipe with{Blend=true},recipe with{AffineDoubleSize=true},recipe with{LoopStarts=[99]},recipe with{Animations=[[new(1,1,false,false)]]},recipe with{AffineAnimations=[[new("end",0,0,0,0,0)]]}})
            {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[recipe.Template]=bad;Reject(source with{SpriteRecipes=recipes},kind);}
            var missing=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);missing.Remove(recipe.Template);Reject(source with{SpriteRecipes=missing},kind);
            var overflow=(int[])args[kind].Clone();overflow[0]=32768;Reject(Primitive(kind,overflow),kind);
            if(kind is 0 or 2)Reject(source with{SineTable=[]},kind);
        }
        Reject(Primitive(1,0,16,2,2),1);Reject(Primitive(1,0,16,0,0),1);Reject(Primitive(1,0,16,0,256),1);
        Reject(Primitive(3,2,0,0,0,16),3);Reject(Primitive(3,1,2,0,0,16),3);Reject(Primitive(3,1,0,0,0,-1),3);Reject(Primitive(3,1,0,0,0,256),3);
        Reject(Primitive(4,10,0,0,0,4,100),4);Reject(Primitive(4,10,0,0,0,0,-1),4);Reject(Primitive(4,10,0,0,0,0,256),4);
    }
}
