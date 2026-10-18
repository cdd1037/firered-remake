using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SecretPowerAnimationTests
{
    private static readonly int[] Tails=[302,345,341,127,57,61,88,44,70,21];
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    private sealed class Audio:ISourceAnimationAudio
    {
        public readonly List<SourceAnimationSound> Cues=[];public readonly List<(int Pan,int Frame)> Pans=[];
        public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);
        public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));public void StopAnimationEffects(){}
    }
    private static AnimationBattler[] Bodies(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body-"+i,5+i,2+i,40,56)).ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static CommittedMoveAnimation Cue(int terrain,int actor=0)=>new("secret-power",17,290,new(actor),new(actor^1)){SourceTerrain=(SourceAnimationTerrain)terrain,VisualSeed=0x98765432};
    private static SourceBattleAnimation New(SourceAnimationScript script,CommittedMoveAnimation? cue=null)=>new(script,cue??Cue(0),Bodies(false),false,new Audio());
    private static SourceAnimationScript Replace(SourceAnimationScript script,int id,Func<SourceAnimationScript,SourceAnimationScript> transform)=>script with{TailPrograms=script.TailPrograms.ToDictionary(p=>p.Key,p=>p.Value.MoveId==id?transform(p.Value):p.Value)};
    private static short[] Arguments(SourceBattleAnimation runtime)=>(short[])typeof(SourceBattleAnimation).GetField("_animationArgs",Hidden)!.GetValue(runtime)!;
    private static string FrameValue(SourceAnimationFrame frame)=>JsonSerializer.Serialize(frame with{Trace=[],SpritePalettes=frame.SpritePalettes.OrderBy(p=>p.Key).ToDictionary(p=>p.Key,p=>p.Value)});

    [TestMethod]
    public void EveryNativeTerrainComposesExactlyItsCanonicalTailOnTheSameTickAndOriginalSeed()
    {
        using var resources=new SourceAnimationResources(Folder);var secret=resources.Resolve(290);
        Assert.AreEqual(11,secret.Script.Commands.Length);Assert.AreEqual(10,secret.Script.ProgramReferences.Length);
        CollectionAssert.AreEqual(Tails,secret.Script.ProgramReferences.Select(label=>secret.Script.TailPrograms[label].MoveId).ToArray());
        Assert.IsTrue(SourceAnimationProgramDependencies.ProgramClosure(secret.Script).Sum(p=>p.Commands.Length)>128);
        foreach(int terrain in Enumerable.Range(0,10))foreach(var layout in new[]{(false,0),(false,1),(true,0),(true,1),(true,2),(true,3)})
        {
            var (doubles,actor)=layout;var cue=Cue(terrain,actor);var leaf=resources.Resolve(Tails[terrain]);var bodies=Bodies(doubles);
            var actualAudio=new Audio();var expectedAudio=new Audio();
            var assets=secret.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));
            foreach(var b in bodies)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x801f,4096).ToArray()));
            var actualScene=new SourceBattleAnimation.SpriteScene();var expectedScene=new SourceBattleAnimation.SpriteScene();
            var actualSurface=resources.CreateBackgroundSurface(terrain);var expectedSurface=resources.CreateBackgroundSurface(terrain);
            ushort[] palette=Enumerable.Range(0,16).Select(i=>actualSurface.PaletteColor(2,i)).ToArray();
            var actual=new SourceBattleAnimation(secret.Script,cue,bodies,doubles,actualAudio,palette,spriteScene:actualScene,spriteMaterials:assets);
            var expected=new SourceBattleAnimation(leaf.Script,cue with{MoveId=Tails[terrain]},bodies,doubles,expectedAudio,palette,spriteScene:expectedScene,spriteMaterials:assets);
            // The direct-tail oracle starts at the source pointer after the
            // synchronous predicate; other source registers are retained verbatim.
            for(int i=0;i<Arguments(actual).Length;i++)Arguments(actual)[i]=Arguments(expected)[i]=(short)(i+17);
            Arguments(expected)[0]=(short)terrain;
            var actualComposer=AnimationCompositionFixture.Create(resources,secret,assets,bodies,actualSurface);
            var expectedComposer=AnimationCompositionFixture.Create(resources,leaf,assets,bodies,expectedSurface);
            int frames=0;
            while(actual.Active&&frames++<2048)
            {
                Assert.IsTrue(expected.Active);var a=actual.Tick();var e=expected.Tick();
                Assert.AreEqual(FrameValue(e),FrameValue(a),$"terrain {terrain}, actor {actor}, frame {frames}");
                CollectionAssert.AreEqual(expectedComposer.ComposeField(e),actualComposer.ComposeField(a));
                Assert.AreEqual(expected.ActiveVisualCount,actual.ActiveVisualCount);Assert.AreSame(cue,actual.Cue);
            }
            Assert.IsFalse(actual.Active);Assert.IsFalse(expected.Active);Assert.IsTrue(frames>1);
            CollectionAssert.AreEqual(expectedAudio.Cues,actualAudio.Cues);CollectionAssert.AreEqual(expectedAudio.Pans,actualAudio.Pans);
            while(actual.HasResidualSprites&&frames++<4096)
                CollectionAssert.AreEqual(expectedComposer.ComposeField(expectedScene.TickIdle(bodies,doubles)),actualComposer.ComposeField(actualScene.TickIdle(bodies,doubles)));
            Assert.IsFalse(actual.HasResidualSprites);Assert.IsFalse(expected.HasResidualSprites);
        }
    }

    [TestMethod]
    public void UnknownTerrainAndDanglingOrCyclicTailsFailBeforeSceneOwnership()
    {
        using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(290).Script;
        foreach(var terrain in new SourceAnimationTerrain?[]{null,(SourceAnimationTerrain)(-1),(SourceAnimationTerrain)10})
        {
            var error=Assert.ThrowsExactly<SourceAnimationException>(()=>New(script,Cue(0) with{SourceTerrain=terrain}));
            Assert.AreEqual(290,error.Diagnostic.MoveId);Assert.AreEqual("Move_SECRET_POWER",error.Diagnostic.Script);
        }
        Assert.ThrowsExactly<InvalidDataException>(()=>New(script with{TailPrograms=new Dictionary<string,SourceAnimationScript>()}));
        foreach(string target in new[]{"Move_MISSING","Move_SECRET_POWER"})
        {
            var bad=script with{Commands=[C("createvisualtask","AnimTask_GetBattleTerrain","5"),C("goto",target)],ProgramReferences=[target]};
            Assert.ThrowsExactly<InvalidDataException>(()=>resources.Resolve(bad));
        }
        Assert.ThrowsExactly<InvalidDataException>(()=>New(script with{Commands=script.Commands.Concat([C("end")]).ToArray()}));
    }

    [TestMethod]
    public void EveryBranchIsAbiAndResourceCheckedButOnlyInvokedCompanionsConsumeCapacity()
    {
        using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(290).Script;
        var badAbi=Replace(script,57,p=>p with{Commands=p.Commands.Prepend(C("not-a-source-operation")).ToArray()});
        var error=Assert.ThrowsExactly<SourceAnimationException>(()=>New(badAbi,Cue(0)));
        Assert.AreEqual(290,error.Diagnostic.MoveId);Assert.AreEqual("Move_SECRET_POWER",error.Diagnostic.Script);
        var missingPalette=script with{SpritePalettes=script.SpritePalettes.Where(p=>p.Key!=script.SpriteTags["ANIM_TAG_LEAF"]).ToDictionary(p=>p.Key,p=>p.Value)};
        Assert.ThrowsExactly<SourceAnimationException>(()=>New(missingPalette,Cue(4)));
        var competingSurf=Replace(script,57,p=>p with{Commands=p.Commands.Prepend(p.Commands[0]).ToArray()});
        New(competingSurf,Cue(0)).Cancel();
        Assert.ThrowsExactly<SourceAnimationException>(()=>New(competingSurf,Cue(4)));
        var noLeafStop=Replace(script,345,p=>p with{Commands=p.Commands.Where(c=>c.Op!="setarg").ToArray()});
        New(noLeafStop,Cue(0)).Cancel();
        Assert.ThrowsExactly<SourceAnimationException>(()=>New(noLeafStop,Cue(1)));
        var oversized=Replace(script,21,p=>p with{Commands=Enumerable.Repeat(C("delay","1"),128).Append(C("end")).ToArray()});
        Assert.ThrowsExactly<InvalidDataException>(()=>New(oversized,Cue(0)));
    }

    [TestMethod]
    public void ImmediateSelectorCannotHideEffectCompanionsAndGraphBudgetUsesMaximumPath()
    {
        using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(290).Script;
        Assert.ThrowsExactly<InvalidDataException>(()=>New(script with{Commands=script.Commands.Prepend(C("setalpha","8","8")).ToArray()}));
        int expected=script.ProgramReferences.Select((label,index)=>Math.Min(index+2,11)+SourceAnimationProgramDependencies.MaximumCost(script.TailPrograms[label],_=>1)).Max();
        Assert.AreEqual(expected,SourceAnimationProgramDependencies.MaximumCost(script,_=>1));
        Assert.IsTrue(expected<128,"Ten mutually exclusive tails must not become one flattened program.");
        foreach(int terrain in Enumerable.Range(0,10))foreach(int ticks in new[]{1,7,19})
        {
            var runtime=New(script,Cue(terrain));for(int i=0;i<ticks&&runtime.Active;i++)runtime.Tick();runtime.Cancel();
            Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);
            Assert.IsNull(runtime.Snapshot().Overlay);Assert.IsNull(runtime.Snapshot().Scanline);
        }
    }
}
