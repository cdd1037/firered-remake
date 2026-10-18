using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ExtrasensoryAnimationTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private const string Distortion="AnimTask_ExtrasensoryDistortion",Clone="AnimTask_TransparentCloneGrowAndShrink";
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound>Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects()=>Stops++;public void SetPan(int pan,int frame){}}
    private sealed class Occupied(int slot,bool affine=false):SourceBattleAnimation.VisualSprite(slot,100){public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"external",default,default,0,false,false,false,256,null,100){Affine=affine};public override void Step(SourceBattleAnimation o){}}
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags)!.GetValue(o)!;
    private static T Property<T>(object o,string n)=>(T)o.GetType().GetProperty(n,Flags)!.GetValue(o)!;
    private static object? Invoke(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Flags)!.Invoke(o,a);
    private static object Nested(string n,params object[] a)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(n,BindingFlags.NonPublic)!,Flags,null,a,CultureInfo.InvariantCulture)!;
    private static SourceAnimationCommand C(string op,params string[] a)=>new(op,a,1);
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static AnimationBattler[] Bodies(bool doubles,int y=0,int elevation=0,bool hidden=false,int absent=-1)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body"+i,y,elevation,64,64){SourceVisibility=new(i!=absent,hidden,hidden)}).ToArray();
    private static int[] V(JsonElement r,string key)=>r.GetProperty(key).EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static ushort[] Terrain(SourceAnimationResources r){var b=r.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>b.PaletteColor(2,i)).ToArray();}
    [TestMethod]
    public void EveryLegalSourceFrameMatchesBothBanksCloneMatrixVisibilityAndAllocationExit()
        =>CompareNative(false);
    [TestMethod]
    public void ApprovedPeriodicBoundaryFramesAreSeparateFromOriginalLegalIndexOracle()
        =>CompareNative(true);
    private static void CompareNative(bool policy)
    {
        using var doc=SourceVectorFixture.Read("extrasensory-source-vectors.json","python tests/Reference/Extrasensory/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/extrasensory-source-vectors.json");int cases=0,rows=0;
        foreach(var c in doc.RootElement.GetProperty("cases").EnumerateArray().Where(c=>c.GetProperty("policy").GetBoolean()==policy))
        {
            int kind=c.GetProperty("kind").GetInt32(),actor=c.GetProperty("actor").GetInt32(),target=c.GetProperty("target").GetInt32(),failure=c.GetProperty("failure").GetInt32();bool doubles=c.GetProperty("doubles").GetBoolean();var bodies=Bodies(doubles,c.GetProperty("yOffset").GetInt32(),c.GetProperty("elevation").GetInt32(),c.GetProperty("hidden").GetBoolean(),c.GetProperty("absent").GetInt32());var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(Primitive(AnimationCompositionFixture.Script(326)),new("extra-native",1,326,new(actor),new(target)),bodies,doubles,new Audio(),spriteScene:scene);
            var registry=Field<object>(scene,"Sprites");if(failure==1)for(int i=0;i<28;i++)Invoke(registry,"Add",new Occupied(63-i,true));if(failure==2)for(int i=bodies.Length;i<64;i++)Invoke(registry,"Add",new Occupied(i));int retained=scene.Count;
            var state=Property<SourceScanlineState>(o,"ScanlineState");if(c.GetProperty("writeBuffer").GetInt32()==1){state.SetParams(SourceScanlineDestination.Bg1Horizontal,false);state.CaptureVBlank();state.Stop();}
            var expected=new ushort[1920];for(int b=0;b<2;b++)for(int i=0;i<960;i++){ushort value=c.GetProperty("seeded").GetBoolean()?unchecked((ushort)(b*4093+i*17)):(ushort)0;expected[b*960+i]=value;state.Write(b,i,value);}
            Invoke(o,"SetScanlineBackgroundOffset",1,new PixelPoint(65501,27));Invoke(o,"SetScanlineBackgroundOffset",2,new PixelPoint(65391,65515));var args=Field<short[]>(o,"_animationArgs");int j=0;foreach(int a in V(c,"arguments"))args[j++]=(short)a;var offsets=Field<PixelPoint[]>(o,"_offsets");for(int i=0;i<4;i++)offsets[i]=new(7,-5);
            object task=Nested(kind==0?"ExtrasensoryDistortionTask":"TransparentCloneTask",o);
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int frame=row.GetProperty("frame").GetInt32();string why=$"policy {policy}, case {cases}, kind {kind}, actor {actor}, target {target}, frame {frame}";
                if(frame>0){state.CaptureVBlank();Invoke(task,"Step",o);}
                Assert.AreEqual(row.GetProperty("alive").GetInt32()!=0,!Property<bool>(task,"Finished"),why);Assert.AreEqual(row.GetProperty("state").GetInt32(),state.State,why);Assert.AreEqual(row.GetProperty("writeBuffer").GetInt32(),state.SourceBuffer,why);
                if(state.State!=0){Assert.IsFalse(state.Word32,why);Assert.AreEqual(row.GetProperty("rank").GetInt32(),state.Destination==SourceScanlineDestination.Bg1Horizontal?1:2,why);}
                foreach(var write in row.GetProperty("writes").EnumerateArray())expected[write[0].GetInt32()]=(ushort)write[1].GetInt32();CollectionAssert.AreEqual(expected,Enumerable.Range(0,1920).Select(i=>state.Read(i/960,i%960)).ToArray(),why);CollectionAssert.AreEqual(V(row,"args").Select(v=>(short)v).ToArray(),args.Take(8).ToArray(),why);Assert.IsTrue(args.Skip(8).All(v=>v==0),why);
                if(kind==0)CollectionAssert.AreEqual(V(row,"data").Select(v=>(short)v).ToArray(),Field<short[]>(task,"_d"),why);
                else
                {
                    CollectionAssert.AreEqual(V(row,"data"),new[]{Field<int>(task,"_phase"),Field<int>(task,"_angle"),Field<int>(task,"_scale")},why);
                    var e=V(row,"copy");var copy=Field<SourceBattleAnimation.VisualSprite?>(task,"_copy");Assert.AreEqual(e.Length>0,copy is not null,why);
                    if(copy is not null)
                    {
                        Assert.AreEqual(e[0]==0,copy.Finished,why);Assert.AreEqual(retained+(copy.Finished?0:1),scene.Count,why);
                        if(e[0]!=0){var f=copy.Frame;Assert.AreEqual(e[1],f.Slot,why);Assert.AreEqual(e[2],f.BattlerCopySlot,why);Assert.AreEqual(new PixelPoint(e[3],e[4]),f.Position,why);Assert.AreEqual(new PixelPoint(e[5],e[6]),f.Offset,why);Assert.AreEqual(e[7],f.Subpriority,why);Assert.AreEqual(e[8],f.OamPriority,why);Assert.AreEqual(e[9]==0,f.Visible,why);Assert.AreEqual(e[10]==1,f.Blend,why);Assert.IsTrue(f.Affine&&f.DoubleSize&&e[11]==3,why);CollectionAssert.AreEqual(e.Skip(12).Take(4).ToArray(),new[]{f.MatrixA,f.MatrixB,f.MatrixC,f.MatrixD},why);Assert.AreEqual(-64,e[16],why);Assert.AreEqual(-64,e[17],why);Assert.IsFalse(copy.Counted,why);}
                    }
                    else Assert.AreEqual(retained,scene.Count,why);
                    uint bitmap=row.GetProperty("matrixBitmap").GetUInt32();Assert.AreEqual(failure==1?uint.MaxValue:copy is not null&&!Property<bool>(task,"Finished")?31u:15u,bitmap,why);
                }
                rows++;
            }
            o.Cancel();cases++;
        }
        Assert.IsTrue(cases>=(policy?5:23));Assert.IsTrue(rows>=(policy?135:517));
    }
    [TestMethod]
    public void CompleteRootComposesDistortionAndAffineCopyForEveryBattlePosition()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var r=resources.Resolve(326);
        foreach(bool doubles in new[]{false,true})foreach(int actor in Enumerable.Range(0,doubles?4:2))
        {
            int target=actor^1;var bodies=Bodies(doubles);var assets=r.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64 is >5 and <58&&i/64 is >5 and <58?(i%7<3?0x83e0:0x801f):0)).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,r,assets,bodies);var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(r.Script,new("extra-full",1,326,new(actor),new(target)),bodies,doubles,audio,Terrain(resources),spriteScene:scene,spriteMaterials:assets);int ticks=0,scanlineFrames=0,cloneFrames=0;bool scanlinePixels=false,clonePixels=false;
            while(o.Active&&ticks++<600)
            {
                var f=o.Tick();var pixels=composer.ComposeField(f);Assert.AreEqual(240*112*4,pixels.Length);if(f.Scanline is not null){scanlineFrames++;scanlinePixels|=!pixels.SequenceEqual(composer.ComposeField(f with{Scanline=null}));}
                if(f.Sprites.Any(s=>s.Template=="TransparentCloneGrowAndShrink")){cloneFrames++;Assert.IsTrue(f.Sprites.Where(s=>s.Template=="TransparentCloneGrowAndShrink").All(s=>s.BattlerCopySlot==actor&&s.Affine&&s.DoubleSize&&s.Blend));clonePixels|=!pixels.SequenceEqual(composer.ComposeField(f with{Sprites=f.Sprites.Where(s=>s.Template!="TransparentCloneGrowAndShrink").ToArray()}));}
                Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(o.Snapshot()));
            }
            string why=$"doubles {doubles}, actor {actor}";Assert.IsFalse(o.Active,why);Assert.IsTrue(scanlineFrames>=72,why);Assert.IsTrue(cloneFrames>0&&cloneFrames<=26,why);Assert.IsTrue(scanlinePixels,why);Assert.IsTrue(clonePixels,why);Assert.AreEqual(0,scene.Count,why);Assert.AreEqual(0,o.ActiveVisualCount,why);Assert.IsFalse(o.GraphicsLoaded,why);var end=o.Snapshot();Assert.IsNull(end.Scanline,why);Assert.IsNull(end.MoveBackground!.Id,why);Assert.AreEqual(0,end.BackgroundSlots.Length,why);Assert.IsTrue(end.BattlerVisibility.All(v=>v.Present&&!v.SpriteInvisible&&!v.LogicalInvisible),why);CollectionAssert.AreEqual(new[]{"SE_M_BIND","SE_M_BIND","SE_M_LEER"},audio.Cues.Select(c=>c.Id).ToArray(),why);o.Cancel();
        }
    }
    [TestMethod]
    public void CancellationAtEveryPhaseStopsOnlyOwnedTransferAndClone()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var r=resources.Resolve(326);
        foreach(int stop in new[]{0,1,17,38,64,90,110,125,150,180,230})
        {
            var bodies=Bodies(true);bodies[2]=bodies[2] with{SourceVisibility=new(true,false,true)};var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(r.Script,new("extra-cancel",1,326,new(0),new(1)),bodies,true,audio,Terrain(resources),spriteScene:scene,spriteMaterials:r.Assets);var initial=o.Snapshot();var state=Property<SourceScanlineState>(o,"ScanlineState");for(int i=0;i<stop&&o.Active;i++)o.Tick();var bank=Enumerable.Range(0,1920).Select(i=>state.Read(i/960,i%960)).ToArray();int sounds=audio.Cues.Count;o.Cancel();o.Cancel();Assert.AreEqual(0,state.State);CollectionAssert.AreEqual(bank,Enumerable.Range(0,1920).Select(i=>state.Read(i/960,i%960)).ToArray());Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,o.ActiveVisualCount);Assert.IsNull(o.Snapshot().Scanline);Assert.IsNull(o.Snapshot().MoveBackground!.Id);Assert.AreEqual(1,audio.Stops);Assert.AreEqual(sounds,audio.Cues.Count);Assert.AreEqual(JsonSerializer.Serialize(initial.BattlerVisibility),JsonSerializer.Serialize(o.Snapshot().BattlerVisibility));
        }
    }
    [TestMethod]
    public void CompetingMalformedAndOverCapacityOwnersRejectBeforeAudioOrAllocation()
    {
        var source=AnimationCompositionFixture.Script(326);var primitive=Primitive(source);
        void Reject(SourceAnimationScript s,int retained=0,bool affine=false){var scene=new SourceBattleAnimation.SpriteScene();var registry=Field<object>(scene,"Sprites");for(int i=0;i<retained;i++)Invoke(registry,"Add",new Occupied(63-i,affine));var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,new("extra-bad",1,326,new(0),new(1)),Bodies(true),true,audio,new ushort[16],spriteScene:scene));Assert.AreEqual(0,audio.Cues.Count);Assert.AreEqual(retained,scene.Count);}
        foreach(var (name,value) in new[]{(Distortion,"-1"),(Distortion,"3"),(Clone,"-1"),(Clone,"4")})Reject(primitive with{Commands=[C("createvisualtask",name,"5",value),C("end")]});
        Reject(primitive with{Commands=[C("createvisualtask",Distortion,"5","0"),C("createvisualtask",Distortion,"5","1"),C("end")]});
        Reject(primitive with{Commands=[C("createvisualtask",Clone,"5","0"),C("waitforvisualfinish"),C("createvisualtask",Clone,"5","0"),C("end")]});
        Reject(source,42);Reject(source,28,true);
        Reject(primitive with{Commands=[..Enumerable.Range(0,16).SelectMany(_=>new[]{C("createvisualtask",Distortion,"5","0"),C("waitforvisualfinish")}),C("end")]});
        Reject(primitive with{Commands=[C("createvisualtask",Distortion,"5","0"),C("createvisualtask","AnimTask_ShakeMon","5","0","0","1","2","1"),C("end")]});
        Reject(source with{SineTable=source.SineTable.Take(319).ToArray()});
    }
}
