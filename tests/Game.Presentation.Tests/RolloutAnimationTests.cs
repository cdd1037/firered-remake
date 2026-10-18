using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class RolloutAnimationTests
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private sealed class Audio:ISourceAnimationAudio{public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects(){}}
    private sealed class Occupied(int slot):SourceBattleAnimation.VisualSprite(slot,100){public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"fixture-occupied",default,default,0,false,false,false,256,null,100);public override void Step(SourceBattleAnimation o){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,13,7,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int actor,int target,int counter)=>new("rollout",1,205,new(actor),new(target)){RolloutCounter=(byte)counter,MovePower=(ushort)(30<<(counter-1))};
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    [TestMethod]
    public void PinnedTaskRawChildrenArcTilesSidePanningAndFailedAllocationMatchEveryFrame()
    {
        var full=AnimationCompositionFixture.Script(205);
        var script=full with{Commands=[C("loadspritegfx","ANIM_TAG_MUD_SAND"),C("loadspritegfx","ANIM_TAG_ROCKS"),C("createvisualtask","AnimTask_Rollout","2"),C("delay","255"),C("end")],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        using var doc=SourceVectorFixture.Read("rollout-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_rollout_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/rollout-source-vectors.json");
        int cases=0;Assert.AreEqual(65536,doc.RootElement.GetProperty("counterProbeCount").GetInt32());
        foreach(var c in doc.RootElement.GetProperty("cases").EnumerateObject())
        {
            var input=N(c.Value.GetProperty("inputs"));bool doubles=input[0]!=0;int actor=input[1],target=input[2],counter=input[3];
            var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var battlers=Battlers(doubles);
            var owner=new SourceBattleAnimation(script,Cue(actor,target,counter),battlers,doubles,audio,spriteScene:scene);
            if(input[4]!=0)for(int i=doubles?4:2;i<64;i++)scene.Sprites.Add(new Occupied(i){Owner=owner});
            var sprites=c.Value.GetProperty("sprites").EnumerateArray().Select(N).GroupBy(r=>r[0]).ToDictionary(g=>g.Key,g=>g.OrderBy(r=>r[1]).ToArray());
            SourceAnimationFrame frame=owner.Snapshot();
            // Graphics-loading callbacks each yield; align to the actual task start.
            for(int i=0;i<12;i++){frame=owner.Tick();if(frame.Trace.Any(t=>t.EndsWith(":createvisualtask")))break;}
            Assert.IsTrue(frame.Trace.Any(t=>t.EndsWith(":createvisualtask")));int start=frame.Frame;
            foreach(var row in c.Value.GetProperty("frames").EnumerateArray().Select(N))
            {
                if(row[0]!=0)frame=owner.Tick();string why=c.Name+" tick "+row[0];
                Assert.AreEqual(row[1],owner.ActiveVisualCount,why+" counted owner");Assert.AreEqual(new PixelPoint(row[2],row[3]),frame.Offsets[actor],why+" task offset");
                var center=battlers[actor].Center(doubles);int x=((center.X+row[4]-32)&511)-center.X+32;if(x>255)x-=512;int y=((center.Y+row[5]-32)&255)-center.Y+32;
                Assert.AreEqual(new PixelPoint(x,y),frame.ObjectOffsets[actor],why+" pre-task OAM");
                var actual=frame.Sprites.Where(s=>s.Template!="fixture-occupied").OrderBy(s=>s.Slot).ToArray();var expected=sprites.GetValueOrDefault(row[0],[]);Assert.AreEqual(expected.Length,actual.Length,why);
                for(int i=0;i<actual.Length;i++)
                {
                    var s=actual[i];var e=expected[i];Assert.AreEqual(e[1],s.Slot,why);Assert.AreEqual(e[2]==0?"gRolloutMudSpriteTemplate":"gRolloutRockSpriteTemplate",s.Template,why);
                    Assert.AreEqual(new PixelPoint(e[3],e[4]),s.Position,why);Assert.AreEqual(new PixelPoint(e[5],e[6]),s.Offset,why);Assert.AreEqual(e[7],s.Tile,why);
                    Assert.AreEqual(35,s.Subpriority);Assert.AreEqual(2,s.OamPriority);Assert.AreEqual(e[2]==0?8:32,s.Width);Assert.AreEqual(s.Width,s.Height);Assert.IsFalse(s.Affine);Assert.IsFalse(s.Blend);
                }
                Assert.IsTrue(scene.Sprites.All(s=>!s.Counted),why+" particles are raw");
            }
            var sounds=c.Value.GetProperty("sounds").EnumerateArray().Select(N).ToArray();Assert.AreEqual(sounds.Length,audio.Cues.Count);
            for(int i=0;i<sounds.Length;i++){var e=sounds[i];Assert.AreEqual(e[0]+start,audio.Cues[i].Frame);Assert.AreEqual(e[1]==155?"SE_M_HEADBUTT":"SE_M_DIG",audio.Cues[i].Id);Assert.AreEqual(e[2],audio.Cues[i].Pan);}
            Assert.IsNull(typeof(SourceBattleAnimation).GetField("_visualRandom",Private)!.GetValue(owner));owner.Cancel();Assert.AreEqual(0,scene.Count);cases++;
        }
        Assert.AreEqual(100,cases);
    }
    [TestMethod]
    public void CompleteRolloutRootsComposeAllCountersPositionsAndAllyTargetsThenRestore()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(205);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})for(int count=1;count<=5;count++)
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var surface=resources.CreateBackgroundSurface(0);var owner=new SourceBattleAnimation(resolved.Script,Cue(actor,target,count),battlers,doubles,new Audio(),Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers,surface);int ticks=0;bool particles=false,hit=false;
            while(owner.Active&&ticks++<400){var f=owner.Tick();particles|=f.Sprites.Any(s=>s.Template.StartsWith("gRollout"));hit|=f.HitSplats.Length>0;Assert.AreEqual(240*112*4,composer.ComposeField(f).Length);}
            Assert.IsFalse(owner.Active);Assert.IsTrue(particles);Assert.IsTrue(hit);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsFalse(owner.HasResidualSprites);var final=owner.Snapshot();Assert.AreEqual(0,final.Sprites.Length);Assert.IsTrue(final.Offsets.All(x=>x==default));Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsFalse(final.BlendEnabled);
        }
    }
    [TestMethod]
    public void MissingCounterRawDependenciesCallbackSoundsDuplicateTaskAndCancellationFailClosed()
    {
        var full=AnimationCompositionFixture.Script(205);var cue=Cue(0,1,1);var battlers=Battlers(true);
        SourceBattleAnimation Make(SourceAnimationScript s,CommittedMoveAnimation c)=>new(s,c,battlers,true,new Audio());
        foreach(byte? count in new byte?[]{null,0,6,255})Assert.Throws<SourceAnimationException>(()=>Make(full,cue with{RolloutCounter=count}));
        Assert.Throws<SourceAnimationException>(()=>Make(full,cue with{MovePower=null}));
        Assert.Throws<SourceAnimationException>(()=>Make(full with{SpriteDependencies=[]},cue));
        var sounds=full.SoundEffects.ToDictionary(p=>p.Key,p=>p.Value);sounds.Remove("SE_M_DIG");Assert.Throws<SourceAnimationException>(()=>Make(full with{SoundEffects=sounds},cue));
        Assert.Throws<SourceAnimationException>(()=>Make(full with{Commands=[C("createvisualtask","AnimTask_Rollout","2"),..full.Commands]},cue));
        foreach(int tick in new[]{0,1,10,25,42,60,85,105}){var owner=Make(full,cue with{RolloutCounter=5,MovePower=480});for(int i=0;i<tick&&owner.Active;i++)owner.Tick();owner.Cancel();owner.Cancel();var f=owner.Snapshot();Assert.IsFalse(owner.Active);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.AreEqual(0,f.Sprites.Length);Assert.IsTrue(f.Offsets.All(x=>x==default));Assert.IsFalse(f.BlendEnabled);}
    }
}
