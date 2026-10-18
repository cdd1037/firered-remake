using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class PetalIceAnimationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound> Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects()=>Stops++;}
    private static readonly string[] Templates=["gPetalDanceBigFlowerSpriteTemplate","gPetalDanceSmallFlowerSpriteTemplate","gIceBallChunkSpriteTemplate","gIceBallImpactShardSpriteTemplate"];
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static object? Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private|BindingFlags.Public)!.Invoke(o,args);
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static AnimationBattler[] Bodies(bool doubles,bool hidden=false)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,(i&1)==0?0:8,0,64,64){SourceVisibility=new(true,false,hidden&&i>=2)}).ToArray();
    private static CommittedMoveAnimation Cue(int id,int actor,int target,int count=1,uint seed=0x12345678)=>new("petal-ice",1,id,new(actor),new(target)){VisualSeed=seed,RolloutCounter=id==301?(byte)count:null,MovePower=id==301?(ushort)(30<<(count-1)):null};
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static ushort[] Terrain(SourceAnimationResources r){var bg=r.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>bg.PaletteColor(2,i)).ToArray();}
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("petal-ice-source-vectors.json","python tests/Reference/PetalIce/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/petal-ice-source-vectors.json");

    [TestMethod]
    public void EveryNativeFrameMatchesImmediateFloatRawFlipArcAffineBreakAndDeferredDestructor()
    {
        using var vectors=Vectors();int cases=0,rows=0;
        foreach(var c in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {
            int kind=c.GetProperty("kind").GetInt32(),actor=c.GetProperty("actor").GetInt32(),target=c.GetProperty("target").GetInt32(),count=c.GetProperty("counter").GetInt32(),id=kind<2?80:301;bool doubles=c.GetProperty("doubles").GetBoolean();uint seed=c.GetProperty("seed").GetUInt32();
            var script=AnimationCompositionFixture.Script(id);var o=new SourceBattleAnimation(Primitive(script),Cue(id,actor,target,count,seed),Bodies(doubles),doubles,new Audio());var args=Field<short[]>(o,"_animationArgs");int i=0;foreach(var a in c.GetProperty("arguments").EnumerateArray())args[i++]=(short)a.GetInt32();
            if(kind==4)
            {
                Assert.IsTrue((bool)Invoke(o,"TryRunPetalIceTask",C("createvisualtask","AnimTask_GetRolloutCounter","5",args[0].ToString()))!);var row=c.GetProperty("rows")[0].EnumerateArray().Select(x=>x.GetInt32()).ToArray();Assert.AreEqual(0,row[0]);CollectionAssert.AreEqual(row.Skip(1).Select(x=>(short)x).ToArray(),args.Take(8).ToArray());cases++;rows++;continue;
            }
            var sprite=(SourceBattleAnimation.VisualSprite)Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType("PetalIceSprite",BindingFlags.NonPublic)!,BindingFlags.Public|Private,null,new object[]{4,2,script.SpriteRecipes[Templates[kind]],o},null)!;
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                var r=row.EnumerateArray().Select(x=>x.GetInt64()).ToArray();if(r[0]>=0){sprite.Step(o);if(!sprite.Finished)sprite.AdvanceAnimation(o);}var frame=sprite.Frame;string why=$"case{cases} kind{kind} actor{actor} target{target} counter{count} frame{r[0]}";
                Assert.AreEqual(r[1]==0,sprite.Finished,why);Assert.AreEqual(new PixelPoint((int)r[2],(int)r[3]),frame.Position,why);Assert.AreEqual(new PixelPoint((int)r[4],(int)r[5]),frame.Offset,why);Assert.AreEqual((int)r[6],frame.Tile,why);Assert.AreEqual(r[7]!=0,frame.HFlip,why);Assert.AreEqual((int)r[8],frame.Subpriority,why);
                var frames=Field<SourceSpriteFrameProgram>(sprite,"_frames");Assert.AreEqual(r[9]!=0,frames.Ended,why);Assert.AreEqual((int)r[13],frames.DelayCounter,why);Assert.AreEqual((short)r[16],args[2],why);
                if(kind==2){var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");Assert.AreEqual((int)r[10],affine.XScale,why);Assert.AreEqual((int)r[11],affine.YScale,why);Assert.AreEqual(r[12]!=0,affine.Ended,why);Assert.AreEqual(r[14]!=0,Field<int>(sprite,"_phase")==2,why);}
                var random=Field<SourceVisualRandom?>(o,"_visualRandom");Assert.AreEqual((uint)r[15],random is null?seed:Field<uint>(random,"_state"),why);rows++;
            }
            cases++;
        }
        Assert.AreEqual(43,cases);Assert.AreEqual(2561,rows);
    }

    [TestMethod,DataRow(80),DataRow(301)]
    public void AuthoredRootsComposeSourceBranchesAndRestoreTheSharedScene(int id)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(id);
        var layouts=id==80?new[]{(false,0,1,1),(false,1,0,1),(true,0,1,1),(true,1,0,1),(true,2,3,1),(true,3,2,1)}:
            Enumerable.Range(1,5).SelectMany(count=>new[]{(false,0,1,count),(false,1,0,count)}).Concat(new[]{(true,2,3,3),(true,3,2,5)}).ToArray();
        foreach(var (doubles,actor,target,count) in layouts)
        {
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64 is >12 and <51&&i/64 is >8 and <55?0x83e0:0)).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(resolved.Script,Cue(id,actor,target,count),bodies,doubles,audio,Terrain(resources),spriteScene:scene,spriteMaterials:resolved.Assets);
            int ticks=0,shards=0;bool pixels=false,affine=false,background=false,big=false,small=false;
            while(o.Active&&ticks++<600)
            {
                var frame=o.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(o.Snapshot()));var rgba=composer.ComposeField(frame);Assert.AreEqual(240*112*4,rgba.Length);
                shards=Math.Max(shards,frame.Sprites.Count(s=>s.Template==Templates[3]));affine|=frame.Sprites.Any(s=>s.Affine);background|=frame.MoveBackground?.Id is not null;big|=frame.Sprites.Any(s=>s.Template==Templates[0]);small|=frame.Sprites.Any(s=>s.Template==Templates[1]);
                if(frame.Sprites.Any(s=>s.Visible))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));
            }
            Assert.IsFalse(o.Active);Assert.IsTrue(pixels);Assert.AreEqual(id==301,affine);Assert.AreEqual(id==301&&count==5,background);Assert.AreEqual(id==80,big&&small);Assert.AreEqual(id==80?0:new[]{5,7,9,10,10}[count-1],shards);
            var final=o.Snapshot();Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,o.ActiveVisualCount);Assert.IsFalse(o.GraphicsLoaded);Assert.IsFalse(final.BlendEnabled);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsTrue(final.Offsets.All(x=>x==default));Assert.IsNull(final.MoveBackground!.Id);Assert.IsTrue(audio.Cues.Count>0);
            var random=Field<SourceVisualRandom?>(o,"_visualRandom");if(id==80)Assert.IsNull(random);else{var expected=new SourceVisualRandom(0x12345678);for(int n=0;n<shards*2;n++)expected.Next();Assert.AreEqual(Field<uint>(expected,"_state"),Field<uint>(random!,"_state"));}
        }
    }

    [TestMethod,DataRow(80),DataRow(301)]
    public void CancellationAndHiddenPartnersRestoreCapturedVisibilityAndOwners(int id)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(id);
        foreach(int tick in new[]{0,1,20,29,45,80,140,200})
        {
            var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(resolved.Script,Cue(id,0,1,5),Bodies(true,true),true,audio,Terrain(resources),spriteScene:scene,spriteMaterials:resolved.Assets);var initial=o.Snapshot().BattlerVisibility;
            for(int n=0;n<tick&&o.Active;n++)o.Tick();int sounds=audio.Cues.Count;o.Cancel();o.Cancel();var final=o.Snapshot();Assert.IsFalse(o.Active);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,o.ActiveVisualCount);Assert.IsFalse(o.GraphicsLoaded);Assert.IsFalse(final.BlendEnabled);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.AreEqual(0,final.PaletteBlends.Length);Assert.IsTrue(final.Offsets.All(x=>x==default));Assert.AreEqual(sounds,audio.Cues.Count);Assert.AreEqual(1,audio.Stops);Assert.AreEqual(JsonSerializer.Serialize(initial),JsonSerializer.Serialize(final.BattlerVisibility));
        }
    }

    [TestMethod]
    public void MissingCommittedInputsAndMalformedSourceProgramsFailBeforeAudio()
    {
        using var resources=new SourceAnimationResources(Folder);
        foreach(int id in new[]{80,301})
        {
            var source=AnimationCompositionFixture.Script(id);void Reject(SourceAnimationScript script,CommittedMoveAnimation? cue=null){var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,cue??Cue(id,0,1),Bodies(false),false,audio,Terrain(resources)));Assert.AreEqual(0,audio.Cues.Count);}
            foreach(string template in Templates.Skip(id==80?0:2).Take(2))
            {
                var r=source.SpriteRecipes[template];foreach(var bad in new[]{r with{Width=64},r with{Callback="SpriteCallbackDummy"},r with{Blend=true},r with{Animations=[]},r with{AffineDoubleSize=!r.AffineDoubleSize}}){var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[template]=bad;Reject(source with{SpriteRecipes=recipes});}
            }
            if(id==301)
            {
                foreach(byte? counter in new byte?[]{null,0,6})Reject(source,Cue(id,0,1) with{RolloutCounter=counter});Reject(source,Cue(id,0,1) with{MovePower=null});Reject(source,Cue(id,0,1) with{VisualSeed=null});
                foreach(string register in new[]{"-1","8","255"})Reject(Primitive(source) with{Commands=[C("createvisualtask","AnimTask_GetRolloutCounter","5",register),C("end")]});
            }
            string name=id==80?Templates[0]:Templates[2];string[] args=id==80?[name,"0","2","0","-24","8","0"]:[name,"1","2","15","0","-12","-16","0","-40"];Reject(Primitive(source) with{Commands=[C("createsprite",args),C("end")]});
        }
    }
}
