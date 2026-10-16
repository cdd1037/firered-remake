using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class FogAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    private static string Root=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    [TestMethod]
    public void FogUsesSourceBaseAveragesIncludingHiddenPartnersAndImmediateTranslation()
    {
        using var resources=new SourceAnimationResources(Root);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int participant in new[]{0,1})foreach(int average in new[]{0,1})foreach(bool visible in new[]{false,true})
        {
            int target=actor^1,selected=participant==0?actor:target;var b=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64){Visible=i!=(selected^2)||visible}).ToArray();
            var source=resources.Resolve(54).Script;var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_MIST_CLOUD"],1),new("createsprite",["gMistCloudSpriteTemplate","ANIM_ATTACKER","2","11","-24","48","240",participant.ToString(),average.ToString()],2),new("waitforvisualfinish",[],3),new("end",[],4)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
            var runtime=new SourceBattleAnimation(script,new("fog-vector",1,54,new(actor),new(target)),b,doubles,new Audio());var p=b[selected].Base(doubles);
            if(average!=0&&doubles){var partner=b[selected^2].Base(true);p=new((p.X+partner.X)/2,(p.Y+partner.Y)/2);}
            int direction=average==0?(actor%2==0?1:-1):(selected%2==0?1:-1),radius=average!=0&&doubles?64:32;int observed=0,ticks=0;
            while(runtime.Active&&ticks++<512)
            {
                var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;var fog=frame.Sprites.Single();int step=++observed+1,angle=(64+3*(step-1))&255;
                Assert.AreEqual(new PixelPoint(p.X+direction*11,p.Y-24+(target%2==0?8:0)),fog.Position);
                Assert.AreEqual(source.SineTable[angle]*radius>>8,fog.Offset.X);
                Assert.AreEqual((((50*step)&65535)>>8)+(source.SineTable[angle+64]*-6>>8),fog.Offset.Y);
                int bg=frame.BattlerBackgroundPriorities[b[selected].BackgroundRank-1];Assert.AreEqual((bg+((ushort)(angle-64)<=127?0:1))&3,fog.OamPriority);
            }
            Assert.IsFalse(runtime.Active);Assert.AreEqual(239,observed);
        }
    }
    [TestMethod]
    public void FogRearOrbitWrapsPriorityThreeToZero()
    {
        using var resources=new SourceAnimationResources(Root);var b=Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
        var source=resources.Resolve(54).Script;var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_MIST_CLOUD"],1),new("createvisualtask",["AnimTask_SporeDoubleBattle","2"],2),new("createsprite",["gMistCloudSpriteTemplate","ANIM_ATTACKER","2","0","0","48","240","0","0"],3),new("waitforvisualfinish",[],4),new("end",[],5)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        var runtime=new SourceBattleAnimation(script,new("fog-wrap",1,54,new(0),new(1)),b,true,new Audio());bool front=false,rear=false;int ticks=0;
        while(runtime.Active&&ticks++<512){var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;Assert.AreEqual(3,frame.BattlerBackgroundPriorities[1]);int priority=frame.Sprites.Single().OamPriority;front|=priority==3;rear|=priority==0;Assert.IsTrue(priority is 0 or 3);}
        Assert.IsTrue(front&&rear);Assert.IsFalse(runtime.Active);
    }
    [TestMethod][DataRow(54)][DataRow(123)]
    public void CompleteFogRootsComposeForAllSixActorPositions(int move)
    {
        using var resources=new SourceAnimationResources(Root);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x5294,240*112).ToArray()));foreach(var battler in b)assets.Add(battler.Asset,new(64,64,Enumerable.Repeat((ushort)0x03e0,4096).ToArray()));
            var runtime=new SourceBattleAnimation(resolved.Script,new("fog-root",1,move,new(actor),new(move==54?actor:actor^1)),b,doubles,new Audio());var composer=new SourceBattleFrameComposer(assets,b,resolved.StatMask,resolved.Metallic);int ticks=0,visible=0;
            while(runtime.Active&&ticks++<1024){var frame=runtime.Tick();visible+=frame.Sprites.Count(s=>s.Visible);Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length);}
            Assert.IsFalse(runtime.Active);Assert.IsGreaterThan(0,visible);if(move==123)CollectionAssert.AreEqual(new[]{1,2},runtime.Snapshot().BattlerBackgroundPriorities);
        }
    }
}
