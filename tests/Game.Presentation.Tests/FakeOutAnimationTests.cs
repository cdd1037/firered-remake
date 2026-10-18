using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FakeOutAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound> Cues=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects(){}}
    private static readonly BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private static AnimationBattler[] Bodies(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body-"+i,0,0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int actor=0)=>new("fake-out",1,252,new(actor),new(actor^1));
    private static SourceAnimationScript Primitive()=>new(252,"Move_FAKE_OUT",[new("createvisualtask",["AnimTask_FakeOut","5"],1),new("waitforvisualfinish",[],2),new("end",[],3)]);
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("fake-out-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_fake_out_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/fake-out-source-vectors.json");
    private static void Check(SourceAnimationFrame frame,int[] row)
    {
        Assert.IsNotNull(frame.Window0);Assert.IsTrue(frame.Window0.Enabled);
        Assert.AreEqual((ushort)row[7],frame.Window0.Horizontal);Assert.AreEqual((ushort)row[8],frame.Window0.Vertical);
        Assert.AreEqual(row[11]&63,frame.Window0.InsideMask);Assert.AreEqual(row[12]&63,frame.Window0.OutsideMask);
        Assert.AreEqual(row[13]&63,frame.BlendFirstTargets);Assert.IsFalse(frame.BlendEnabled);
        if(row[13]==0)Assert.IsNull(frame.Brightness);
        else {Assert.IsNotNull(frame.Brightness);Assert.AreEqual((SourceBrightnessMode)((row[13]>>6)&3),frame.Brightness.Mode);Assert.AreEqual(row[14],frame.Brightness.Coefficient);}
        var blends=frame.PaletteBlends;
        Assert.AreEqual(row[15]==0?0:3,blends.Length);
        if(row[15]!=0){CollectionAssert.AreEqual(new[]{1,2,3},blends.Select(p=>p.Palette).ToArray());Assert.IsTrue(blends.All(p=>p.Coefficient==row[17]&&p.Color==row[18]));}
    }
    [TestMethod]
    public void EveryOriginalCallbackAndPostTaskVBlankLatchMatchesIndependentSourceC()
    {
        using var vectors=Vectors();var body=Bodies(false);var runtime=new SourceBattleAnimation(Primitive(),Cue(),body,false,new Audio());
        var type=typeof(SourceBattleAnimation).GetNestedType("FakeOutTask",BindingFlags.NonPublic)!;
        var task=Activator.CreateInstance(type,Private|BindingFlags.Public,null,[runtime],null)!;var step=type.GetMethod("Step")!;int count=0;
        foreach(var item in vectors.RootElement.GetProperty("tasks").GetProperty("0").EnumerateArray())
        {
            int[] row=item.EnumerateArray().Select(v=>v.GetInt32()).ToArray();if(row[0]!=0)step.Invoke(task,[runtime]);
            Check(runtime.Snapshot(),row);Assert.AreEqual(row[1]!=0,type.GetField("_closed",Private)!.GetValue(task));
            Assert.AreEqual(row[2],type.GetField("_left",Private)!.GetValue(task));Assert.AreEqual(row[3],type.GetField("_right",Private)!.GetValue(task));Assert.AreEqual(row[4],type.GetField("_timer",Private)!.GetValue(task));
            Assert.AreEqual(row[6]==0,type.GetProperty("Finished")!.GetValue(task));count++;
        }
        Assert.AreEqual(17,count);runtime.Cancel();runtime=new SourceBattleAnimation(Primitive(),Cue(),body,false,new Audio());count=0;
        foreach(var item in vectors.RootElement.GetProperty("frames").GetProperty("0").EnumerateArray())
        {
            int[] row=item.EnumerateArray().Select(v=>v.GetInt32()).ToArray();var frame=runtime.Tick();Check(frame,row);
            Assert.AreEqual(row[0],frame.Frame);Assert.AreEqual(row[9],(int)frame.Window0!.Horizontal);Assert.AreEqual(row[10],(int)frame.Window0.Vertical);Assert.AreEqual(row[6],runtime.ActiveVisualCount);count++;
        }
        Assert.AreEqual(16,count);for(int i=0;i<10&&runtime.Active;i++)runtime.Tick();Assert.IsFalse(runtime.Active);
    }
    [TestMethod]
    public void CompleteRootComposesEveryFrameAndReplaysAllSixLayoutsWithSharedAffineAndPaletteCleanup()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(252);
        using var vectors=Vectors();var expected=vectors.RootElement.GetProperty("frames").GetProperty("0").EnumerateArray().Select(e=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).ToArray();
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());assets.TryAdd("impact",new(32,32,new ushort[1024]));foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();
            var runtime=new SourceBattleAnimation(resolved.Script,Cue(actor),bodies,doubles,audio,spriteScene:scene,spriteMaterials:resolved.Assets);
            var replay=new SourceBattleAnimation(resolved.Script,Cue(actor),bodies,doubles,new Audio(),spriteMaterials:resolved.Assets);
            byte[] baseline=composer.ComposeField(runtime.Snapshot());int ticks=0;bool dark=false,light=false,stretch=false,shake=false;
            while(runtime.Active&&ticks++<160)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(replay.Tick()));
                if(frame.Frame<expected.Length)Check(frame,expected[frame.Frame]);
                var pixels=composer.ComposeField(frame);Assert.AreEqual(240*112*4,pixels.Length);
                if(frame.Brightness?.Mode==SourceBrightnessMode.Darken)dark|=!pixels.SequenceEqual(baseline);
                if(frame.Brightness?.Mode==SourceBrightnessMode.Lighten)light|=!pixels.SequenceEqual(baseline);
                stretch|=frame.BattlerAffine[actor^1].YScale!=256;shake|=frame.Offsets[actor^1].X!=0;
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.IsTrue(dark&&light&&stretch&&shake);
            Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.HasResidualSprites);
            var final=runtime.Snapshot();Assert.IsNull(final.Brightness);Assert.AreEqual((ushort)0,final.Window0!.Horizontal);Assert.AreEqual((ushort)0,final.Window0.Vertical);Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.AreEqual(0,final.PaletteBlends.Length);
            CollectionAssert.AreEqual(baseline,composer.ComposeField(final));
            CollectionAssert.AreEqual(new[]{"SE_M_FLATTER","SE_M_SKETCH"},audio.Cues.Select(c=>c.Id).ToArray());
            Assert.AreEqual(0,audio.Cues[0].Pan);Assert.AreEqual(actor%2==0?63:-63,audio.Cues[1].Pan);CollectionAssert.AreEqual(new[]{0,16},audio.Cues.Select(c=>c.Frame).ToArray());
        }
    }
    [TestMethod]
    public void CancellationAndUnreviewedRegisterCompanionsNeverLeakWindowOrBrightnessOwnership()
    {
        foreach(int stop in new[]{0,1,9,10,14,15,16})
        {
            var runtime=new SourceBattleAnimation(Primitive(),Cue(),Bodies(false),false,new Audio());for(int i=0;i<stop&&runtime.Active;i++)runtime.Tick();runtime.Cancel();runtime.Cancel();
            var frame=runtime.Snapshot();Assert.IsNull(frame.Window0);Assert.IsNull(frame.Brightness);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,frame.PaletteBlends.Length);
        }
        var commands=Primitive().Commands;
        foreach(var command in new[]{new SourceAnimationCommand("createvisualtask",["AnimTask_FakeOut","4"],1),new("createvisualtask",["AnimTask_FakeOut","5","0"],1)})
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(Primitive() with{Commands=[command,..commands.Skip(1)]},Cue(),Bodies(false),false,new Audio()));
        foreach(var companion in new[]{new SourceAnimationCommand("setalpha",["8","8"],1),new("monbg",["ANIM_TARGET"],1),new("createvisualtask",["AnimTask_FakeOut","5"],1),new("goto",["again"],1)})
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(Primitive() with{Commands=[companion,..commands]},Cue(),Bodies(false),false,new Audio()));
    }
}
