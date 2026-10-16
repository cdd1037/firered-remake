using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class BackgroundMoveAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {
        public int Count {get;private set;}
        public bool IsPlaying=>false;
        public void Play(SourceAnimationSound cue)=>Count++;
        public void SetPan(int pan,int frame){}
        public void StopAnimationEffects(){}
    }
    [TestMethod]
    [DataRow(168,1)][DataRow(242,1)][DataRow(60,3)][DataRow(95,3)][DataRow(149,3)][DataRow(93,3)][DataRow(94,3)]
    public void SourceBackgroundRootsFadeHitAndRestoreForEveryBattlePosition(int move,int background)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        using var backgrounds=new SourceAnimationBackgroundCatalog(Path.Combine(TestAssets.Root,"content/generated/battle/animations/backgrounds"));
        var terrainPalette=backgrounds.Terrain(0).Palette.Span[..16].ToArray();
        var resolved=resources.Resolve(move);Assert.AreEqual(background,resolved.Script.BackgroundSymbols[background==1?"BG_DARK":"BG_PSYCHIC"]);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var battlers=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();var audio=new Audio();
            var runtime=new SourceBattleAnimation(resolved.Script,new("background",1,move,new(actor),new(actor^1)),battlers,doubles,audio,terrainPalette);
            int frames=0;bool dark=false,black=false,impact=false;
            while(runtime.Active&&frames++<1500)
            {
                var frame=runtime.Tick();dark|=frame.MoveBackground?.Id==background;black|=frame.MoveBackground?.Darkening==16;impact|=frame.Sprites.Any(s=>s.Asset is not null)||frame.BlendedObjectSlots.Length>0;
            }
            Assert.IsFalse(runtime.Active);Assert.IsTrue(dark);Assert.IsTrue(black);Assert.IsTrue(impact);Assert.IsTrue(audio.Count>0);
            var final=runtime.Snapshot();Assert.IsNull(final.MoveBackground!.Id);Assert.AreEqual(0,final.MoveBackground.Darkening);Assert.IsFalse(final.BlendEnabled);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsFalse(runtime.GraphicsLoaded);
        }
    }
    [TestMethod]
    [DataRow(93)][DataRow(94)]
    public void PsychicScalingRestoresBlendedObjectOverIndependentMonBackground(int move)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var resolved=resources.Resolve(move);var battlers=Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
        var surface=resources.CreateBackgroundSurface(0);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));
        foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(((i%64/4+i/64/4)%2==0)?0x801f:0xfc00)).ToArray()));
        var composer=new SourceBattleFrameComposer(assets,battlers,resolved.StatMask,resolved.Metallic,surface,resolved.Backgrounds);
        var runtime=new SourceBattleAnimation(resolved.Script,new("psychic-scale",1,move,new(0),new(1)),battlers,false,new Audio(),Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray());
        bool combined=false;int frames=0;
        while(runtime.Active&&frames++<1000)
        {
            var frame=runtime.Tick();
            if(combined||!frame.BlendedObjectSlots.Contains(1)||frame.BattlerAffine[1].XScale==256)continue;
            Assert.IsTrue(frame.BackgroundSlots.Contains(1));Assert.IsTrue(frame.RestoredObjectSlots.Contains(1));Assert.IsTrue(frame.BattlerAffine[1].DoubleSize);
            var actual=composer.ComposeField(frame);var backgroundOnly=composer.ComposeField(frame with{RestoredObjectSlots=[],BlendedObjectSlots=[]});Assert.IsFalse(actual.SequenceEqual(backgroundOnly));combined=true;
        }
        Assert.IsTrue(combined);Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.Snapshot().BlendedObjectSlots.Length);
    }
}
