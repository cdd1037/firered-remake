using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SocialFourAnimationTests
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private static readonly string[] Templates=["gThoughtBubbleSpriteTemplate","gTauntFingerSpriteTemplate","gFollowMeFingerSpriteTemplate","gRedHeartProjectileSpriteTemplate","gRedHeartRisingSpriteTemplate"];
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound> Cues=[];public readonly List<(int,int)> Pans=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));public void StopAnimationEffects()=>Stops++;}
    private sealed class Occupied(int slot):SourceBattleAnimation.VisualSprite(slot,100){public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"fixture-occupied",default,default,0,false,false,false,256,null,100);public override void Step(SourceBattleAnimation owner){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static bool IsFull(JsonDocument vectors)=>vectors.RootElement.GetProperty("profile").GetString()=="full";
    private static JsonDocument Vectors()
    {
        var vectors=SourceVectorFixture.Read("social-four-source-vectors.json","python tests/Reference/SocialFour/generate_motion.py content/raw/pokefirered/037335f4");
        string? profile=vectors.RootElement.GetProperty("profile").GetString();
        Assert.IsTrue(profile is "representative" or "full","Unknown source-vector profile.");
        var representatives=new Dictionary<string,string[]>
        {
            ["cases"]="thought-bubble-000 thought-bubble-004 thought-bubble-019 thought-bubble-122 thought-bubble-124 taunt-finger-001 taunt-finger-016 taunt-finger-089 taunt-finger-112 follow-me-finger-002 follow-me-finger-017 follow-me-finger-123 follow-me-finger-125 red-heart-projectile-003 red-heart-projectile-018 red-heart-projectile-087 red-heart-rising-007 red-heart-rising-121".Split(' '),
            ["taskCases"]="hearts-background-000 torment-attacker-001 torment-attacker-005 torment-attacker-018 torment-attacker-019 torment-attacker-021 torment-attacker-022 torment-attacker-023 torment-attacker-024 torment-attacker-025".Split(' '),
            ["directCases"]="frame-0-0-baseflip0 frame-0-0-baseflip1 frame-0-1-baseflip0 frame-0-1-baseflip1 frame-0-2-baseflip0 frame-0-2-baseflip1 frame-0-3-baseflip0 frame-0-3-baseflip1 frame-1-0-baseflip0 frame-1-0-baseflip1 frame-1-1-baseflip0 frame-1-1-baseflip1 frame-1-2-baseflip0 frame-1-2-baseflip1 frame-1-3-baseflip0 frame-1-3-baseflip1 follow-affine-0 follow-affine-1".Split(' '),
            ["integratedCases"]="hearts-overlay-00 hearts-overlay-03".Split(' '),
        };
        var fullCounts=new Dictionary<string,int>{["cases"]=126,["taskCases"]=26,["directCases"]=18,["integratedCases"]=4};
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
    private static int I(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static CommittedMoveAnimation Cue(int move,int actor,int target)=>new("social-four",1,move,new(actor),new(target));
    private static AnimationBattler[] Battlers(bool doubles,int visible=15,int[]? widths=null,int[]? heights=null,int[]? offsets=null)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,offsets?[i]??0,0,widths?[i]??64,heights?[i]??64){Visible=(visible&(1<<i))!=0}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript full)=>full with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static SourceBattleAnimation Owner(SourceAnimationScript full,JsonElement c,Audio audio,SourceBattleAnimation.SpriteScene? scene=null)
    {bool doubles=c.GetProperty("doubles").GetBoolean();int[]? Get(string key)=>c.TryGetProperty(key,out var x)?N(x):null;var owner=new SourceBattleAnimation(Primitive(full),Cue(I(c,"root"),I(c,"attacker"),I(c,"target")),Battlers(doubles,c.TryGetProperty("visibleMask",out var m)?m.GetInt32():15,Get("sourceWidths"),Get("sourceHeights"),Get("sourceYOffsets")),doubles,audio,spriteScene:scene);Set(owner,"_frame",0);return owner;}
    private static short[] Arguments(SourceBattleAnimation owner,JsonElement array)
    {var args=Field<short[]>(owner,"_animationArgs");var native=N(array);Assert.AreEqual(8,native.Length);for(int i=0;i<8;i++)args[i]=(short)native[i];for(int i=8;i<args.Length;i++)args[i]=(short)(0x5a00+i);return args.Skip(8).ToArray();}
    private static object Nested(string name,params object[] args)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(name,BindingFlags.NonPublic)!,BindingFlags.Public|Private,null,args,null)!;
    private static SourceBattleAnimation.VisualSprite Sprite(SourceBattleAnimation owner,SourceAnimationScript full,int kind,int priority,int slot=4)=>(SourceBattleAnimation.VisualSprite)Nested("SocialSprite",slot,priority,full.SpriteRecipes[Templates[kind]],owner);
    private static void Step(SourceBattleAnimation.VisualSprite sprite,SourceBattleAnimation owner){if(sprite.Finished)return;sprite.Step(owner);if(!sprite.Finished)sprite.AdvanceAnimation(owner);}
    private static void ArgsRow(SourceBattleAnimation owner,JsonElement row,short[] extension,string why)
    {CollectionAssert.AreEqual(N(row.GetProperty("args")).Select(v=>(short)v).ToArray(),Field<short[]>(owner,"_animationArgs").Take(8).ToArray(),why+" native arguments");CollectionAssert.AreEqual(extension,Field<short[]>(owner,"_animationArgs").Skip(8).ToArray(),why+" untouched extended registers");Assert.IsNull(Field<object?>(owner,"_visualRandom"),why+" source has no random call");}
    private static void SpriteRow(SourceBattleAnimation.VisualSprite sprite,JsonElement e,string why,bool players=true)
    {
        var f=sprite.Frame;Assert.AreEqual(I(e,"alive")==0,sprite.Finished,why+" life");CollectionAssert.AreEqual(N(e.GetProperty("position")),new[]{f.Position.X,f.Position.Y,f.Offset.X,f.Offset.Y},why+" position");Assert.AreEqual(I(e,"tile"),f.Tile,why+" rendered tile");Assert.AreEqual(I(e,"visible")!=0,f.Visible,why+" visibility");Assert.AreEqual(I(e,"priority"),f.OamPriority,why+" priority");Assert.AreEqual(I(e,"subpriority"),f.Subpriority,why+" subpriority");Assert.AreEqual(I(e,"objMode")==1,f.Blend,why+" blend");
        // DestroySpriteAndMatrix clears the native affine mode immediately before slot reset;
        // the retired managed frame is never emitted. Compare its last matrix separately.
        if(!sprite.Finished){bool affine=(I(e,"affineMode")&1)!=0;Assert.AreEqual(affine,f.Affine,why+" affine mode");int bits=I(e,"oamMatrixNum");Assert.AreEqual(!affine&&(bits&8)!=0,f.HFlip,why+" rendered H flip");Assert.AreEqual(!affine&&(bits&16)!=0,f.VFlip,why+" rendered V flip");}
        if(!players)return;
        var frames=Field<SourceSpriteFrameProgram>(sprite,"_frames");if(f.Template is "gThoughtBubbleSpriteTemplate" or "gTauntFingerSpriteTemplate"){Assert.AreEqual(I(e,"animEnded")!=0,frames.Ended,why+" frame end");Assert.AreEqual(I(e,"animDelay"),frames.DelayCounter,why+" frame delay");}
        var affinePlayer=Field<SourceSpriteAffineProgram?>(sprite,"_affine");if(affinePlayer is not null&&!sprite.Finished){Assert.AreEqual(I(e,"xScale"),affinePlayer.XScale,why+" affine X");Assert.AreEqual(I(e,"yScale"),affinePlayer.YScale,why+" affine Y");Assert.AreEqual(I(e,"rotation"),affinePlayer.Rotation,why+" affine rotation");Assert.AreEqual(I(e,"affineEnded")!=0,affinePlayer.Ended,why+" affine end");}
    }
    [TestMethod]
    public void NativeSpriteCallbacksMatchGeometryHandoffsArgumentsPlayersAndLifetime()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0,rows=0;
        foreach(var c in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {var full=resources.Resolve(I(c,"root")).Script;var audio=new Audio();var owner=Owner(full,c,audio);var extension=Arguments(owner,c.GetProperty("initialArgs"));var sprite=Sprite(owner,full,I(c,"kindId"),I(c,"initialSubpriority"));foreach(var row in c.GetProperty("rows").EnumerateArray()){int frame=I(row,"frame");Set(owner,"_frame",frame);if(frame>0)Step(sprite,owner);string why=c.GetProperty("id").GetString()+" frame "+frame;SpriteRow(sprite,row.GetProperty("sprite"),why);ArgsRow(owner,row,extension,why);Assert.AreEqual(sprite.Finished?0:1,I(row,"visualCount"),why);Assert.AreEqual(0,audio.Cues.Count,why);rows++;}Assert.IsTrue(sprite.Finished);owner.Cancel();cases++;}
        Assert.AreEqual(IsFull(vectors)?126:18,cases);Assert.AreEqual(IsFull(vectors)?14530:1635,rows);
    }
    [TestMethod]
    public void OriginalFrameAndAffineProgramsRetainBaseFlipAndFiniteLoopSemantics()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0;
        foreach(var c in vectors.RootElement.GetProperty("directCases").EnumerateArray())
        {int kind=I(c,"kindId");var recipe=resources.Resolve(kind==2?266:269).Script.SpriteRecipes[Templates[kind]];if(c.TryGetProperty("affineAnim",out var a)){var program=new SourceSpriteAffineProgram(recipe.AffineAnimations[a.GetInt32()]);foreach(var row in c.GetProperty("rows").EnumerateArray()){if(I(row,"frame")>0)program.Step();var e=row.GetProperty("sprite");string why=c.GetProperty("id").GetString()+" frame "+I(row,"frame");Assert.AreEqual(I(e,"xScale"),program.XScale,why);Assert.AreEqual(I(e,"yScale"),program.YScale,why);Assert.AreEqual(I(e,"rotation"),program.Rotation,why);Assert.AreEqual(I(e,"affineEnded")!=0,program.Ended,why);}}
         else{var program=new SourceSpriteFrameProgram(recipe,I(c,"anim"));foreach(var row in c.GetProperty("rows").EnumerateArray()){if(I(row,"frame")>0)program.Step();var e=row.GetProperty("sprite");int bits=I(e,"oamMatrixNum");Assert.AreEqual(I(e,"tile"),program.Frame.Tile);Assert.AreEqual((bits&8)!=0,(I(c,"baseHFlip")!=0)^program.Frame.HFlip);Assert.AreEqual((bits&16)!=0,(I(c,"baseVFlip")!=0)^program.Frame.VFlip);Assert.AreEqual(I(e,"animDelay"),program.DelayCounter);Assert.AreEqual(I(e,"animEnded")!=0,program.Ended);}}cases++;}
        Assert.AreEqual(18,cases);
    }
    private static object AddTask(SourceBattleAnimation owner,string name){var task=Nested(name,owner);((IList)Field<object>(owner,"_tasks")).Add(task);return task;}
    private static void SeedBg2(SourceBattleAnimation owner){Field<int[]>(owner,"_battlerBackgroundPriorities")[1]=2;Field<HashSet<int>>(owner,"_backgrounds").Add(2);}
    private static void OverlayRow(SourceBattleAnimation owner,JsonElement row,string why)
    {var f=owner.Snapshot();int[] r=N(row.GetProperty("registers"));Assert.AreEqual(r[1]&255,f.AlphaA,why+" EVA");Assert.AreEqual(r[1]>>8,f.AlphaB,why+" EVB");Assert.AreEqual((r[0]&64)!=0,f.BlendEnabled,why+" blend enable");Assert.AreEqual(row.GetProperty("bg1Attributes")[4].GetInt32(),f.BattlerBackgroundPriorities[0],why+" BG1 priority");Assert.AreEqual(2,f.BattlerBackgroundPriorities[1],why+" unrelated BG2 priority");Assert.IsTrue(f.BackgroundSlots.Contains(2),why+" unrelated BG2 content survives");if(I(row,"frame")<270){Assert.AreEqual("attract",f.Overlay!.MaterialId);Assert.AreEqual(0,f.Overlay.ScrollX);Assert.AreEqual(0,f.Overlay.ScrollY);}else Assert.IsNull(f.Overlay,why+" BG1 clears before blend teardown");}
    [TestMethod]
    public void NativeHeartsOverlayAndEightRisingSpritesSharePhaseOrderAndPreserveBg2()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0;
        foreach(var c in vectors.RootElement.GetProperty("taskCases").EnumerateArray().Where(c=>I(c,"kindId")==5).Concat(vectors.RootElement.GetProperty("integratedCases").EnumerateArray()))
        {var full=resources.Resolve(213).Script;var audio=new Audio();var owner=Owner(full,c,audio);SeedBg2(owner);AddTask(owner,"HeartsBackgroundTask");var sprites=new List<SourceBattleAnimation.VisualSprite>();if(c.TryGetProperty("recipes",out var recipes)){foreach(var r in recipes.EnumerateArray()){var args=Field<short[]>(owner,"_animationArgs");for(int i=0;i<3;i++)args[i]=(short)r[i].GetInt32();sprites.Add(Sprite(owner,full,4,Math.Max(3,Battlers(c.GetProperty("doubles").GetBoolean())[I(c,"attacker")].Subpriority-40),4+sprites.Count));}}var extension=Arguments(owner,c.GetProperty("rows")[0].GetProperty("args"));
         foreach(var row in c.GetProperty("rows").EnumerateArray()){int frame=I(row,"frame");Set(owner,"_frame",frame);if(frame>0){foreach(var s in sprites)Step(s,owner);Invoke(owner,"RunSourceTasks");}string why=c.GetProperty("id").GetString()+" frame "+frame;OverlayRow(owner,row,why);ArgsRow(owner,row,extension,why);Assert.AreEqual(I(row,"alive"),Field<IEnumerable>(owner,"_tasks").Cast<object>().Count(),why+" task lifetime");Assert.AreEqual(I(row,"visualCount"),sprites.Count(s=>!s.Finished)+Field<IEnumerable>(owner,"_tasks").Cast<object>().Count(),why+" visual counter");foreach(var e in row.GetProperty("sprites").EnumerateArray())SpriteRow(sprites[I(e,"slot")-4],e,why);Assert.AreEqual(0,audio.Cues.Count);}
         Assert.IsTrue(sprites.All(s=>s.Finished));owner.Cancel();cases++;}
        Assert.AreEqual(IsFull(vectors)?10:3,cases);
    }
    [TestMethod]
    public void NativeTormentGlobalScanCapacityFailuresCountersAndTaskAffineAreExact()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0,failures=0,foreign=0;
        foreach(var c in vectors.RootElement.GetProperty("taskCases").EnumerateArray().Where(c=>I(c,"kindId")==6))
        {var full=resources.Resolve(259).Script;var scene=new SourceBattleAnimation.SpriteScene();var oldAudio=new Audio();var old=Owner(full,c,oldAudio,scene);old.Cancel();var audio=new Audio();var owner=Owner(full,c,audio,scene);int[] pre=N(c.GetProperty("preexistingMatchingSlots"));int free=I(c,"additionalFreeSlots");var tracked=new Dictionary<int,SourceBattleAnimation.VisualSprite>();foreach(int slot in pre){int i=slot-4;var bubble=(SourceBattleAnimation.VisualSprite)Nested("TormentBubble",slot,40-i,full.SpriteRecipes[Templates[0]],new PixelPoint(24+i*16,24),(i&1)!=0);bubble.Owner=old;scene.Sprites.Add(bubble);tracked[slot]=bubble;}
         // Native fixture reserves the four battler slots even for singles/hidden partners.
         for(int slot=0;slot<64;slot++)if((slot<4&&!Battlers(c.GetProperty("doubles").GetBoolean(),I(c,"visibleMask")).Any(b=>b.Slot.Value==slot&&b.Visible))||slot>=4+pre.Length+free)scene.Sprites.Add(new Occupied(slot){Owner=old});
         var extension=Arguments(owner,c.GetProperty("initialArgs"));var task=AddTask(owner,"TormentTask");int sounds=0;
         foreach(var row in c.GetProperty("rows").EnumerateArray()){int frame=I(row,"frame");Set(owner,"_frame",frame);if(frame>0){Invoke(owner,"AdvanceSourceSprites");Invoke(owner,"RunSourceTasks");}string why=c.GetProperty("id").GetString()+" frame "+frame;foreach(var s in scene.Sprites.Where(s=>s is not Occupied))tracked.TryAdd(s.Slot,s);foreach(var e in row.GetProperty("sprites").EnumerateArray()){int slot=I(e,"slot");Assert.IsTrue(tracked.ContainsKey(slot),why+" native allocation");var s=tracked[slot];SpriteRow(s,e,why,false);Assert.IsFalse(s.Counted);Assert.AreSame(pre.Contains(slot)?old:owner,s.Owner,why+" original callback/material/RNG owner");}
          Assert.AreEqual(I(row,"alive"),Field<IEnumerable>(owner,"_tasks").Cast<object>().Count(),why+" task lifetime");Assert.AreEqual(I(row,"visualCount"),owner.ActiveVisualCount,why+" raw children never add visual count");if(frame>=119)Assert.AreEqual(row.GetProperty("data")[6].GetInt32(),Field<int>(task,"_remaining"),why+" adopted counter");else Assert.AreEqual(row.GetProperty("data")[6].GetInt32(),Field<int>(task,"_wait"),why+" inter-attempt wait");Assert.AreEqual(row.GetProperty("data")[0].GetInt32(),Field<int>(task,"_phase"),why+" task phase");ArgsRow(owner,row,extension,why);int actor=I(c,"attacker");var battler=row.GetProperty("battler");var offset=Field<PixelPoint[]>(owner,"_offsets")[actor];Assert.AreEqual(battler.GetProperty("position")[2].GetInt32(),offset.X,why+" battler X");Assert.AreEqual(battler.GetProperty("position")[3].GetInt32(),offset.Y,why+" battler foot anchor");int[] matrix=N(row.GetProperty("battlerMatrix"));CollectionAssert.AreEqual(matrix,new[]{Field<int[]>(owner,"_xScale")[actor],Field<int[]>(owner,"_matrixB")[actor],Field<int[]>(owner,"_matrixC")[actor],Field<int[]>(owner,"_yScale")[actor]},why+" task affine matrix");if(frame>0)Assert.AreEqual(I(battler,"affineMode")==3,Field<bool[]>(owner,"_affineMode")[actor],why+" double-size task affine teardown");
          var events=row.GetProperty("events").EnumerateArray().Where(e=>e[0].GetInt32()==7).ToArray();Assert.AreEqual(events.Length,audio.Cues.Count,why+" sound despite capacity failure");for(int i=0;i<events.Length;i++){Assert.AreEqual("SE_M_METRONOME",audio.Cues[i].Id);Assert.AreEqual(events[i][2].GetInt32(),audio.Cues[i].Pan);Assert.AreEqual(frame,audio.Cues[i].Frame);Assert.IsTrue(audio.Cues[i].ApplyPanning);}sounds+=audio.Cues.Count;audio.Cues.Clear();Assert.AreEqual(0,audio.Pans.Count);Assert.AreEqual(0,oldAudio.Cues.Count);failures+=row.GetProperty("events").EnumerateArray().Count(e=>e[0].GetInt32()==3&&e[1].GetInt32()==64);}
         Assert.AreEqual(6,sounds);Assert.AreEqual(pre.Length+Math.Min(free,6),tracked.Count,"all successful allocations and preexisting children");Assert.AreEqual(Math.Min(6,pre.Length+Math.Min(free,6)),tracked.Values.Count(s=>s.Finished),"source scan adopts the first six matching scene slots");foreign+=pre.Length;owner.Cancel();Assert.IsTrue(scene.Sprites.All(s=>ReferenceEquals(s.Owner,old)));scene.Cancel();cases++;}
        Assert.AreEqual(IsFull(vectors)?20:9,cases);Assert.IsTrue(failures>0);Assert.IsTrue(foreign>0);
    }
    [TestMethod][DataRow(213)][DataRow(259)][DataRow(266)][DataRow(269)]
    public void CompleteRootsComposeAllPositionsAndDrainTheirAuthoredSpriteClosure(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in move==266?new[]{actor}:doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {var bodies=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());assets.TryAdd("impact",new(32,32,new ushort[1024]));foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),bodies,doubles,audio,spriteScene:scene,spriteMaterials:resolved.Assets);var replay=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),bodies,doubles,new Audio(),spriteMaterials:resolved.Assets);var seen=new HashSet<SourceBattleAnimation.VisualSprite>();var counts=new Dictionary<string,int>();bool pixels=false,overlay=false;int ticks=0;
         while(runtime.Active&&ticks++<700){runtime.Tick();replay.Tick();var f=runtime.Snapshot();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Snapshot()),"complete deterministic frame including material palettes");foreach(var s in scene.Sprites)if(seen.Add(s))counts[s.Frame.Template]=counts.GetValueOrDefault(s.Frame.Template)+1;if(f.Sprites.Length>0&&ticks%11==0)pixels|=!composer.ComposeField(f).SequenceEqual(composer.ComposeField(f with{Sprites=[]}));if(f.Overlay is not null){overlay=true;Assert.AreEqual("attract",f.Overlay.MaterialId);Assert.IsTrue(composer.ComposeField(f).Length>0);}}
         Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.IsTrue(pixels);Assert.IsTrue(audio.Cues.Count>0);Assert.AreEqual(0,audio.Pans.Count);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.HasResidualSprites);Assert.IsNull(Field<object?>(runtime,"_visualRandom"));Assert.IsNull(runtime.Snapshot().Overlay);Assert.IsFalse(runtime.Snapshot().BlendEnabled);Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));
         if(move==213){Assert.IsTrue(overlay);Assert.AreEqual(1,counts[Templates[3]]);Assert.AreEqual(8,counts[Templates[4]]);Assert.AreEqual(6,counts["gRedHeartBurstSpriteTemplate"]);}if(move==259){Assert.AreEqual(6,counts[Templates[0]]);Assert.AreEqual(6,seen.Count(s=>!s.Counted));Assert.AreEqual(2,counts["gAngerMarkSpriteTemplate"]);Assert.AreEqual(6,audio.Cues.Count(c=>c.Id=="SE_M_METRONOME"));}if(move==266)Assert.AreEqual(1,counts[Templates[2]]);if(move==269){Assert.AreEqual(1,counts[Templates[0]]);Assert.AreEqual(1,counts[Templates[1]]);Assert.AreEqual(2,counts["gAngerMarkSpriteTemplate"]);}}
    }
    [TestMethod]
    public void CancellationReleasesRawChildrenAffineOverlayAndSoundAtEveryPhase()
    {
        using var resources=new SourceAnimationResources(Folder);foreach(int move in new[]{213,259,266,269})foreach(int tick in new[]{0,2,12,35,80,119,130,205,270}){var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(resources.Resolve(move).Script,Cue(move,0,move==266?0:1),Battlers(true),true,audio,spriteScene:scene);for(int i=0;i<tick&&runtime.Active;i++)runtime.Tick();int sounds=audio.Cues.Count;runtime.Cancel();runtime.Cancel();Assert.ThrowsExactly<InvalidOperationException>(()=>runtime.Tick());Assert.AreEqual(0,scene.Count);Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.Snapshot().BlendEnabled);Assert.IsNull(runtime.Snapshot().Overlay);Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));Assert.IsTrue(Field<bool[]>(runtime,"_affineMode").All(v=>!v));Assert.AreEqual(sounds,audio.Cues.Count);Assert.AreEqual(1,audio.Stops);}
    }
    [TestMethod]
    public void MissingRecipesImplicitDependenciesAndActualOwnershipCollisionsFailClosed()
    {
        using var resources=new SourceAnimationResources(Folder);foreach(int move in new[]{213,266,269}){var s=resources.Resolve(move).Script;string name=s.Commands.First(c=>c.Op=="createsprite").Args[0];var r=s.SpriteRecipes[name];foreach(var bad in new[]{r with{Width=r.Width+8},r with{Blend=!r.Blend},r with{LoopStarts=[]}})Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s with{SpriteRecipes=s.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==name?bad:p.Value)},Cue(move,0,1),Battlers(true),true,new Audio()));}
        var torment=resources.Resolve(259).Script;foreach(var bad in new[]{torment with{SpriteDependencies=[]},torment with{SoundEffects=torment.SoundEffects.Where(p=>p.Key!="SE_M_METRONOME").ToDictionary(p=>p.Key,p=>p.Value)},torment with{TaskAffinePrograms=new Dictionary<string,SourceTaskAffineCommand[]>()},torment with{Commands=[C("createsprite",Templates[0],"0","2","0","45"),..torment.Commands]}})Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(bad,Cue(259,0,1),Battlers(true),true,new Audio()));
        var attract=resources.Resolve(213).Script;foreach(var bad in new[]{attract with{OverlayPalettes=new Dictionary<string,ushort[]>()},attract with{Commands=[C("monbg","0"),..attract.Commands]},attract with{Commands=[C("createvisualtask","AnimTask_HeartsBackground","5"),..attract.Commands]}})Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(bad,Cue(213,0,1),Battlers(true),true,new Audio()));
        // Shared catalogs contain unused helpers; only invoked code contributes a collision.
        var unused=torment with{Subroutines=torment.Subroutines.Append(new KeyValuePair<string,SourceAnimationCommand[]>("UnusedThought",[C("createsprite",Templates[0],"0","2","0","45"),C("return")])).ToDictionary(p=>p.Key,p=>p.Value)};var accepted=new SourceBattleAnimation(unused,Cue(259,0,1),Battlers(true),true,new Audio());accepted.Cancel();
    }
}
