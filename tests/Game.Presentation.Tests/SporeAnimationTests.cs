using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class SporeAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    [TestMethod]
    public void SporeUsesLiveBackgroundPriorityAndCallbackLifetimeAfterTileEnd()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        foreach(int target in new[]{0,1,2,3})foreach(int initial in new[]{61,62,189,190})foreach(int mode in new[]{0,1})foreach(bool partnerVisible in new[]{false,true})
        {
            var battlers=Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64){Visible=i!=(target^2)||partnerVisible}).ToArray();
            var source=resources.Scripts[147];
            var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_SPORE"],1),new("monbg",["ANIM_DEF_PARTNER"],2),new("createvisualtask",["AnimTask_SporeDoubleBattle","2"],3),new("createsprite",["gSporeParticleSpriteTemplate","ANIM_ATTACKER","2","0","-20",initial.ToString(),"80",mode.ToString()],4),new("waitforvisualfinish",[],5),new("clearmonbg",["ANIM_DEF_PARTNER"],6),new("end",[],7)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
            var runtime=new SourceBattleAnimation(script,new("spore-vector",1,147,new(target^1),new(target)),battlers,true,new Audio());int observed=0,ticks=0;
            while(runtime.Active&&ticks++<256)
            {
                var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;
                var sprite=frame.Sprites.Single();int step=++observed+1,angle=(initial+2*(step-1))&255;
                int priority=frame.BattlerBackgroundPriorities[battlers[target].BackgroundRank-1];
                Assert.AreEqual((ushort)(angle-64)<128?priority:Math.Min(priority+1,3),sprite.OamPriority);
                Assert.AreEqual(new PixelPoint((source.SineTable[angle]*32)>>8,((source.SineTable[angle+64]*-3)>>8)+((24*step)>>8)),sprite.Offset);
                Assert.AreEqual(mode==1,sprite.Blend);Assert.AreEqual(mode==0?0:4,sprite.Tile);
            }
            Assert.IsFalse(runtime.Active);Assert.AreEqual(79,observed);
            CollectionAssert.AreEqual(battlers[target].BackgroundRank==1?new[]{2,3}:new[]{1,2},runtime.Snapshot().BattlerBackgroundPriorities);
        }
    }
}
