using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class PunchTraceAnimationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound>Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects()=>Stops++;public void SetPan(int pan,int frame){}}
    private sealed class Occupied(int slot):SourceBattleAnimation.VisualSprite(slot,100)
    {public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"foreign",default,default,0,false,false,false,256,null,100);public override void Step(SourceBattleAnimation o){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static object? Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private|BindingFlags.Public)!.Invoke(o,args);
    private static int I(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static CommittedMoveAnimation Cue(int move,int actor,int target)=>new("punch-trace",1,move,new(actor),new(target));
    private static AnimationBattler[] Battlers(bool doubles,int hidden=-1)=>Enumerable.Range(0,doubles?4:2)
        .Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,48){SourceVisibility=new(true,false,i==hidden)}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript script)=>script with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static Dictionary<string,SourcePixelAsset> Assets(SourceResolvedAnimation r,AnimationBattler[] bodies)
    {
        var assets=r.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());assets.TryAdd("impact",new(32,32,new ushort[1024]));
        foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64>12&&i%64<51&&i/64>8&&i/64<55?0x8000|((b.Slot.Value*503+(i%15+1)*1703)&32767):0)).ToArray());
        return assets;
    }
    [TestMethod]
    public void UnmodifiedNativeStepAndCloneCallbacksMatchAtTheExplicitlyApprovedInitializationBoundary()
    {
        using var vectors=SourceVectorFixture.Read("punch-trace-source-vectors.json","python tests/Reference/PunchTrace/generate.py content/raw/pokefirered/037335f4 --repo . --output tests/Game.Presentation.Tests/Fixtures/punch-trace-source-vectors.json");
        Assert.AreEqual("firered-punch-trace-policy-v1",vectors.RootElement.GetProperty("schema").GetString());
        using var resources=new SourceAnimationResources(Folder);int cases=0,rows=0;
        foreach(var c in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {
            bool doubles=c.GetProperty("doubles").GetBoolean();int actor=I(c,"actor"),free=I(c,"free"),style=I(c,"style"),move=style==0?183:325;
            var resolved=resources.Resolve(move);var bodies=Battlers(doubles,c.GetProperty("hidden").GetBoolean()?actor:-1);var assets=Assets(resolved,bodies);var scene=new SourceBattleAnimation.SpriteScene();
            var o=new SourceBattleAnimation(Primitive(resolved.Script),Cue(move,actor,actor^1),bodies,doubles,new Audio(),spriteScene:scene,spriteMaterials:assets);
            int first=doubles?4:2;for(int slot=first+free;slot<64;slot++)scene.Sprites.Add(new Occupied(slot){Owner=o});
            Field<PixelPoint[]>(o,"_offsets")[actor]=new(I(c,"initialX"),-2);Field<PixelPoint[]>(o,"_battlerPositionOffsets")[actor]=new(7,-3);
            var args=Field<short[]>(o,"_animationArgs");args[0]=(short)(style==0?8|(9<<5)|(28<<10):0);args[1]=(short)(style==0?10:13);var before=(short[])args.Clone();
            Assert.IsTrue((bool)Invoke(o,"TryCreatePunchTraceTask",C("createvisualtask","AnimTask_AttackerPunchWithTrace","2"))!);
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int frame=I(row,"frame");if(frame>0){Invoke(o,"AdvanceSourceSprites");Invoke(o,"SortSourceSprites",null!,false,null!);Invoke(o,"RunSourceTasks");}
                string why=c.GetProperty("id").GetString()+" frame "+frame;var tasks=Field<IEnumerable>(o,"_tasks").Cast<object>().ToArray();
                Assert.AreEqual(I(row,"alive"),tasks.Length,why+" counted task lifetime");CollectionAssert.AreEqual(N(row.GetProperty("offset")),new[]{Field<PixelPoint[]>(o,"_offsets")[actor].X,Field<PixelPoint[]>(o,"_offsets")[actor].Y},why+" signed16 motion");
                var copies=scene.Sprites.Where(s=>s is not Occupied).OrderBy(s=>s.Slot).ToArray();var native=row.GetProperty("copies").EnumerateArray().ToArray();Assert.AreEqual(native.Length,copies.Length,why+" raw allocation and retirement");
                if(tasks.Length>0){Assert.AreEqual(row.GetProperty("task")[5].GetInt32(),tasks[0].GetType().GetField("LiveCopies")!.GetValue(tasks[0]));Assert.AreEqual(native.Length,row.GetProperty("task")[5].GetInt32());}
                for(int i=0;i<copies.Length;i++)
                {
                    var s=copies[i].Frame;var n=native[i];Assert.AreEqual(I(n,"rank"),s.Slot-first,why+" ascending cell rank including reuse");
                    CollectionAssert.AreEqual(N(n.GetProperty("position")),new[]{s.Position.X,s.Position.Y,s.Offset.X,s.Offset.Y},why+" captured actual coordinates");Assert.AreEqual(I(n,"life"),Field<int>(copies[i],"_remaining"));
                    Assert.AreEqual(I(n,"priority"),s.OamPriority);Assert.AreEqual(I(n,"subpriority"),s.Subpriority);Assert.AreEqual(I(n,"visible")!=0,s.Visible);Assert.AreEqual(I(n,"mode")!=0,s.Blend);Assert.AreEqual(4,s.PaletteSlot);Assert.AreEqual(actor,s.BattlerCopySlot);Assert.IsFalse(copies[i].Counted);Assert.IsNotNull(s.Material);
                    for(int p=1;p<16;p++)Assert.AreEqual(row.GetProperty("palette")[p].GetInt32(),(int)(SourcePaletteMath.Apply((ushort)(0x8000|((actor*503+p*1703)&32767)),s.BattlerCopyPalette,false)&32767),why+" own normal palette tint");
                }
                Assert.AreEqual(native.Count(n=>I(n,"life")<8),o.Snapshot().Sprites.Count(s=>s.Template=="AnimBattlerTrace"),why+" pre-task OAM capture defers newborn clones");
                Assert.AreEqual(I(row,"paletteLive")!=0,o.Snapshot().SpritePalettes.ContainsKey(resolved.Script.SpriteTags["ANIM_TAG_BENT_SPOON"]));
                CollectionAssert.AreEqual(before,args);Assert.IsNull(Field<object?>(o,"_visualRandom"));rows++;
            }
            o.Cancel();cases++;
        }
        Assert.AreEqual(10,cases);Assert.AreEqual(173,rows);
    }
    [TestMethod]
    public void ApprovedPolicyIsExplicitlyDifferentFromHistoricalSpriteIdAndStalePaletteInitialization()
    {
        using var vectors=SourceVectorFixture.Read("punch-trace-source-vectors.json","python tests/Reference/PunchTrace/generate.py content/raw/pokefirered/037335f4 --repo . --output tests/Game.Presentation.Tests/Fixtures/punch-trace-source-vectors.json");
        foreach(var c in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {
            var row=c.GetProperty("rows")[0];int intended=I(c,"initialX");
            Assert.AreEqual(unchecked((short)(intended-I(row,"sourceId"))),I(row,"nativeInitialX"));Assert.AreNotEqual(I(row,"nativeInitialX"),row.GetProperty("offset")[0].GetInt32());
            Assert.AreEqual(intended,row.GetProperty("offset")[0].GetInt32());Assert.IsFalse(N(row.GetProperty("nativePalette")).SequenceEqual(N(row.GetProperty("palette"))));
        }
    }
    [TestMethod]
    public void DetachedActorNormalMaterialAndAffineSnapshotSurviveLaterFadesPaletteSlotReuseAndBodyMutation()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(325);var bodies=Battlers(true,0);var assets=Assets(resolved,bodies);var scene=new SourceBattleAnimation.SpriteScene();
        var o=new SourceBattleAnimation(Primitive(resolved.Script),Cue(325,0,1),bodies,true,new Audio(),spriteScene:scene,spriteMaterials:assets);
        // A legitimate prior faded-to-normal copy is a sequential RGB555 prefix.
        var normal=new SourcePaletteState(new(16,6,0x7fff),true,false);Field<Dictionary<int,SourcePaletteState>>(o,"_unfadedPaletteStates")[16]=normal;
        Field<bool[]>(o,"_affineMode")[0]=true;Field<int[]>(o,"_xScale")[0]=192;Field<int[]>(o,"_matrixB")[0]=-32;Field<int[]>(o,"_matrixC")[0]=24;Field<int[]>(o,"_yScale")[0]=320;
        Field<PixelPoint[]>(o,"_offsets")[0]=new(11,-4);var args=Field<short[]>(o,"_animationArgs");args[0]=0;args[1]=13;
        // Freed-bank data must not affect the approved actor-owned colors.
        Field<ushort[][]>(o,"_spritePaletteUnfadedBanks")[4]=Enumerable.Repeat((ushort)0x7c00,16).ToArray();
        Invoke(o,"TryCreatePunchTraceTask",C("createvisualtask","AnimTask_AttackerPunchWithTrace","2"));Invoke(o,"RunSourceTasks");
        var copy=scene.Sprites.Single();var frame=copy.Frame;Assert.AreEqual(new PixelPoint(11,-4),frame.Offset);Assert.IsTrue(frame.Affine&&frame.DoubleSize);CollectionAssert.AreEqual(new[]{192,-32,24,320},new[]{frame.MatrixA,frame.MatrixB,frame.MatrixC,frame.MatrixD});
        Assert.IsNotNull(frame.Material);var pixels=(ushort[])frame.Material.Pixels.Clone();var transform=frame.BattlerCopyPalette;Assert.IsNotNull(transform);Assert.AreEqual(normal,transform.Unfaded);
        Field<Dictionary<int,SourcePaletteState>>(o,"_unfadedPaletteStates")[16]=new(new(16,16,0),false,false);Field<Dictionary<int,SourcePaletteBlend>>(o,"_paletteBlends")[16]=new(16,16,0x7fff);Array.Fill(assets["b0"].Pixels,(ushort)0xffff);
        Invoke(o,"FreeSpritePalette",resolved.Script.SpriteTags["ANIM_TAG_BENT_SPOON"]);Assert.AreEqual(4,Invoke(o,"AllocateSpritePalette",4321));Field<ushort[][]>(o,"_spritePaletteBanks")[4]=Enumerable.Repeat((ushort)31,16).ToArray();
        CollectionAssert.AreEqual(pixels,copy.Frame.Material!.Pixels);Assert.AreEqual(transform,copy.Frame.BattlerCopyPalette);
        foreach(ushort p in pixels.Where(p=>(p&0x8000)!=0).Distinct())Assert.AreEqual(SourcePaletteMath.Blend(SourcePaletteMath.Apply(p,normal.Blend,normal.Inverted,normal.Greyscale),13,0),SourcePaletteMath.Apply(p,copy.Frame.BattlerCopyPalette,false));
        o.Cancel();o.Cancel();Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,o.ActiveVisualCount);Assert.AreEqual(0,o.Snapshot().SpritePalettes.Count);
    }
    [TestMethod][DataRow(183)][DataRow(325)]
    public void CompleteRootsComposeAllPositionsWithSourceBackgroundTimingAndCancelCleanly(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);Assert.AreEqual(move==183?"Move_MACH_PUNCH":"Move_SHADOW_PUNCH",resolved.Script.Label);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var bodies=Battlers(doubles);var assets=Assets(resolved,bodies);var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var o=new SourceBattleAnimation(resolved.Script,Cue(move,actor,actor^1),bodies,doubles,audio,spriteScene:scene,spriteMaterials:assets);
            int frames=0;bool copies=false,pixels=false,background=false,alpha=false,hidden=false;
            while(o.Active&&frames++<500)
            {
                var f=o.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(o.Snapshot()));background|=f.MoveBackground is not null;alpha|=f.BlendEnabled;hidden|=f.BackgroundSlots.Contains(actor);
                var trace=f.Sprites.Where(s=>s.Template=="AnimBattlerTrace").ToArray();copies|=trace.Length>0;var composed=composer.ComposeField(f);
                if(trace.Length>0){pixels|=!composed.SequenceEqual(composer.ComposeField(f with{Sprites=f.Sprites.Where(s=>s.Template!="AnimBattlerTrace").ToArray()}));foreach(var s in trace){Assert.IsNotNull(s.Material);Assert.IsNotNull(s.BattlerCopyPalette);}}
            }
            Assert.IsFalse(o.Active);Assert.IsTrue(copies&&pixels&&background&&alpha&&hidden);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,o.ActiveVisualCount);Assert.AreEqual(0,o.Snapshot().SpritePalettes.Count);Assert.IsFalse(o.Snapshot().BlendEnabled);Assert.AreEqual(0,o.Snapshot().BackgroundSlots.Length);Assert.IsFalse(o.GraphicsLoaded);Assert.IsFalse(o.HasResidualSprites);Assert.IsTrue(o.Snapshot().ObjectOffsets.All(p=>p==default));Assert.AreEqual(2,audio.Cues.Count);Assert.IsNull(Field<object?>(o,"_visualRandom"));
            foreach(int cancel in new[]{0,1,16,32,35,40,46,54,80,110})
            {var s=new SourceBattleAnimation.SpriteScene();var a=new Audio();var r=new SourceBattleAnimation(resolved.Script,Cue(move,actor,actor^1),bodies,doubles,a,spriteScene:s,spriteMaterials:assets);for(int i=0;i<cancel&&r.Active;i++)r.Tick();r.Cancel();r.Cancel();Assert.AreEqual(0,s.Count);Assert.AreEqual(0,r.ActiveVisualCount);Assert.AreEqual(0,r.Snapshot().SpritePalettes.Count);Assert.IsFalse(r.Snapshot().BlendEnabled);Assert.AreEqual(0,r.Snapshot().BackgroundSlots.Length);Assert.AreEqual(1,a.Stops);}
        }
    }
    [TestMethod]
    public void MalformedColorCoefficientOwnerAndUnreviewedMutationFailBeforeEffects()
    {
        using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(325).Script;
        var bads=new[]{script with{SpriteTags=script.SpriteTags.Where(x=>x.Key!="ANIM_TAG_BENT_SPOON").ToDictionary()},script with{Commands=[C("createvisualtask","AnimTask_AttackerPunchWithTrace","2","0","17"),C("end")]},script with{Commands=[C("createvisualtask","AnimTask_AttackerPunchWithTrace","2","32768","13"),C("end")]},script with{Commands=[C("createvisualtask","AnimTask_AttackerPunchWithTrace","2","0","13"),..script.Commands]},script with{Commands=[C("createvisualtask","AnimTask_NightShadeClone","2","1"),..script.Commands]}};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(325,0,1),Battlers(true),true,new Audio()));
        foreach(var bad in bads){var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var bodies=Battlers(true);var assets=Assets(resources.Resolve(325),bodies);Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(bad,Cue(325,0,1),bodies,true,audio,spriteScene:scene,spriteMaterials:assets));Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,audio.Cues.Count);}
    }
}
