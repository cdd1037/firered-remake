using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SpiteAnimationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound>Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects()=>Stops++;}
    private sealed class Occupied(int slot):SourceBattleAnimation.VisualSprite(slot,100)
    {public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"foreign",default,default,0,false,false,false,256,null,100);public override void Step(SourceBattleAnimation o){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static object? Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private|BindingFlags.Public)!.Invoke(o,args);
    private static int I(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static CommittedMoveAnimation Cue(int target)=>new("spite",1,180,new(target^1),new(target));
    private static AnimationBattler[] Battlers(bool doubles,int target=-1,int y=48,bool logical=false,bool invisible=false)=>Enumerable.Range(0,doubles?4:2)
        .Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,48){DisplayCenter=i==target?new AnimationBattler(new(i),"",0,0,64,48).Base(doubles) with{Y=y}:null,SourceVisibility=new(true,i==target&&logical,i==target&&invisible)}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static Dictionary<string,SourcePixelAsset> Assets(SourceResolvedAnimation r,AnimationBattler[] bodies)
    {
        var assets=r.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());
        foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64>12&&i%64<51&&i/64>8&&i/64<55?0x8000|((b.Slot.Value*503+(i%15+1)*1703)&32767):0)).ToArray());
        return assets;
    }
    private static void Create(SourceBattleAnimation o)
    {var command=C("createvisualtask","AnimTask_SpiteTargetShadow","2");Assert.IsTrue((bool)Invoke(o,"TryCreateSpiteTask",command)!);Invoke(o,"AssignTaskOrder",0,0,command);}
    private static void Step(SourceBattleAnimation o)
    {Invoke(o,"AdvanceSourceSprites");Invoke(o,"SortSourceSprites",null!,false,null!);Invoke(o,"RunSourceTasks");Invoke(o,"AdvanceScanlineVBlank");}

    [TestMethod]
    public void ExtractedSpiteAndWaveCallbacksMatchEveryFrameEveryRetainedWordAndDeferredDestruction()
    {
        using var vectors=SourceVectorFixture.Read("spite-source-vectors.json","python tests/Reference/Spite/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/spite-source-vectors.json");
        Assert.AreEqual("firered-spite-wave-v1",vectors.RootElement.GetProperty("schema").GetString());
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(180);int cases=0,rows=0;
        foreach(var c in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {
            bool doubles=c.GetProperty("doubles").GetBoolean();int target=I(c,"target"),failure=I(c,"paletteFailure");
            var bodies=Battlers(doubles,target,I(c,"y"),c.GetProperty("logical").GetBoolean(),c.GetProperty("spriteInvisible").GetBoolean());var assets=Assets(resolved,bodies);var scene=new SourceBattleAnimation.SpriteScene();
            var o=new SourceBattleAnimation(Primitive(resolved.Script),Cue(target),bodies,doubles,new Audio(),spriteScene:scene,spriteMaterials:assets);
            int first=doubles?4:2;for(int slot=first+I(c,"free");slot<64;slot++)scene.Sprites.Add(new Occupied(slot){Owner=o});
            Field<PixelPoint[]>(o,"_offsets")[target]=new(3,-2);int rank=bodies[target].BackgroundRank;
            Invoke(o,"SetScanlineBackgroundOffset",rank,new PixelPoint(32-bodies[target].Base(doubles).X-3,0));
            if(c.GetProperty("affine").GetBoolean())
            {Field<bool[]>(o,"_affineMode")[target]=true;Field<int[]>(o,"_xScale")[target]=192;Field<int[]>(o,"_matrixB")[target]=-32;Field<int[]>(o,"_matrixC")[target]=24;Field<int[]>(o,"_yScale")[target]=320;}
            if(failure!=0)for(int bank=4;bank<(failure==15?15:16);bank++)Field<int[]>(o,"_spritePaletteTags")[bank]=12000+bank;
            Create(o);
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int frame=I(row,"frame");if(frame>0)Step(o);string why=c.GetProperty("id").GetString()+" frame "+frame;var snapshot=o.Snapshot();var state=scene.Scanlines;
                var tasks=Field<IEnumerable>(o,"_tasks").Cast<object>().ToArray();var wave=tasks.SingleOrDefault(t=>t.GetType().Name=="BackgroundScanlineWaveTask");
                Assert.AreEqual(I(row,"alive"),o.ActiveVisualCount,why+" counted visual owner");Assert.AreEqual(I(row,"waveAlive")!=0,wave is not null,why+" separate wave owner");
                if(wave is not null){Assert.AreEqual(0,(int)wave.GetType().GetProperty("Priority")!.GetValue(wave)!);Assert.IsFalse((bool)wave.GetType().GetProperty("Counted")!.GetValue(wave)!);Assert.AreEqual(I(row,"waveOffset"),Field<int>(wave,"_offset"));}
                Assert.AreEqual(I(row,"state"),state.State,why);Assert.AreEqual(I(row,"sourceBuffer"),state.SourceBuffer,why);
                for(int bank=0;bank<2;bank++)CollectionAssert.AreEqual(N(row.GetProperty("buffers")[bank]),Enumerable.Range(0,576).Select(i=>(int)state.Read(bank,i)).ToArray(),why+" bank "+bank);
                for(int bank=0;bank<2;bank++)for(int i=576;i<SourceScanlineState.BankWords;i++)Assert.AreEqual((ushort)0,state.Read(bank,i),why+" untouched bank tail");
                var raster=row.GetProperty("raster");if(raster.ValueKind==JsonValueKind.Null)Assert.IsNull(snapshot.Scanline,why);else{Assert.IsNotNull(snapshot.Scanline);Assert.AreEqual(rank==1?SourceScanlineDestination.Bg1Horizontal:SourceScanlineDestination.Bg2Horizontal,snapshot.Scanline.Destination);CollectionAssert.AreEqual(N(raster),snapshot.Scanline.Words.Select(x=>(int)x).ToArray(),why+" every visible and terminal register");}
                CollectionAssert.AreEqual(N(row.GetProperty("alpha")),new[]{snapshot.AlphaA,snapshot.AlphaB},why);Assert.AreEqual(I(row,"blend")!=0,snapshot.BlendEnabled,why);Assert.AreEqual(I(row,"bgEnabled")!=0,snapshot.BattlerBackgroundEnabled[rank-1],why);
                Assert.AreEqual(I(row,"originalInvisible")!=0,snapshot.BattlerVisibility.Single(v=>v.Slot==target).SpriteInvisible,why);
                Assert.AreEqual(I(row,"paletteLive")!=0,snapshot.SpritePalettes.ContainsKey(resolved.Script.SpriteTags["ANIM_TAG_BENT_SPOON"]),why);
                var targetBlend=snapshot.PaletteBlends.SingleOrDefault(p=>p.Palette==16+target);for(int k=0;k<16;k++)Assert.AreEqual(row.GetProperty("targetPalette")[k].GetInt32(),(int)(SourcePaletteMath.Apply((ushort)((target*503+k*1703)&32767),targetBlend,false)&32767),why+" target RGB555");
                var copies=scene.Sprites.Where(s=>s is not Occupied).ToArray();var copy=row.GetProperty("copy");Assert.AreEqual(copy.ValueKind==JsonValueKind.Null?0:1,copies.Length,why);
                if(copies.Length==1)
                {
                    var s=copies[0].Frame;CollectionAssert.AreEqual(N(copy.GetProperty("position")),new[]{s.Position.X,s.Position.Y,s.Offset.X,s.Offset.Y},why);Assert.AreEqual(I(copy,"visible")!=0,s.Visible,why);Assert.AreEqual(I(copy,"priority"),s.OamPriority);Assert.AreEqual(I(copy,"palette"),s.PaletteSlot);Assert.IsFalse(s.Blend);Assert.AreEqual(I(copy,"affine")!=0,s.Affine);Assert.AreEqual(target,s.BattlerCopySlot);Assert.IsFalse(copies[0].Counted);Assert.IsNotNull(s.Material);Assert.IsNotNull(s.BattlerCopyPalette);
                    if(frame>0)for(int k=0;k<16;k++)Assert.AreEqual(row.GetProperty("copyPalette")[k].GetInt32(),(int)(SourcePaletteMath.Apply((ushort)((target*503+k*1703)&32767),s.BattlerCopyPalette,false)&32767),why+" detached normal→faded copy");
                }
                Assert.AreEqual(I(row,"oamCopy"),snapshot.Sprites.Count(s=>s.Template=="SpiteTargetShadow"),why+" pre-task OAM destruction delay");Assert.IsNull(Field<object?>(o,"_visualRandom"));rows++;
            }
            o.Cancel();Assert.AreEqual(0,scene.Count);Assert.IsNull(o.Snapshot().Scanline);cases++;
        }
        Assert.AreEqual(10,cases);Assert.AreEqual(955,rows);
    }
    [TestMethod]
    public void WaveInitializationHasNoBgOffsetAndLaterFillsUseLiveSharedRegistersAndDelay()
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(180);var bodies=Battlers(false);var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(Primitive(r.Script),Cue(1),bodies,false,new Audio(),spriteScene:scene,spriteMaterials:Assets(r,bodies));
        Invoke(o,"SetScanlineBackgroundOffset",1,new PixelPoint(613,0));Invoke(o,"StartBackgroundScanlineWave",1,0,64,2,6,1);var state=scene.Scanlines;
        for(int bank=0;bank<2;bank++)for(int row=0;row<64;row++)Assert.AreEqual(unchecked((ushort)(r.Script.SineTable[(row*2)&255]*6/256)),state.Read(bank,row));
        Invoke(o,"AdvanceScanlineVBlank");Assert.AreEqual(1,state.SourceBuffer);var first=o.Snapshot().Scanline!;Assert.AreEqual((ushort)0,first.Words[0]);
        for(int turn=0;turn<5;turn++)
        {
            int offset=turn/2;Invoke(o,"SetScanlineBackgroundOffset",1,new PixelPoint(613+turn*17,0));Invoke(o,"RunSourceTasks");int bank=state.SourceBuffer;
            for(int row=0;row<64;row++)Assert.AreEqual(unchecked((ushort)(r.Script.SineTable[((row+offset)*2)&255]*6/256+613+turn*17)),state.Read(bank,row));
            Invoke(o,"AdvanceScanlineVBlank");
        }
        Invoke(o,"StopScanlineDeferred");Assert.AreEqual(3,state.State);Assert.AreEqual(1,Field<IEnumerable>(o,"_tasks").Cast<object>().Count());Invoke(o,"AdvanceScanlineVBlank");Assert.AreEqual(0,state.State);Assert.AreEqual(1,Field<IEnumerable>(o,"_tasks").Cast<object>().Count());Invoke(o,"RunSourceTasks");Assert.AreEqual(0,Field<IEnumerable>(o,"_tasks").Cast<object>().Count());o.Cancel();
        foreach(var bad in new[]{new[]{0,0,64,2,6,0},new[]{1,0,64,0,6,0},new[]{1,0,64,1,6,0},new[]{1,192,256,2,6,0},new[]{1,0,64,2,256,0},new[]{1,0,64,2,6,256}})
        {var x=new SourceBattleAnimation(Primitive(r.Script),Cue(1),bodies,false,new Audio(),spriteMaterials:Assets(r,bodies));var error=Assert.Throws<TargetInvocationException>(()=>Invoke(x,"StartBackgroundScanlineWave",bad.Cast<object>().ToArray()));Assert.IsInstanceOfType<InvalidDataException>(error.InnerException);x.Cancel();}
    }
    [TestMethod]
    public void CloneOwnsLogicalVisibilityAffineAndNormalColorsThroughSourceFadeAndLaterBodyMutation()
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(180);var bodies=Battlers(true,3,40,false,true);var assets=Assets(r,bodies);var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(Primitive(r.Script),Cue(3),bodies,true,new Audio(),spriteScene:scene,spriteMaterials:assets);
        var normal=new SourcePaletteState(new(19,6,0x7fff),true,false);Field<Dictionary<int,SourcePaletteState>>(o,"_unfadedPaletteStates")[19]=normal;
        Field<bool[]>(o,"_affineMode")[3]=true;Field<int[]>(o,"_xScale")[3]=192;Field<int[]>(o,"_matrixB")[3]=-32;Field<int[]>(o,"_matrixC")[3]=24;Field<int[]>(o,"_yScale")[3]=320;
        Create(o);Step(o);var frame=scene.Sprites.Single().Frame;Assert.IsTrue(frame.Visible&&frame.Affine&&frame.DoubleSize);Assert.IsFalse(frame.Blend);CollectionAssert.AreEqual(new[]{192,-32,24,320},new[]{frame.MatrixA,frame.MatrixB,frame.MatrixC,frame.MatrixD});Assert.AreEqual(normal,frame.BattlerCopyPalette!.Unfaded);
        var pixels=(ushort[])frame.Material!.Pixels.Clone();Field<Dictionary<int,SourcePaletteState>>(o,"_unfadedPaletteStates")[19]=new(new(19,16,0),false,false);Array.Fill(assets["b3"].Pixels,(ushort)0xffff);Array.Fill(Field<ushort[][]>(o,"_spritePaletteBanks")[4],(ushort)31);
        CollectionAssert.AreEqual(pixels,scene.Sprites.Single().Frame.Material!.Pixels);Assert.AreEqual(normal,scene.Sprites.Single().Frame.BattlerCopyPalette!.Unfaded);o.Cancel();o.Cancel();Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,o.ActiveVisualCount);
    }
    [TestMethod]
    public void CompleteRootComposesBothRanksRestoresAndCancelsEveryPhase()
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(180);Assert.AreEqual("Move_SPITE",r.Script.Label);
        foreach(bool doubles in new[]{false,true})for(int target=0;target<(doubles?4:2);target++)
        {
            var bodies=Battlers(doubles);var assets=Assets(r,bodies);var composer=AnimationCompositionFixture.Create(resources,r,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var o=new SourceBattleAnimation(r.Script,Cue(target),bodies,doubles,audio,spriteScene:scene,spriteMaterials:assets);
            int ticks=0;bool wave=false,copy=false,pixels=false,disabled=false;
            while(o.Active&&ticks++<400)
            {
                var f=o.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(o.Snapshot()));wave|=f.Scanline is not null;copy|=f.Sprites.Any(s=>s.Template=="SpiteTargetShadow");disabled|=!f.BattlerBackgroundEnabled[bodies[target].BackgroundRank-1];
                var composed=composer.ComposeField(f);if(f.Scanline is not null&&f.BlendEnabled)pixels|=!composed.SequenceEqual(composer.ComposeField(f with{Scanline=null}));
            }
            Assert.IsFalse(o.Active);Assert.IsTrue(wave&&copy&&pixels&&disabled);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,o.ActiveVisualCount);Assert.AreEqual(0,o.Snapshot().SpritePalettes.Count);Assert.IsNull(o.Snapshot().Scanline);Assert.IsFalse(o.Snapshot().BlendEnabled);Assert.AreEqual(0,o.Snapshot().BackgroundSlots.Length);Assert.IsTrue(o.Snapshot().BattlerBackgroundEnabled.All(x=>x));Assert.IsTrue(o.Snapshot().BattlerVisibility.All(v=>!v.SpriteInvisible));Assert.AreEqual(4,audio.Cues.Count);Assert.IsFalse(o.GraphicsLoaded);
            foreach(int cancel in new[]{0,1,16,32,34,35,36,37,38,39,80,165,166,167,168,190})
            {var s=new SourceBattleAnimation.SpriteScene();var a=new Audio();var x=new SourceBattleAnimation(r.Script,Cue(target),bodies,doubles,a,spriteScene:s,spriteMaterials:assets);for(int i=0;i<cancel&&x.Active;i++)x.Tick();x.Cancel();x.Cancel();Assert.AreEqual(0,s.Count);Assert.AreEqual(0,s.Scanlines.State);Assert.IsNull(x.Snapshot().Scanline);Assert.AreEqual(0,x.ActiveVisualCount);Assert.AreEqual(0,x.Snapshot().SpritePalettes.Count);Assert.IsFalse(x.Snapshot().BlendEnabled);Assert.AreEqual(1,a.Stops);}
        }
    }
    [TestMethod]
    public void MalformedCallbackClosureMissingMaterialAndSourcePaletteLeakBoundaryFailBeforeEffects()
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(180);var script=r.Script;var bodies=Battlers(true);var assets=Assets(r,bodies);
        foreach(var bad in new[]{script with{SineTable=[]},script with{SpriteTags=new Dictionary<string,int>()},script with{Commands=[C("createvisualtask","AnimTask_SpiteTargetShadow","2","0"),C("end")]},script with{Commands=[C("createvisualtask","AnimTask_SpiteTargetShadow","2"),..script.Commands]},script with{Commands=[C("createvisualtask","AnimTask_NightShadeClone","2","1"),..script.Commands]}})
        {var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(bad,Cue(1),bodies,true,audio,spriteScene:scene,spriteMaterials:assets));Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,audio.Cues.Count);}
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(1),bodies,true,new Audio()));
        foreach(int limit in new[]{15,16})
        {var o=new SourceBattleAnimation(script,Cue(1),bodies,true,new Audio(),spriteMaterials:assets);for(int bank=4;bank<limit;bank++)Field<int[]>(o,"_spritePaletteTags")[bank]=12000+bank;typeof(SourceBattleAnimation).GetField("_validatingInvokedProgram",Private)!.SetValue(o,true);var error=Assert.Throws<TargetInvocationException>(()=>Invoke(o,"ValidateSpiteCommand",C("createvisualtask","AnimTask_SpiteTargetShadow","2")));Assert.IsInstanceOfType<SourceAnimationException>(error.InnerException);Assert.AreEqual(0,o.ActiveVisualCount);o.Cancel();}
    }
}
