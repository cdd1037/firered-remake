using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class OverlayAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles=true)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor=0,int target=1)=>new("overlay",1,move,new(actor),new(target));
    private static SourceAnimationScript Script(int move)=>AnimationCompositionFixture.Script(move);
    private static SourceAnimationScript Isolated(int move,string task)=>Script(move) with{Commands=[new("createvisualtask",[task,"5"],1),new("waitforvisualfinish",[],2),new("end",[],3)],Labels=new Dictionary<string,int>()};
    [TestMethod]
    public void EveryReachableOverlayTaskTickMatchesExtractedSourceRegistersAndCleanup()
    {
        using var vectors=SourceVectorFixture.Read("overlay-task-source-vectors.json", "python tests/Reference/Bg1Overlay/overlay/generate.py content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/overlay-task-source-vectors.json");
        int checkedCases=0;
        foreach(var entry in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {
            if(entry.GetProperty("contest").GetBoolean())continue;
            string name=entry.GetProperty("name").GetString()!;
            var (move,task)=name switch{"scary-face"=>(184,"AnimTask_ScaryFace"),"haze-scrolling-fog"=>(114,"AnimTask_HazeScrollingFog"),"mist-ball-fog"=>(296,"AnimTask_MistBallFog"),_=>(0,"")};
            if(move==0)continue;
            int actor=entry.GetProperty("attacker").GetInt32(),target=entry.GetProperty("target").GetInt32();
            var runtime=new SourceBattleAnimation(Isolated(move,task),Cue(move,actor,target),Battlers(),true,new Audio());
            foreach(var row in entry.GetProperty("rows").EnumerateArray())
            {
                if(row.GetProperty("phase").GetInt32()!=1)continue;
                var frame=runtime.Tick();int blend=row.GetProperty("registers")[0].GetInt32(),alpha=row.GetProperty("registers")[1].GetInt32();
                string context=$"{name} {actor}->{target}, source tick {row.GetProperty("frame")}";
                Assert.AreEqual(row.GetProperty("alive").GetInt32(),runtime.ActiveVisualCount,context);
                Assert.AreEqual(alpha&31,frame.AlphaA,context);Assert.AreEqual(alpha>>8&31,frame.AlphaB,context);Assert.AreEqual(blend!=0,frame.BlendEnabled,context);
                Assert.AreEqual(1,frame.BattlerBackgroundPriorities[0],context);
                if(blend==0){Assert.IsNull(frame.Overlay,context);Assert.AreEqual(0,frame.BackgroundSlots.Length,context);}
                else
                {
                    Assert.IsNotNull(frame.Overlay,context);Assert.AreEqual(blend&63,frame.BlendFirstTargets,context);Assert.AreEqual(blend>>8&63,frame.BlendSecondTargets,context);
                    Assert.AreEqual(move==184?(target%2==0?"scary-face-opponent":"scary-face-player"):"fog",frame.Overlay.MaterialId,context);
                    Assert.AreEqual(row.GetProperty("bg1")[0].GetInt32(),frame.Overlay.ScrollX,context);Assert.AreEqual(row.GetProperty("bg1")[1].GetInt32(),frame.Overlay.ScrollY,context);
                }
            }
            for(int i=0;runtime.Active&&i<5;i++)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.HasResidualSprites);checkedCases++;
        }
        Assert.AreEqual(vectors.RootElement.GetProperty("profile").GetString()=="full"?48:6,checkedCases);
    }
    [TestMethod][DataRow(114)][DataRow(137)][DataRow(184)][DataRow(296)]
    public void CompleteRootsRenderBothSidesReplayAndDrainAllSourceResources(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        foreach(int target in move==114?new[]{actor}:doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(x=>x.Key,x=>x.Value);
            assets.Add("background",new(240,112,new ushort[240*112]));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();
            var cue=Cue(move,actor,target);var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio());
            bool pixels=false,sawOverlay=false,sawMistBrightening=false;int ticks=0,overlayTicks=0;
            while(runtime.Active&&ticks++<600)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(replay.Tick()));
                Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(runtime.Snapshot()));
                if(frame.Overlay is not null)
                {
                    sawOverlay=true;overlayTicks++;sawMistBrightening|=frame.AlphaA+frame.AlphaB==17;
                    if(frame.AlphaA>0&&overlayTicks%11==0)
                    {
                        var rendered=composer.ComposeField(frame);CollectionAssert.AreEqual(rendered,composer.ComposeField(frame));
                        pixels|=!rendered.SequenceEqual(composer.ComposeField(frame with{Overlay=null}));
                    }
                }
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.IsTrue(sawOverlay);Assert.IsTrue(pixels);
            Assert.AreEqual((move==114?194:move==296?121:78)-1,overlayTicks,"Cleanup invocation removes BG1 before snapshot.");
            Assert.AreEqual(move==296,sawMistBrightening);Assert.AreEqual(move==114?1:move==137?2:3,audio.Cues.Count);
            Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.HasResidualSprites);Assert.IsFalse(runtime.GraphicsLoaded);
            var end=runtime.Snapshot();Assert.IsNull(end.Overlay);Assert.IsFalse(end.BlendEnabled);Assert.AreEqual(0,end.Sprites.Length);Assert.AreEqual(0,end.BackgroundSlots.Length);Assert.IsTrue(end.Offsets.All(p=>p==default));
        }
    }
    [TestMethod]
    public void CancelAtLoadingRampHoldAndCleanupReleasesOverlayAndAllChildren()
    {
        foreach(int move in new[]{114,137,184,296})foreach(int stop in new[]{0,5,50,100,150})
        {
            var audio=new Audio();var runtime=new SourceBattleAnimation(Script(move),Cue(move),Battlers(),true,audio);
            for(int i=0;i<stop&&runtime.Active;i++)runtime.Tick();int sounds=audio.Cues.Count;
            runtime.Cancel();runtime.Cancel();var frame=runtime.Snapshot();
            Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.HasResidualSprites);Assert.AreEqual(0,runtime.ActiveVisualCount);
            Assert.IsNull(frame.Overlay);Assert.IsFalse(frame.BlendEnabled);Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.AreEqual(sounds,audio.Cues.Count);
        }
    }
    [TestMethod]
    public void MissingOverlayPaletteRawChildAndLoadOnlyMaterialFailBeforeSideEffects()
    {
        var scary=Isolated(184,"AnimTask_ScaryFace");var glare=Script(137);var mist=Script(296);
        SourceAnimationScript[] invalid=[
            scary with{OverlayDependencies=[]},scary with{OverlayPalettes=new Dictionary<string,ushort[]>()},
            scary with{OverlayPalettes=scary.OverlayPalettes.ToDictionary(p=>p.Key,p=>new ushort[15])},
            glare with{SpriteDependencies=[]},glare with{SpritePalettes=new Dictionary<int,ushort[]>()},
            glare with{SpriteTags=new Dictionary<string,int>()},mist with{MaterialDependencies=[]},
            mist with{SpritePalettes=mist.SpritePalettes.Where(p=>p.Key!=mist.SpriteTags["ANIM_TAG_WHITE_FEATHER"]).ToDictionary(p=>p.Key,p=>p.Value)},
            scary with{Commands=[new("monbg",["ANIM_TARGET"],1),..scary.Commands]},
            scary with{Commands=[new("createvisualtask",["AnimTask_ScaryFace","5"],1),..scary.Commands]},
            scary with{Commands=[new("call",["mixed"],1),..scary.Commands],Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"mixed",[new("monbg",["ANIM_TARGET"],1),new("return",[],2)]}}}
        ];
        foreach(var script in invalid)
        {
            var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(script.MoveId),Battlers(),true,audio));Assert.AreEqual(0,audio.Cues.Count);
        }
        var unused=scary with{Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"unused",[new("monbg",["ANIM_TARGET"],1),new("return",[],2)]}}};
        var accepted=new SourceBattleAnimation(unused,Cue(184),Battlers(),true,new Audio());accepted.Cancel();
    }

    [TestMethod]
    public void SourceProfileRetainsExplicitSemanticAndBoundaryCases()
    {
        using var vectors=SourceVectorFixture.Read("overlay-task-source-vectors.json", "python tests/Reference/Bg1Overlay/overlay/generate.py content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/overlay-task-source-vectors.json");
        Assert.IsTrue(vectors.RootElement.GetProperty("profile").GetString() is "representative" or "full");
        var cases=vectors.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        CollectionAssert.AreEquivalent(new[]{"scary-face","haze-scrolling-fog","mist-ball-fog","scary-face-cleanup-edge","haze-scrolling-fog-cleanup-edge","mist-ball-fog-cleanup-edge"},cases.Select(c=>c.GetProperty("name").GetString()!).Distinct().ToArray());
        Assert.IsTrue(cases.Any(c=>c.GetProperty("contest").GetBoolean()));
        foreach(string name in new[]{"scary-face","haze-scrolling-fog","mist-ball-fog"})
            CollectionAssert.AreEquivalent(new[]{0,1},cases.Where(c=>c.GetProperty("name").GetString()==name&&!c.GetProperty("contest").GetBoolean()).Select(c=>c.GetProperty("target").GetInt32()&1).Distinct().ToArray());
    }
}
