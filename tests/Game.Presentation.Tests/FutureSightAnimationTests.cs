using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class FutureSightAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    [TestMethod]
    public void SetupOnlyFutureSightComposesEveryActorAndRestoresPsychicBackground()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(248);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();var surface=resources.CreateBackgroundSurface(0);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));foreach(var battler in b)assets.Add(battler.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var runtime=new SourceBattleAnimation(resolved.Script,new("future-setup",1,248,new(actor),new(actor^1)),b,doubles,new Audio(),Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray());var composer=AnimationCompositionFixture.Create(resources,resolved,assets,b,surface);int ticks=0;bool psychic=false;
            while(runtime.Active&&ticks++<2048){var frame=runtime.Tick();psychic|=frame.MoveBackground?.Id==3;Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length);}
            Assert.IsFalse(runtime.Active);Assert.IsTrue(psychic);var final=runtime.Snapshot();Assert.IsNull(final.MoveBackground?.Id);Assert.AreEqual(0,final.MoveBackground!.ScrollX);Assert.AreEqual(0,final.MoveBackground.ScrollY);Assert.AreEqual(0,final.Sprites.Length);
        }
    }
}
