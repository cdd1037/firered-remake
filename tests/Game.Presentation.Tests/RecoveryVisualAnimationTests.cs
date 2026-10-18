using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class RecoveryVisualAnimationTests
{
    private const string Green="gGreenStarSpriteTemplate",Moon="gMoonSpriteTemplate",Ring="gAuroraBeamRingSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound> Cues=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles=true)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor=0,int target=1)=>new("recovery",1,move,new(actor),new(target)){VisualSeed=0x12345678};
    private static SourceAnimationScript Script(int move)=>AnimationCompositionFixture.Script(move);
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    [TestMethod][DataRow(62)][DataRow(156)][DataRow(234)][DataRow(236)]
    public void CompleteRootsReplayBothSidesComposeAndRetainExactAuthoredCleanup(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        using var schedules=SourceVectorFixture.Read("recovery-schedule-source-vectors.json", "python tests/Reference/Recovery/check_vectors.py --reference content/raw/pokefirered/037335f4 --prepare tests/Game.Presentation.Tests/Fixtures");
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            int target=move==62?actor^1:actor;var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());assets.TryAdd("impact",new(32,32,new ushort[1024]));foreach(var b in battlers)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),battlers,doubles,audio,spriteScene:scene,spriteMaterials:resolved.Assets);var replay=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),battlers,doubles,new Audio(),spriteMaterials:resolved.Assets);
            int ticks=0,peak=0;bool pixels=false,overlay=false,background=false,directEntries=false,moon=false,sparkle=false,ringScale=false;List<int> births=[];var instances=new HashSet<SourceBattleAnimation.VisualSprite>();
            string targetTemplate=move==234?Green:move==156?"gSleepLetterZSpriteTemplate":Ring;
            while(runtime.Active&&ticks++<700)
            {
                var f=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(runtime.Snapshot()));
                peak=Math.Max(peak,scene.Count);foreach(var s in scene.Sprites.Where(s=>s.Frame.Template==targetTemplate&&s.Counted))if(instances.Add(s))births.Add(f.Frame);
                if(f.Sprites.Length>0&&ticks%9==0)pixels|=!composer.ComposeField(f).SequenceEqual(composer.ComposeField(f with{Sprites=[]}));
                overlay|=f.Overlay?.MaterialId=="morning-sun";background|=f.MoveBackground?.Id==20;directEntries|=f.BackgroundPaletteEntries.Length==45;moon|=f.Sprites.Any(s=>s.Template==Moon);sparkle|=f.Sprites.Any(s=>s.Template=="gMoonlightSparkleSpriteTemplate");ringScale|=f.Sprites.Any(s=>s.Template==Ring&&s.MatrixA!=256);
                if(f.Overlay is not null){Assert.AreEqual(2,f.BlendFirstTargets);Assert.AreEqual(63,f.BlendSecondTargets);Assert.AreEqual(1,f.BattlerBackgroundPriorities[0]);}
                if(f.BackgroundPaletteEntries.Length>0)Assert.IsFalse(f.BackgroundPaletteEntries.Any(e=>e.Index%16==0));
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.IsTrue(pixels);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.HasResidualSprites);var last=runtime.Snapshot();Assert.AreEqual(0,last.BackgroundPaletteEntries.Length);Assert.IsNull(last.Overlay);Assert.IsTrue(last.Offsets.All(o=>o==default));
            if(move!=236)
            {
                var schedule=schedules.RootElement.GetProperty("cases").EnumerateArray().Single(c=>c.GetProperty("move").GetInt32()==move);int[] expected=schedule.GetProperty("birthFrames").EnumerateArray().Select(v=>v.GetInt32()).ToArray();int origin=move==62?births[0]:0;CollectionAssert.AreEqual(expected,births.Select(f=>f-origin).ToArray());Assert.IsFalse(last.BlendEnabled);
            }
            if(move==156){Assert.AreEqual(3,peak);Assert.AreEqual(1,audio.Cues.Count);Assert.AreEqual("SE_M_SNORE",audio.Cues[0].Id);}
            if(move==234){Assert.IsTrue(overlay);Assert.IsTrue(peak<=22);Assert.AreEqual(4,audio.Cues.Count(c=>c.Id=="SE_M_MORNING_SUN"));Assert.AreEqual(1,audio.Cues.Count(c=>c.Id=="SE_M_ABSORB_2"));}
            if(move==236){Assert.IsTrue(directEntries&&moon&&sparkle);Assert.IsTrue(last.BlendEnabled);Assert.AreEqual(16,last.AlphaA);Assert.AreEqual(0,last.AlphaB);Assert.AreEqual(2,audio.Cues.Count);}
            if(move==62){Assert.IsTrue(background&&ringScale);Assert.IsNull(last.MoveBackground!.Id);Assert.AreEqual(0,last.MoveBackground.Darkening);Assert.IsTrue(peak<=7);}
        }
    }
    [TestMethod]
    public void CancellationAtLoadingRawChildrenSparsePaletteAndRestoreStagesReleasesOwnership()
    {
        foreach(int move in new[]{62,156,234,236})foreach(int stop in new[]{0,4,15,40,90,170,235,290})
        {
            var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(Script(move),Cue(move),Battlers(),true,audio,spriteScene:scene);for(int i=0;i<stop&&runtime.Active;i++)runtime.Tick();int sounds=audio.Cues.Count;runtime.Cancel();runtime.Cancel();var f=runtime.Snapshot();Assert.IsFalse(runtime.Active);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.PaletteFadeActive);Assert.IsFalse(f.BlendEnabled);Assert.IsNull(f.Overlay);Assert.AreEqual(0,f.BackgroundPaletteEntries.Length);Assert.AreEqual(sounds,audio.Cues.Count);
        }
    }
    [TestMethod]
    public void MissingDependenciesUnboundedCompanionsAndUnreviewedPaletteMixingFailBeforeEffects()
    {
        var sun=Script(234);var moon=Script(236);var aurora=Script(62);var rest=Script(156);
        SourceAnimationScript Changed(SourceAnimationScript s,SourceSpriteRecipe r)=>s with{SpriteRecipes=s.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==r.Template?r:p.Value)};
        SourceAnimationScript[] invalid=[sun with{OverlayDependencies=[]},sun with{OverlayPalettes=new Dictionary<string,ushort[]>()},sun with{SpriteDependencies=[]},sun with{SpritePalettes=new Dictionary<int,ushort[]>()},sun with{SoundEffects=sun.SoundEffects.Where(p=>p.Key!="SE_M_MORNING_SUN").ToDictionary(p=>p.Key,p=>p.Value)},moon with{SpritePalettes=new Dictionary<int,ushort[]>()},moon with{Commands=[C("monbg","ANIM_ATTACKER"),..moon.Commands]},sun with{Commands=[C("monbg","ANIM_ATTACKER"),..sun.Commands]},sun with{Commands=[C("createvisualtask","AnimTask_CreateRaindrops","2","0","3","120"),..sun.Commands]},Changed(rest,rest.SpriteRecipes["gSleepLetterZSpriteTemplate"] with{AffineAnimations=[]}),Changed(aurora,aurora.SpriteRecipes[Ring] with{AffineDoubleSize=false}),Changed(sun,sun.SpriteRecipes[Green] with{LoopStarts=[0,0,0]})];
        foreach(var script in invalid){var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(script.MoveId),Battlers(),true,audio,spriteScene:scene));Assert.AreEqual(0,audio.Cues.Count);Assert.AreEqual(0,scene.Count);}
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(sun,Cue(234) with{VisualSeed=null},Battlers(),true,new Audio()));
    }
    private sealed class Occupied(int slot,int fragments):SourceBattleAnimation.VisualSprite(slot,255)
    {public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"retained",default,default,0,false,false,false,256,null,255){Subsprites=Enumerable.Repeat(new SourceSpriteSubsprite(0,0,8,8,0,0),fragments).ToArray()};public override void Step(SourceBattleAnimation owner){}}
    [TestMethod]
    public void CapacityIncludesRetainedOamFragmentsAndRejectsSimultaneousRawFamilies()
    {
        var sun=Script(234);var crowded=sun with{Commands=[C("loadspritegfx","ANIM_TAG_GREEN_STAR"),..Enumerable.Range(0,15).Select(_=>C("createsprite",Green,"0","2","30","640")),C("waitforvisualfinish"),C("end")],Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(crowded,Cue(234),Battlers(),true,new Audio()));
        var scene=new SourceBattleAnimation.SpriteScene();scene.Sprites.Add(new Occupied(4,32));Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(sun,Cue(234),Battlers(),true,new Audio(),spriteScene:scene));Assert.AreEqual(1,scene.Count);scene.Sprites.Clear();
    }
    [TestMethod]
    public void IndependentIndexedMaterialsAndEveryAdmittedTileFlipRenderMatchSourcePixels()
    {
        using var doc=SourceVectorFixture.Read("recovery-material-source-vectors.json", "python tests/Reference/Recovery/check_vectors.py --reference content/raw/pokefirered/037335f4 --prepare tests/Game.Presentation.Tests/Fixtures");using var resources=new SourceAnimationResources(Folder);int cases=0;
        foreach(var source in doc.RootElement.GetProperty("sprites").EnumerateArray())
        {
            string id=source.GetProperty("id").GetString()!;int move=id=="letter-z"?156:id=="green-star"?234:id=="rainbow-rings"?62:236;var asset=resources.Resolve(move).Assets[id];CollectionAssert.AreEqual(source.GetProperty("palette").EnumerateArray().Select(v=>(ushort)v.GetInt32()).ToArray(),asset.Palette);int w=source.GetProperty("frameSize")[0].GetInt32(),h=source.GetProperty("frameSize")[1].GetInt32();var composer=new SourceBattleFrameComposer(new Dictionary<string,SourcePixelAsset>{{id,asset},{"background",new(240,112,new ushort[240*112])},{"impact",new(32,32,new ushort[1024])}},[]);
            foreach(var frame in source.GetProperty("frames").EnumerateArray())
            {
                var sprite=new SourceVisualSpriteFrame(4,"source-material",new(120,56),default,frame.GetProperty("tile").GetInt32(),true,frame.GetProperty("hFlip").GetBoolean(),false,256,id,28){Width=w,Height=h,VFlip=frame.GetProperty("vFlip").GetBoolean(),MaterialPalette=asset.Palette};var state=new SourceAnimationFrame(0,false,[],[],0,0,[],[]){Sprites=[sprite]};byte[] actual=composer.ComposeField(state);int[] indices=frame.GetProperty("indices").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
                for(int y=0;y<112;y++)for(int x=0;x<240;x++){int px=x-(120-w/2),py=y-(56-h/2),index=px>=0&&px<w&&py>=0&&py<h?indices[py*w+px]:0;int color=index==0?0:asset.Palette[index],at=(y*240+x)*4;for(int c=0;c<3;c++)Assert.AreEqual((byte)(((color>>(5*c))&31)*255/31),actual[at+c],id);Assert.AreEqual((byte)255,actual[at+3]);}cases++;
            }
        }
        Assert.AreEqual(48,cases);
        foreach(var b in doc.RootElement.GetProperty("backgrounds").EnumerateArray())
        {
            string id=b.GetProperty("id").GetString()!;var resolved=resources.Resolve(id=="morning-sun"?234:62);var surface=resources.CreateBackgroundSurface(0);if(id=="aurora")surface.Apply(resolved.Backgrounds[20]);
            foreach(var s in b.GetProperty("samples").EnumerateArray()){int x=s.GetProperty("point")[0].GetInt32(),y=s.GetProperty("point")[1].GetInt32();int index=s.GetProperty("index").GetInt32();var expected=new SourceBackgroundPixel(id=="aurora"&&index==0?0:s.GetProperty("bank").GetInt32(),index);Assert.AreEqual(expected,id=="morning-sun"?resolved.Overlays[id].Sample(x,y):surface.Sample(x,y,0));Assert.AreEqual(expected,id=="morning-sun"?resolved.Overlays[id].Sample(x+256,y-256):surface.Sample(x+256,y-256,0));}
        }
    }
}
