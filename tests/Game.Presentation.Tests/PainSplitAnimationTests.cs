using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class PainSplitAnimationTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private const string Projectile="gPainSplitProjectileSpriteTemplate",Movement="AnimTask_PainSplitMovement";
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound>Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects()=>Stops++;}
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags)!.GetValue(o)!;
    private static T Property<T>(object o,string n)=>(T)o.GetType().GetProperty(n,Flags)!.GetValue(o)!;
    private static object? Invoke(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Flags)!.Invoke(o,a);
    private static object Nested(string n,params object[] a)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(n,BindingFlags.NonPublic)!,Flags,null,a,CultureInfo.InvariantCulture)!;
    private static SourceAnimationCommand C(string op,params string[] a)=>new(op,a,1);
    private static CommittedMoveAnimation Cue(int actor,int target)=>new("pain-split",1,220,new(actor),new(target));
    private static AnimationBattler[] Bodies(bool doubles,int y=8,int e=0,int hidden=0)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,y,e,64,64){SourceVisibility=new(true,(hidden&(1<<i))!=0,(hidden&(1<<i))!=0)}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("pain-split-source-vectors.json","python tests/Reference/PainSplit/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/pain-split-source-vectors.json");
    private static ushort[] Terrain(SourceAnimationResources r){var bg=r.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>bg.PaletteColor(2,i)).ToArray();}

    [TestMethod]
    public void OriginalFiniteProjectileAndThirteenFrameTransformMatchAll1848NativeRows()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(220).Script;int cases=0,rows=0;
        foreach(var c in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {
            int kind=c.GetProperty("kind").GetInt32(),actor=c.GetProperty("actor").GetInt32(),target=c.GetProperty("target").GetInt32(),hidden=c.GetProperty("hiddenMask").GetInt32();bool doubles=c.GetProperty("doubles").GetBoolean();var bodies=Bodies(doubles,c.GetProperty("yOffset").GetInt32(),c.GetProperty("elevation").GetInt32(),hidden);
            var owner=new SourceBattleAnimation(Primitive(script),Cue(actor,target),bodies,doubles,new Audio());var args=Field<short[]>(owner,"_animationArgs");int i=0;foreach(var a in c.GetProperty("arguments").EnumerateArray())args[i++]=(short)a.GetInt32();var untouched=(short[])args.Clone();int selected=args[0]==0?actor:target;
            if(kind==1){int offset=c.GetProperty("initialOffset").GetInt32();Field<PixelPoint[]>(owner,"_offsets")[selected]=new(offset,-offset);}
            object part=kind==0?Nested("PainSplitSprite",4,2,script.SpriteRecipes[Projectile],owner):Nested("PainSplitTask",owner);
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int[] r=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();string why=$"case {cases}, kind {kind}, frame {r[0]}";
                if(r[0]>0){Invoke(part,"Step",owner);if(kind==0&&!Property<bool>(part,"Finished"))Invoke(part,"AdvanceAnimation",owner);}
                Assert.AreEqual(r[1]!=0,!Property<bool>(part,"Finished"),why);CollectionAssert.AreEqual(untouched,args,why+" no argument writes");CollectionAssert.AreEqual(r.Skip(kind==0?14:17).Select(x=>(short)x).ToArray(),args.Take(8).ToArray(),why+" native argument registers");
                if(kind==0&&r[1]!=0)
                {
                    var f=((SourceBattleAnimation.VisualSprite)part).Frame;Assert.AreEqual(new PixelPoint(r[2],r[3]),f.Position,why);Assert.AreEqual(new PixelPoint(r[4],r[5]),f.Offset,why);Assert.AreEqual(r[6],f.Tile,why);Assert.IsTrue(f.Visible);Assert.IsFalse(f.Affine||f.Blend||f.HFlip||f.VFlip);Assert.AreEqual((16,16),(f.Width,f.Height));
                    var frames=Field<SourceSpriteFrameProgram>(part,"_frames");Assert.AreEqual(r[7]!=0,frames.Ended,why+" finite frame END");Assert.AreEqual(r[8],frames.DelayCounter,why);Assert.AreEqual((short)r[10],Field<short>(part,"_fixedX"),why+" signed fixed X");Assert.AreEqual((short)r[11],Field<short>(part,"_velocityY"),why+" truncating bounce velocity");Assert.AreEqual((short)r[12],Field<short>(part,"_initialY"),why);Assert.AreEqual(r[13]!=0,Field<bool>(part,"_bounced"),why+" one bounce only");
                }
                else if(kind==1)
                {
                    Assert.AreEqual(selected,r[2],why);Assert.AreEqual(new PixelPoint(r[3],r[4]),Field<PixelPoint[]>(owner,"_offsets")[selected],why+" correction and shake");Assert.AreEqual(r[5]==3,Field<bool[]>(owner,"_affineMode")[selected],why+" affine ownership");
                    CollectionAssert.AreEqual(r.Skip(10).Take(4).ToArray(),new[]{Field<int[]>(owner,"_xScale")[selected],Field<int[]>(owner,"_matrixB")[selected],Field<int[]>(owner,"_matrixC")[selected],Field<int[]>(owner,"_yScale")[selected]},why+" original matrix");
                    Assert.AreEqual(r[14],Field<int>(part,"_ticks"),why);Assert.AreEqual(r[15],Field<int>(part,"_shake"),why);Assert.AreEqual((hidden&(1<<selected))!=0,r[6]!=0,why+" native hidden body");Assert.AreEqual(r[6]!=0,(bool)Invoke(owner,"SourceBattlerSpriteInvisible",selected)!,why+" runtime hidden body");
                    Assert.IsTrue(Field<PixelPoint[]>(owner,"_offsets").Where((_,slot)=>slot!=selected).All(p=>p==default),why+" other battlers untouched");
                    if(r[0]==0){int mode=args[1];CollectionAssert.AreEqual(new[]{mode==0?224:208,mode==0?320:304,mode==0?0:mode==1?0xf00:0xf100},r.Skip(7).Take(3).ToArray(),why+" native scale and rotation inputs");}
                }
                rows++;
            }
            Assert.IsTrue(Property<bool>(part,"Finished"));owner.Cancel();cases++;
        }
        Assert.AreEqual(104,cases);Assert.AreEqual(1848,rows);
    }

    [TestMethod]
    public void CompleteRootMatchesSourceBirthSoundAndDrainScheduleInSinglesAndDoubles()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(220);var schedule=vectors.RootElement.GetProperty("schedule").EnumerateArray().Select(r=>r.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).ToArray();
        foreach(var (doubles,actor,target) in new[]{(false,0,1),(false,1,0),(true,0,3),(true,1,2),(true,2,1),(true,3,0)})
        {
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64 is >10 and <54&&i/64 is >6 and <58?(i%7<3?0x83e0:0x801f):0)).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var idle=scene.CaptureIdle(bodies,doubles);var audio=new Audio();var owner=new SourceBattleAnimation(resolved.Script,Cue(actor,target),bodies,doubles,audio,Terrain(resources),spriteScene:scene,spriteMaterials:resolved.Assets);var replay=new SourceBattleAnimation(resolved.Script,Cue(actor,target),bodies,doubles,new Audio(),Terrain(resources),spriteMaterials:resolved.Assets);
            List<int>births=[];var seen=new HashSet<SourceBattleAnimation.VisualSprite>();bool projectilePixels=false,affinePixels=false;int ticks=0,maxOwners=0;
            while(owner.Active&&ticks++<120)
            {
                var f=owner.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(owner.Snapshot()));Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));maxOwners=Math.Max(maxOwners,owner.ActiveVisualCount);foreach(var s in scene.Sprites.Where(s=>seen.Add(s)))births.Add(f.Frame);
                var pixels=composer.ComposeField(f);if(f.Sprites.Length>0)projectilePixels|=!pixels.SequenceEqual(composer.ComposeField(f with{Sprites=[]}));
                if(Field<bool[]>(owner,"_affineMode").Any(x=>x))affinePixels|=!composer.ComposeField(f with{Sprites=[]}).SequenceEqual(composer.ComposeField(idle));
            }
            CollectionAssert.AreEqual(schedule.Where(r=>r[1]==0).Select(r=>r[0]).ToArray(),births.ToArray());CollectionAssert.AreEqual(schedule.Where(r=>r[1]==2).Select(r=>r[0]).ToArray(),audio.Cues.Select(s=>s.Frame).ToArray());Assert.IsTrue(audio.Cues.All(s=>s.Id=="SE_M_SWAGGER2"&&s.Pan==0));Assert.AreEqual(schedule.Single(r=>r[1]==3)[0],owner.Snapshot().Frame);Assert.AreEqual(4,maxOwners);Assert.IsTrue(projectilePixels);Assert.IsTrue(affinePixels);Assert.IsFalse(owner.Active||replay.Active||owner.GraphicsLoaded||owner.HasResidualSprites);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsNull(Field<object?>(owner,"_visualRandom"));Assert.IsTrue(owner.Snapshot().Offsets.All(p=>p==default));Assert.IsFalse(Field<bool[]>(owner,"_affineMode").Any(x=>x));CollectionAssert.AreEqual(composer.ComposeField(idle),composer.ComposeField(owner.Snapshot()));
        }
    }

    [TestMethod]
    public void CancellationAcrossEveryWaveAndAffinePhaseRestoresCapturedVisibilityAndPose()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(220);
        foreach(int stop in new[]{0,1,3,9,15,17,26,28,40,42,52,53,65,67,77,78})
        {
            var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var owner=new SourceBattleAnimation(resolved.Script,Cue(3,0),Bodies(true,20,32,1<<2),true,audio,Terrain(resources),spriteScene:scene,spriteMaterials:resolved.Assets);var initial=owner.Snapshot().BattlerVisibility;
            for(int tick=0;tick<stop&&owner.Active;tick++)owner.Tick();int sounds=audio.Cues.Count;owner.Cancel();owner.Cancel();Assert.IsFalse(owner.Active||owner.GraphicsLoaded);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.AreEqual(1,audio.Stops);Assert.AreEqual(sounds,audio.Cues.Count);Assert.IsTrue(owner.Snapshot().Offsets.All(p=>p==default));Assert.IsFalse(Field<bool[]>(owner,"_affineMode").Any(x=>x));Assert.IsTrue(Field<int[]>(owner,"_xScale").All(x=>x==256));Assert.IsTrue(Field<int[]>(owner,"_yScale").All(x=>x==256));Assert.AreEqual(JsonSerializer.Serialize(initial),JsonSerializer.Serialize(owner.Snapshot().BattlerVisibility));
        }
    }
    private sealed class Occupied(int slot,int fragments,bool affine=false):SourceBattleAnimation.VisualSprite(slot,100)
    {public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"retained",default,default,0,false,false,false,256,null,100){Affine=affine,Subsprites=Enumerable.Repeat(new SourceSpriteSubsprite(0,0,8,8,0,0),fragments).ToArray()};public override void Step(SourceBattleAnimation owner){}}
    [TestMethod]
    public void CompletePathCountsSixSpritesAllTasksAndRetainedOamBeforeEffects()
    {
        using var resources=new SourceAnimationResources(Folder);var s=resources.Resolve(220).Script;Assert.AreEqual(6,SourceAnimationProgramDependencies.MaximumCost(s,c=>c.Op=="createsprite"?1:0));
        var unused=s.Subroutines.ToDictionary(p=>p.Key,p=>p.Value);unused["UnusedPainSplitOwner"]=[C("createvisualtask",Movement,"2","ANIM_ATTACKER","0"),C("return")];
        var unusedLabels=s.SubroutineLabels.ToDictionary(p=>p.Key,p=>p.Value);unusedLabels["UnusedPainSplitOwner"]=new Dictionary<string,int>();var unusedScript=s with{Subroutines=unused,SubroutineLabels=unusedLabels};
        Assert.AreEqual(6,SourceAnimationProgramDependencies.MaximumCost(unusedScript,c=>c.Op=="createsprite"?1:0));var uninvoked=new SourceBattleAnimation(unusedScript,Cue(0,1),Bodies(true),true,new Audio());uninvoked.Cancel();
        foreach(int fragments in new[]{36,37}){var scene=new SourceBattleAnimation.SpriteScene();scene.Sprites.Add(new Occupied(63,fragments));var audio=new Audio();if(fragments==36){var owner=new SourceBattleAnimation(s,Cue(0,1),Bodies(true),true,audio,spriteScene:scene);owner.Cancel();}else Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(0,1),Bodies(true),true,audio,spriteScene:scene));Assert.AreEqual(1,scene.Count);Assert.AreEqual(0,audio.Cues.Count);}
        var matrices=new SourceBattleAnimation.SpriteScene();for(int i=4;i<33;i++)matrices.Sprites.Add(new Occupied(i,0,true));Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(0,1),Bodies(true),true,new Audio(),spriteScene:matrices));Assert.AreEqual(29,matrices.Count);
        var sounds=Enumerable.Repeat(C("waitplaysewithpan","SE_M_SWAGGER2","0","1"),10);Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s with{Commands=[..sounds,..s.Commands]},Cue(0,1),Bodies(true),true,new Audio()));
    }
    [TestMethod]
    public void CorruptRecipeMissingSoundInvalidArgumentsAndOverlappingBattlerOwnersFailClosed()
    {
        using var resources=new SourceAnimationResources(Folder);var s=resources.Resolve(220).Script;var r=s.SpriteRecipes[Projectile];
        void Reject(SourceAnimationScript bad){var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(bad,Cue(0,1),Bodies(true),true,audio,spriteScene:scene));Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,audio.Cues.Count);}
        foreach(var recipe in new[]{r with{Callback="Other"},r with{Width=32},r with{Blend=true},r with{AffineDoubleSize=true},r with{Animations=[]},r with{LoopStarts=[0]},r with{FrameLoops=[[],[]]},r with{Animations=[[new(0,5,false,false),new(4,8,false,false),new(8,5,false,false)]]}})Reject(s with{SpriteRecipes=s.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==Projectile?recipe:p.Value)});
        Reject(s with{SoundEffects=new Dictionary<string,int>()});Reject(s with{SpriteTags=new Dictionary<string,int>()});
        foreach(var c in new[]{C("createsprite",Projectile,"ANIM_TARGET","2","-8","-42","2"),C("createsprite",Projectile,"ANIM_TARGET","2","32768","-42","1"),C("createvisualtask",Movement,"2","2","0"),C("createvisualtask",Movement,"2","1","3"),C("createvisualtask",Movement,"2","1")})Reject(s with{Commands=[c,..s.Commands]});
        Reject(s with{Commands=s.Commands.Where(c=>c.Op!="waitforvisualfinish").ToArray()});Reject(s with{Commands=[C("createvisualtask","AnimTask_ShakeMon","2","ANIM_TARGET","1","0","2","1"),..s.Commands]});
    }
}
