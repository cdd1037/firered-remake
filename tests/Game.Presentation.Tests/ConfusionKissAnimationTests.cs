using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ConfusionKissAnimationTests
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private static readonly string[] Templates=["gConfuseRayBallBounceSpriteTemplate","gConfuseRayBallSpiralSpriteTemplate","gDevilSpriteTemplate","gPinkHeartSpriteTemplate","gAngelSpriteTemplate","gRedHeartBurstSpriteTemplate","gForesightMagnifyingGlassSpriteTemplate"];
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound> Cues=[];public readonly List<(int Pan,int Frame)> Pans=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("confusion-kiss-motion-source-vectors.json", "python tests/Reference/ConfusionKiss/generate_motion.py content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/confusion-kiss-motion-source-vectors.json");
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private)!.SetValue(o,value);
    private static object? Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private|BindingFlags.Public)!.Invoke(o,args);
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static int I(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static string Text(int n)=>n.ToString(CultureInfo.InvariantCulture);
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static CommittedMoveAnimation Cue(int move,int actor,int target,uint seed=0x12345678)=>new("confusion-kiss",1,move,new(actor),new(target)){VisualSeed=seed};
    private static AnimationBattler[] Battlers(bool doubles,int visible=15,int[]? widths=null,int[]? heights=null)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,widths?[i]??64,heights?[i]??64){Visible=(visible&(1<<i))!=0}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript full)=>full with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static SourceBattleAnimation Owner(SourceAnimationScript script,JsonElement source,Audio audio)
    {
        bool doubles=source.GetProperty("doubles").GetBoolean();int[]? widths=source.TryGetProperty("sourceWidths",out var w)?N(w):null,heights=source.TryGetProperty("sourceHeights",out var h)?N(h):null;
        var owner=new SourceBattleAnimation(Primitive(script),Cue(I(source,"root"),I(source,"attacker"),I(source,"target"),source.GetProperty("seed").GetUInt32()),Battlers(doubles,source.TryGetProperty("visibleMask",out var mask)?mask.GetInt32():15,widths,heights),doubles,audio);
        var priorities=Field<int[]>(owner,"_battlerBackgroundPriorities");priorities[0]=1;priorities[1]=2;Set(owner,"_frame",0);if(source.TryGetProperty("customPan",out var pan))Set(owner,"_customPan",pan.GetInt32());return owner;
    }
    private static short[] Arguments(SourceBattleAnimation owner,JsonElement array)
    {var args=Field<short[]>(owner,"_animationArgs");var native=N(array);Assert.AreEqual(8,native.Length);for(int i=0;i<8;i++)args[i]=(short)native[i];for(int i=8;i<args.Length;i++)args[i]=(short)(0x5a00+i);return args.Skip(8).ToArray();}
    private static SourceBattleAnimation.VisualSprite Sprite(SourceBattleAnimation owner,SourceAnimationScript full,int kind,int priority,int slot=4)
    {var type=typeof(SourceBattleAnimation).GetNestedType("ConfusionKissSprite",BindingFlags.NonPublic)!;return(SourceBattleAnimation.VisualSprite)Activator.CreateInstance(type,BindingFlags.Public|Private,null,[slot,priority,full.SpriteRecipes[Templates[kind]],owner],null)!;}
    private static uint RandomState(SourceBattleAnimation owner,uint seed)=>Field<object?>(owner,"_visualRandom") is SourceVisualRandom rng?Field<uint>(rng,"_state"):seed;
    private static void Step(SourceBattleAnimation.VisualSprite sprite,SourceBattleAnimation owner){if(sprite.Finished)return;sprite.Step(owner);if(!sprite.Finished)sprite.AdvanceAnimation(owner);}
    private static void AudioRow(Audio audio,JsonElement row,string why)
    {
        var expected=row.GetProperty("events").EnumerateArray().Where(e=>e[0].GetInt32() is 7 or 8).ToArray();Assert.AreEqual(expected.Length,audio.Cues.Count,why+" audio count");
        for(int i=0;i<expected.Length;i++){var e=expected[i];var a=audio.Cues[i];Assert.AreEqual("SE_M_CONFUSE_RAY",a.Id,why+" sound");Assert.AreEqual(I(row,"frame"),a.Frame,why+" audio frame");Assert.AreEqual(e[0].GetInt32()==7,a.ApplyPanning,why+" native playback API");if(a.ApplyPanning)Assert.AreEqual(e[2].GetInt32(),a.Pan,why+" audio pan");}
        Assert.AreEqual(0,audio.Pans.Count,why+" custom-pan task must not control active sound tokens");audio.Cues.Clear();
    }
    private static void SpriteRow(SourceBattleAnimation owner,SourceBattleAnimation.VisualSprite sprite,JsonElement source,JsonElement row,int kind,string why)
    {
        var f=sprite.Frame;Assert.AreEqual(I(row,"alive")==0,sprite.Finished,why+" life");CollectionAssert.AreEqual(N(row.GetProperty("position")),new[]{f.Position.X,f.Position.Y,f.Offset.X,f.Offset.Y},why+" position");Assert.AreEqual(I(row,"tile"),f.Tile,why+" tile");Assert.AreEqual(I(row,"visible")!=0,f.Visible,why+" visibility");Assert.AreEqual(I(row,"priority"),f.OamPriority,why+" OAM priority");
        bool nonAffine=(I(row,"affineMode")&1)==0;int matrix=I(row,"oamMatrixNum");Assert.AreEqual(nonAffine&&(matrix&8)!=0,f.HFlip,why+" rendered H flip");Assert.AreEqual(nonAffine&&(matrix&16)!=0,f.VFlip,why+" rendered V flip");Assert.AreEqual(!nonAffine,f.Affine,why+" affine mode");Assert.AreEqual(I(row,"objMode")==1,f.Blend,why+" OBJ blend");
        int expected=I(row,"subpriority");if(kind==2){int target=I(source,"target");int[] oracle=[30,20,28,18];expected+=Battlers(source.GetProperty("doubles").GetBoolean())[target].Subpriority-oracle[target];}Assert.AreEqual(expected,f.Subpriority,why+" subpriority");
        var affine=Field<SourceSpriteAffineProgram?>(sprite,"_affine");if(affine is not null){Assert.AreEqual(I(row,"xScale"),affine.XScale,why);Assert.AreEqual(I(row,"yScale"),affine.YScale,why);Assert.AreEqual(I(row,"rotation"),affine.Rotation,why);Assert.AreEqual(I(row,"affineEnded")!=0,affine.Ended,why);}
        var snapshot=owner.Snapshot();int[] registers=N(row.GetProperty("registers"));Assert.AreEqual(registers[1]&255,snapshot.AlphaA,why+" EVA");Assert.AreEqual(registers[1]>>8,snapshot.AlphaB,why+" EVB");Assert.AreEqual((registers[0]&64)!=0,snapshot.BlendEnabled,why+" blend enable");
        Assert.AreEqual(row.GetProperty("rng").GetUInt32(),RandomState(owner,source.GetProperty("seed").GetUInt32()),why+" RNG");Assert.AreEqual(I(row,"customPan"),Field<int>(owner,"_customPan"),why+" custom pan");
        CollectionAssert.AreEqual(N(row.GetProperty("args")).Select(v=>(short)v).ToArray(),Field<short[]>(owner,"_animationArgs").Take(8).ToArray(),why+" shared native args");
    }
    [TestMethod]
    public void OriginalCallbacksPlayersAudioRngAndDestructionMatchAllSourceRows()
    {
        using var fixture=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0,rows=0;
        foreach(var source in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            var full=resources.Resolve(I(source,"root")).Script;var audio=new Audio();var owner=Owner(full,source,audio);var extension=Arguments(owner,source.GetProperty("initialArgs"));int kind=I(source,"kindId");var sprite=Sprite(owner,full,kind,I(source,"initialSubpriority"));
            foreach(var row in source.GetProperty("rows").EnumerateArray()){int frame=I(row,"frame");Set(owner,"_frame",frame);if(frame>0)Step(sprite,owner);string why=source.GetProperty("id").GetString()+" frame "+frame;SpriteRow(owner,sprite,source,row,kind,why);AudioRow(audio,row,why);CollectionAssert.AreEqual(extension,Field<short[]>(owner,"_animationArgs").Skip(8).ToArray(),why+" extended registers");rows++;}
            Assert.IsTrue(sprite.Finished);owner.Cancel();cases++;
        }
        Assert.AreEqual(fixture.RootElement.GetProperty("profile").GetString()=="full"?149:32,cases);Assert.AreEqual(fixture.RootElement.GetProperty("profile").GetString()=="full"?11661:2344,rows);
    }
    private static SourceAnimationCommand PanCommand(int[] a)=>C("createvisualtask","SoundTask_AdjustPanningVar","2",Text(a[0]),Text(a[1]),Text(a[2]),Text(a[3]));
    [TestMethod]
    public void CustomPanAndSharedTagPaletteFadeMatchNativeDelayAndFinishingOrder()
    {
        using var fixture=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0,rows=0;
        foreach(var source in fixture.RootElement.GetProperty("taskCases").EnumerateArray())
        {
            var full=resources.Resolve(109).Script;var audio=new Audio();var owner=Owner(full,source,audio);int kind=I(source,"kindId");int[] a=N(source.GetProperty("initialArgs"));var extension=Arguments(owner,source.GetProperty("initialArgs"));var tags=Field<int[]>(owner,"_spritePaletteTags");tags[4]=10013;var banks=Field<ushort[][]>(owner,"_spritePaletteBanks");var unfaded=Field<ushort[][]>(owner,"_spritePaletteUnfadedBanks");for(int i=0;i<16;i++)banks[4][i]=unfaded[4][i]=(ushort)(((i+3)&31)|(((i*7+5)&31)<<5)|(((i*11+9)&31)<<10));var original=(ushort[])unfaded[4].Clone();
            var command=kind==7?PanCommand(a):C("createvisualtask","AnimTask_BlendColorCycleByTag","2","ANIM_TAG_YELLOW_BALL",Text(a[1]),Text(a[2]),Text(a[3]),Text(a[4]),Text(a[5]));Assert.AreEqual(true,Invoke(owner,"TryCreateConfusionKissTask",command));
            foreach(var row in source.GetProperty("rows").EnumerateArray())
            {int frame=I(row,"frame");string why=source.GetProperty("id").GetString()+" frame "+frame;if(frame>0){if(Field<object?>(owner,"_fade") is {} fade)Invoke(fade,"Update",owner);Invoke(owner,"RunSourceTasks");}Assert.AreEqual(I(row,"alive"),Field<IEnumerable>(owner,"_tasks").Cast<object>().Count(),why+" task life");Assert.AreEqual(I(row,"customPan"),Field<int>(owner,"_customPan"),why+" published pan");Assert.AreEqual(row.GetProperty("fade")[0].GetInt32()!=0,owner.PaletteFadeActive,why+" normal fade");CollectionAssert.AreEqual(N(row.GetProperty("palette")).Select(v=>(ushort)v).ToArray(),banks[4],why+" faded tagged palette");CollectionAssert.AreEqual(original,unfaded[4],why+" reusable unfaded palette");CollectionAssert.AreEqual(extension,Field<short[]>(owner,"_animationArgs").Skip(8).ToArray(),why);Assert.AreEqual(0,audio.Cues.Count,why);Assert.AreEqual(0,audio.Pans.Count,why);rows++;}
            owner.Cancel();cases++;
        }
        Assert.AreEqual(fixture.RootElement.GetProperty("profile").GetString()=="full"?35:8,cases);Assert.AreEqual(fixture.RootElement.GetProperty("profile").GetString()=="full"?3234:624,rows);
    }
    [TestMethod]
    public void LovelyKissFourHeartsConsumeSharedSeedInSpriteSlotOrder()
    {
        using var fixture=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0,rows=0;
        foreach(var source in fixture.RootElement.GetProperty("groupCases").EnumerateArray())
        {
            var full=resources.Resolve(142).Script;var owner=Owner(full,source,new Audio());var sprites=new List<SourceBattleAnimation.VisualSprite>();var args=Field<short[]>(owner,"_animationArgs");foreach(var recipe in source.GetProperty("recipes").EnumerateArray()){args[0]=(short)recipe[0].GetInt32();args[1]=(short)recipe[1].GetInt32();sprites.Add(Sprite(owner,full,3,18,4+sprites.Count));}
            foreach(var row in source.GetProperty("rows").EnumerateArray())
            {int frame=I(row,"frame");string why=source.GetProperty("id").GetString()+" frame "+frame;if(frame>0)foreach(var s in sprites)Step(s,owner);Assert.AreEqual(row.GetProperty("rng").GetUInt32(),RandomState(owner,source.GetProperty("seed").GetUInt32()),why+" RNG order");Assert.AreEqual(I(row,"visualCount"),sprites.Count(s=>!s.Finished),why);foreach(var expected in row.GetProperty("sprites").EnumerateArray()){var s=sprites[I(expected,"slot")-4];var f=s.Frame;Assert.AreEqual(I(expected,"alive")==0,s.Finished,why);CollectionAssert.AreEqual(N(expected.GetProperty("position")),new[]{f.Position.X,f.Position.Y,f.Offset.X,f.Offset.Y},why);Assert.AreEqual(I(expected,"visible")!=0,f.Visible,why);}rows++;}
            owner.Cancel();cases++;
        }
        Assert.AreEqual(fixture.RootElement.GetProperty("profile").GetString()=="full"?18:4,cases);Assert.AreEqual(fixture.RootElement.GetProperty("profile").GetString()=="full"?1008:224,rows);
    }
    [TestMethod]
    public void ConfuseRayConsumesPreTaskCustomPanAndPlainPlaySEKeepsExistingChannelPan()
    {
        using var fixture=Vectors();using var resources=new SourceAnimationResources(Folder);int cases=0,rows=0,panned=0,plain=0;
        foreach(var source in fixture.RootElement.GetProperty("integratedCases").EnumerateArray())
        {
            var full=resources.Resolve(109).Script;var audio=new Audio();var owner=Owner(full,source,audio);Assert.AreEqual(true,Invoke(owner,"TryCreateConfusionKissTask",PanCommand(N(source.GetProperty("panArgs")))));Arguments(owner,source.GetProperty("initialArgs"));var sprite=Sprite(owner,full,0,I(source,"initialSubpriority"));
            foreach(var row in source.GetProperty("rows").EnumerateArray())
            {int frame=I(row,"frame");string why=source.GetProperty("id").GetString()+" frame "+frame;Set(owner,"_frame",frame);Assert.AreEqual(I(row,"customPanBeforeSprites"),Field<int>(owner,"_customPan"),why+" AnimateSprites before RunTasks");if(frame>0){Step(sprite,owner);Invoke(owner,"RunSourceTasks");}SpriteRow(owner,sprite,source,row,0,why);panned+=audio.Cues.Count(c=>c.ApplyPanning);plain+=audio.Cues.Count(c=>!c.ApplyPanning);AudioRow(audio,row,why);Assert.AreEqual(I(row,"visualCount"),Field<IEnumerable>(owner,"_tasks").Cast<object>().Count()+(sprite.Finished?0:1),why);rows++;}
            owner.Cancel();cases++;
        }
        Assert.AreEqual(fixture.RootElement.GetProperty("profile").GetString()=="full"?10:3,cases);Assert.AreEqual(fixture.RootElement.GetProperty("profile").GetString()=="full"?770:186,rows);Assert.IsTrue(panned>0);Assert.IsTrue(plain>0);
    }
    [TestMethod][DataRow(109)][DataRow(142)][DataRow(186)][DataRow(193)]
    public void CompleteRootsComposeOpponentsAndSelectedAlliesWithExactLifecycle(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var bodies=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());assets.TryAdd("impact",new(32,32,new ushort[1024]));foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),bodies,doubles,audio,spriteScene:scene,spriteMaterials:resolved.Assets);var replay=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),bodies,doubles,new Audio(),spriteMaterials:resolved.Assets);
            var seen=new HashSet<SourceBattleAnimation.VisualSprite>();var counts=new Dictionary<string,int>();bool pixels=false,ghost=false,paletteChanged=false;int ticks=0;
            while(runtime.Active&&ticks++<800){runtime.Tick();replay.Tick();var f=runtime.Snapshot();var other=replay.Snapshot();CollectionAssert.AreEqual(f.AnimationArguments,other.AnimationArguments);Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(other),"complete deterministic snapshot");foreach(var s in scene.Sprites)if(seen.Add(s))counts[s.Frame.Template]=counts.GetValueOrDefault(s.Frame.Template)+1;if(f.Sprites.Length>0&&ticks%11==0)pixels|=!composer.ComposeField(f).SequenceEqual(composer.ComposeField(f with{Sprites=[]}));ghost|=f.MoveBackground?.Id is not null;if(move==109){int slot=Array.IndexOf(Field<int[]>(runtime,"_spritePaletteTags"),10013);if(slot!=255&&slot>=0)paletteChanged|=!Field<ushort[][]>(runtime,"_spritePaletteBanks")[slot].SequenceEqual(Field<ushort[][]>(runtime,"_spritePaletteUnfadedBanks")[slot]);}}
            Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.IsTrue(pixels);Assert.IsTrue(audio.Cues.Count>0);Assert.AreEqual(0,audio.Pans.Count,"Custom panning only affects newly triggered callback sounds.");Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.HasResidualSprites);Assert.IsFalse(runtime.PaletteFadeActive);var last=runtime.Snapshot();Assert.IsFalse(last.BlendEnabled);Assert.AreEqual(0,last.BackgroundSlots.Length);Assert.IsNull(last.MoveBackground?.Id);Assert.IsTrue(last.Offsets.All(o=>o==default));
            if(move==109){Assert.AreEqual(1,counts[Templates[0]]);Assert.AreEqual(1,counts[Templates[1]]);Assert.IsTrue(ghost);Assert.IsTrue(paletteChanged);}
            if(move==142){Assert.AreEqual(1,counts[Templates[2]]);Assert.AreEqual(4,counts[Templates[3]]);}
            if(move==186){Assert.AreEqual(1,counts[Templates[4]]);Assert.AreEqual(6,counts[Templates[5]]);}
            if(move==193)Assert.AreEqual(1,counts[Templates[6]]);
        }
    }
    [TestMethod]
    public void CancellationReleasesBackgroundPaletteAndSoundOwnershipAtEveryPhase()
    {
        using var resources=new SourceAnimationResources(Folder);foreach(int move in new[]{109,142,186,193})foreach(int tick in new[]{0,5,25,70,135,200})
        {var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(resources.Resolve(move).Script,Cue(move,0,1),Battlers(true),true,audio,spriteScene:scene);for(int i=0;i<tick&&runtime.Active;i++)runtime.Tick();int sounds=audio.Cues.Count,pans=audio.Pans.Count;runtime.Cancel();runtime.Cancel();Assert.ThrowsExactly<InvalidOperationException>(()=>runtime.Tick());Assert.AreEqual(0,scene.Count);Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.PaletteFadeActive);Assert.IsFalse(runtime.Snapshot().BlendEnabled);Assert.IsNull(runtime.Snapshot().MoveBackground?.Id);Assert.AreEqual(sounds,audio.Cues.Count);Assert.AreEqual(pans,audio.Pans.Count);}
    }
    [TestMethod]
    public void ChangedProgramsMissingSeedSoundAndNarrowingHazardsFailClosed()
    {
        using var resources=new SourceAnimationResources(Folder);foreach(int move in new[]{109,142,186,193})
        {var s=resources.Resolve(move).Script;string template=s.Commands.First(c=>c.Op=="createsprite").Args[0];var recipe=s.SpriteRecipes[template];foreach(var bad in new[]{recipe with{Width=recipe.Width+8},recipe with{Blend=!recipe.Blend},recipe with{LoopStarts=[]}}){var changed=s with{SpriteRecipes=s.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==template?bad:p.Value)};Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(changed,Cue(move,0,1),Battlers(true),true,new Audio()));}}
        var lovely=resources.Resolve(142).Script;Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(lovely,Cue(142,0,1) with{VisualSeed=null},Battlers(true),true,new Audio()));var ray=resources.Resolve(109).Script;Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(ray with{SoundEffects=ray.SoundEffects.Where(p=>p.Key!="SE_M_CONFUSE_RAY").ToDictionary(p=>p.Key,p=>p.Value)},Cue(109,0,1),Battlers(true),true,new Audio()));
        foreach(var bad in new[]{C("createvisualtask","SoundTask_AdjustPanningVar","2","-64","62","4","0"),C("createvisualtask","SoundTask_AdjustPanningVar","2","-64","63","-128","0"),C("createvisualtask","AnimTask_BlendColorCycleByTag","2","ANIM_TAG_YELLOW_BALL","64","6","0","14","351"),C("createvisualtask","AnimTask_BlendColorCycleByTag","2","ANIM_TAG_YELLOW_BALL","-14","6","0","14","351"),C("createsprite",Templates[0],"0","2","-32768","0","1")})
        {var script=Primitive(ray) with{Commands=[bad,C("waitforvisualfinish"),C("end")]};Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(109,0,1),Battlers(true),true,new Audio()));}
    }

    [TestMethod]
    public void SourceProfileRetainsExplicitSemanticAndBoundaryCases()
    {
        using var vectors=SourceVectorFixture.Read("confusion-kiss-motion-source-vectors.json", "python tests/Reference/ConfusionKiss/generate_motion.py content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/confusion-kiss-motion-source-vectors.json");
        Assert.IsTrue(vectors.RootElement.GetProperty("profile").GetString() is "representative" or "full");
        var cases=vectors.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        CollectionAssert.AreEquivalent(Enumerable.Range(0,7).ToArray(), cases.Select(c=>I(c,"kindId")).Distinct().ToArray());
        CollectionAssert.AreEquivalent(new uint[]{0,1,0xffffffff,0x80000000},cases.Where(c=>c.GetProperty("variant").GetString()=="rng-state").Select(c=>c.GetProperty("seed").GetUInt32()).ToArray());
        CollectionAssert.AreEquivalent(new[]{-64,0,63},cases.Where(c=>c.GetProperty("variant").GetString()=="custom-pan-consumption").Select(c=>I(c,"customPan")).ToArray());
        var tasks=vectors.RootElement.GetProperty("taskCases").EnumerateArray().ToArray();
        CollectionAssert.AreEquivalent(new[]{7,8},tasks.Select(c=>I(c,"kindId")).Distinct().ToArray());
        CollectionAssert.AreEquivalent(new[]{-2,1,3},tasks.Where(c=>c.GetProperty("variant").GetString()=="fade-delay-and-final-zero").Select(c=>c.GetProperty("initialArgs")[1].GetInt32()).ToArray());
        foreach(string key in new[]{"groupCases","integratedCases"})Assert.IsTrue(vectors.RootElement.GetProperty(key).GetArrayLength()>0);
        var integrated=vectors.RootElement.GetProperty("integratedCases").EnumerateArray().ToArray();
        foreach(int audioOperation in new[]{7,8})
            Assert.IsTrue(integrated.Any(c=>c.GetProperty("rows").EnumerateArray().Any(r=>r.GetProperty("events").EnumerateArray().Any(e=>e[0].GetInt32()==audioOperation))),"Integrated traces retain both panned and plain source audio dispatch.");
    }
}
