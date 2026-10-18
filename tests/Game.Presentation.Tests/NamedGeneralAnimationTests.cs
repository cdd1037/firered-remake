using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class NamedGeneralAnimationTests
{
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    private static AnimationBattler[] Bodies(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body-"+i,5+i,2+i,40,56)).ToArray();
    private static CommittedMoveAnimation Cue(string label,int actor,int target)=>new("named-general",17,0,new(actor),new(target)){GeneralAnimationLabel=label,VisualSeed=0x98765432};
    private static string FrameValue(SourceAnimationFrame f)=>JsonSerializer.Serialize(f with{Trace=[],SpritePalettes=f.SpritePalettes.OrderBy(p=>p.Key).ToDictionary(p=>p.Key,p=>p.Value)});
    private static Dictionary<string,SourcePixelAsset> Assets(SourceResolvedAnimation resolved,AnimationBattler[] bodies)
    {
        var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));
        foreach(var b in bodies)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)(0x8000+31+(b.Slot.Value<<8)),4096).ToArray()));return assets;
    }
    private static ushort[] Palette(SourceAnimationBackgroundSurface surface)=>Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray();

    [TestMethod][DataRow("General_Sun",241)][DataRow("General_Sandstorm",201)][DataRow("General_Hail",258)]
    public void SingleGotoKeepsCanonicalLeafAndGeneralTaskPhaseWithoutTerrainInput(string label,int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.ResolveGeneral(label);var leaf=resources.Resolve(move);
        Assert.AreEqual(1,resolved.Script.Commands.Length);Assert.AreEqual("goto",resolved.Script.Commands[0].Op);
        CollectionAssert.AreEqual(new[]{leaf.Script.Label},resolved.Script.ProgramReferences);Assert.AreEqual(move,resolved.Script.TailPrograms[leaf.Script.Label].MoveId);
        CollectionAssert.AreEquivalent(leaf.Assets.Keys.ToArray(),resolved.Assets.Keys.ToArray());
        foreach(var (doubles,actor) in new[]{(false,0),(false,1),(true,0),(true,1),(true,2),(true,3)})
        {
            var bodies=Bodies(doubles);var assets=Assets(resolved,bodies);var cue=Cue(label,actor,actor);Assert.IsNull(cue.SourceTerrain);
            var audio=new Audio();var expectedAudio=new Audio();var surface=resources.CreateBackgroundSurface(0);var expectedSurface=resources.CreateBackgroundSurface(0);
            var scene=new SourceBattleAnimation.SpriteScene();var expectedScene=new SourceBattleAnimation.SpriteScene();
            var actual=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,audio,Palette(surface),spriteScene:scene,spriteMaterials:assets);
            // Enter the same canonical leaf under the controller's general-task
            // scheduling phase. Only the pointer changes in the authored goto.
            var expected=new SourceBattleAnimation(leaf.Script with{MoveId=0,Label=label},cue,bodies,doubles,expectedAudio,Palette(expectedSurface),spriteScene:expectedScene,spriteMaterials:assets);
            var compositor=AnimationCompositionFixture.Create(resources,resolved,assets,bodies,surface);var expectedCompositor=AnimationCompositionFixture.Create(resources,leaf,assets,bodies,expectedSurface);
            var arguments=typeof(SourceBattleAnimation).GetField("_animationArgs",BindingFlags.Instance|BindingFlags.NonPublic)!;
            for(int i=0;i<16;i++)((short[])arguments.GetValue(actual)!)[i]=((short[])arguments.GetValue(expected)!)[i]=(short)(i+21);
            int frames=0;bool pixels=false;
            while(actual.Active&&frames++<2048)
            {
                Assert.IsTrue(expected.Active);var a=actual.Tick();var e=expected.Tick();Assert.AreEqual(FrameValue(e),FrameValue(a),$"{label}/{actor}/{frames}");
                CollectionAssert.AreEqual(expectedCompositor.ComposeField(e),compositor.ComposeField(a));pixels|=a.Sprites.Length>0||a.Overlay is not null;
                Assert.AreSame(cue,actual.Cue);Assert.AreEqual(expected.ActiveVisualCount,actual.ActiveVisualCount);
            }
            Assert.IsFalse(actual.Active||expected.Active);Assert.IsTrue(pixels);CollectionAssert.AreEqual(expectedAudio.Cues,audio.Cues);
            while(actual.HasResidualSprites&&frames++<4096)CollectionAssert.AreEqual(expectedCompositor.ComposeField(expectedScene.TickIdle(bodies,doubles)),compositor.ComposeField(scene.TickIdle(bodies,doubles)));
            Assert.IsFalse(actual.HasResidualSprites||expected.HasResidualSprites);Assert.IsFalse(actual.GraphicsLoaded||expected.GraphicsLoaded);
        }
    }

    [TestMethod][DataRow("General_Rain")][DataRow("General_FutureSightHit")][DataRow("General_DoomDesireHit")][DataRow("General_FocusPunchSetUp")]
    public void AuthoredGeneralProgramComposesAllLayoutsAndCompletesWithoutRetainingOwners(string label)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.ResolveGeneral(label);
        Assert.AreEqual(0,resolved.Script.MoveId);Assert.AreEqual(label,resolved.Script.Label);
        foreach(var (doubles,actor) in new[]{(false,0),(false,1),(true,0),(true,1),(true,2),(true,3)})
        {
            int target=label is "General_FutureSightHit" or "General_DoomDesireHit"?actor^1:actor;var bodies=Bodies(doubles);var assets=Assets(resolved,bodies);var surface=resources.CreateBackgroundSurface(0);
            var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(resolved.Script,Cue(label,actor,target),bodies,doubles,audio,Palette(surface),spriteScene:scene,spriteMaterials:assets);
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies,surface);var baseline=composer.ComposeField(runtime.Snapshot());int frames=0;bool visual=false,pixels=false,shaken=false;
            while(runtime.Active&&frames++<1024)
            {
                var frame=runtime.Tick();var before=JsonSerializer.Serialize(frame);var rgba=composer.ComposeField(frame);composer.ComposeField(frame);Assert.AreEqual(before,JsonSerializer.Serialize(frame));
                visual|=frame.Sprites.Length>0||frame.MoveBackground?.Id is not null||frame.Overlay is not null;shaken|=frame.Offsets[target]!=default;
                if(frame.Sprites.Length>0)pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));
                if(frame.MoveBackground?.Id is not null||frame.Overlay is not null)pixels|=!rgba.SequenceEqual(baseline);
            }
            Assert.IsFalse(runtime.Active);Assert.IsTrue(visual&&pixels);Assert.IsTrue(audio.Cues.Count>0);
            if(label=="General_FutureSightHit")Assert.IsTrue(shaken);
            while(runtime.HasResidualSprites&&frames++<2048)composer.ComposeField(scene.TickIdle(bodies,doubles));
            Assert.IsFalse(runtime.HasResidualSprites||runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);
            var final=runtime.Snapshot();Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsNull(final.Overlay);Assert.IsNull(final.MoveBackground?.Id);Assert.IsFalse(final.BlendEnabled);Assert.IsTrue(final.Offsets.All(p=>p==default));
            Assert.AreEqual(0,final.PaletteBlends.Length);
        }
    }

    [TestMethod]
    public void CancellationAndReceiptGateReleaseAllSevenGeneralPrograms()
    {
        using var resources=new SourceAnimationResources(Folder);
        foreach(string label in new[]{"General_Rain","General_Sun","General_Sandstorm","General_Hail","General_FutureSightHit","General_DoomDesireHit","General_FocusPunchSetUp"})
        foreach(int stop in new[]{1,5,20,60,120,250,512})
        {
            var resolved=resources.ResolveGeneral(label);var bodies=Bodies(true);var surface=resources.CreateBackgroundSurface(0);var cue=Cue(label,3,label=="General_FutureSightHit"?1:3);var gate=new SourceBattleAnimationGate();
            Assert.IsTrue(gate.TryStart(cue,()=>new(resolved.Script,cue,bodies,true,new Audio(),Palette(surface))));
            int reached=0;while(gate.IsBlocking&&reached++<stop)gate.Current!.Tick();gate.Current!.Cancel();gate.Current.Cancel();
            Assert.IsFalse(gate.IsBlocking||gate.Current.HasResidualSprites||gate.Current.GraphicsLoaded);Assert.AreEqual(0,gate.Current.ActiveVisualCount);
            var final=gate.Current.Snapshot();Assert.IsNull(final.Overlay);Assert.IsNull(final.Scanline);Assert.IsFalse(final.BlendEnabled);Assert.AreEqual(0,final.Sprites.Length);Assert.AreEqual(0,final.PaletteBlends.Length);Assert.IsTrue(final.Offsets.All(p=>p==default));
            Assert.IsFalse(gate.TryStart(cue,()=>throw new InvalidOperationException("Same source receipt cannot recreate its general animation.")));
        }
    }

    [TestMethod]
    public void TailAliasesRejectMissingTargetsCyclesAndEffectPrefixes()
    {
        using var resources=new SourceAnimationResources(Folder);var script=resources.ResolveGeneral("General_Hail").Script;
        SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
        foreach(string target in new[]{"Move_MISSING","General_Hail"})Assert.ThrowsExactly<InvalidDataException>(()=>resources.Resolve(script with{Commands=[C("goto",target)],ProgramReferences=[target]}));
        foreach(var commands in new[]{new[]{C("setalpha","8","8"),script.Commands[0]},new[]{script.Commands[0],C("end")}})
            Assert.ThrowsExactly<InvalidDataException>(()=>resources.Resolve(script with{Commands=commands}));
        Assert.ThrowsExactly<InvalidDataException>(()=>resources.Resolve(script with{ProgramReferences=["Move_HAIL","Move_HAIL"]}));
    }

    [TestMethod]
    public void DelayedTargetSetterRequiresNamedFirstCommandAndRainRequiresCommittedVisualSeed()
    {
        using var resources=new SourceAnimationResources(Folder);var script=resources.ResolveGeneral("General_FutureSightHit").Script;var bodies=Bodies(false);var cue=Cue(script.Label,0,1);var palette=Palette(resources.CreateBackgroundSurface(0));
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,cue with{GeneralAnimationLabel=null},bodies,false,new Audio(),palette));
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script with{Commands=[new("delay",["1"],1),..script.Commands]},cue,bodies,false,new Audio(),palette));
        var rain=resources.ResolveGeneral("General_Rain").Script;
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(rain,Cue(rain.Label,0,0) with{VisualSeed=null},bodies,false,new Audio()));
    }
}
