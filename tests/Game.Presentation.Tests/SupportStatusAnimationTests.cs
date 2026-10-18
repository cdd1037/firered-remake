using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SupportStatusAnimationTests
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private static readonly string[] Templates=["gHelpingHandClapSpriteTemplate","gFastFlyingMusicNotesSpriteTemplate","sImprisonOrbSpriteTemplate"];
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound> Cues=[];public readonly List<(int,int)> Pans=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));public void StopAnimationEffects()=>Stops++;}
    private sealed class Occupied(int slot):SourceBattleAnimation.VisualSprite(slot,100){public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"fixture-occupied",default,default,0,false,false,false,256,null,100);public override void Step(SourceBattleAnimation owner){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static bool IsFull(JsonDocument vectors)=>vectors.RootElement.GetProperty("profile").GetString()=="full";
    private static JsonDocument Vectors()
    {
        var vectors=SourceVectorFixture.Read("support-status-source-vectors.json","python tests/Reference/SupportStatus/generate_motion.py content/raw/pokefirered/037335f4");
        string? profile=vectors.RootElement.GetProperty("profile").GetString();
        Assert.IsTrue(profile is "representative" or "full","Unknown source-vector profile.");
        var representatives=new Dictionary<string,string[]>
        {
            ["cases"]="helping-clap-000 helping-clap-019 helping-clap-082 helping-clap-099 flying-note-002 flying-note-024 flying-note-088 flying-note-107".Split(' '),
            ["taskCases"]="helping-movement-000 helping-movement-033 teeter-movement-002 teeter-movement-035 imprison-orbs-001 imprison-orbs-034 imprison-orbs-036 imprison-orbs-039 imprison-orbs-044 imprison-orbs-047".Split(' '),
            ["directCases"]="note-frame-affine-000 note-frame-affine-001 note-frame-affine-002 note-frame-affine-003 note-frame-affine-004 note-frame-affine-005 note-frame-affine-006 note-frame-affine-007".Split(' '),
            ["integratedCases"]="helping-integrated-000 helping-integrated-022 teeter-integrated-001 teeter-integrated-023".Split(' '),
            ["hiddenCases"]="imprison-orbs-000 imprison-orbs-005 imprison-orbs-010 imprison-orbs-015 imprison-orbs-016".Split(' '),
        };
        var fullCounts=new Dictionary<string,int>{["cases"]=108,["taskCases"]=68,["directCases"]=8,["integratedCases"]=24,["hiddenCases"]=20};
        foreach(var (group, expectedIds) in representatives)
        {
            var cases=vectors.RootElement.GetProperty(group).EnumerateArray().ToArray();
            var ids=cases.Select(c=>c.GetProperty("id").GetString()!).ToArray();
            Assert.AreEqual(ids.Length,ids.Distinct().Count(),group+" duplicate case identity");
            if(IsFull(vectors))Assert.AreEqual(fullCounts[group],cases.Length,group+" complete source corpus");
            else CollectionAssert.AreEquivalent(expectedIds,ids,group+" branch representatives");
            foreach(var c in cases)
            {
                var rows=c.GetProperty("rows");Assert.IsTrue(rows.GetArrayLength()>0,group+" empty rows");
                int frame=0;foreach(var row in rows.EnumerateArray())Assert.AreEqual(frame++,I(row,"frame"),c.GetProperty("id").GetString()+" contiguous source frames");
            }
        }
        return vectors;
    }
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private)!.SetValue(o,value);
    private static object? Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private|BindingFlags.Public)!.Invoke(o,args);
    private static object Nested(string name,params object[] args)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(name,BindingFlags.NonPublic)!,BindingFlags.Public|Private,null,args,null)!;
    private static int I(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static CommittedMoveAnimation Cue(int move,int actor,int target)=>new("support-status",1,move,new(actor),new(target));
    private static AnimationBattler[] Battlers(bool doubles,int[]? widths=null,int[]? heights=null,int[]? offsets=null)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,offsets?[i]??0,0,widths?[i]??64,heights?[i]??64)).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript full)=>full with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static SourceBattleAnimation Owner(SourceAnimationScript full,JsonElement c,Audio audio,SourceBattleAnimation.SpriteScene? scene=null)
    {var owner=new SourceBattleAnimation(Primitive(full),Cue(I(c,"root"),I(c,"attacker"),I(c,"target")),Battlers(c.GetProperty("doubles").GetBoolean(),N(c.GetProperty("sourceWidths")),N(c.GetProperty("sourceHeights")),N(c.GetProperty("sourceYOffsets"))),c.GetProperty("doubles").GetBoolean(),audio,spriteScene:scene);Set(owner,"_frame",0);return owner;}
    private static short[] Arguments(SourceBattleAnimation owner,JsonElement array)
    {var args=Field<short[]>(owner,"_animationArgs");var native=N(array);Assert.AreEqual(8,native.Length);for(int i=0;i<8;i++)args[i]=(short)native[i];for(int i=8;i<args.Length;i++)args[i]=(short)(0x5a00+i);return args.Skip(8).ToArray();}
    private static void ArgsRow(SourceBattleAnimation owner,JsonElement row,short[] extension,string why)
    {CollectionAssert.AreEqual(N(row.GetProperty("args")).Select(v=>(short)v).ToArray(),Field<short[]>(owner,"_animationArgs").Take(8).ToArray(),why+" native arguments");CollectionAssert.AreEqual(extension,Field<short[]>(owner,"_animationArgs").Skip(8).ToArray(),why+" untouched extension");Assert.IsNull(Field<object?>(owner,"_visualRandom"),why+" no source RNG");}
    private static SourceBattleAnimation.VisualSprite Sprite(SourceBattleAnimation owner,SourceAnimationScript full,int kind,int priority,int slot=4)=>(SourceBattleAnimation.VisualSprite)Nested("SupportStatusSprite",slot,priority,full.SpriteRecipes[Templates[kind]],owner);
    private static void Step(SourceBattleAnimation.VisualSprite sprite,SourceBattleAnimation owner){if(sprite.Finished)return;sprite.Step(owner);if(!sprite.Finished)sprite.AdvanceAnimation(owner);}
    private static object AddTask(SourceBattleAnimation owner,int kind){var task=Nested(kind switch{2=>"HelpingHandMovement",3=>"ImprisonOrbs",_=>"TeeterDanceMovement"},owner);((IList)Field<object>(owner,"_tasks")).Add(task);return task;}
    private static void SpriteRow(SourceBattleAnimation.VisualSprite sprite,JsonElement e,string why,bool players=true)
    {
        var f=sprite.Frame;Assert.AreEqual(I(e,"alive")==0,sprite.Finished,why+" lifetime");CollectionAssert.AreEqual(N(e.GetProperty("position")),new[]{f.Position.X,f.Position.Y,f.Offset.X,f.Offset.Y},why+" base and secondary position");Assert.AreEqual(I(e,"tile"),f.Tile,why+" raw rendered tile");Assert.AreEqual(I(e,"visible")!=0,f.Visible,why+" visibility");Assert.AreEqual(I(e,"priority"),f.OamPriority,why+" priority");Assert.AreEqual(I(e,"subpriority"),f.Subpriority,why+" subpriority");Assert.AreEqual(I(e,"objMode")==1,f.Blend,why+" OBJ mode");
        if(!sprite.Finished){bool affine=(I(e,"affineMode")&1)!=0;Assert.AreEqual(affine,f.Affine,why+" affine mode");Assert.AreEqual(I(e,"affineMode")==3,f.DoubleSize,why+" double-size");int bits=I(e,"oamMatrixNum");Assert.AreEqual(!affine&&(bits&8)!=0,f.HFlip,why+" effective OAM H flip");Assert.AreEqual(!affine&&(bits&16)!=0,f.VFlip,why+" effective OAM V flip");}
        if(!players||f.Template!=Templates[1])return;
        var frames=Field<SourceSpriteFrameProgram>(sprite,"_frames");Assert.AreEqual(I(e,"animEnded")!=0,frames.Ended,why+" frame end");Assert.AreEqual(I(e,"animDelay"),frames.DelayCounter,why+" frame delay");
        var affinePlayer=Field<SourceSpriteAffineProgram>(sprite,"_affine");Assert.AreEqual(I(e,"xScale"),affinePlayer.XScale,why+" affine X");Assert.AreEqual(I(e,"yScale"),affinePlayer.YScale,why+" affine Y");Assert.AreEqual(I(e,"rotation"),affinePlayer.Rotation,why+" affine rotation");Assert.AreEqual(I(e,"affineEnded")!=0,affinePlayer.Ended,why+" affine end");
    }
    [TestMethod]
    public void NativeCallbacksMatchRawClapOamMusicArgumentsFramesAndAffinePhases()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0,rows=0;
        foreach(var c in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {var full=resources.Resolve(I(c,"root")).Script;var audio=new Audio();var owner=Owner(full,c,audio);var extension=Arguments(owner,c.GetProperty("initialArgs"));var sprite=Sprite(owner,full,I(c,"kindId"),I(c,"initialSubpriority"));foreach(var row in c.GetProperty("rows").EnumerateArray()){int frame=I(row,"frame");Set(owner,"_frame",frame);if(frame>0)Step(sprite,owner);string why=c.GetProperty("id").GetString()+" frame "+frame;SpriteRow(sprite,row.GetProperty("sprite"),why);ArgsRow(owner,row,extension,why);rows++;}Assert.IsTrue(sprite.Finished);Assert.AreEqual(0,audio.Cues.Count);owner.Cancel();cases++;}
        Assert.AreEqual(IsFull(vectors)?108:8,cases);Assert.AreEqual(IsFull(vectors)?5580:440,rows);
    }
    private static void TaskRow(SourceBattleAnimation owner,object task,JsonElement c,JsonElement row,string why)
    {
        int kind=I(c,"kindId");Assert.AreEqual(I(row,"alive"),Field<IEnumerable>(owner,"_tasks").Cast<object>().Count(),why+" task lifetime");Assert.AreEqual(I(row,"visualCount"),owner.ActiveVisualCount,why+" actual counted ownership");Assert.AreEqual(row.GetProperty("data")[0].GetInt32(),Field<int>(task,"_phase"),why+" phase");int actor=I(c,"attacker");int[] p=N(row.GetProperty("battler").GetProperty("position"));int baseX=c.GetProperty("battlerCoordinates")[actor][2].GetInt32();var offset=Field<PixelPoint[]>(owner,"_offsets")[actor];Assert.AreEqual(p[2],offset.X,why+" independent X2");Assert.AreEqual(p[0]-baseX,Field<PixelPoint[]>(owner,"_battlerPositionOffsets")[actor].X,why+" independent raw base X");Assert.AreEqual(p[0]-baseX+p[2],owner.Snapshot().Offsets[actor].X,why+" rendered base-X plus X2");Assert.AreEqual(p[3],offset.Y,why+" Y2");
        if(kind==3){int[] registers=N(row.GetProperty("registers"));var f=owner.Snapshot();Assert.AreEqual((registers[0]&64)!=0,f.BlendEnabled,why+" BLDCNT");Assert.AreEqual(registers[1]&255,f.AlphaA,why+" EVA");Assert.AreEqual(registers[1]>>8,f.AlphaB,why+" EVB");Assert.AreEqual(row.GetProperty("data")[2].GetInt32(),Field<int>(task,"_attempt"),why+" five attempts include failures");}
    }
    [TestMethod]
    public void NativeTasksPreserveSeparateTeeterCountersImprisonCapacityAndRawOwnerCleanup()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0,failed=0;
        foreach(var c in vectors.RootElement.GetProperty("taskCases").EnumerateArray())
        {int kind=I(c,"kindId");var full=resources.Resolve(I(c,"root")).Script;var scene=new SourceBattleAnimation.SpriteScene();var old=Owner(full,c,new Audio(),scene);old.Cancel();var audio=new Audio();var owner=Owner(full,c,audio,scene);if(kind==3){int capacity=I(c,"additionalFreeSlots");for(int slot=0;slot<64;slot++)if(slot>=4+capacity||slot>=2&&slot<4&&!c.GetProperty("doubles").GetBoolean())scene.Sprites.Add(new Occupied(slot){Owner=old});}var extension=Arguments(owner,c.GetProperty("initialArgs"));var task=AddTask(owner,kind);var tracked=new Dictionary<int,SourceBattleAnimation.VisualSprite>();
         foreach(var row in c.GetProperty("rows").EnumerateArray()){int frame=I(row,"frame");Set(owner,"_frame",frame);if(frame>0){Invoke(owner,"AdvanceSourceSprites");Invoke(owner,"RunSourceTasks");}foreach(var sprite in scene.Sprites.Where(s=>s is not Occupied))tracked[sprite.Slot]=sprite;string why=c.GetProperty("id").GetString()+" frame "+frame;TaskRow(owner,task,c,row,why);ArgsRow(owner,row,extension,why);foreach(var e in row.GetProperty("sprites").EnumerateArray()){var sprite=tracked[I(e,"slot")];SpriteRow(sprite,e,why,false);Assert.IsFalse(sprite.Counted);Assert.AreSame(owner,sprite.Owner);}failed+=row.GetProperty("events").EnumerateArray().Count(e=>e[0].GetInt32()==3&&e[1].GetInt32()==64);}
         if(kind==3)Assert.AreEqual(Math.Min(5,I(c,"additionalFreeSlots")),tracked.Count,"successful source allocation attempts");Assert.IsTrue(tracked.Values.All(s=>s.Finished));Assert.AreEqual(0,audio.Cues.Count);owner.Cancel();Assert.IsTrue(scene.Sprites.All(s=>ReferenceEquals(s.Owner,old)));scene.Cancel();cases++;}
        Assert.AreEqual(IsFull(vectors)?68:10,cases);Assert.IsTrue(failed>0);
    }
    [TestMethod]
    public void HiddenPresentImprisonActorUsesExactFormGeometryAndMonotonicFreeCellRankMapping()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0;
        foreach(var c in vectors.RootElement.GetProperty("hiddenCases").EnumerateArray())
        {int actor=I(c,"attacker");var full=resources.Resolve(286).Script;var bodies=Battlers(true,N(c.GetProperty("sourceWidths")),N(c.GetProperty("sourceHeights")),N(c.GetProperty("sourceYOffsets"))).Select(b=>b.Slot.Value==actor?b with{Visible=false}:b).ToArray();var scene=new SourceBattleAnimation.SpriteScene();var owner=new SourceBattleAnimation(Primitive(full),Cue(286,actor,actor),bodies,true,new Audio(),spriteScene:scene);var task=AddTask(owner,3);var observed=new List<SourceBattleAnimation.VisualSprite>();
         foreach(var row in c.GetProperty("rows").EnumerateArray()){int frame=I(row,"frame");if(frame>0){Invoke(owner,"AdvanceSourceSprites");Invoke(owner,"RunSourceTasks");}foreach(var sprite in scene.Sprites)if(!observed.Contains(sprite))observed.Add(sprite);var native=row.GetProperty("sprites").EnumerateArray().ToArray();Assert.AreEqual(native.Length,observed.Count);for(int rank=0;rank<native.Length;rank++)SpriteRow(observed[rank],native[rank],"hidden raw free-cell rank "+rank+" frame "+frame,false);Assert.AreEqual(I(row,"visualCount"),owner.ActiveVisualCount);}
         // No artificial occupant is added for hidden actor. Numeric native IDs
         // are used only as child handles: the ascending free-cell rank mapping
         // preserves callback, creation order, all five successes and teardown.
         Assert.AreEqual(5,observed.Count);Assert.AreEqual(actor,observed[0].Slot);Assert.IsTrue(observed.All(s=>s.Finished));Assert.AreEqual(0,scene.Count);owner.Cancel();cases++;}
        Assert.AreEqual(IsFull(vectors)?20:5,cases);
    }
    [TestMethod]
    public void AuthoredClapAndTeeterLeafInterleavingMatchesNativeBaseAndStaticCoordinateReads()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0;int[][] notes=[[0,16,-2],[0,0,-2],[0,-16,-2],[1,-8,-2],[2,8,-2]];
        foreach(var c in vectors.RootElement.GetProperty("integratedCases").EnumerateArray())
        {bool helping=I(c,"kindId")==5;var full=resources.Resolve(I(c,"root")).Script;var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var owner=Owner(full,c,audio,scene);var extension=Arguments(owner,c.GetProperty("initialArgs"));AddTask(owner,helping?2:4);var tracked=new Dictionary<int,SourceBattleAnimation.VisualSprite>();
         void Spawn(int kind,int[] args){var registers=Field<short[]>(owner,"_animationArgs");for(int i=0;i<args.Length;i++)registers[i]=(short)args[i];int slot=scene.Sprites.FindFreeSlot(i=>i<4);int priority=Math.Max(3,Battlers(c.GetProperty("doubles").GetBoolean())[I(c,"attacker")].Subpriority-(helping?40:2));var sprite=Sprite(owner,full,kind,priority,slot);sprite.Owner=owner;scene.Sprites.Add(sprite);tracked[slot]=sprite;}
         if(helping){Spawn(0,[0]);Spawn(0,[1]);}else Spawn(1,notes[0]);
         foreach(var row in c.GetProperty("rows").EnumerateArray()){int frame=I(row,"frame");Set(owner,"_frame",frame);if(frame>0){if(!helping&&frame<=96&&frame%24==0)Spawn(1,notes[frame/24]);Invoke(owner,"AdvanceSourceSprites");Invoke(owner,"RunSourceTasks");}string why=c.GetProperty("id").GetString()+" frame "+frame;ArgsRow(owner,row,extension,why);Assert.AreEqual(I(row,"visualCount"),owner.ActiveVisualCount,why);int[] p=N(row.GetProperty("battler").GetProperty("position"));int actor=I(c,"attacker");Assert.AreEqual(p[2],Field<PixelPoint[]>(owner,"_offsets")[actor].X,why+" independent X2");int rawDelta=p[0]-c.GetProperty("battlerCoordinates")[actor][2].GetInt32();Assert.AreEqual(rawDelta,Field<PixelPoint[]>(owner,"_battlerPositionOffsets")[actor].X,why+" independent raw base X");Assert.AreEqual(rawDelta+p[2],owner.Snapshot().Offsets[actor].X,why+" rendered source sum");foreach(var e in row.GetProperty("sprites").EnumerateArray())SpriteRow(tracked[I(e,"slot")],e,why);}
         Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,audio.Cues.Count);owner.Cancel();cases++;}
        Assert.AreEqual(IsFull(vectors)?24:4,cases);
    }
    [TestMethod]
    public void AllEightNoteFrameSelectorsAndLoopingAffineTablesMatchSourceWithoutOamFlips()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);var recipe=resources.Resolve(298).Script.SpriteRecipes[Templates[1]];int cases=0;
        foreach(var c in vectors.RootElement.GetProperty("directCases").EnumerateArray()){int anim=c.GetProperty("initialArgs")[0].GetInt32();var frames=new SourceSpriteFrameProgram(recipe,anim);var affine=new SourceSpriteAffineProgram(recipe.AffineAnimations[0]);foreach(var row in c.GetProperty("rows").EnumerateArray()){if(I(row,"frame")>0){frames.Step();affine.Step();}var e=row.GetProperty("sprite");Assert.AreEqual(I(e,"tile"),frames.Frame.Tile);Assert.AreEqual(I(e,"animDelay"),frames.DelayCounter);Assert.AreEqual(I(e,"animEnded")!=0,frames.Ended);Assert.AreEqual(anim>=6,frames.Frame.VFlip);Assert.AreEqual(4,I(e,"oamMatrixNum"),"frame VFlip never modifies affine matrix index");Assert.AreEqual(I(e,"xScale"),affine.XScale);Assert.AreEqual(I(e,"yScale"),affine.YScale);Assert.AreEqual(I(e,"rotation"),affine.Rotation);}cases++;}Assert.AreEqual(8,cases);
    }
    [TestMethod][DataRow(270)][DataRow(286)][DataRow(298)]
    public void CompleteRootsComposeAllPositionsMaterialsBackgroundAndSharedTaskPhases(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {int target=move==270&&doubles?actor^2:move==286?actor:actor^1;var bodies=Battlers(doubles);var surface=resources.CreateBackgroundSurface(0);ushort[] palette=Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray();var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies,surface);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),bodies,doubles,audio,palette,spriteScene:scene,spriteMaterials:resolved.Assets);var replay=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),bodies,doubles,new Audio(),palette,spriteMaterials:resolved.Assets);var seen=new HashSet<SourceBattleAnimation.VisualSprite>();var counts=new Dictionary<string,int>();bool pixels=false,bg=false,partnerBlend=false;int ticks=0;
         while(runtime.Active&&ticks++<700){runtime.Tick();replay.Tick();var f=runtime.Snapshot();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Snapshot()),"complete deterministic snapshot with indexed palettes");foreach(var s in scene.Sprites)if(seen.Add(s))counts[s.Frame.Template]=counts.GetValueOrDefault(s.Frame.Template)+1;if(f.Sprites.Length>0&&ticks%7==0)pixels|=!composer.ComposeField(f).SequenceEqual(composer.ComposeField(f with{Sprites=[]}));bg|=f.MoveBackground!.Id is not null;if(move==270&&doubles)partnerBlend|=f.PaletteBlends.Any(p=>p.Palette==16+(actor^2));}
         Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.IsTrue(pixels);Assert.IsTrue(audio.Cues.Count>0);Assert.AreEqual(0,audio.Pans.Count);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.HasResidualSprites);Assert.IsNull(Field<object?>(runtime,"_visualRandom"));Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));Assert.IsFalse(runtime.Snapshot().BlendEnabled);
         if(move==270){Assert.AreEqual(2,counts[Templates[0]]);Assert.AreEqual(3,audio.Cues.Count);if(doubles)Assert.IsTrue(partnerBlend);}if(move==286){Assert.IsTrue(bg);Assert.AreEqual(5,counts[Templates[2]]);Assert.AreEqual(1,counts["gRedXSpriteTemplate"]);Assert.IsTrue(seen.Where(s=>s.Frame.Template==Templates[2]).All(s=>!s.Counted));Assert.IsNull(runtime.Snapshot().MoveBackground!.Id);}if(move==298){Assert.AreEqual(5,counts[Templates[1]]);Assert.AreEqual(5,audio.Cues.Count);Assert.IsTrue(resolved.Assets.ContainsKey("duck"));}}
    }
    [TestMethod]
    public void CancellationRestoresOffsetsRawSpritesBlendAndAudioAcrossEverySourcePhase()
    {
        using var resources=new SourceAnimationResources(Folder);foreach(int move in new[]{270,286,298})foreach(int tick in new[]{0,2,9,13,20,26,39,45,60,61,77,79,128,145,160,180}){var resolved=resources.Resolve(move);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(move,0,move==270?2:1),Battlers(true),true,audio,new ushort[16],spriteScene:scene);for(int i=0;i<tick&&runtime.Active;i++)runtime.Tick();int sounds=audio.Cues.Count;runtime.Cancel();runtime.Cancel();Assert.ThrowsExactly<InvalidOperationException>(()=>runtime.Tick());Assert.AreEqual(0,scene.Count);Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.Snapshot().BlendEnabled);Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));Assert.IsTrue(Field<bool[]>(runtime,"_affineMode").All(v=>!v));Assert.AreEqual(sounds,audio.Cues.Count);Assert.AreEqual(1,audio.Stops);}
    }
    [TestMethod]
    public void NoteSelectorTwoRequiresItsSourceFortyOneFrameDuration()
    {
        using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(298).Script;var recipe=script.SpriteRecipes[Templates[1]];
        CollectionAssert.AreEqual(new[]{10,10,41,10,10,10,10,10},recipe.Animations.Select(a=>a[0].Duration).ToArray());
        var allowed=new SourceBattleAnimation(script,Cue(298,0,1),Battlers(true),true,new Audio());allowed.Cancel();
        var animations=recipe.Animations.Select(a=>a.ToArray()).ToArray();animations[2][0]=animations[2][0] with{Duration=10};var bad=recipe with{Animations=animations};
        var error=Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script with{SpriteRecipes=script.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==Templates[1]?bad:p.Value)},Cue(298,0,1),Battlers(true),true,new Audio()));
        StringAssert.Contains(error.Message,"Support frame commands differ.");
    }
    [TestMethod]
    public void MissingRawDependenciesFailClosedWhileIndependentMotionAndMonBgCompose()
    {
        using var resources=new SourceAnimationResources(Folder);foreach(int move in new[]{270,298}){var s=resources.Resolve(move).Script;string name=Templates[move==270?0:1];var r=s.SpriteRecipes[name];foreach(var bad in new[]{r with{Width=r.Width+8},r with{Blend=!r.Blend},r with{LoopStarts=[]}})Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s with{SpriteRecipes=s.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==name?bad:p.Value)},Cue(move,0,1),Battlers(true),true,new Audio()));}
        var imprisoned=resources.Resolve(286).Script;Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(imprisoned with{SpriteDependencies=[]},Cue(286,0,0),Battlers(true),true,new Audio(),new ushort[16]));
        var teeter=resources.Resolve(298).Script;Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(teeter with{MaterialDependencies=[]},Cue(298,0,1),Battlers(true),true,new Audio()));
        Assert.AreEqual("end",teeter.Commands[^1].Op);
        // Native Teeter writes x and x2 independently. The shared raw-position
        // register now composes with other x2 writers and monbg's source sum.
        foreach(var composed in new[]{teeter with{Commands=[C("createvisualtask","AnimTask_ShakeMon2","2","0","2","0","5","1"),..teeter.Commands]},teeter with{Commands=[C("monbg","ANIM_ATTACKER"),..teeter.Commands[..^1],C("clearmonbg","ANIM_ATTACKER"),C("delay","2"),C("end")]}})
        {
            var runtime=new SourceBattleAnimation(composed,Cue(298,0,1),Battlers(true),true,new Audio());int ticks=0;
            while(runtime.Active&&ticks++<700){var frame=runtime.Tick();var raw=Field<PixelPoint[]>(runtime,"_battlerPositionOffsets");var pos2=Field<PixelPoint[]>(runtime,"_offsets");for(int i=0;i<4;i++)Assert.AreEqual(new PixelPoint(raw[i].X+pos2[i].X,raw[i].Y+pos2[i].Y),frame.Offsets[i]);}
            Assert.IsFalse(runtime.Active);Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));runtime.Cancel();Assert.AreEqual(0,runtime.Snapshot().BackgroundSlots.Length);Assert.IsTrue(Field<PixelPoint[]>(runtime,"_battlerPositionOffsets").All(p=>p==default));Assert.IsTrue(Field<PixelPoint[]>(runtime,"_offsets").All(p=>p==default));
        }
        var unused=teeter with{Subroutines=teeter.Subroutines.Append(new KeyValuePair<string,SourceAnimationCommand[]>("UnusedMove",[C("createvisualtask","AnimTask_ShakeMon2","2","0","2","0","5","1"),C("return")])).ToDictionary(p=>p.Key,p=>p.Value)};var allowed=new SourceBattleAnimation(unused,Cue(298,0,1),Battlers(true),true,new Audio());allowed.Cancel();
    }
}
