using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class BattlerCopyAnimationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio{public List<SourceAnimationSound>Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void SetPan(int pan,int frame){}public void StopAnimationEffects()=>Stops++;}
    private sealed class Occupied(int slot):SourceBattleAnimation.VisualSprite(slot,100){public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"foreign",default,default,0,false,false,false,256,null,100);public override void Step(SourceBattleAnimation o){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("battler-copy-source-vectors.json", "python tests/Reference/BattlerCopy/generate.py content/raw/pokefirered/037335f4 --repo . --output tests/Game.Presentation.Tests/Fixtures/battler-copy-source-vectors.json");
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static object? Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private|BindingFlags.Public)!.Invoke(o,args);
    private static int I(JsonElement e,string key)=>e.GetProperty(key).GetInt32();private static int[] N(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static CommittedMoveAnimation Cue(int move,int actor,int target)=>new("battler-copy",1,move,new(actor),new(target));
    private static AnimationBattler[] Battlers(bool doubles,int hidden=-1)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,48){SourceVisibility=new(true,false,i==hidden)}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static object Nested(string name,params object[] args)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(name,BindingFlags.NonPublic)!,BindingFlags.Public|Private,null,args,null)!;
    [TestMethod]
    public void NativeTasksMatchAffineOffsetsBlendPlaneRawCapacityPaletteLifetimeAndSignals()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0,rows=0;
        foreach(var c in vectors.RootElement.GetProperty("cases").EnumerateArray().Where(c=>I(c,"kind")<3))
        {
            int kind=I(c,"kind"),actor=I(c,"actor"),target=I(c,"target"),move=kind==0?101:kind==1?102:104;bool doubles=c.GetProperty("doubles").GetBoolean();var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var full=resources.Resolve(move).Script;var o=new SourceBattleAnimation(Primitive(full),Cue(move,actor,target),Battlers(doubles,c.GetProperty("hidden").GetBoolean()?target:-1),doubles,audio,spriteScene:scene);var args=Field<short[]>(o,"_animationArgs");for(int i=0;i<4;i++)Field<PixelPoint[]>(o,"_offsets")[i]=new(3,-2);for(int i=8;i<args.Length;i++)args[i]=(short)(0x5a00+i);var extension=args.Skip(8).ToArray();
            if(kind==0)args[0]=(short)I(c,"hold");if(kind==1){args[0]=(short)I(c,"speed");args[1]=(short)I(c,"duration");}if(kind==2)for(int slot=4+I(c,"free");slot<64;slot++)scene.Sprites.Add(new Occupied(slot){Owner=o});
            string callback=kind==0?"AnimTask_NightShadeClone":kind==1?"AnimTask_ShrinkTargetCopy":"AnimTask_DoubleTeam";Assert.IsTrue((bool)Invoke(o,"TryCreateBattlerCopyTask",C("createvisualtask",callback,"5"))!);var copies=scene.Sprites.Where(s=>s is not Occupied).OrderBy(s=>s.Slot).ToArray();
            foreach(var r in c.GetProperty("rows").EnumerateArray())
            {
                int frame=I(r,"frame");if(frame>0){if(kind==1&&frame==I(c,"signal"))args[7]=-1;Invoke(o,"AdvanceSourceSprites");Invoke(o,"RunSourceTasks");}string why=c.GetProperty("id").GetString()+" frame "+frame;
                Assert.AreEqual(I(r,"alive"),Field<IEnumerable>(o,"_tasks").Cast<object>().Count(),why+" task lifetime");var b=r.GetProperty("body");int slot=kind==1?target:actor;var pos=N(b.GetProperty("position"));Assert.AreEqual(new PixelPoint(pos[2],pos[3]),Field<PixelPoint[]>(o,"_offsets")[slot],why+" offset");var matrix=N(b.GetProperty("matrix"));CollectionAssert.AreEqual(matrix,new[]{Field<int[]>(o,"_xScale")[slot],Field<int[]>(o,"_matrixB")[slot],Field<int[]>(o,"_matrixC")[slot],Field<int[]>(o,"_yScale")[slot]},why+" native affine matrix");Assert.AreEqual(I(b,"mode")==1,Field<bool[]>(o,"_blendedObject")[slot],why+" source OBJ mode");
                var f=o.Snapshot();var blend=N(r.GetProperty("blend"));Assert.AreEqual((blend[0]&64)!=0,f.BlendEnabled,why+" BLDCNT");Assert.AreEqual(blend[1]&255,f.AlphaA,why+" EVA");Assert.AreEqual(blend[1]>>8,f.AlphaB,why+" EVB");
                if(kind==2)
                {
                    int rank=Battlers(doubles)[actor].BackgroundRank;Assert.AreEqual((I(r,"dispcnt")&(1<<(rank+8)))!=0,f.BattlerBackgroundEnabled[rank-1],why+" entire source BG plane");Assert.AreEqual(I(r,"paletteLive")!=0,f.SpritePalettes.ContainsKey(full.SpriteTags["ANIM_TAG_BENT_SPOON"]),why+" palette bank owner");var native=r.GetProperty("copies").EnumerateArray().ToArray();Assert.AreEqual(native.Length,copies.Length);
                    for(int i=0;i<copies.Length;i++){var s=copies[i].Frame;Assert.AreEqual(I(native[i],"alive")==0,copies[i].Finished,why+" clone life");var p=N(native[i].GetProperty("position"));CollectionAssert.AreEqual(p,new[]{s.Position.X,s.Position.Y,s.Offset.X,s.Offset.Y},why+" clone motion");Assert.IsFalse(copies[i].Counted);Assert.AreEqual(actor,s.BattlerCopySlot);Assert.AreEqual(4,s.PaletteSlot);Assert.AreEqual(new SourcePaletteBlend(0,11,0),s.BattlerCopyPalette);}
                    // Every visible palette entry is copied from the unfaded body,
                    // independently of actor fades. The unused index0 is opaque-neutral.
                    if(I(r,"paletteLive")!=0)for(int index=1;index<16;index++)Assert.AreEqual(r.GetProperty("palette")[index].GetInt32(),(int)SourcePaletteMath.Blend((ushort)((actor*503+index*1703)&32767),11,0),why+" palette equivalence "+index);
                }
                CollectionAssert.AreEqual(N(r.GetProperty("args")).Select(v=>(short)v).ToArray(),args.Take(8).ToArray(),why+" source arguments");CollectionAssert.AreEqual(extension,args.Skip(8).ToArray());Assert.IsNull(Field<object?>(o,"_visualRandom"));rows++;
            }
            o.Cancel();cases++;
        }
        Assert.AreEqual(vectors.RootElement.GetProperty("profile").GetString()=="full"?32:11,cases);Assert.AreEqual(vectors.RootElement.GetProperty("profile").GetString()=="full"?2374:765,rows);
    }
    [TestMethod]
    public void NativeMimicOrbMatchesZeroScaleGrowthRetainedShrinkFastLinearAndDeferredDestruction()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0,rows=0;
        foreach(var c in vectors.RootElement.GetProperty("cases").EnumerateArray().Where(c=>I(c,"kind")==3))
        {
            bool doubles=c.GetProperty("doubles").GetBoolean();var full=resources.Resolve(102).Script;var o=new SourceBattleAnimation(Primitive(full),Cue(102,I(c,"actor"),I(c,"target")),Battlers(doubles),doubles,new Audio());var args=Field<short[]>(o,"_animationArgs");args[0]=-12;args[1]=24;var sprite=(SourceBattleAnimation.VisualSprite)Nested("MimicOrbSprite",4,42,full.SpriteRecipes["gMimicOrbSpriteTemplate"],o);
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int frame=I(row,"frame");if(frame>0&&!sprite.Finished){sprite.Step(o);if(!sprite.Finished)sprite.AdvanceAnimation(o);}var n=row.GetProperty("copies")[0];var f=sprite.Frame;var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");string why=c.GetProperty("id").GetString()+" frame "+frame;
                Assert.AreEqual(I(n,"alive")==0,sprite.Finished,why+" lifetime");CollectionAssert.AreEqual(N(n.GetProperty("position")),new[]{f.Position.X,f.Position.Y,f.Offset.X,f.Offset.Y},why+" raw plus fast-linear position");CollectionAssert.AreEqual(N(n.GetProperty("matrix")),new[]{f.MatrixA,f.MatrixB,f.MatrixC,f.MatrixD},why+" matrix");CollectionAssert.AreEqual(N(n.GetProperty("affine")),new[]{affine.XScale,affine.YScale,affine.Rotation,affine.Ended?1:0},why+" source affine state");Assert.AreEqual(I(n,"tile"),f.Tile);Assert.AreEqual(I(n,"visible")!=0,f.Visible);CollectionAssert.AreEqual(N(row.GetProperty("args")).Select(v=>(short)v).ToArray(),args.Take(8).ToArray());rows++;
            }
            o.Cancel();cases++;
        }
        Assert.AreEqual(vectors.RootElement.GetProperty("profile").GetString()=="full"?8:3,cases);Assert.AreEqual(vectors.RootElement.GetProperty("profile").GetString()=="full"?352:132,rows);
    }
    [TestMethod][DataRow(101)][DataRow(102)][DataRow(104)]
    public void CompleteRootsComposeBothSidesLeaveNoOwnedEffectsAndCancelAtEveryPhase(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            int target=move==104?actor:actor^1;var bodies=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());assets.TryAdd("impact",new(32,32,new ushort[1024]));foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var o=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),bodies,doubles,audio,spriteScene:scene,spriteMaterials:resolved.Assets);bool affine=false,clone=false,staticBg=false,disabled=false,pixels=false;int ticks=0;
            while(o.Active&&ticks++<600){var f=o.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(o.Snapshot()));affine|=f.BattlerAffine.Any(a=>a.DoubleSize);clone|=f.Sprites.Any(s=>s.BattlerCopySlot>=0);staticBg|=f.StaticBackgroundCopies.Length>0;disabled|=f.BattlerBackgroundEnabled.Any(x=>!x);if(f.Sprites.Length>0&&ticks%7==0)pixels|=!composer.ComposeField(f).SequenceEqual(composer.ComposeField(f with{Sprites=[]}));else composer.ComposeField(f);}
            Assert.IsFalse(o.Active);Assert.AreEqual(0,o.ActiveVisualCount);Assert.AreEqual(0,scene.Count);Assert.IsFalse(o.GraphicsLoaded);Assert.IsFalse(o.HasResidualSprites);Assert.IsFalse(o.Snapshot().BlendEnabled);Assert.IsTrue(o.Snapshot().BattlerBackgroundEnabled.All(x=>x));Assert.AreEqual(move!=104,affine);Assert.AreEqual(move==104,clone);Assert.AreEqual(move==102,staticBg);Assert.AreEqual(move==104,disabled);if(move!=101)Assert.IsTrue(pixels);Assert.IsNull(Field<object?>(o,"_visualRandom"));
            foreach(int cancel in new[]{0,1,3,16,30,70,110,150}){var s=new SourceBattleAnimation.SpriteScene();var sound=new Audio();var r=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),bodies,doubles,sound,spriteScene:s);for(int i=0;i<cancel&&r.Active;i++)r.Tick();r.Cancel();r.Cancel();Assert.AreEqual(0,s.Count);Assert.AreEqual(0,r.ActiveVisualCount);Assert.IsFalse(r.Snapshot().BlendEnabled);Assert.IsTrue(r.Snapshot().BattlerBackgroundEnabled.All(x=>x));Assert.IsFalse(r.GraphicsLoaded);Assert.AreEqual(1,sound.Stops);}
        }
    }
    [TestMethod]
    public void MissingPaletteTagCorruptAffineMissingRestoreAndUnreviewedCopyCompanionsFailBeforeEffects()
    {
        using var resources=new SourceAnimationResources(Folder);var d=resources.Resolve(104).Script;var m=resources.Resolve(102).Script;
        var invalid=new[]{d with{SpriteTags=new Dictionary<string,int>()},d with{Commands=[C("createvisualtask","AnimTask_BlendColorCycle","2","F_PAL_ATTACKER","0","2","0","11","RGB_WHITE"),..d.Commands]},m with{Commands=m.Commands.Where(c=>c.Op!="setarg").ToArray()},m with{SpriteRecipes=m.SpriteRecipes.Where(p=>p.Key!="gMimicOrbSpriteTemplate").ToDictionary()}};
        foreach(var script in invalid){var scene=new SourceBattleAnimation.SpriteScene();var sound=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(script.MoveId,0,1),Battlers(true),true,sound,spriteScene:scene));Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,sound.Cues.Count);}
        var recipe=m.SpriteRecipes["gMimicOrbSpriteTemplate"];foreach(var bad in new[]{recipe with{AffineDoubleSize=false},recipe with{Callback="AnimPowerAbsorptionOrb"},recipe with{AffineAnimations=[]},recipe with{Animations=[[new(0,1,false,false)]]}}){var script=m with{SpriteRecipes=m.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==recipe.Template?bad:p.Value)};Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(102,0,1),Battlers(true),true,new Audio()));}
    }

    [TestMethod]
    public void SourceProfileRetainsExplicitSemanticAndBoundaryCases()
    {
        using var vectors=SourceVectorFixture.Read("battler-copy-source-vectors.json", "python tests/Reference/BattlerCopy/generate.py content/raw/pokefirered/037335f4 --repo . --output tests/Game.Presentation.Tests/Fixtures/battler-copy-source-vectors.json");
        Assert.IsTrue(vectors.RootElement.GetProperty("profile").GetString() is "representative" or "full");
        var cases=vectors.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        CollectionAssert.AreEquivalent(new[]{0,1,2,3}, cases.Select(c=>I(c,"kind")).Distinct().ToArray());
        CollectionAssert.AreEquivalent(new[]{0,1,2}, cases.Where(c=>I(c,"kind")==2).Select(c=>I(c,"free")).Distinct().ToArray());
        Assert.IsTrue(cases.Any(c=>c.GetProperty("hidden").GetBoolean()));
        Assert.IsTrue(cases.Any(c=>I(c,"hold")==0));
        Assert.IsTrue(cases.Any(c=>I(c,"speed")==32767));
        Assert.IsTrue(cases.Any(c=>I(c,"kind")==3&&I(c,"actor")==I(c,"target")));
    }
}
