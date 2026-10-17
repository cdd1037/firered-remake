using Game.Presentation.BattleAnimations;
using Game.Domain.Randomness;
using System.Text.Json;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class VisualRandomTests
{
    [TestMethod]
    public void StreamUsesSourceUnsignedOverflowAndHighHalfword()
    {
        var random=new SourceVisualRandom(0);
        ushort[] expected=[0,59774,21105,12720,36418,58060,44997,26587];
        CollectionAssert.AreEqual(expected,Enumerable.Range(0,expected.Length).Select(_=>random.Next()).ToArray());
        foreach(uint seed in new[]{0u,1u,0x7fffffffu,0x80000000u,0xffffffffu})
        {
            uint state=seed;random=new(seed);
            for(int i=0;i<1024;i++)
            {
                state=(uint)(((ulong)state*1103515245ul+24691ul)&0xfffffffful);
                Assert.AreEqual((ushort)(state>>16),random.Next());
            }
        }
    }
    [TestMethod]
    public void SeedEncodingIsStableAndDistinguishesInvocationIdentity()
    {
        var cue=new CommittedMoveAnimation("battle-α",17,28,new(2),new(3)){MoveTurn=0};
        Assert.AreEqual(1789325650u,SourceAnimationVisualSeed.For(cue));
        Assert.AreEqual(SourceAnimationVisualSeed.For(cue),SourceAnimationVisualSeed.For(cue with{}));
        Assert.AreEqual(SourceAnimationVisualSeed.For(cue),SourceAnimationVisualSeed.For(cue with{VisualSeed=0xffffffff}));
        foreach(var changed in new[]{cue with{Sequence=18},cue with{MoveTurn=1},cue with{MoveTurn=null},cue with{BattleId="battle-β"},cue with{MoveId=189},cue with{Actor=new(0)},cue with{Target=new(1)},cue with{GeneralAnimationLabel="General_StatsChange"},cue with{StatAnimationArgument=27}})
            Assert.AreNotEqual(SourceAnimationVisualSeed.For(cue),SourceAnimationVisualSeed.For(changed));
    }
    [TestMethod]
    public void IndependentReplayStreamsDoNotConsumeOneAnother()
    {
        var cue=new CommittedMoveAnimation("replay",91,28,new(0),new(1));
        uint seed=SourceAnimationVisualSeed.For(cue);var first=new SourceVisualRandom(seed);var replay=new SourceVisualRandom(seed);
        var cancelled=new SourceVisualRandom(seed);for(int i=0;i<19;i++)cancelled.Next();
        for(int i=0;i<512;i++)Assert.AreEqual(first.Next(),replay.Next());
        var nextHit=new SourceVisualRandom(SourceAnimationVisualSeed.For(cue with{Sequence=92}));var restarted=new SourceVisualRandom(seed);
        Assert.IsFalse(Enumerable.Range(0,32).Select(_=>nextHit.Next()).SequenceEqual(Enumerable.Range(0,32).Select(_=>restarted.Next())));
    }
    private sealed class Silent : ISourceAnimationAudio
    { public bool IsPlaying=>false; public void Play(SourceAnimationSound sound){} public void StopAnimationEffects(){} }
    [TestMethod]
    public void RuntimePlaybackAndCancellationLeaveGameplayStreamIndependent()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var script=resources.Resolve(28).Script;
        var battlers=Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
        var cue=new CommittedMoveAnimation("rng-isolation",42,28,new(0),new(1));
        cue=cue with{VisualSeed=SourceAnimationVisualSeed.For(cue)};
        var runtime=new SourceBattleAnimation(script,cue,battlers,false,new Silent());
        var replay=new SourceBattleAnimation(script,cue,battlers,false,new Silent());
        var gameplay=new RngState(0x12345678,91);var expectedGameplay=gameplay;
        int ticks=0;
        while(runtime.Active&&ticks++<300)
        {
            // Real gameplay draws between frames cannot perturb the animation-local stream.
            gameplay=Gen3Rng.Next(gameplay,"independent-rule-draw",100).State;
            expectedGameplay=Gen3Rng.Next(expectedGameplay,"independent-rule-draw",100).State;
            var before=gameplay;
            Assert.AreEqual(JsonSerializer.Serialize(runtime.Tick()),JsonSerializer.Serialize(replay.Tick()));
            runtime.Snapshot();runtime.Snapshot();
            Assert.AreEqual(before,gameplay);Assert.AreEqual(expectedGameplay,gameplay);
        }
        Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);
        var finalGameplay=gameplay;runtime.Cancel();replay.Cancel();
        Assert.AreEqual(finalGameplay,gameplay);
        Assert.AreEqual(Gen3Rng.Next(expectedGameplay,"next-rule"),Gen3Rng.Next(gameplay,"next-rule"));
    }
    [TestMethod]
    public void SharedUnusedRandomHelpersNeedNoSeedButKeepStaticValidation()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var source=resources.Resolve(28).Script;
        string helper=source.Subroutines.First(p=>p.Value.Any(c=>c.Op=="createsprite"&&c.Args[0]=="gSandAttackDirtSpriteTemplate")).Key;
        var battlers=Enumerable.Range(0,2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
        var cue=new CommittedMoveAnimation("unused-random-helper",1,28,new(0),new(1));
        var unused=source with{Commands=[new("end",[],1)]};
        var runtime=new SourceBattleAnimation(unused,cue,battlers,false,new Silent());
        runtime.Tick();Assert.IsFalse(runtime.Active);
        var invoked=source with{Commands=[new("call",[helper],1),new("end",[],2)]};
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(invoked,cue,battlers,false,new Silent()));
        var nested=source.Subroutines.ToDictionary(p=>p.Key,p=>p.Value);
        nested["outer"]=[new("call",[helper],1),new("return",[],2)];
        SourceAnimationCommand[] entry=[new("call",["outer"],1),new("end",[],2)];
        var closure=SourceAnimationProgramDependencies.RequiredSubroutines(entry,nested);
        Assert.IsTrue(closure.ContainsKey("outer"));Assert.IsTrue(closure.ContainsKey(helper));
        // Closure resolution is recursive, but this VM still explicitly rejects nested calls.
        Assert.ThrowsExactly<InvalidDataException>(()=>new SourceBattleAnimation(source with{Commands=entry,Subroutines=nested},cue with{VisualSeed=0},battlers,false,new Silent()));
        var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);
        recipes["gSandAttackDirtSpriteTemplate"]=recipes["gSandAttackDirtSpriteTemplate"] with{Width=32};
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(unused with{SpriteRecipes=recipes},cue,battlers,false,new Silent()));
        var broken=source.Subroutines.ToDictionary(p=>p.Key,p=>p.Value);
        broken[helper]=[new("unknown-source-command",[],1),new("return",[],2)];
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(unused with{Subroutines=broken},cue,battlers,false,new Silent()));
    }
}
