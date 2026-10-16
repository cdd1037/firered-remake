using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class SharedAnimationCommandTests
{
    private sealed class Audio:ISourceAnimationAudio
    {
        public List<SourceAnimationSound> Sounds {get;}=[];
        public List<(int Pan,int Frame)> Pans {get;}=[];
        public bool IsPlaying=>false;
        public void Play(SourceAnimationSound cue)=>Sounds.Add(cue);
        public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));
        public void StopAnimationEffects(){}
    }
    private static AnimationBattler[] Battlers()=>Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    [TestMethod][DataRow(0)][DataRow(1)]
    public void PanSweepUsesSourceCreationFrameStepAndExactAsymmetricEndpoints(int actor)
    {
        var script=new SourceAnimationScript(53,"pan",[new("panse",["SE_M_FLAMETHROWER","SOUND_PAN_ATTACKER","SOUND_PAN_TARGET","+2","0"],1),new("end",[],2)]);
        var audio=new Audio();var runtime=new SourceBattleAnimation(script,new("pan",1,53,new(actor),new(actor^1)),Battlers(),true,audio);
        int frames=0;while(runtime.Active&&frames++<100)runtime.Tick();
        Assert.IsFalse(runtime.Active);Assert.AreEqual(1,audio.Sounds.Count);Assert.AreEqual(actor==0?-64:63,audio.Sounds[0].Pan);
        Assert.AreEqual(actor==0?64:63,audio.Pans.Count);Assert.AreEqual((actor==0?-62:61,0),audio.Pans[0]);Assert.AreEqual((actor==0?63:-63,actor==0?63:62),audio.Pans[^1]);
    }
    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void SplitBackgroundPriorityAppliesOnlyForSourceLeadPositions(int target)
    {
        var script=new SourceAnimationScript(53,"priority",[new("monbg",["ANIM_TARGET"],1),new("splitbgprio",["ANIM_TARGET"],2),new("delay",["2"],3),new("end",[],4)]);
        var runtime=new SourceBattleAnimation(script,new("priority",1,53,new(target^1),new(target)),Battlers(),true,new Audio());
        var frame=runtime.Tick();CollectionAssert.AreEqual(target is 0 or 3?new[]{target^2}:Array.Empty<int>(),frame.ForegroundBackgroundSlots);
        runtime.Cancel();Assert.AreEqual(0,runtime.Snapshot().ForegroundBackgroundSlots.Length);
    }
    [TestMethod]
    public void PromotedBackgroundOccludesPriorityTwoEffectPixels()
    {
        var battlers=Battlers().Select(b=>b with{Visible=b.Slot.Value==1,DisplayCenter=new PixelPoint(40,40)}).ToArray();
        var assets=new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,new ushort[240*112])},{"impact",new(32,32,Enumerable.Repeat((ushort)0x83e0,1024).ToArray())}};
        foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x801f,4096).ToArray()));
        var composer=new SourceBattleFrameComposer(assets,battlers);
        var frame=new SourceAnimationFrame(0,true,new PixelPoint[4],[1],0,0,[],[]){Doubles=true,BattlerOamRanks=[0,1,2,3],Sprites=[new(4,"test",new(40,40),default,0,true,false,false,256,"impact",1){OamRank=4}]};
        int at=(40*240+40)*4;var ordinary=composer.ComposeField(frame);Assert.AreEqual((byte)255,ordinary[at+1]);
        var promoted=composer.ComposeField(frame with{ForegroundBackgroundSlots=[1]});Assert.AreEqual((byte)255,promoted[at]);Assert.AreEqual((byte)0,promoted[at+1]);
    }

}
