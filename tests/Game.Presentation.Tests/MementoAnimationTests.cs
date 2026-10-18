using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class MementoAnimationTests
{
    private const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound>Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects()=>Stops++;}
    private static object? Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Hidden)!.Invoke(o,args);
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Hidden)!.GetValue(o)!;
    private static int I(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Bodies(bool doubles,int height=48,int offset=8,int elevation=0,int hidden=-1)=>Enumerable.Range(0,doubles?4:2)
        .Select(i=>new AnimationBattler(new(i),"b"+i,offset,elevation,64,height){SourceVisibility=new(true,i==hidden,i==hidden)}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript script)=>script with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static Dictionary<string,SourcePixelAsset> Assets(SourceResolvedAnimation resolved,AnimationBattler[] bodies)
    {
        var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0xfc00,240*112).ToArray());
        foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64>8&&i%64<55&&i/64>5&&i/64<58?0x8000|((i%64)&31)|(((i/64)&31)<<5):0)).ToArray());
        return assets;
    }
    private static object Task(SourceBattleAnimation o)=>Field<IEnumerable>(o,"_tasks").Cast<object>().Single(x=>x.GetType().Name=="MementoShadowTask");
    private static void Create(SourceBattleAnimation o,string name)
    {var c=C("createvisualtask",name,name is "AnimTask_InitMementoShadow" or "AnimTask_MementoHandleBg"?"2":"5");Assert.IsTrue((bool)Call(o,"TryCreateMementoTask",c)!);Call(o,"AssignTaskOrder",0,0,c);}
    private static void Seed(SourceScanlineState state)
    {for(int bank=0;bank<2;bank++)for(int i=0;i<960;i++)state.Write(bank,i,(ushort)(0x4000+bank*2048+i));}
    private static int[] Bank(SourceScanlineState state)=>Enumerable.Range(0,1920).Select(i=>(int)state.Read(i/960,i%960)).ToArray();
    [TestMethod]
    public void ExtractedCallbacksMatchEveryTaskRegisterBankWriteWindowAlphaAndDeferredStop()
    {
        using var vectors=SourceVectorFixture.Read("memento-source-vectors.json","python tests/Reference/Memento/generate.py content/raw/pokefirered/037335f4 --repo . --output tests/Game.Presentation.Tests/Fixtures/memento-source-vectors.json");
        Assert.AreEqual("firered-memento-v1",vectors.RootElement.GetProperty("schema").GetString());using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(262);int cases=0,frames=0;
        foreach(var c in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {
            int mode=I(c,"mode"),slot=I(c,"slot");bool doubles=c.GetProperty("doubles").GetBoolean();var bodies=Bodies(doubles,I(c,"height"),I(c,"offset"),I(c,"elevation"),c.GetProperty("hiddenPartner").GetBoolean()?slot^2:-1);
            var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(Primitive(resolved.Script),new("memento",1,262,new(mode==0?slot:slot^1),new(mode==0?slot^1:slot)),bodies,doubles,new Audio(),spriteScene:scene);
            Call(o,"SetAlphaBlend",0,63,0,16);
            if(mode==0)Create(o,"AnimTask_InitMementoShadow");else {Call(o,"MoveBattlerToStaticBackground",slot);if(doubles&&!c.GetProperty("hiddenPartner").GetBoolean())Call(o,"MoveBattlerToStaticBackground",slot^2);}
            Call(o,"AdvanceScanlineVBlank");Seed(scene.Scanlines);var expected=Bank(scene.Scanlines);
            Create(o,mode==0?"AnimTask_MoveAttackerMementoShadow":"AnimTask_MoveTargetMementoShadow");var task=Task(o);
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                if(I(row,"frame")>0){Call(o,"RunSourceTasks");Call(o,"AdvanceScanlineVBlank");}
                string why=c.GetProperty("id").GetString()+" frame "+I(row,"frame");var frame=o.Snapshot();
                CollectionAssert.AreEqual(N(row.GetProperty("task")),Field<short[]>(task,"_d").Select(x=>(int)x).ToArray(),why+" task registers");
                Assert.AreEqual(I(row,"setup")!=0,Field<bool>(task,"_setup"),why);Assert.AreEqual(I(row,"alive"),o.ActiveVisualCount,why);
                foreach(var w in row.GetProperty("writes").EnumerateArray())expected[w[0].GetInt32()]=w[1].GetInt32();CollectionAssert.AreEqual(expected,Bank(scene.Scanlines),why+" every retained word");
                Assert.AreEqual(I(row,"state"),scene.Scanlines.State,why);Assert.AreEqual(I(row,"bank"),scene.Scanlines.SourceBuffer,why);
                CollectionAssert.AreEqual(N(row.GetProperty("alpha")),new[]{frame.AlphaA,frame.AlphaB},why);Assert.AreEqual(I(row,"blend")&63,frame.BlendFirstTargets,why);
                CollectionAssert.AreEqual(N(row.GetProperty("bg")),frame.BackgroundScrollOffsets.SelectMany(p=>new[]{p.X,p.Y}).ToArray(),why);
                CollectionAssert.AreEqual(N(row.GetProperty("priority")),Field<int[]>(o,"_battlerObjectPriorities"),why);
                var win=frame.Window0??new SourceWindow0Frame(true,0,0,63,63);CollectionAssert.AreEqual(N(row.GetProperty("window")),new[]{(int)win.Horizontal,win.Vertical,win.InsideMask,win.OutsideMask},why);
                var raster=row.GetProperty("raster");if(raster.ValueKind==JsonValueKind.Null)Assert.IsNull(frame.Scanline,why);else{Assert.IsNotNull(frame.Scanline,why);Assert.IsFalse(frame.Scanline.Word32);Assert.AreEqual(bodies[slot].BackgroundRank==1?SourceScanlineDestination.Bg1Vertical:SourceScanlineDestination.Bg2Vertical,frame.Scanline.Destination);CollectionAssert.AreEqual(N(raster),frame.Scanline.Words.Select(v=>(int)v).ToArray(),why);}
                if(I(row,"fillBank")>=0){var copy=frame.StaticBackgroundCopies.Single(x=>x.Slot==slot);Assert.AreEqual((ushort)0x8000,SourcePaletteMath.Apply(0xffff,copy.Palette,false));}
                frames++;
            }
            o.Cancel();cases++;
        }
        Assert.AreEqual(15,cases);Assert.AreEqual(1099,frames);
        foreach(var c in vectors.RootElement.GetProperty("helper").EnumerateArray())
        {
            var bodies=Bodies(false);var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(Primitive(resolved.Script),new("memento",1,262,new(0),new(1)),bodies,false,new Audio(),spriteScene:scene);
            Create(o,"AnimTask_MoveAttackerMementoShadow");var task=Task(o);var d=Field<short[]>(task,"_d");var a=N(c.GetProperty("args"));Seed(scene.Scanlines);
            if(a[5]==1){scene.Scanlines.CaptureVBlank();}
            var expected=Bank(scene.Scanlines);Array.Clear(d);d[4]=(short)a[0];d[5]=(short)a[1];d[6]=(short)a[2];d[7]=(short)a[3];d[10]=unchecked((short)a[4]);d[13]=unchecked((short)((d[7]-d[6])*256));
            Call(task,"Distort",o);foreach(var w in c.GetProperty("row").GetProperty("writes").EnumerateArray())expected[w[0].GetInt32()]=w[1].GetInt32();CollectionAssert.AreEqual(expected,Bank(scene.Scanlines));o.Cancel();
        }
    }
    [TestMethod]
    public void FullSourceRootComposesBothRanksRetainsOriginalObjectsAndRestoresVisiblePriorities()
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(262);Assert.AreEqual("Move_MEMENTO",r.Script.Label);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var bodies=Bodies(doubles);var assets=Assets(r,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var o=new SourceBattleAnimation(r.Script,new("memento",1,262,new(actor),new(actor^1)),bodies,doubles,audio,spriteScene:scene);
            var initial=o.Snapshot();var composer=AnimationCompositionFixture.Create(resources,r,assets,bodies);int ticks=0,scan=0;bool changed=false,collapsed=false,black=false;SourceScanlineFrame? retained=null;ushort[]? retainedWords=null;
            while(o.Active&&ticks++<400)
            {
                var f=o.Tick();var pixels=composer.ComposeField(f);collapsed|=f.Window0 is {Horizontal:>0} w&&(w.Horizontal>>8)==(w.Horizontal&255);black|=f.StaticBackgroundCopies.Any(c=>c.Palette?.Coefficient==16&&c.Palette.Color==0);
                Assert.IsTrue(f.BattlerVisibility.All(v=>!v.SpriteInvisible));
                if(f.Scanline is not null){scan++;retained??=f.Scanline;retainedWords??=(ushort[])retained.Words.Clone();changed|=!pixels.SequenceEqual(composer.ComposeField(f with{Scanline=null}));}
                Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(o.Snapshot()));
            }
            Assert.IsFalse(o.Active);Assert.IsTrue(scan>100&&changed&&collapsed&&black);Assert.AreEqual(3,audio.Cues.Count);Assert.AreEqual(0,o.ActiveVisualCount);Assert.AreEqual(0,scene.Count);
            var final=o.Snapshot();Assert.IsNull(final.Scanline);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.AreEqual(0,final.StaticBackgroundCopies.Length);Assert.IsFalse(final.BlendEnabled);Assert.IsTrue(final.BattlerObjectPriorities.All(p=>p==2));Assert.AreEqual(new SourceWindow0Frame(true,0,0,63,63),final.Window0);CollectionAssert.AreEqual(retainedWords!,retained!.Words);
            Assert.IsFalse(new SourceCompletedBattlerState().Capture(initial,final));
            foreach(int stop in new[]{0,1,2,3,4,20,48,60,70,82,96,100,112,130,160,190})
            {
                var s=new SourceBattleAnimation.SpriteScene();var x=new SourceBattleAnimation(r.Script,new("cancel",1,262,new(actor),new(actor^1)),bodies,doubles,new Audio(),spriteScene:s);
                for(int i=0;i<stop&&x.Active;i++)x.Tick();var banks=Bank(s.Scanlines);x.Cancel();x.Cancel();Assert.AreEqual(0,s.Scanlines.State);CollectionAssert.AreEqual(banks,Bank(s.Scanlines));Assert.IsNull(x.Snapshot().Window0);Assert.IsNull(x.Snapshot().Scanline);Assert.AreEqual(0,x.ActiveVisualCount);
            }
            var next=new SourceBattleAnimation(Primitive(r.Script),new("next",2,262,new(actor),new(actor^1)),bodies,doubles,new Audio(),spriteScene:scene);Call(next,"StartVerticalScanline",1);o.Cancel();Assert.AreEqual(1,scene.Scanlines.State);Assert.IsNotNull(next.Tick().Scanline);next.Cancel();scene.Cancel();Assert.IsTrue(Bank(scene.Scanlines).All(x=>x==0));
        }
    }
    [TestMethod]
    public void HiddenPresentTargetRunsItsTaskWithoutStaticCopyRevealOrPriorityReset()
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(262);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            int target=actor^1;var bodies=Bodies(doubles,hidden:target);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();
            var o=new SourceBattleAnimation(r.Script,new("hidden-target",1,262,new(actor),new(target)),bodies,doubles,audio,spriteScene:scene);
            var composer=AnimationCompositionFixture.Create(resources,r,Assets(r,bodies),bodies);var initial=o.Snapshot();var original=composer.ComposeField(initial);int ticks=0,scan=0;
            while(o.Active&&ticks++<400)
            {
                var frame=o.Tick();composer.ComposeField(frame);if(frame.Scanline is not null)scan++;
                var hidden=frame.BattlerVisibility.Single(v=>v.Slot==target);Assert.IsTrue(hidden.Present&&hidden.LogicalInvisible&&hidden.SpriteInvisible&&hidden.OamInvisible);
                Assert.IsFalse(frame.BackgroundSlots.Contains(target));Assert.IsFalse(frame.StaticBackgroundCopies.Any(c=>c.Slot==target));
            }
            Assert.IsFalse(o.Active);Assert.IsTrue(scan>100);Assert.AreEqual(3,audio.Cues.Count);var final=o.Snapshot();
            Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsNull(final.Scanline);Assert.IsFalse(final.BlendEnabled);Assert.AreEqual(3,final.BattlerObjectPriorities[target]);
            Assert.IsTrue(final.BattlerVisibility.Where(v=>v.Slot!=target).All(v=>final.BattlerObjectPriorities[v.Slot]==2));CollectionAssert.AreEqual(original,composer.ComposeField(final));
            for(int slot=0;slot<bodies.Length;slot++)Assert.AreEqual(initial.Offsets[slot],final.Offsets[slot]);
            o.Cancel();Assert.IsTrue(o.Snapshot().BattlerVisibility.Single(v=>v.Slot==target).SpriteInvisible);Assert.IsNull(o.Snapshot().Window0);
            foreach(int stop in new[]{3,70,85,100,130,170})
            {
                var cancelled=new SourceBattleAnimation(r.Script,new("hidden-cancel",2,262,new(actor),new(target)),bodies,doubles,new Audio(),spriteScene:scene);
                for(int i=0;i<stop&&cancelled.Active;i++)cancelled.Tick();var retained=Bank(scene.Scanlines);cancelled.Cancel();CollectionAssert.AreEqual(retained,Bank(scene.Scanlines));Assert.AreEqual(0,scene.Scanlines.State);Assert.IsTrue(cancelled.Snapshot().BattlerVisibility.Single(v=>v.Slot==target).SpriteInvisible);
            }
            // Hidden monbg_static leaves the selected BG shadow registers alone.
            // The target callback reads that retained Y even with no copied tiles.
            var isolated=new SourceBattleAnimation(Primitive(r.Script),new("retained-target",3,262,new(actor),new(target)),bodies,doubles,new Audio(),spriteScene:scene);
            int rank=bodies[target].BackgroundRank;Call(isolated,"SetScanlineBackgroundOffset",rank,new PixelPoint(17,23));Call(isolated,"MoveBattlerToStaticBackground",target);
            Create(isolated,"AnimTask_MoveTargetMementoShadow");var task=Task(isolated);
            for(int i=0;i<5;i++){Call(isolated,"RunSourceTasks");Call(isolated,"AdvanceScanlineVBlank");}
            Assert.AreEqual((short)23,Field<short[]>(task,"_d")[10]);Assert.AreEqual(new PixelPoint(17,23),isolated.Snapshot().BackgroundScrollOffsets[rank-1]);Assert.AreEqual(0,isolated.Snapshot().StaticBackgroundCopies.Length);
            isolated.Cancel();scene.Cancel();Assert.IsTrue(Bank(scene.Scanlines).All(x=>x==0));
        }
    }
    [TestMethod]
    public void CmdEndRestoresOnlyVisibleBattlersWithoutChangingGeometryOrHiddenPriority()
    {
        using var resources=new SourceAnimationResources(Folder);var bodies=Bodies(true,hidden:2);var o=new SourceBattleAnimation(Primitive(resources.Resolve(262).Script),new("priority",1,262,new(0),new(1)),bodies,true,new Audio());
        for(int slot=0;slot<4;slot++)Call(o,"SetBattlerObjectPriority",slot,3);Field<PixelPoint[]>(o,"_offsets")[0]=new(7,-3);
        while(o.Active)o.Tick();CollectionAssert.AreEqual(new[]{2,2,3,2},o.Snapshot().BattlerObjectPriorities);Assert.AreEqual(new PixelPoint(7,-3),o.Snapshot().Offsets[0]);Assert.IsTrue(o.Snapshot().BattlerVisibility.Single(v=>v.Slot==2).SpriteInvisible);o.Cancel();
    }
    [TestMethod]
    public void InvalidTaskClosureRejectsBeforeAudioAndRetainedStateChanges()
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(262);
        foreach(var commands in new[]{new[]{C("createvisualtask","AnimTask_MoveAttackerMementoShadow","2"),C("end")},r.Script.Commands.Prepend(C("createvisualtask","AnimTask_MoveAttackerMementoShadow","5")).ToArray(),r.Script.Commands.Prepend(C("monbg","ANIM_TARGET")).ToArray()})
        {var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(r.Script with{Commands=commands},new("bad",1,262,new(0),new(1)),Bodies(false),false,audio,spriteScene:scene));Assert.AreEqual(0,audio.Cues.Count);Assert.AreEqual(0,scene.Scanlines.State);}
    }
}
