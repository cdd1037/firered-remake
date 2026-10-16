using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class StatReductionAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    [TestMethod][DataRow(46,2)][DataRow(47,1)][DataRow(48,4)][DataRow(50,8)]
    [DataRow(55,5)][DataRow(57,5)][DataRow(39,2)][DataRow(15,2)][DataRow(16,1)][DataRow(19,8)][DataRow(22,2)][DataRow(23,1)]
    public void StatArgumentUsesSourcePaletteAndCompletesSharedMaskLifecycle(int argument,int palette)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var resolved=resources.Resolve(0);Assert.IsNotNull(resolved.StatMask);
        for(int slot=0;slot<4;slot++)
        {
            var battlers=Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
            var assets=new Dictionary<string,SourcePixelAsset>(resolved.Assets){{"background",new(240,112,Enumerable.Repeat((ushort)0xfc00,240*112).ToArray())}};
            foreach(var b in battlers)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0x801f,4096).ToArray());
            var runtime=new SourceBattleAnimation(resolved.Script,new("stat-reduction",1,0,new(slot),new(slot)){StatAnimationArgument=argument},battlers,true,new Audio());
            var composer=new SourceBattleFrameComposer(assets,battlers,resolved.StatMask);int ticks=0;bool loaded=false;
            while(runtime.Active&&ticks++<512)
            {
                var frame=runtime.Tick();composer.ComposeField(frame);
                if(frame.StatMask is {BackgroundLoaded:true} mask){loaded=true;Assert.AreEqual(palette,mask.Palette);Assert.AreEqual(slot,mask.MaskSlot);}
            }
            Assert.IsTrue(loaded);Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);
            var ended=runtime.Snapshot();Assert.IsNotNull(ended.StatMask);
            Assert.IsFalse(ended.StatMask.WindowEnabled);Assert.IsFalse(ended.StatMask.BackgroundLoaded);
            Assert.AreEqual(-1,ended.StatMask.PriorityPartner);Assert.IsFalse(ended.BlendEnabled);
            Assert.AreEqual(0,ended.AlphaA);Assert.AreEqual(0,ended.AlphaB);
            CollectionAssert.AreEqual(composer.ComposeField(ended with{StatMask=null}),composer.ComposeField(ended));
        }
    }
}
