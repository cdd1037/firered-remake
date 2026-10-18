using System.Globalization;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class DoomDesireAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(byte? turn=0,int actor=0,int target=1)=>new("doom-setup",1,353,new(actor),new(target)){MoveTurn=turn};
    private static void Clean(SourceBattleAnimation runtime)
    {
        Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);var f=runtime.Snapshot();
        Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.IsFalse(f.BlendEnabled);
        Assert.IsTrue(f.Offsets.All(p=>p==default));Assert.IsTrue(f.BattlerAffine.All(a=>!a.DoubleSize&&a.XScale==256&&a.YScale==256));
        Assert.AreEqual(0,f.GreyscaleSlots.Length);Assert.AreEqual(0,f.PaletteBlends.Length);Assert.AreEqual(0,f.InvertedPalettes.Length);
    }
    [TestMethod]
    public void AllSourcePredicateByteTurnsAndRetainedRegistersMatchExtractedC()
    {
        using var doc=SourceVectorFixture.Read("doom_desire_vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_doom_desire_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/doom_desire_vectors.json");
        var rows=doc.RootElement.GetProperty("rows");Assert.AreEqual(1024,rows.GetArrayLength());var coverage=new HashSet<(int,int)>();
        foreach(var entry in rows.EnumerateArray())
        {
            int[] row=entry.EnumerateArray().Select(x=>x.GetInt32()).ToArray();Assert.AreEqual(13,row.Length);Assert.IsTrue(coverage.Add((row[0],row[1])));
            var commands=Enumerable.Range(0,7).Select(i=>C("setarg",i.ToString(CultureInfo.InvariantCulture),(i*37-100).ToString(CultureInfo.InvariantCulture))).ToList();
            commands.Add(C("setarg","7",row[1].ToString(CultureInfo.InvariantCulture)));commands.Add(C("createvisualtask","GetIsDoomDesireHitTurn","255"));commands.Add(C("delay","1"));commands.Add(C("end"));
            var runtime=new SourceBattleAnimation(new(353,"predicate",commands.ToArray()),Cue((byte)row[0]),Battlers(false),false,new Audio());var frame=runtime.Tick();
            Assert.AreEqual(0,frame.Frame);CollectionAssert.AreEqual(row.Skip(2).Take(8).Select(x=>(short)x).ToArray(),frame.AnimationArguments.Take(8).ToArray());
            Assert.AreEqual(row[10],runtime.ActiveVisualCount);Assert.AreEqual(0,row[11]);Assert.AreEqual(1,row[12]);while(runtime.Active)runtime.Tick();Clean(runtime);
        }
    }
    [TestMethod]
    public void PredicateNeedsCommittedTurnAndExactAbiOnlyWhenInvoked()
    {
        var callback=C("createvisualtask","GetIsDoomDesireHitTurn","2");var script=new SourceAnimationScript(353,"predicate",[callback,C("end")]);
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(null),Battlers(false),false,new Audio()));
        foreach(var args in new[]{new[]{"GetIsDoomDesireHitTurn"},new[]{"GetIsDoomDesireHitTurn","-1"},new[]{"GetIsDoomDesireHitTurn","256"},new[]{"GetIsDoomDesireHitTurn","2","0"}})
            Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script with{Commands=[C("createvisualtask",args),C("end")]},Cue(),Battlers(false),false,new Audio()));
        var helper=script with{Commands=[C("end")],Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"UnusedPredicate",[callback,C("return")]}}};
        var runtime=new SourceBattleAnimation(helper,Cue(null),Battlers(false),false,new Audio());runtime.Tick();Clean(runtime);
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(helper with{Commands=[C("call","UnusedPredicate"),C("end")]},Cue(null),Battlers(false),false,new Audio()));
    }
    [TestMethod]
    public void NativeImmediatePredicateAndDelayOnePrecedeAnyPaletteOrMonbgWork()
    {
        var runtime=new SourceBattleAnimation(AnimationCompositionFixture.Script(353),Cue(),Battlers(false),false,new Audio());
        // Cmd_createvisualtask invokes and destroys the predicate immediately.
        // Cmd_delay(1) then takes a decrement callback and a callback hand-back
        // before the script resumes on frame three, as WaitAnimFrameCount does.
        for(int frame=0;frame<3;frame++)
        {
            var f=runtime.Tick();Assert.AreEqual(frame,f.Frame);Assert.AreEqual((short)0,f.AnimationArguments[7]);
            Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.AreEqual(0,f.GreyscaleSlots.Length);
        }
        var next=runtime.Tick();Assert.AreEqual(3,next.Frame);CollectionAssert.AreEqual(new[]{1},next.GreyscaleSlots);Assert.IsTrue(next.BackgroundSlots.Contains(0));
        runtime.Cancel();Clean(runtime);
    }
    [TestMethod]
    public void DedicatedSetupComposesAllSidesAndSelectedAlliesWithoutHitAssets()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(353);
        CollectionAssert.AreEqual(new[]{"impact"},resolved.Assets.Keys.Order().ToArray());Assert.AreEqual(17,resolved.Script.Commands.Length);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x801f,240*112).ToArray()));
            foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();var cue=Cue(0,actor,target);
            var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio(),spriteMaterials:assets);
            int ticks=0;bool grey=false,scale=false,background=false,pixelChange=false;
            while(runtime.Active&&ticks++<256)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(replay.Tick()));
                grey|=frame.GreyscaleSlots.Contains(target);scale|=frame.BattlerAffine[actor].XScale!=256;background|=frame.BackgroundSlots.Contains(actor);
                if(frame.GreyscaleSlots.Contains(target))pixelChange|=!composer.ComposeField(frame).SequenceEqual(composer.ComposeField(frame with{GreyscaleSlots=[]}));
                Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length);Assert.IsFalse(frame.Sprites.Any(s=>s.Asset is not null));
            }
            Clean(runtime);Clean(replay);Assert.IsTrue(grey);Assert.IsTrue(scale);Assert.IsTrue(background);Assert.IsTrue(pixelChange);
            Assert.AreEqual(1,audio.Cues.Count);Assert.AreEqual("SE_M_PSYBEAM",audio.Cues[0].Id);Assert.AreEqual(actor%2==0?-64:63,audio.Cues[0].Pan);
        }
    }
    [TestMethod]
    public void CancellationAtEveryTickRestoresPaletteMonbgAndAffine()
    {
        var script=AnimationCompositionFixture.Script(353);var battlers=Battlers(true);var duration=new SourceBattleAnimation(script,Cue(),battlers,true,new Audio());int ticks=0;while(duration.Active&&ticks++<256)duration.Tick();Clean(duration);
        for(int stop=0;stop<=ticks;stop++)
        {
            var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(script,Cue(),battlers,true,new Audio(),spriteScene:scene);
            for(int i=0;i<stop&&runtime.Active;i++)runtime.Tick();runtime.Cancel();runtime.Cancel();Clean(runtime);Assert.AreEqual(0,scene.Count);
        }
    }
}
