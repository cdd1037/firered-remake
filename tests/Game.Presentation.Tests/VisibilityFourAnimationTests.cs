using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class VisibilityFourAnimationTests
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound> Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void SetPan(int pan,int frame){}public void StopAnimationEffects()=>Stops++;}
    private sealed class Occupied(int slot):SourceBattleAnimation.VisualSprite(slot,100){public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"foreign",default,default,0,false,false,false,256,null,100);public override void Step(SourceBattleAnimation owner){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("visibility-four-source-vectors.json", "python tests/Reference/VisibilityFour/generate_motion.py content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/visibility-four-source-vectors.json");
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static object? Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private|BindingFlags.Public)!.Invoke(o,args);
    private static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private)!.SetValue(o,value);
    private static int I(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static CommittedMoveAnimation Cue(int move,int actor,int target)=>new("visibility-four",1,move,new(actor),new(target)){SourceTerrain=SourceAnimationTerrain.Grass};
    private static AnimationBattler[] Battlers(bool doubles,int present=15,int logical=0,int sprite=0)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,i%2==0?0:4,0,64,64){Visible=(present&(1<<i))!=0&&(sprite&(1<<i))==0,SourceVisibility=new((present&(1<<i))!=0,(logical&(1<<i))!=0,(sprite&(1<<i))!=0)}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript full)=>full with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static SourceBattleAnimation Owner(SourceAnimationScript full,JsonElement c,SourceBattleAnimation.SpriteScene? scene=null)
    {bool doubles=c.GetProperty("doubles").GetBoolean();return new(Primitive(full),Cue(full.MoveId,I(c,"attacker"),I(c,"target")) with{SourceTerrain=(SourceAnimationTerrain)I(c,"terrain")},Battlers(doubles,I(c,"presentMask"),I(c,"logicalMask"),I(c,"spriteMask")),doubles,new Audio(),spriteScene:scene);}
    private static string TaskName(int kind)=>kind switch{0=>"AnimTask_AttackerFadeToInvisible",1=>"AnimTask_AttackerFadeFromInvisible",2=>"AnimTask_InitAttackerFadeFromInvisible",3=>"AnimTask_SetAttackerInvisibleWaitForSignal",4=>"AnimTask_SetAllNonAttackersInvisiblity",5=>"AnimTask_SetCamouflageBlend",6=>"AnimTask_BlendBattleAnimPalExclude",_=>"AnimTask_OdorSleuthMovement"};
    private static void CompareVisibility(SourceBattleAnimation o,JsonElement row,string why)
    {
        foreach(var f in o.Snapshot().BattlerVisibility){var expected=row.GetProperty("visibility")[f.Slot];CollectionAssert.AreEqual(N(expected).Take(4).ToArray(),new[]{f.Present?1:0,f.LogicalInvisible?1:0,f.SpriteInvisible?1:0,f.OamInvisible?1:0},why+" dual flags and OAM latch "+f.Slot);Assert.AreEqual(expected[4].GetInt32()!=0,(bool)Invoke(o,"IsSourceBattlerVisible",f.Slot)!,why+" native predicate "+f.Slot);}
    }
    private static void CompareSprite(SourceBattleAnimation.VisualSprite s,JsonElement native,string why)
    {
        var f=s.Frame;Assert.AreEqual(I(native,"alive")==0,s.Finished,why+" lifetime");CollectionAssert.AreEqual(N(native.GetProperty("position")),new[]{f.Position.X,f.Position.Y,f.Offset.X,f.Offset.Y},why+" independent raw base and pos2");Assert.AreEqual(I(native,"visible")!=0,f.Visible,why+" raw invisibility");Assert.AreEqual(I(native,"subpriority"),f.Subpriority,why+" subpriority");Assert.AreEqual(I(native,"priority"),f.OamPriority,why+" copied OBJ priority");Assert.AreEqual(I(native,"objMode")==1,f.Blend,why+" OBJ mode");Assert.AreEqual(I(native,"tile"),f.Tile,why+" tile");
        if(f.Template=="gThinRingShrinkingSpriteTemplate")
        {var affine=Field<SourceSpriteAffineProgram>(s,"_affine");Assert.AreEqual(I(native,"xScale"),affine.XScale,why+" affine X");Assert.AreEqual(I(native,"yScale"),affine.YScale,why+" affine Y");Assert.AreEqual(I(native,"affineEnded")!=0,affine.Ended,why+" affine end");Assert.IsTrue(f.Affine&&f.DoubleSize);}
    }
    [TestMethod]
    public void OriginalCVisibilityTasksPreserveFlagsPhasePaletteMasksArgumentsAndUncountedSignal()
    {
        using var data=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0,rows=0;
        foreach(var c in data.RootElement.GetProperty("cases").EnumerateArray().Where(c=>I(c,"kindId")<7))
        {
            int kind=I(c,"kindId");var full=resources.Resolve(kind==5?293:347).Script;var owner=Owner(full,c);var args=Field<short[]>(owner,"_animationArgs");var initial=N(c.GetProperty("initialArgs"));for(int i=0;i<8;i++)args[i]=(short)initial[i];for(int i=8;i<args.Length;i++)args[i]=(short)(0x5a00+i);var extension=args.Skip(8).ToArray();Assert.IsTrue((bool)Invoke(owner,"TryCreateVisibilityRootTask",C("createvisualtask",TaskName(kind),"5"))!);int at=0;
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int frame=I(row,"frame");while(at<frame){at++;if(kind==3&&at==5)args[7]=0x1000;Invoke(owner,"AdvanceSourceSprites");Invoke(owner,"CaptureSourceVisibilityOam");Invoke(owner,"RunSourceTasks");}
                string why=c.GetProperty("id").GetString()+" frame "+frame;CompareVisibility(owner,row,why);Assert.AreEqual(I(row,"taskAlive"),Field<IEnumerable>(owner,"_tasks").Cast<object>().Count(),why+" task lifetime");Assert.AreEqual(I(row,"visualCount"),owner.ActiveVisualCount,why+" counted task ownership");CollectionAssert.AreEqual(N(row.GetProperty("args")).Select(v=>(short)v).ToArray(),args.Take(8).ToArray(),why+" source argument mutation");CollectionAssert.AreEqual(extension,args.Skip(8).ToArray(),why+" untouched extension");
                var f=owner.Snapshot();var registers=N(row.GetProperty("registers"));Assert.AreEqual((registers[0]&64)!=0,f.BlendEnabled,why+" BLDCNT enable");Assert.AreEqual(registers[1]&255,f.AlphaA,why+" EVA");Assert.AreEqual(registers[1]>>8,f.AlphaB,why+" EVB");if(registers[0]!=0){Assert.AreEqual(registers[0]&63,f.BlendFirstTargets,why+" first targets");Assert.AreEqual((registers[0]>>8)&63,f.BlendSecondTargets,why+" second targets");}
                for(int bank=0;bank<20;bank++){int coef=row.GetProperty("palettes")[bank][0].GetInt32();var p=f.PaletteBlends.SingleOrDefault(p=>p.Palette==bank);Assert.AreEqual(Math.Max(coef,0),p?.Coefficient??0,why+" palette coefficient "+bank);if(coef>0)Assert.AreEqual(row.GetProperty("palettes")[bank][1].GetInt32(),(int)p!.Color,why+" palette color "+bank);}Assert.IsNull(Field<object?>(owner,"_visualRandom"));rows++;
            }
            owner.Cancel();cases++;
        }
        Assert.AreEqual(data.RootElement.GetProperty("profile").GetString()=="full"?217:57,cases);Assert.AreEqual(data.RootElement.GetProperty("profile").GetString()=="full"?7775:1376,rows);
    }
    [TestMethod]
    public void OriginalCCommandsAndAffinePreparationDistinguishBothInvisibleFlags()
    {
        using var data=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0,cleared=0,retained=0;
        foreach(var c in data.RootElement.GetProperty("cases").EnumerateArray().Where(c=>I(c,"kindId") is 8 or 9))
        {var owner=Owner(resources.Resolve(347).Script,c);int actor=I(c,"attacker");foreach(var row in c.GetProperty("rows").EnumerateArray()){int frame=I(row,"frame");if(I(c,"kindId")==9){Invoke(owner,"PrepareBattlerRotScale",actor,0);bool both=(I(c,"logicalMask")&(1<<actor))!=0&&(I(c,"spriteMask")&(1<<actor))!=0;if(both)retained++;else if((I(c,"spriteMask")&(1<<actor))!=0)cleared++;Assert.AreEqual(row.GetProperty("battlers")[actor][4].GetInt32()==3,Field<bool[]>(owner,"_affineMode")[actor]);}else{var slot=(int?)Invoke(owner,"SourceAnimBattlerSlot",frame%4);if(slot is {} selected)Invoke(owner,"SetSourceBattlerSpriteInvisible",selected,frame<4);}Invoke(owner,"CaptureSourceVisibilityOam");CompareVisibility(owner,row,c.GetProperty("id").GetString()+" frame "+frame);}owner.Cancel();cases++;}
        Assert.AreEqual(data.RootElement.GetProperty("profile").GetString()=="full"?58:20,cases);Assert.IsTrue(cleared>0&&retained>0);
    }
    [TestMethod]
    public void OriginalCOdorClonesPreserveRawPositionsPaletteIdentityOwnerCapacityAndNativeLifetime()
    {
        using var data=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0,failed=0;
        foreach(var c in data.RootElement.GetProperty("cases").EnumerateArray().Where(c=>I(c,"kindId")==7))
        {
            var scene=new SourceBattleAnimation.SpriteScene();var full=resources.Resolve(316).Script;var old=Owner(full,c,scene);old.Cancel();var owner=Owner(full,c,scene);var nativeRows=c.GetProperty("rows").EnumerateArray().ToArray();int capacity=I(c,"capacity"),target=I(c,"target");scene.Y[target]=77;
            for(int slot=4+capacity;slot<64;slot++)scene.Sprites.Add(new Occupied(slot){Owner=old});
            foreach(var b in owner.Snapshot().Battlers){int i=b.Slot.Value;var p=N(nativeRows[0].GetProperty("battlers")[i]);Invoke(owner,"SetBattlerPosition",i,new PixelPoint(p[0],p[1]));Field<PixelPoint[]>(owner,"_offsets")[i]=new(p[2],p[3]);}
            // Native fixed battler reservations and renderer event-time presence
            // differ only by free-cell rank. No callback branches on these IDs.
            Assert.IsTrue((bool)Invoke(owner,"TryCreateVisibilityRootTask",C("createvisualtask",TaskName(7),"5"))!);var observed=scene.Sprites.Where(s=>s is not Occupied).OrderBy(s=>s.Slot).ToArray();int frame=0;foreach(var child in observed)Assert.AreEqual(77,scene.Y[child.Slot],"raw struct copy retains source OAM Y even for initially hidden child");
            foreach(var row in nativeRows)
            {
                int next=I(row,"frame");while(frame<next){frame++;Invoke(owner,"AdvanceSourceSprites");Invoke(owner,"CaptureSourceVisibilityOam");Invoke(owner,"RunSourceTasks");}
                string why=c.GetProperty("id").GetString()+" frame "+frame;CompareVisibility(owner,row,why);Assert.AreEqual(I(row,"taskAlive"),Field<IEnumerable>(owner,"_tasks").Cast<object>().Count(),why+" parent wait task");Assert.AreEqual(I(row,"visualCount"),owner.ActiveVisualCount,why+" clone children uncounted");var native=row.GetProperty("sprites").EnumerateArray().ToArray();if(observed.Length==0){Assert.IsTrue(native.All(s=>I(s,"alive")==0));failed++;continue;}Assert.AreEqual(2,observed.Length);Assert.AreEqual(2,native.Length);for(int rank=0;rank<2;rank++){CompareSprite(observed[rank],native[rank],why+" free rank "+rank);Assert.AreSame(owner,observed[rank].Owner);Assert.IsFalse(observed[rank].Counted);Assert.AreEqual(target,observed[rank].Frame.BattlerCopySlot);}}
            Assert.IsTrue(observed.All(s=>s.Finished));owner.Cancel();Assert.IsTrue(scene.Sprites.All(s=>ReferenceEquals(old,s.Owner)));scene.Cancel();cases++;
        }
        Assert.AreEqual(data.RootElement.GetProperty("profile").GetString()=="full"?37:18,cases);Assert.IsTrue(failed>=4);
    }
    [TestMethod]
    public void OriginalCShrinkingRingUsesSharedFrameAndAffinePlayersThroughTerminalCallback()
    {
        using var data=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0;
        foreach(var c in data.RootElement.GetProperty("cases").EnumerateArray().Where(c=>I(c,"kindId")==10))
        {var full=resources.Resolve(347).Script;var owner=Owner(full,c);var row0=c.GetProperty("rows")[0].GetProperty("sprites")[0];int priority=I(row0,"subpriority");Invoke(owner,"LoadSpriteGraphics","ANIM_TAG_THIN_RING");Assert.IsTrue((bool)Invoke(owner,"TryCreateVisibilityRootSprite",C("createsprite","gThinRingShrinkingSpriteTemplate","ANIM_ATTACKER","40","0","0","0","0"),4,priority)!);var sprite=Field<IEnumerable>(owner,"_sprites").Cast<SourceBattleAnimation.VisualSprite>().Single();foreach(var row in c.GetProperty("rows").EnumerateArray()){int frame=I(row,"frame");if(frame>0)Invoke(owner,"AdvanceSourceSprites");CompareSprite(sprite,row.GetProperty("sprites")[0],"ring frame "+frame);}Assert.IsTrue(sprite.Finished);owner.Cancel();cases++;}
        Assert.AreEqual(data.RootElement.GetProperty("profile").GetString()=="full"?6:2,cases);
    }
    [TestMethod][DataRow(185)][DataRow(293)][DataRow(316)][DataRow(347)]
    public void CompleteRootsReplayAllPositionsWithExactSharedMaterialsBackgroundAndCleanup(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            int target=move is 293 or 347?actor:actor^1;var bodies=Battlers(doubles);var surface=resources.CreateBackgroundSurface(0);ushort[] palette=Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray();var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(0x8000|(i%31+1)|((b.Slot.Value+4)<<5))).ToArray());var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies,surface);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),bodies,doubles,audio,palette,spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),bodies,doubles,new Audio(),palette,spriteMaterials:assets);var initial=composer.ComposeField(runtime.Snapshot());var seen=new HashSet<SourceBattleAnimation.VisualSprite>();bool changed=false,dark=false,logicalHidden=false;int ticks=0;
            while(runtime.Active&&ticks++<800){runtime.Tick();replay.Tick();var f=runtime.Snapshot();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Snapshot()),"complete deterministic frame including shared palette arrays");foreach(var sprite in scene.Sprites)seen.Add(sprite);if(ticks%5==0)changed|=!initial.SequenceEqual(composer.ComposeField(f));dark|=f.MoveBackground!.Id is not null;logicalHidden|=f.BattlerVisibility[actor].LogicalInvisible;}
            Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.IsTrue(changed);Assert.IsTrue(audio.Cues.Count>0);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.HasResidualSprites);Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));Assert.IsFalse(runtime.Snapshot().BlendEnabled);Assert.IsTrue(runtime.Snapshot().BattlerVisibility.All(v=>v.Present&&!v.LogicalInvisible&&!v.SpriteInvisible&&!v.OamInvisible));Assert.IsNull(Field<object?>(runtime,"_visualRandom"));
            if(move==185){Assert.IsTrue(dark&&logicalHidden);Assert.IsNull(runtime.Snapshot().MoveBackground!.Id);}if(move==347)Assert.AreEqual(3,seen.Count(s=>s.Frame.Template=="gThinRingShrinkingSpriteTemplate"));if(move==316){Assert.AreEqual(2,seen.Count(s=>s.Frame.Template=="MoveOdorSleuthClone"));Assert.IsTrue(seen.Where(s=>s.Frame.Template=="MoveOdorSleuthClone").All(s=>s.Frame.BattlerCopySlot==target&&!s.Counted));}
        }
    }
    [TestMethod]
    public void MonbgRetainsCopiedBodyWhileRawObjectHidesAndCancellationRestoresInitialFlags()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(347);var bodies=Battlers(true);var script=Primitive(resolved.Script) with{Commands=[C("monbg","ANIM_ATTACKER"),C("delay","4"),C("clearmonbg","ANIM_ATTACKER"),C("waitforvisualfinish"),C("end")]};var owner=new SourceBattleAnimation(script,Cue(347,0,0),bodies,true,new Audio());owner.Tick();var f=owner.Snapshot();Assert.IsTrue(f.BackgroundSlots.Contains(0));Assert.IsTrue(f.BattlerVisibility[0].SpriteInvisible&&f.BattlerVisibility[0].OamInvisible);Assert.IsFalse(f.BattlerVisibility[0].LogicalInvisible);
        var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8421,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)(0x8001+((b.Slot.Value+1)<<5)),4096).ToArray());var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies,resources.CreateBackgroundSurface(0));var copied=composer.ComposeField(f);Assert.IsFalse(copied.SequenceEqual(composer.ComposeField(f with{BackgroundSlots=[]})),"hidden OBJ's retained monbg copy remains rendered");Invoke(owner,"SetSourceBattlerLogicalInvisible",0,true);Assert.IsFalse((bool)Invoke(owner,"IsSourceBattlerVisible",0)!);Assert.IsTrue(copied.SequenceEqual(composer.ComposeField(owner.Snapshot())),"logical hide cannot erase an existing BG copy");owner.Cancel();Assert.IsTrue(owner.Snapshot().BattlerVisibility.All(v=>v.Present&&!v.LogicalInvisible&&!v.SpriteInvisible&&!v.OamInvisible));Assert.AreEqual(0,owner.Snapshot().BackgroundSlots.Length);
    }
    [TestMethod]
    public void TaskWritesLatchAfterOamAndRawHiddenPresentSpritesReserveTheirSceneCells()
    {
        using var resources=new SourceAnimationResources(Folder);var full=resources.Resolve(347).Script;var bodies=Battlers(true,15,2,2);var owner=new SourceBattleAnimation(Primitive(full),Cue(347,0,1),bodies,true,new Audio());Assert.AreEqual(4,(int)Invoke(owner,"FindFreeSpriteSlot",-1)!);Field<short[]>(owner,"_animationArgs")[0]=1;Invoke(owner,"TryCreateVisibilityRootTask",C("createvisualtask","AnimTask_SetAllNonAttackersInvisiblity","5","TRUE"));var f=owner.Snapshot();Assert.IsTrue(f.BattlerVisibility[2].SpriteInvisible);Assert.IsFalse(f.BattlerVisibility[2].OamInvisible,"task occurs after that frame's OAM construction");Assert.IsTrue(f.BattlerVisibility[1].LogicalInvisible&&f.BattlerVisibility[1].SpriteInvisible);Invoke(owner,"CaptureSourceVisibilityOam");Assert.IsTrue(owner.Snapshot().BattlerVisibility[2].OamInvisible);owner.Cancel();Assert.AreEqual(new SourceBattlerVisibilityFrame(1,true,true,true,true),owner.Snapshot().BattlerVisibility[1]);
    }
    [TestMethod]
    public void EveryRootCancellationRestoresIndependentFlagsPositionsPaletteOwnersAndAudio()
    {
        using var resources=new SourceAnimationResources(Folder);foreach(int move in new[]{185,293,316,347})foreach(int tick in new[]{0,1,2,8,16,24,32,45,60,73,90,120,160,220})foreach(bool hidden in new[]{false,true})
        {var resolved=resources.Resolve(move);var bodies=Battlers(true,15,hidden?8:0,hidden?8:0);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(move,0,move is 293 or 347?0:1),bodies,true,audio,new ushort[16],spriteScene:scene);for(int i=0;i<tick&&runtime.Active;i++)runtime.Tick();int sounds=audio.Cues.Count;runtime.Cancel();runtime.Cancel();Assert.ThrowsExactly<InvalidOperationException>(()=>runtime.Tick());Assert.AreEqual(0,scene.Count);Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.Snapshot().BlendEnabled);Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));Assert.IsTrue(Field<PixelPoint[]>(runtime,"_battlerPositionOffsets").All(p=>p==default));Assert.AreEqual(0,runtime.Snapshot().PaletteBlends.Length);foreach(var b in bodies){var expected=b.SourceVisibility!.Value;var actual=runtime.Snapshot().BattlerVisibility[b.Slot.Value];Assert.AreEqual(new SourceBattlerVisibilityFrame(b.Slot.Value,expected.Present,expected.LogicalInvisible,expected.SpriteInvisible,expected.SpriteInvisible),actual);}Assert.AreEqual(sounds,audio.Cues.Count);Assert.AreEqual(1,audio.Stops);}
    }
    [TestMethod]
    public void TerrainUnknownSignalMissingAndUnreviewedCloneMutationFailClosed()
    {
        using var resources=new SourceAnimationResources(Folder);var camouflage=resources.Resolve(293).Script;
        foreach(SourceAnimationTerrain? terrain in new SourceAnimationTerrain?[]{null,(SourceAnimationTerrain)(-1),(SourceAnimationTerrain)10,(SourceAnimationTerrain)19})Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(camouflage,Cue(293,0,0) with{SourceTerrain=terrain},Battlers(true),true,new Audio()));
        for(int terrain=0;terrain<10;terrain++){var runtime=new SourceBattleAnimation(camouflage,Cue(293,0,0) with{SourceTerrain=(SourceAnimationTerrain)terrain},Battlers(true),true,new Audio());runtime.Cancel();}
        var faint=resources.Resolve(185).Script;Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(faint with{Commands=faint.Commands.Where(c=>c.Op!="setarg").ToArray()},Cue(185,0,1),Battlers(true),true,new Audio()));
        var odor=resources.Resolve(316).Script;Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(odor with{Commands=[C("createvisualtask","AnimTask_Withdraw","5"),..odor.Commands]},Cue(316,0,1),Battlers(true),true,new Audio()));
        var calm=resources.Resolve(347).Script;var ring=calm.SpriteRecipes["gThinRingShrinkingSpriteTemplate"];var bad=ring with{AffineAnimations=[[new("frame",512,512,0,0,0),new("frame",-16,-16,0,29,0),new("end",0,0,0,0,0)]]};Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(calm with{SpriteRecipes=calm.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==ring.Template?bad:p.Value)},Cue(347,0,0),Battlers(true),true,new Audio()));
    }

    [TestMethod]
    public void SourceProfileRetainsExplicitSemanticAndBoundaryCases()
    {
        using var vectors=SourceVectorFixture.Read("visibility-four-source-vectors.json", "python tests/Reference/VisibilityFour/generate_motion.py content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/visibility-four-source-vectors.json");
        Assert.IsTrue(vectors.RootElement.GetProperty("profile").GetString() is "representative" or "full");
        var cases=vectors.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        CollectionAssert.AreEquivalent(Enumerable.Range(0,11).ToArray(),cases.Select(c=>I(c,"kindId")).Distinct().ToArray());
        CollectionAssert.AreEquivalent(Enumerable.Range(0,10).ToArray(),cases.Where(c=>I(c,"kindId")==5).Select(c=>I(c,"terrain")).Distinct().ToArray());
        CollectionAssert.AreEquivalent(Enumerable.Range(0,8).ToArray(),cases.Where(c=>I(c,"kindId")==6).Select(c=>c.GetProperty("initialArgs")[0].GetInt32()).Distinct().ToArray());
        CollectionAssert.AreEquivalent(new[]{0,1,2,3,60},cases.Where(c=>I(c,"kindId")==7).Select(c=>I(c,"capacity")).Distinct().ToArray());
        CollectionAssert.AreEquivalent(new[]{(false,false),(false,true),(true,false),(true,true)},cases.Where(c=>I(c,"kindId")==9).Select(c=>(I(c,"logicalMask")!=0,I(c,"spriteMask")!=0)).Distinct().ToArray());
        foreach(var c in cases.Where(c=>I(c,"kindId")!=9))
            CollectionAssert.AreEqual(Enumerable.Range(0,c.GetProperty("rows").GetArrayLength()).ToArray(),c.GetProperty("rows").EnumerateArray().Select(r=>I(r,"frame")).ToArray(),"Every frame remains, including the full signed-delay wrap trace.");
    }
}
