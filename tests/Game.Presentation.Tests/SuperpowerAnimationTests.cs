using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SuperpowerAnimationTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly string[] Templates=["gSuperpowerOrbSpriteTemplate","gSuperpowerRockSpriteTemplate","gSuperpowerFireballSpriteTemplate"];
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound>Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects()=>Stops++;}
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags)!.GetValue(o)!;
    private static object? Invoke(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Flags)!.Invoke(o,a);
    private static SourceAnimationCommand C(string op,params string[] a)=>new(op,a,1);
    private static CommittedMoveAnimation Cue(int actor,int target)=>new("superpower",1,276,new(actor),new(target));
    private static AnimationBattler[] Bodies(bool doubles,int y=8,int e=0)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,y,e,64,64){SourceVisibility=new(true,false,false)}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("superpower-source-vectors.json","python tests/Reference/Superpower/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/superpower-source-vectors.json");
    private static ushort[] Terrain(SourceAnimationResources r){var b=r.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>b.PaletteColor(2,i)).ToArray();}
    [TestMethod]
    public void All7188NativeRowsMatchOrbRockAndFireballAcrossBothSides()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(276).Script;int cases=0,rows=0;
        foreach(var c in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {
            int I(string n)=>c.GetProperty(n).GetInt32();int kind=I("kind");bool doubles=c.GetProperty("doubles").GetBoolean();var owner=new SourceBattleAnimation(Primitive(script),Cue(I("actor"),I("target")),Bodies(doubles,I("yOffset"),I("elevation")),doubles,new Audio());
            var args=Field<short[]>(owner,"_animationArgs");int j=0;foreach(var a in c.GetProperty("arguments").EnumerateArray())args[j++]=(short)a.GetInt32();var initial=(short[])args.Clone();Invoke(owner,"SetAlphaBlend",0,63,12,8);
            var part=(SourceBattleAnimation.VisualSprite)Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType("SuperpowerSprite",BindingFlags.NonPublic)!,Flags,null,[4,20,script.SpriteRecipes[Templates[kind]],new AnimationSlot(I("initial")),owner],CultureInfo.InvariantCulture)!;
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int[] r=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();string why=$"case {cases} kind {kind} frame {r[0]}";
                if(r[0]>0){part.Step(owner);if(!part.Finished)part.AdvanceAnimation(owner);}
                Assert.AreEqual(r[1]!=0,!part.Finished,why+" lifetime");CollectionAssert.AreEqual(initial,args,why+" args");CollectionAssert.AreEqual(r.Skip(24).Select(x=>(short)x).ToArray(),args.Take(8).ToArray(),why+" native args");
                Assert.AreEqual(r[14]!=0,owner.Snapshot().BlendEnabled,why+" BLDCNT");
                if(r[1]!=0)
                {
                    var f=part.Frame;Assert.AreEqual(new PixelPoint(r[2],r[3]),f.Position,why);Assert.AreEqual(new PixelPoint(r[4],r[5]),f.Offset,why);Assert.AreEqual(r[6],f.Tile,why);Assert.AreEqual(r[7],f.OamPriority,why);Assert.AreEqual(r[8]!=0,f.HFlip,why);Assert.AreEqual(r[9]!=0,f.VFlip,why);Assert.IsTrue(f.Visible,why);Assert.AreEqual(kind==0,f.Blend,why);Assert.AreEqual(kind==0,f.Affine,why);Assert.AreEqual(kind==0,f.DoubleSize,why);
                    if(kind==0){var affine=Field<SourceSpriteAffineProgram>(part,"_affine");CollectionAssert.AreEqual(r.Skip(10).Take(3).ToArray(),new[]{affine.XScale,affine.YScale,affine.Rotation},why+" affine");Assert.AreEqual(r[13]!=0,affine.Ended,why);}
                }
                rows++;
            }
            Assert.IsTrue(part.Finished);owner.Cancel();cases++;
        }
        Assert.AreEqual(86,cases);Assert.AreEqual(7188,rows);
    }
    [TestMethod]
    public void CompleteRootUsesOriginalScheduleSharedMaterialsAndRestoresScene()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(276);
        var schedule=vectors.RootElement.GetProperty("schedule").EnumerateArray().Select(r=>r.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).ToArray();
        foreach(var (doubles,actor,target) in new[]{(false,0,1),(false,1,0),(true,0,3),(true,1,2),(true,2,1),(true,3,0)})
        {
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64 is >10 and <54&&i/64 is >6 and <58?(i%7<3?0x83e0:0x801f):0)).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var idle=scene.CaptureIdle(bodies,doubles);var audio=new Audio();var owner=new SourceBattleAnimation(resolved.Script,Cue(actor,target),bodies,doubles,audio,Terrain(resources),spriteScene:scene,spriteMaterials:resolved.Assets);var replay=new SourceBattleAnimation(resolved.Script,Cue(actor,target),bodies,doubles,new Audio(),Terrain(resources),spriteMaterials:resolved.Assets);
            var seen=new HashSet<SourceBattleAnimation.VisualSprite>();List<(int Frame,int Kind)>births=[];var rendered=new HashSet<string>();int ticks=0;bool clear=false,terrain=false;
            while(owner.Active&&ticks++<1000)
            {
                var f=owner.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));foreach(var s in scene.Sprites.Where(s=>seen.Add(s)))births.Add((f.Frame,Array.IndexOf(Templates,s.Frame.Template)));
                var pixels=composer.ComposeField(f);foreach(string n in Templates)if(f.Sprites.Any(s=>s.Template==n)&&!pixels.SequenceEqual(composer.ComposeField(f with{Sprites=f.Sprites.Where(s=>s.Template!=n).ToArray()})))rendered.Add(n);
                clear|=f.Sprites.Any(s=>s.Template==Templates[0])&&!f.BlendEnabled;terrain|=f.MoveBackground is {ScrollY:not 0};
            }
            CollectionAssert.AreEqual(schedule.Where(r=>r[1]==0).Select(r=>(r[0],r[2])).ToArray(),births.ToArray());CollectionAssert.AreEqual(schedule.Where(r=>r[1]==1).Select(r=>r[0]).ToArray(),audio.Cues.Select(s=>s.Frame).ToArray());
            CollectionAssert.AreEqual(new[]{"SE_M_MEGA_KICK","SE_M_EARTHQUAKE","SE_M_SWAGGER","SE_M_MEGA_KICK2"},audio.Cues.Select(s=>s.Id).ToArray());Assert.AreEqual(2,audio.Cues[1].PanningPlayer);Assert.IsTrue(clear);Assert.IsTrue(terrain);Assert.AreEqual(3,rendered.Count);Assert.IsFalse(owner.Active||replay.Active||owner.GraphicsLoaded||owner.HasResidualSprites);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsFalse(owner.Snapshot().BlendEnabled);Assert.IsTrue(owner.Snapshot().Offsets.All(p=>p==default));Assert.AreEqual(0,owner.Snapshot().BackgroundSlots.Length);Assert.IsFalse(Field<bool[]>(owner,"_affineMode").Any(x=>x));CollectionAssert.AreEqual(composer.ComposeField(idle),composer.ComposeField(owner.Snapshot()));
        }
    }
    [TestMethod]
    public void CancelDuringEveryCallbackPhaseRestoresOwnedState()
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(276);
        foreach(int stop in new[]{0,1,3,8,25,60,100,150,180,185,205,225,260})
        {var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var owner=new SourceBattleAnimation(r.Script,Cue(3,0),Bodies(true),true,audio,Terrain(resources),spriteScene:scene,spriteMaterials:r.Assets);var visibility=owner.Snapshot().BattlerVisibility;for(int n=0;n<stop&&owner.Active;n++)owner.Tick();int sounds=audio.Cues.Count;owner.Cancel();owner.Cancel();Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsFalse(owner.Active||owner.GraphicsLoaded||owner.Snapshot().BlendEnabled);Assert.IsTrue(owner.Snapshot().Offsets.All(p=>p==default));Assert.AreEqual(0,owner.Snapshot().BackgroundSlots.Length);Assert.AreEqual(1,audio.Stops);Assert.AreEqual(sounds,audio.Cues.Count);Assert.AreEqual(JsonSerializer.Serialize(visibility),JsonSerializer.Serialize(owner.Snapshot().BattlerVisibility));}
    }
    private sealed class Occupied(int slot,int fragments,bool affine=false):SourceBattleAnimation.VisualSprite(slot,100)
    {public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"retained",default,default,0,false,false,false,256,null,100){Affine=affine,Subsprites=Enumerable.Repeat(new SourceSpriteSubsprite(0,0,8,8,0,0),fragments).ToArray()};public override void Step(SourceBattleAnimation o){}}
    [TestMethod]
    public void InvalidSourceRecipesOperandsAndSceneCapacityFailBeforeEffects()
    {
        using var resources=new SourceAnimationResources(Folder);var s=resources.Resolve(276).Script;
        void Reject(SourceAnimationScript bad,SourceBattleAnimation.SpriteScene? scene=null){scene??=new();int before=scene.Count;var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(bad,Cue(0,1),Bodies(true),true,audio,spriteScene:scene));Assert.AreEqual(before,scene.Count);Assert.AreEqual(0,audio.Cues.Count);}
        foreach(string name in Templates){var r=s.SpriteRecipes[name];foreach(var bad in new[]{r with{Callback="Other"},r with{Width=8},r with{Blend=!r.Blend},r with{LoopStarts=[-1]},r with{Animations=[]}})Reject(s with{SpriteRecipes=s.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==name?bad:p.Value)});}
        foreach(var c in new[]{C("createsprite",Templates[0],"ANIM_TARGET","2","2"),C("createsprite",Templates[1],"ANIM_ATTACKER","41","200","0","0","1"),C("createsprite",Templates[1],"ANIM_ATTACKER","41","200","96","5","1"),C("createsprite",Templates[2],"ANIM_TARGET","2"),C("createvisualtask","AnimTask_TraceMonBlended","2","0","1","3","4")})Reject(s with{Commands=[c,..s.Commands]});
        foreach(int fragments in new[]{34,35}){var scene=new SourceBattleAnimation.SpriteScene();scene.Sprites.Add(new Occupied(63,fragments));if(fragments==34){var o=new SourceBattleAnimation(s,Cue(0,1),Bodies(true),true,new Audio(),spriteScene:scene);o.Cancel();}else Reject(s,scene);Assert.AreEqual(1,scene.Count);}
        var matrices=new SourceBattleAnimation.SpriteScene();for(int n=4;n<32;n++)matrices.Sprites.Add(new Occupied(n,0,true));Reject(s,matrices);
        var unused=s.Subroutines.ToDictionary(p=>p.Key,p=>p.Value);unused["UnusedSuperpower"]=[C("createsprite",Templates[0],"ANIM_TARGET","2","0"),C("return")];var owner=new SourceBattleAnimation(s with{Subroutines=unused},Cue(0,1),Bodies(true),true,new Audio());owner.Cancel();
    }
}
