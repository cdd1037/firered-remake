using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class WeatherAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {
        public List<SourceAnimationSound> Cues{get;}=[];public List<(int Pan,int Frame)> Pans{get;}=[];
        public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));public void StopAnimationEffects(){}
    }
    private const string Crescent="gFlyingSandCrescentSpriteTemplate",Rain="gRainDropSpriteTemplate",Sun="gSunlightRaySpriteTemplate";
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles=true)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor=0,int target=1)=>new("weather",1,move,new(actor),new(target)){VisualSeed=0x12345678};
    private static SourceAnimationScript Script(int move)=>AnimationCompositionFixture.Script(move);
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceAnimationScript Primitive(SourceAnimationScript script,params SourceAnimationCommand[] commands)=>script with
    {Commands=commands,Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    [TestMethod][DataRow(201)][DataRow(240)][DataRow(241)][DataRow(257)]
    public void CompleteWeatherRootsReplayComposeBothSidesAndDrainTheirSourceResources(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        using var schedules=SourceVectorFixture.Read("weather-schedule-source-vectors.json", "python tests/Reference/Weather/check_vectors.py --reference content/raw/pokefirered/037335f4 --prepare tests/Game.Presentation.Tests/Fixtures");
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            int target=move==257?actor^1:actor;var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(x=>x.Key,x=>x.Value);
            assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray()));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            assets.TryAdd("impact",new(32,32,new ushort[1024]));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();var cue=Cue(move,actor,target);
            var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio,spriteMaterials:resolved.Assets);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio(),spriteMaterials:resolved.Assets);
            var schedule=move is 201 or 257?schedules.RootElement.GetProperty("cases").EnumerateArray().Single(c=>c.GetProperty("move").GetInt32()==move&&c.GetProperty("attacker").GetInt32()==actor):default;
            JsonElement[] scheduleRows=move is 201 or 257?schedule.GetProperty("rows").EnumerateArray().ToArray():[];List<int> births=[];
            bool pixels=false,background=false,palette=false;int ticks=0,overlayTicks=0,maxSprites=0,maxPieces=0;
            while(runtime.Active&&ticks++<500)
            {
                var f=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(runtime.Snapshot()));
                if(f.Trace.Any(t=>t.EndsWith(":createsprite",StringComparison.Ordinal)))births.Add(f.Frame);
                if(f.Frame<scheduleRows.Length)Assert.AreEqual(scheduleRows[f.Frame].GetProperty("visibleOamCount").GetInt32(),f.Sprites.Length,$"Source script schedule move={move} actor={actor} frame={f.Frame}");
                maxSprites=Math.Max(maxSprites,f.Sprites.Length);maxPieces=Math.Max(maxPieces,f.Sprites.Sum(s=>Math.Max(1,s.Subsprites.Length)));
                if(f.Overlay is not null)
                {
                    overlayTicks++;Assert.AreEqual("sandstorm",f.Overlay.MaterialId);Assert.AreEqual(2,f.BlendFirstTargets);Assert.AreEqual(63,f.BlendSecondTargets);Assert.AreEqual(1,f.BattlerBackgroundPriorities[0]);
                    if(ticks%13==0&&f.AlphaA>0)background|=!composer.ComposeField(f).SequenceEqual(composer.ComposeField(f with{Overlay=null}));
                }
                if(f.Sprites.Length>0&&ticks%11==0)pixels|=!composer.ComposeField(f).SequenceEqual(composer.ComposeField(f with{Sprites=[]}));
                palette|=f.PaletteBlends.Any(p=>p.Coefficient==(move==240?4:6));
                foreach(var s in f.Sprites)
                {
                    Assert.IsNotNull(s.MaterialPalette);
                    if(move is 201 or 257){Assert.AreEqual(Crescent,s.Template);Assert.AreEqual(2,s.Subsprites.Length);Assert.AreEqual(move==257&&!cue.Actor.IsPlayer,s.HFlip);}
                    else if(move==240){Assert.AreEqual(Rain,s.Template);Assert.IsFalse(s.Affine);}
                    else{Assert.AreEqual(Sun,s.Template);Assert.IsTrue(s.Affine);Assert.IsTrue(s.Blend);Assert.IsFalse(s.DoubleSize);}
                }
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.IsTrue(pixels);Assert.IsTrue(maxSprites>0);AssertClean(runtime);
            Assert.AreEqual(1,audio.Cues.Count);
            if(move is 201 or 257)
            {
                Assert.IsTrue(background);Assert.AreEqual(157,overlayTicks);
                CollectionAssert.AreEqual(schedule.GetProperty("birthFrames").EnumerateArray().Select(v=>v.GetInt32()).ToArray(),births.ToArray());
                int peak=schedule.GetProperty("peakVisibleOamCount").GetInt32();Assert.AreEqual(4,peak);Assert.AreEqual(peak,maxSprites);Assert.AreEqual(2*peak,maxPieces);
            }
            if(move==240){Assert.AreEqual(14,maxSprites);Assert.IsTrue(palette);}
            if(move==241){Assert.AreEqual(4,maxSprites);Assert.IsTrue(palette);Assert.AreEqual(-64,audio.Cues[0].Pan);CollectionAssert.AreEqual(Enumerable.Range(-63,127).ToArray(),audio.Pans.Select(p=>p.Pan).ToArray());}
            if(move==257)Assert.IsTrue(palette);
            if(move is 240 or 241)Assert.IsFalse(runtime.Snapshot().PaletteBlends.Any(p=>p.Coefficient!=0));
        }
    }
    [TestMethod]
    public void CancellationDuringLoadingRampEmissionAndCleanupReleasesAllOwnedWeatherState()
    {
        foreach(int move in new[]{201,240,241,257})foreach(int stop in new[]{0,4,24,75,140,165})
        {
            var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(Script(move),Cue(move),Battlers(),true,audio,spriteScene:scene);
            for(int i=0;i<stop&&runtime.Active;i++)runtime.Tick();int cues=audio.Cues.Count,pans=audio.Pans.Count;
            runtime.Cancel();runtime.Cancel();AssertClean(runtime);Assert.AreEqual(0,scene.Count);Assert.AreEqual(cues,audio.Cues.Count);Assert.AreEqual(pans,audio.Pans.Count);
        }
    }
    [TestMethod]
    public void RainRawChildrenOutliveAnIsolatedEmitterAndKeepTheirPaletteAcrossControllers()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(240);var scene=new SourceBattleAnimation.SpriteScene();var battlers=Battlers();int tag=resolved.Script.SpriteTags["ANIM_TAG_RAIN_DROPS"];
        ushort[] red=Enumerable.Repeat((ushort)31,16).ToArray(),blue=Enumerable.Repeat((ushort)(31<<10),16).ToArray();
        var oldScript=Primitive(resolved.Script,C("loadspritegfx","ANIM_TAG_RAIN_DROPS"),C("createvisualtask","AnimTask_CreateRaindrops","2","0","3","6"),C("waitforvisualfinish"),C("end")) with{SpritePalettes=new Dictionary<int,ushort[]>{{tag,red}}};
        var oldAssets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);oldAssets["rain-drops"]=oldAssets["rain-drops"] with{Palette=red};
        var old=new SourceBattleAnimation(oldScript,Cue(240),battlers,true,new Audio(),spriteScene:scene,spriteMaterials:oldAssets);int ticks=0;
        while(old.Active&&ticks++<30)old.Tick();Assert.IsFalse(old.Active);Assert.AreEqual(0,old.ActiveVisualCount);Assert.IsTrue(old.HasResidualSprites);Assert.IsFalse(old.GraphicsLoaded);Assert.IsTrue(scene.Count>0);
        var nextScript=Primitive(resolved.Script,C("loadspritegfx","ANIM_TAG_RAIN_DROPS"),C("delay","100"),C("end")) with{SpritePalettes=new Dictionary<int,ushort[]>{{tag,blue}}};
        var next=new SourceBattleAnimation(nextScript,Cue(240) with{Sequence=2},battlers,true,new Audio(),spriteScene:scene,spriteMaterials:resolved.Assets);SourceAnimationFrame frame=next.Snapshot();
        for(int i=0;i<4;i++)frame=next.Tick();Assert.IsTrue(frame.Sprites.Length>0);Assert.AreEqual(0,next.ActiveVisualCount);
        foreach(var sprite in frame.Sprites)CollectionAssert.AreEqual(red,sprite.MaterialPalette);
        CollectionAssert.AreEqual(blue,frame.SpritePaletteSlots[4]);next.Cancel();Assert.IsTrue(old.HasResidualSprites);Assert.IsFalse(next.HasResidualSprites);
        ticks=0;while(old.HasResidualSprites&&ticks++<30)scene.TickIdle(battlers,true);Assert.IsFalse(old.HasResidualSprites);Assert.AreEqual(0,scene.Count);
        scene.Cancel();scene.Cancel();Assert.AreEqual(0,scene.Count);
    }
    [TestMethod]
    public void WeatherDependenciesAndSourceRecipesFailBeforeAudioOrSceneSideEffects()
    {
        var sand=Script(201);var rain=Script(240);var sun=Script(241);var crescent=sand.SpriteRecipes[Crescent];var drop=rain.SpriteRecipes[Rain];var ray=sun.SpriteRecipes[Sun];
        SourceAnimationScript Changed(SourceAnimationScript script,SourceSpriteRecipe recipe)=>script with{SpriteRecipes=script.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==recipe.Template?recipe:p.Value)};
        SourceAnimationScript[] invalid=[
            sand with{OverlayDependencies=[]},sand with{OverlayPalettes=new Dictionary<string,ushort[]>()},sand with{SpritePalettes=new Dictionary<int,ushort[]>()},
            sand with{OverlayPalettes=new Dictionary<string,ushort[]>{{"sandstorm",new ushort[15]}}},Changed(sand,crescent with{Subsprites=[]}),
            Changed(sand,crescent with{Subsprites=[new(-16,0,32,16,0,2),new(16,0,32,16,8,1)]}),
            rain with{SpriteDependencies=[]},rain with{SpritePalettes=new Dictionary<int,ushort[]>()},rain with{SpriteRecipes=new Dictionary<string,SourceSpriteRecipe>()},
            Changed(rain,drop with{LoopStarts=[0]}),Changed(sun,ray with{AffineDoubleSize=true}),Changed(sun,ray with{AffineAnimations=[]}),
            sun with{SpritePalettes=new Dictionary<int,ushort[]>()},
            sand with{Commands=[C("monbg","ANIM_ATTACKER"),..sand.Commands]},sand with{Commands=[..sand.Commands.Take(sand.Commands.Length-1),C("createvisualtask","AnimTask_LoadSandstormBackground","5","0"),C("end")]},
            sand with{Commands=[C("createvisualtask","AnimTask_CreateRaindrops","2","0","3","120"),..sand.Commands]}
        ];
        foreach(var script in invalid)
        {
            var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(script.MoveId),Battlers(),true,audio,spriteScene:scene));Assert.AreEqual(0,audio.Cues.Count);Assert.AreEqual(0,scene.Count);
        }
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(rain,Cue(240) with{VisualSeed=null},Battlers(),true,new Audio()));
        foreach(string interval in new[]{"0","-1","32768"})
        {
            var script=Primitive(rain,C("createvisualtask","AnimTask_CreateRaindrops","2","0",interval,"120"),C("end"));
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(240),Battlers(),true,new Audio()));
        }
    }
    private sealed class OccupiedSprite(int slot,int fragments):SourceBattleAnimation.VisualSprite(slot,255)
    {
        public override bool Counted=>false;
        public override SourceVisualSpriteFrame Frame=>new(Slot,"occupied",default,default,0,false,false,false,256,null,255){Subsprites=Enumerable.Repeat(new SourceSpriteSubsprite(0,0,8,8,0,0),fragments).ToArray()};
        public override void Step(SourceBattleAnimation owner){}
    }
    [TestMethod]
    public void SandCapacityCountsRetainedFragmentsAndRejectsOverflowBeforeAttaching()
    {
        // Source stable field uses22 OAM entries; seven possible crescents add14.
        // A retained logical object can own many fragments, not merely one slot.
        foreach(int fragments in new[]{29,64})
        {
            var scene=new SourceBattleAnimation.SpriteScene();var registry=typeof(SourceBattleAnimation.SpriteScene).GetField("Sprites",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(scene)!;
            registry.GetType().GetMethod("Add")!.Invoke(registry,[new OccupiedSprite(4,fragments)]);
            var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(Script(201),Cue(201),Battlers(),true,audio,spriteScene:scene));Assert.AreEqual(0,audio.Cues.Count);Assert.AreEqual(1,scene.Count);
            registry.GetType().GetMethod("Clear")!.Invoke(registry,[]);
        }
    }
    private static void AssertClean(SourceBattleAnimation runtime)
    {
        Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.HasResidualSprites);Assert.IsFalse(runtime.GraphicsLoaded);
        var frame=runtime.Snapshot();Assert.IsNull(frame.Overlay);Assert.IsFalse(frame.BlendEnabled);Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.IsTrue(frame.Offsets.All(p=>p==default));
    }
}
