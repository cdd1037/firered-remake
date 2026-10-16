using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class WallMoveAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    [TestMethod]
    [DataRow(112)][DataRow(113)][DataRow(115)][DataRow(243)][DataRow(277)]
    public void SourceWallRootsCompleteAndRestoreOpponentBackgroundCopies(int move)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var runtime=new SourceBattleAnimation(resolved.Script,new("wall",1,move,new(actor),new(actor^1)),Battlers(doubles),doubles,new Audio());int frames=0;bool visible=false;
            while(runtime.Active&&frames++<500){var frame=runtime.Tick();visible|=frame.Sprites.Any(s=>s.Visible&&s.Blend);}
            Assert.IsFalse(runtime.Active);Assert.IsTrue(visible);Assert.AreEqual(0,runtime.Snapshot().BackgroundSlots.Length);Assert.AreEqual(0,runtime.Snapshot().Sprites.Length);Assert.IsFalse(runtime.Snapshot().BlendEnabled);Assert.IsFalse(runtime.GraphicsLoaded);
        }
    }
    [TestMethod]
    [DataRow(0,false)][DataRow(1,false)][DataRow(0,true)][DataRow(3,true)]
    public void WallHasExactAlphaRampAndTwoDistinctBackgroundRestorationCallbacks(int actor,bool doubles)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var source=resources.Resolve(112).Script;var create=source.Commands.Single(c=>c.Op=="createsprite");
        var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_GRAY_LIGHT_WALL"],1),new("setalpha",["0","16"],2),create,new("waitforvisualfinish",[],3),new("end",[],4)]};
        var bytes=File.ReadAllBytes(Path.Combine(TestAssets.Root,"content/generated/battle/animations/walls-gray-light-wall.pal16"));var original=Enumerable.Range(0,16).Select(i=>BitConverter.ToUInt16(bytes,i*2)).ToArray();
        var battlers=Battlers(doubles);var runtime=new SourceBattleAnimation(script,new("wall-vector",1,112,new(actor),new(actor^1)),battlers,doubles,new Audio());for(int i=0;i<3;i++)runtime.Tick();
        for(int tick=1;tick<=60;tick++)
        {
            var frame=runtime.Tick();var wall=frame.Sprites.Single();Assert.AreEqual((short)(!doubles&&actor%2!=0?-40:40),frame.AnimationArguments[0]);int alpha=tick<=13?tick:tick<=45?13:Math.Max(0,59-tick);
            int rotations=Math.Clamp((tick-13)/2,0,16);var expectedPalette=original.ToArray();for(int i=1;i<=8;i++)expectedPalette[i]=original[1+((i-1-rotations%8+8)%8)];CollectionAssert.AreEqual(expectedPalette,frame.SpritePalettes[10169]);
            Assert.AreEqual(alpha,frame.AlphaA,$"alpha tick{tick}");Assert.AreEqual(16-alpha,frame.AlphaB);Assert.AreEqual(tick<59,wall.Visible);
            var center=battlers[actor].Base(doubles);var expected=doubles?(actor%2==0?new PixelPoint(72,80):new PixelPoint(176,40)):new PixelPoint(center.X+(actor%2==0?40:-40),center.Y);
            Assert.AreEqual(expected,wall.Position);if(actor%2==0)Assert.AreEqual(200,wall.Subpriority);
            CollectionAssert.AreEqual(tick<60?(doubles?new[]{1,3}:new[]{1}):Array.Empty<int>(),frame.BackgroundSlots);
            if(tick==59)foreach(int slot in doubles?new[]{1,3}:new[]{1})Assert.IsTrue(frame.RestoredObjectSlots.Contains(slot));
        }
        Assert.AreEqual(0,runtime.Tick().Sprites.Length);
    }
}
