using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SurfAnimationTests
{
    private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound> Cues=[];public readonly List<int> Pans=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void SetPan(int pan,int frame)=>Pans.Add(pan);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static AnimationBattler[] Bodies(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body-"+i,5+i,2+i,40,56)).ToArray();
    private static SourceAnimationScript Minimal(SourceAnimationScript script,params SourceAnimationCommand[] commands)=>script with{Commands=commands,Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static object? Call(SourceBattleAnimation r,string name,params object[] args)=>typeof(SourceBattleAnimation).GetMethod(name,Hidden)!.Invoke(r,args);
    private static object Field(object r,string name)=>r.GetType().GetField(name,Hidden)!.GetValue(r)!;
    private static SourceScanlineState State(SourceBattleAnimation r)=>(SourceScanlineState)typeof(SourceBattleAnimation).GetProperty("ScanlineState",Hidden)!.GetValue(r)!;
    private static object[] Tasks(SourceBattleAnimation r)=>((IEnumerable)Field(r,"_tasks")).Cast<object>().ToArray();
    private static int[] Numbers(JsonElement row)=>row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static SourceBattleAnimation New(SourceAnimationScript script,int actor,bool doubles,Audio? audio=null)=>new(script,new("surf",1,script.MoveId,new(actor),new(actor^1)),Bodies(doubles),doubles,audio??new Audio());
    [TestMethod]
    public void EverySurfWholeSourceCaseMatchesBothBanksRegistersPaletteAndRawTaskLifetime()
    {
        using var resources=new SourceAnimationResources(Folder);using var vectors=SourceVectorFixture.Read("surf-source-vectors.json","python tests/Game.Presentation.Tests/Reference/generate_surf_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");
        var water=resources.Resolve(57).Script;var mud=resources.Resolve(330).Script;var source=Minimal(water,C("end")) with{OverlayDependencies=water.OverlayDependencies.Concat(mud.OverlayDependencies).ToArray(),OverlayPalettes=water.OverlayPalettes.Concat(mud.OverlayPalettes).ToDictionary(p=>p.Key,p=>p.Value)};
        int cases=0,frames=0;
        foreach(var entry in vectors.RootElement.GetProperty("cases").EnumerateObject())
        {
            int[] key=entry.Name.Split(':').Select(int.Parse).ToArray(),config=Numbers(vectors.RootElement.GetProperty("configs")[key[1]]);var r=New(source,key[0],true);var state=State(r);
            if(config[2]!=0){state.SetParams(SourceScanlineDestination.Bg1Horizontal,false);state.CaptureVBlank();state.Stop();}
            for(int bank=0;bank<2;bank++)for(int i=0;i<960;i++)state.Write(bank,i,unchecked((ushort)(0x1234+bank*17011+i*977)));
            ((short[])Field(r,"_animationArgs"))[0]=unchecked((short)config[0]);var command=C("createvisualtask","AnimTask_CreateSurfWave",config[1].ToString(),config[0].ToString());Assert.AreEqual(true,Call(r,"TryCreateSurfTask",command));Call(r,"AssignTaskOrder",0,0,command);
            var expectedBanks=new ushort[1920];string material=(config[0]==0?"surf":"muddy-water")+(key[0]%2==0?"-player":"-opponent");ushort[] original=source.OverlayPalettes[material];
            foreach(var expected in entry.Value.EnumerateArray())
            {
                int[] row=Numbers(expected.GetProperty("state"));if(row[0]>=0)Call(r,"RunSourceTasks");var frame=r.Snapshot();var tasks=Tasks(r);string where=entry.Name+" tick "+row[0];
                Assert.AreEqual(row[1],tasks.Count(t=>t.GetType().Name=="SurfWaveTask"),where);Assert.AreEqual(row[2],tasks.Count(t=>t.GetType().Name=="SurfScanlineTask"),where);Assert.AreEqual(row[3]!=0,frame.Overlay is not null,where);
                Assert.AreEqual(new PixelPoint(row[5],row[6]),frame.BackgroundScrollOffsets[0],where);Assert.AreEqual(row[7],frame.BlendEnabled?frame.BlendFirstTargets|64|(frame.BlendSecondTargets<<8):0,where);Assert.AreEqual(row[8],(int)Field(r,"_alphaA")|((int)Field(r,"_alphaB")<<8),where);Assert.AreEqual(row[9],frame.BattlerBackgroundPriorities[0],where);Assert.AreEqual(row[11],state.State,where);Assert.AreEqual(row[12],state.SourceBuffer,where);
                foreach(var change in expected.GetProperty("changes").EnumerateArray()){int[] pair=Numbers(change);expectedBanks[pair[0]]=(ushort)pair[1];}
                for(int bank=0;bank<2;bank++)for(int i=0;i<960;i++)Assert.AreEqual(expectedBanks[bank*960+i],state.Read(bank,i),where+" bank/word "+bank+"/"+i);
                int[] palette=Numbers(expected.GetProperty("palette"));var faded=frame.BackgroundPaletteEntries.ToDictionary(p=>p.Index,p=>p.Color);
                for(int i=0;i<16;i++){Assert.AreEqual(palette[2*i],(int)original[i],where+" unfaded "+i);Assert.AreEqual(palette[2*i+1],(int)faded.GetValueOrDefault(128+i,original[i]),where+" faded "+i);}
                if(row[0]>=0)Call(r,"AdvanceScanlineVBlank");frames++;
            }
            Assert.AreEqual(0,state.State);Assert.AreEqual(0,Tasks(r).Length);cases++;
        }
        Assert.AreEqual(vectors.RootElement.GetProperty("coverage").GetProperty("caseCount").GetInt32(),cases);Assert.AreEqual(vectors.RootElement.GetProperty("coverage").GetProperty("frames").GetInt32(),frames);Assert.IsTrue(cases>=24&&frames>3000);
    }
    [TestMethod]
    [DataRow(57,false,0)][DataRow(57,false,1)][DataRow(330,false,0)][DataRow(330,false,1)]
    [DataRow(57,true,2)][DataRow(57,true,3)][DataRow(330,true,2)][DataRow(330,true,3)]
    public void CompleteRootsKeepSourceOverlayRowsAudioAndCleanup(int move,bool doubles,int actor)
    {
        using var resources=new SourceAnimationResources(Folder);var audio=new Audio();var r=New(resources.Resolve(move).Script,actor,doubles,audio);int frames=0,scanlines=0;SourceScanlineFrame? retained=null;ushort[]? original=null;
        while(r.Active&&frames++<300)
        {
            var frame=r.Tick();if(frame.Scanline is {} scan){Assert.AreEqual(SourceScanlineDestination.BlendAlpha,scan.Destination);Assert.IsFalse(scan.Word32);Assert.AreEqual(161,scan.Words.Length);scanlines++;if(retained is null){retained=scan;original=(ushort[])scan.Words.Clone();}}
            if(retained is not null)CollectionAssert.AreEqual(original!,retained.Words);
        }
        Assert.IsFalse(r.Active);Assert.IsTrue(scanlines>120);var last=r.Snapshot();Assert.IsNull(last.Overlay);Assert.IsNull(last.Scanline);Assert.AreEqual(default(PixelPoint),last.BackgroundScrollOffsets[0]);Assert.IsFalse(last.BlendEnabled);Assert.AreEqual(0,Tasks(r).Length);Assert.AreEqual(1,audio.Cues.Count);Assert.IsTrue(audio.Pans.Count>60);Assert.AreEqual(actor%2==0?63:-63,audio.Pans[^1]);
    }
    [TestMethod]
    public void CancellationStopsOnlyTheOwnedDmaAndRetainsItsNativeBanks()
    {
        using var resources=new SourceAnimationResources(Folder);
        foreach(int ticks in new[]{1,26,81,134})
        {
            var r=New(resources.Resolve(57).Script,0,false);for(int i=0;i<ticks;i++)r.Tick();var state=State(r);ushort[] bank=Enumerable.Range(0,960).Select(i=>state.Read(0,i)).ToArray();r.Cancel();Assert.AreEqual(0,state.State);Assert.IsNull(r.Snapshot().Scanline);Assert.IsNull(r.Snapshot().Overlay);CollectionAssert.AreEqual(bank,Enumerable.Range(0,960).Select(i=>state.Read(0,i)).ToArray());
        }
    }
    [TestMethod]
    public void CompletedPaletteWritesRemainInOldSnapshotWhileNextSceneOwnerLoadsFreshBank()
    {
        using var resources=new SourceAnimationResources(Folder);var scene=new SourceBattleAnimation.SpriteScene();
        var surf=new SourceBattleAnimation(resources.Resolve(57).Script,new("surf-handoff",1,57,new(0),new(1)),Bodies(false),false,new Audio(),spriteScene:scene);var initial=surf.Snapshot();
        for(int tick=0;surf.Active&&tick<300;tick++)surf.Tick();Assert.IsFalse(surf.Active);var completed=surf.Snapshot();
        Assert.AreEqual(7,completed.BackgroundPaletteEntries.Length);Assert.IsNull(completed.Overlay);Assert.IsNull(completed.Scanline);var held=new SourceCompletedBattlerState();Assert.IsFalse(held.Capture(initial,completed));Assert.IsFalse(held.HasState);
        ushort[] retained=completed.BackgroundPaletteEntries.Select(p=>p.Color).ToArray();var state=State(surf);ushort[] bank=Enumerable.Range(0,960).Select(i=>state.Read(0,i)).ToArray();
        var mud=new SourceBattleAnimation(resources.Resolve(330).Script,new("mud-handoff",2,330,new(1),new(0)),Bodies(false),false,new Audio(),spriteScene:scene);Assert.AreEqual(0,mud.Snapshot().BackgroundPaletteEntries.Length);CollectionAssert.AreEqual(bank,Enumerable.Range(0,960).Select(i=>state.Read(0,i)).ToArray());
        var first=mud.Tick();Assert.AreEqual("muddy-water-opponent",first.Overlay!.MaterialId);Assert.AreEqual(0,first.BackgroundPaletteEntries.Length);surf.Cancel();Assert.AreEqual(1,state.State);Assert.IsNotNull(mud.Tick().Scanline);CollectionAssert.AreEqual(retained,completed.BackgroundPaletteEntries.Select(p=>p.Color).ToArray());mud.Cancel();
    }
    [TestMethod]
    public void InvalidOperandsCompetingOwnersAndNativeTaskExhaustionAreRejected()
    {
        using var resources=new SourceAnimationResources(Folder);var source=resources.Resolve(57).Script;
        foreach(string priority in new[]{"-1","256"})Assert.ThrowsExactly<SourceAnimationException>(()=>New(Minimal(source,C("createvisualtask","AnimTask_CreateSurfWave",priority,"0"),C("waitforvisualfinish"),C("end")),0,false));
        foreach(string arg in new[]{"-32769","32768"})Assert.ThrowsExactly<SourceAnimationException>(()=>New(Minimal(source,C("createvisualtask","AnimTask_CreateSurfWave","2",arg),C("waitforvisualfinish"),C("end")),0,false));
        var task=C("createvisualtask","AnimTask_CreateSurfWave","2","0");
        foreach(var commands in new[]{new[]{task,task,C("waitforvisualfinish"),C("end")},new[]{task,C("setalpha","8","8"),C("waitforvisualfinish"),C("end")},new[]{task}.Concat(Enumerable.Repeat(C("panse","SE_M_SURF","SOUND_PAN_ATTACKER","SOUND_PAN_TARGET","2","0"),14)).Concat(new[]{C("waitforvisualfinish"),C("end")}).ToArray()})Assert.ThrowsExactly<SourceAnimationException>(()=>New(Minimal(source,commands),0,false));
    }
}
