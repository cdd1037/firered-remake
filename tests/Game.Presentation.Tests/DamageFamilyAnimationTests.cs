using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class DamageFamilyAnimationTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio
    {
        public readonly List<SourceAnimationSound> Cues=[];public int Stops;
        public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);
        public void StopAnimationEffects()=>Stops++;public void SetPan(int pan,int frame){}
    }
    private sealed class Occupied(int slot,bool affine=false):SourceBattleAnimation.VisualSprite(slot,100)
    {
        public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame=>new(Slot,"external",default,default,0,false,false,false,256,null,100){Affine=affine};
        public override void Step(SourceBattleAnimation owner){}
    }
    private static T Field<T>(object value,string name)=>(T)value.GetType().GetField(name,Flags)!.GetValue(value)!;
    private static T Property<T>(object value,string name)=>(T)value.GetType().GetProperty(name,Flags)!.GetValue(value)!;
    private static object? Invoke(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Flags)!.Invoke(value,args);
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceAnimationScript Primitive(SourceAnimationScript source)=>source with
    {Commands=[C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static AnimationBattler[] Bodies(bool doubles)=>Enumerable.Range(0,doubles?4:2)
        .Select(i=>new AnimationBattler(new(i),"body"+i,0,0,64,64){SourceVisibility=new(true,false,false)}).ToArray();
    private static ushort[] Terrain(SourceAnimationResources resources)
    {var surface=resources.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray();}

    [TestMethod]
    [DataRow(229)][DataRow(245)][DataRow(279)][DataRow(314)]
    public void CompleteRootsComposeTheirSourceMechanismsAtAllSixBattlePositions(int move)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})foreach(int actor in Enumerable.Range(0,doubles?4:2))
        {
            int target=actor^1;string why=$"move {move}, doubles {doubles}, actor {actor}";
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(pair=>pair.Key,pair=>pair.Value);
            assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());
            foreach(var body in bodies)assets[body.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64 is >5 and <58&&i/64 is >5 and <58?(i%7<3?0x83e0:0x801f):0)).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();
            var animation=new SourceBattleAnimation(resolved.Script,new("damage-complete",1,move,new(actor),new(target)){MovePower=move==229?20:null},bodies,doubles,audio,Terrain(resources),spriteScene:scene,spriteMaterials:assets);
            var initial=animation.Snapshot();var idle=composer.ComposeField(initial);int ticks=0,scanlineFrames=0,hiddenFrames=0,affineFrames=0,dustBirths=0,waveBirths=0;
            bool effectPixels=false,scanlinePixels=false;var observed=new HashSet<string>();var previous=new HashSet<(string,int)>();
            while(animation.Active&&ticks++<1000)
            {
                var frame=animation.Tick();var pixels=composer.ComposeField(frame);Assert.AreEqual(240*112*4,pixels.Length,why);
                Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(animation.Snapshot()),why);
                Assert.IsTrue(scene.Count<=64,why);
                if(frame.Scanline is not null){scanlineFrames++;scanlinePixels|=!pixels.SequenceEqual(composer.ComposeField(frame with{Scanline=null}));}
                hiddenFrames+=frame.BattlerVisibility.Any(b=>b.SpriteInvisible)?1:0;
                affineFrames+=frame.Sprites.Any(s=>s.Affine)?1:0;
                var current=frame.Sprites.Select(s=>(s.Template,s.Slot)).ToHashSet();
                foreach(var sprite in frame.Sprites)
                {
                    observed.Add(sprite.Template);
                    if(!previous.Contains((sprite.Template,sprite.Slot))){if(sprite.Template=="gSpeedDustSpriteTemplate")dustBirths++;if(sprite.Template=="gAirWaveProjectileSpriteTemplate")waveBirths++;}
                }
                previous=current;
                if(frame.Sprites.Length>0)effectPixels|=!pixels.SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));
            }
            Assert.IsFalse(animation.Active,why);Assert.IsTrue(ticks>30,why);Assert.IsTrue(effectPixels,why);
            Assert.AreEqual(0,scene.Count,why);Assert.AreEqual(0,animation.ActiveVisualCount,why);Assert.IsFalse(animation.GraphicsLoaded,why);
            var end=animation.Snapshot();Assert.IsNull(end.Scanline,why);Assert.IsNull(end.MoveBackground!.Id,why);Assert.AreEqual(0,end.BackgroundSlots.Length,why);
            Assert.AreEqual(JsonSerializer.Serialize(initial.BattlerVisibility),JsonSerializer.Serialize(end.BattlerVisibility),why);
            CollectionAssert.AreEqual(idle,composer.ComposeField(end),why);
            switch(move)
            {
                case 229:
                    Assert.IsTrue(observed.Contains("gRapidSpinSpriteTemplate"),why);Assert.IsTrue(scanlineFrames>60&&scanlinePixels,why);
                    Assert.AreEqual(8,audio.Cues.Count(c=>c.Id=="SE_M_RAZOR_WIND2"),why);break;
                case 245:
                    Assert.IsTrue(observed.Contains("gMonEdgeHitSplatSpriteTemplate"),why);Assert.IsTrue(hiddenFrames>0&&affineFrames>0,why);Assert.AreEqual(24,dustBirths,why);break;
                case 279:
                    Assert.IsTrue(new[]{"gRevengeSmallScratchSpriteTemplate","gRevengeBigScratchSpriteTemplate","gPersistHitSplatSpriteTemplate"}.All(observed.Contains),why);Assert.IsTrue(affineFrames>0,why);
                    CollectionAssert.AreEqual(new[]{"SE_M_TAKE_DOWN","SE_M_SWAGGER","SE_M_VITAL_THROW2","SE_M_MEGA_KICK2"},audio.Cues.Select(c=>c.Id).ToArray(),why);break;
                case 314:
                    Assert.IsTrue(observed.Contains("gAirCutterSliceSpriteTemplate"),why);Assert.AreEqual(3,waveBirths,why);
                    CollectionAssert.AreEqual(new[]{"SE_M_BLIZZARD2","SE_M_BLIZZARD2","SE_M_BLIZZARD2","SE_M_CUT"},audio.Cues.Select(c=>c.Id).ToArray(),why);break;
            }
            animation.Cancel();
        }
    }

    [TestMethod]
    [DataRow(229)][DataRow(245)][DataRow(279)][DataRow(314)]
    public void CancellationAcrossPhaseBoundariesRestoresOnlyOwnedState(int move)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(move);
        foreach(int stop in new[]{0,1,4,8,14,24,40,60,90,120,150,190,230,290})
        {
            var bodies=Bodies(true);bodies[2]=bodies[2] with{SourceVisibility=new(true,false,true)};
            var scene=new SourceBattleAnimation.SpriteScene();var previousOwner=new SourceBattleAnimation(Primitive(resolved.Script),new("damage-residual",1,move,new(0),new(1)),bodies,true,new Audio(),spriteScene:scene);
            Invoke(Field<object>(previousOwner,"_sprites"),"Add",new Occupied(63));previousOwner.Tick();Assert.IsFalse(previousOwner.Active);Assert.IsTrue(previousOwner.HasResidualSprites);var audio=new Audio();
            var animation=new SourceBattleAnimation(resolved.Script,new("damage-cancel",1,move,new(0),new(1)){MovePower=move==229?20:null},bodies,true,audio,Terrain(resources),spriteScene:scene,spriteMaterials:resolved.Assets);
            var initial=animation.Snapshot();var scanline=Property<SourceScanlineState>(animation,"ScanlineState");
            for(int i=0;i<stop&&animation.Active;i++)animation.Tick();
            var buffers=Enumerable.Range(0,1920).Select(i=>scanline.Read(i/960,i%960)).ToArray();int sounds=audio.Cues.Count;
            animation.Cancel();animation.Cancel();Assert.AreEqual(1,scene.Count);Assert.AreEqual(0,animation.ActiveVisualCount);Assert.IsFalse(animation.GraphicsLoaded);
            Assert.AreEqual(0,scanline.State);CollectionAssert.AreEqual(buffers,Enumerable.Range(0,1920).Select(i=>scanline.Read(i/960,i%960)).ToArray());
            var end=animation.Snapshot();Assert.IsNull(end.Scanline);Assert.IsNull(end.MoveBackground!.Id);Assert.AreEqual(0,end.BackgroundSlots.Length);
            Assert.AreEqual(JsonSerializer.Serialize(initial.BattlerVisibility),JsonSerializer.Serialize(end.BattlerVisibility));Assert.AreEqual(1,audio.Stops);Assert.AreEqual(sounds,audio.Cues.Count);previousOwner.Cancel();Assert.AreEqual(0,scene.Count);
        }
    }

    [TestMethod]
    [DataRow(229)][DataRow(245)][DataRow(279)][DataRow(314)]
    public void CapacityAndMalformedProgramsFailBeforeAudioOrSceneMutation(int move)
    {
        var source=AnimationCompositionFixture.Script(move);var primitive=Primitive(source);
        void Reject(SourceAnimationScript script,int retained=0,bool affine=false)
        {
            var scene=new SourceBattleAnimation.SpriteScene();var registry=Field<object>(scene,"Sprites");for(int i=0;i<retained;i++)Invoke(registry,"Add",new Occupied(63-i,affine));var audio=new Audio();
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,new("damage-reject",1,move,new(0),new(1)){MovePower=move==229?20:null},Bodies(true),true,audio,new ushort[16],spriteScene:scene));
            Assert.AreEqual(0,audio.Cues.Count);Assert.AreEqual(retained,scene.Count);
        }
        Reject(source,42);Reject(source,29,true);Reject(source with{SineTable=source.SineTable.Take(319).ToArray()});
        if(move==229)
        {
            Reject(primitive with{Commands=[C("createvisualtask","AnimTask_RapinSpinMonElevation","2","0","0","0"),C("end")]});
            Reject(primitive with{Commands=[C("createvisualtask","AnimTask_RapinSpinMonElevation","2","0","2","0"),C("createvisualtask","AnimTask_RapinSpinMonElevation","2","0","2","1"),C("end")]});
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source,new("rapid-missing-power",1,move,new(0),new(1)),Bodies(false),false,new Audio()));
        }
        if(move==245)
        {
            Reject(source with{SpriteDependencies=[]});Reject(source with{TaskAffinePrograms=new Dictionary<string,SourceTaskAffineCommand[]>()});
            Reject(primitive with{Commands=[C("createvisualtask","AnimTask_SpeedDust","2"),C("waitforvisualfinish"),C("createvisualtask","AnimTask_SpeedDust","2"),C("end")]});
        }
        if(move==279)
        {
            var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes.Remove("gPersistHitSplatSpriteTemplate");Reject(source with{SpriteRecipes=recipes});
            Reject(primitive with{Commands=[C("createsprite","gPersistHitSplatSpriteTemplate","1","3","0","0","1","4","8"),C("end")]});
        }
        if(move==314)
        {
            Reject(source with{SpriteDependencies=[]});var sounds=source.SoundEffects.ToDictionary(p=>p.Key,p=>p.Value);sounds.Remove("SE_M_BLIZZARD2");Reject(source with{SoundEffects=sounds});
            foreach(int speed in new[]{0,1})Reject(primitive with{Commands=[C("createvisualtask","AnimTask_AirCutterProjectile","2","32","-24",speed.ToString(),"2","128"),C("end")]});
            Reject(primitive with{Commands=[C("createvisualtask","AnimTask_AirCutterProjectile","2","32","-24","1536","32","128"),C("end")]});
        }
    }

    [TestMethod]
    public void AirCutterFixedPointValidationAdmitsSmallestPositiveReciprocalAndZeroVerticalNumerator()
    {
        var source=Primitive(AnimationCompositionFixture.Script(314));
        foreach(int speed in new[]{2,3,4,6,1536,32766})
        {
            var script=source with{Commands=[C("createvisualtask","AnimTask_AirCutterProjectile","2","32","40",speed.ToString(),"2","128"),C("end")]};
            var animation=new SourceBattleAnimation(script,new("air-valid-fixed-point",1,314,new(0),new(1)),Bodies(false),false,new Audio());animation.Cancel();
        }
        var bodies=Bodies(true);bodies[1]=bodies[1] with{SourceVisibility=new(false,true,true)};
        var zero=source with{Commands=[C("createvisualtask","AnimTask_AirCutterProjectile","2","-62","0","1536","2","128"),C("end")]};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(zero,new("air-zero-duration",1,314,new(2),new(3)),bodies,true,new Audio()));
    }
}
