using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FocusSoundAnimationTests
{
    private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound> Cues=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static ushort[] TerrainPalette(SourceAnimationResources resources){var surface=resources.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray();}
    private static AnimationBattler[] Bodies(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body-"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],new[]{56,40,32,64}[i],new[]{64,48,32,56}[i])).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor=0,int target=1)=>new("focus-sound",1,move,new(actor),new(target));
    private static SourceAnimationCommand C(string op,params string[] a)=>new(op,a,1);
    private static SourceAnimationScript Minimal(SourceAnimationScript s,params SourceAnimationCommand[] commands)=>s with{Commands=commands,Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static object? Call(SourceBattleAnimation r,string method,params object[] args)=>typeof(SourceBattleAnimation).GetMethod(method,Hidden)!.Invoke(r,args);
    private static object Field(object obj,string field)=>obj.GetType().GetField(field,Hidden)!.GetValue(obj)!;
    private static object[] Sprites(SourceBattleAnimation r)=>((IEnumerable)Field(r,"_sprites")).Cast<object>().ToArray();
    private static SourceVisualSpriteFrame Sprite(object s)=>(SourceVisualSpriteFrame)s.GetType().GetProperty("Frame")!.GetValue(s)!;
    private static bool Finished(object s)=>(bool)s.GetType().GetProperty("Finished")!.GetValue(s)!;
    private static int[] Row(SourceVisualSpriteFrame f)=>[f.Slot,f.Position.X,f.Position.Y,f.Offset.X,f.Offset.Y,f.Visible?1:0,f.Tile,f.HFlip?1:0,f.VFlip?1:0,f.OamPriority,f.Subpriority,f.MatrixA,f.MatrixB,f.MatrixC,f.MatrixD,f.Affine?1:0,f.DoubleSize?1:0];
    private static object[] Tasks(SourceBattleAnimation r)=>((IEnumerable)Field(r,"_tasks")).Cast<object>().ToArray();
    private static int[] Numbers(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("focus-sound-source-vectors.json","python tests/Game.Presentation.Tests/Reference/generate_focus_sound_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");
    [TestMethod]
    public void EverySelectedWholeSourceCaseMatchesGeometryFramesMatricesPalettesAudioAndCleanup()
    {
        using var resources=new SourceAnimationResources(Folder);using var vectors=Vectors();var root=vectors.RootElement;int cases=0,frames=0,rows=0;
        foreach(var entry in root.GetProperty("cases").EnumerateObject())
        {
            int[] key=entry.Name.Split(':').Select(int.Parse).ToArray();int kind=key[0],actor=key[2],target=key[3],move=new[]{170,170,170,199,173,253,253,253}[kind];bool doubles=key[1]!=0;int slots=doubles?4:2;
            int[] args=Numbers(root.GetProperty("configs")[kind][key[4]]);string template=root.GetProperty("templates")[kind].GetString()!;var resolved=resources.Resolve(move);var audio=new Audio();
            var r=new SourceBattleAnimation(Minimal(resolved.Script,C("end")),Cue(move,actor,target),Bodies(doubles),doubles,audio,TerrainPalette(resources));
            string asset=kind==7?"thin-ring":resolved.Script.SpriteRecipes[template].Asset;string tag="ANIM_TAG_"+asset.ToUpperInvariant().Replace('-','_');Call(r,"LoadSpriteGraphics",tag);
            ushort[] colors=Enumerable.Range(320,16).Select(i=>(ushort)(i*997&32767)).ToArray();Call(r,"LoadSpritePaletteRange",4,0,colors);
            short[] registers=(short[])Field(r,"_animationArgs");for(int i=0;i<args.Length;i++)registers[i]=unchecked((short)args[i]);
            if(kind==3)
            {
                int corner=args[0];for(int c=corner==5?0:corner;c<=(corner==5?4:corner);c++){registers[0]=(short)c;string t=c==0?template:"gLockOnMoveTargetSpriteTemplate";Assert.AreEqual(true,Call(r,"TryCreateFocusSoundSprite",C("createsprite",t,"ANIM_TARGET","2"),slots+(corner==5?c:0),28));}
            }
            else if(kind<7)Assert.AreEqual(true,Call(r,"TryCreateFocusSoundSprite",C("createsprite",template,"ANIM_TARGET","2"),slots,28));
            else Assert.AreEqual(true,Call(r,"TryCreateFocusSoundTask",C("createvisualtask",template,"2",args[0].ToString())));
            foreach(var expected in entry.Value.EnumerateArray())
            {
                int[] state=Numbers(expected.GetProperty("state"));int tick=state[0];string where=entry.Name+" tick "+tick;
                typeof(SourceBattleAnimation).GetField("_frame",Hidden)!.SetValue(r,tick);
                if(tick>=0)
                {
                    Call(r,"AdvanceSourceSprites");if(kind==3&&tick==args[1])registers[7]=-1;
                    foreach(var task in Tasks(r).Where(t=>!Finished(t)))task.GetType().GetMethod("Step")!.Invoke(task,[r]);
                }
                var actual=Sprites(r).Select(Sprite).OrderBy(s=>s.Slot).Select(Row).ToArray();var wanted=expected.GetProperty("sprites").EnumerateArray().Select(Numbers).ToArray();
                Assert.AreEqual(wanted.Length,actual.Length,where);for(int i=0;i<wanted.Length;i++){CollectionAssert.AreEqual(wanted[i],actual[i],where+" sprite "+i);rows++;}
                var tasks=Tasks(r);Assert.AreEqual(state[1],tasks.Count(t=>!Finished(t))+Sprites(r).Length,where+" visual count");Assert.AreEqual(state[2]!=0,tasks.Any(t=>!Finished(t)),where+" task");
                Assert.AreEqual(state[5],(int)registers[1],where+" arg1");Assert.AreEqual(state[6],(int)registers[2],where+" arg2");
                var f=r.Snapshot();if(kind==1){Assert.AreEqual(state[3]&63,f.BlendFirstTargets,where);Assert.AreEqual((state[3]>>8)&63,f.BlendSecondTargets,where);Assert.AreEqual((state[3]&0xc0)==0x40,f.BlendEnabled,where);Assert.AreEqual(state[4]&31,f.AlphaA,where);Assert.AreEqual((state[4]>>8)&31,f.AlphaB,where);}
                if(kind==3)
                {
                    int[] mask=Enumerable.Range(0,32).Where(i=>(state[7]&(1<<i))!=0&&state[8]!=0).ToArray();CollectionAssert.AreEqual(mask,f.PaletteBlends.Select(p=>p.Palette).ToArray(),where+" palette mask");Assert.IsTrue(f.PaletteBlends.All(p=>p.Coefficient==state[8]&&p.Color==32767),where);
                }
                if(kind==7)for(int i=0;i<slots;i++)
                {
                    int at=9+i*6;Assert.AreEqual(state[at]==3,((bool[])Field(r,"_affineMode"))[i],where+" body double-size mode" );Assert.AreEqual(state[at+1],((PixelPoint[])Field(r,"_offsets"))[i].Y,where+" foot offset");
                    CollectionAssert.AreEqual(state.Skip(at+2).Take(4).ToArray(),new[]{((int[])Field(r,"_xScale"))[i],((int[])Field(r,"_matrixB"))[i],((int[])Field(r,"_matrixC"))[i],((int[])Field(r,"_yScale"))[i]},where+" body matrix");
                }
                var unfaded=((ushort[][])Field(r,"_spritePaletteUnfadedBanks"))[4];var faded=f.SpritePaletteSlots[4];CollectionAssert.AreEqual(Numbers(expected.GetProperty("palette")),Enumerable.Range(0,16).SelectMany(i=>new[]{(int)unfaded[i],(int)faded[i]}).ToArray(),where+" exact source normal/faded bank");
                var sounds=expected.GetProperty("sounds").EnumerateArray().Select(Numbers).ToArray();Assert.AreEqual(sounds.Length,audio.Cues.Count,where+" sound count");for(int i=0;i<sounds.Length;i++){Assert.AreEqual(sounds[i][0],resolved.Script.SoundEffects[audio.Cues[i].Id],where);Assert.AreEqual(sounds[i][1],audio.Cues[i].Pan,where);Assert.AreEqual(tick,audio.Cues[i].Frame,where);}audio.Cues.Clear();frames++;
            }
            r.Cancel();cases++;
        }
        var coverage=root.GetProperty("coverage");Assert.AreEqual(coverage.GetProperty("caseCount").GetInt32(),cases);Assert.AreEqual(coverage.GetProperty("frames").GetInt32(),frames);Assert.AreEqual(coverage.GetProperty("spriteRows").GetInt32(),rows);Assert.IsTrue(cases>0&&frames>cases&&rows>0);
    }
    [TestMethod]
    [DataRow(170)] [DataRow(199)] [DataRow(173)] [DataRow(253)]
    public void CompleteRootsComposeEveryFrameAcrossAllSixLayoutsAndRestoreScene(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());assets.TryAdd("impact",new(32,32,new ushort[1024]));foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>i%64 is >4 and <59&&i/64 is >4 and <59?(ushort)0x83e0:(ushort)0).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var cue=Cue(move,actor,actor^1);
            var r=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,audio,TerrainPalette(resources),spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,new Audio(),TerrainPalette(resources),spriteMaterials:assets);byte[] baseline=composer.ComposeField(r.Snapshot());int ticks=0;bool changed=false,particles=false;
            while(r.Active&&ticks++<700){var f=r.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));var rgba=composer.ComposeField(f);Assert.AreEqual(240*112*4,rgba.Length);changed|=!rgba.SequenceEqual(baseline);particles|=f.Sprites.Any(s=>s.Asset is not null);}
            Assert.IsFalse(r.Active);Assert.IsFalse(replay.Active);Assert.AreEqual(0,r.ActiveVisualCount);Assert.AreEqual(0,scene.Count);Assert.IsFalse(r.GraphicsLoaded);Assert.IsTrue(changed&&particles);var last=r.Snapshot();Assert.IsFalse(last.BlendEnabled);Assert.IsNull(last.MoveBackground!.Id);Assert.AreEqual(0,last.BackgroundSlots.Length);Assert.IsTrue(last.Offsets.All(p=>p==default));Assert.AreEqual(0,last.PaletteBlends.Length);CollectionAssert.AreEqual(baseline,composer.ComposeField(last));Assert.IsTrue(audio.Cues.Count>0);
            if(move==199){Assert.AreEqual(20,audio.Cues.Count(x=>x.Id=="SE_M_LOCK_ON"));Assert.AreEqual(1,audio.Cues.Count(x=>x.Id=="SE_M_LEER"));}
        }
    }
    [TestMethod]
    public void NumericMonBgByteSelectorsMatchSourceSymbolsAcrossLayouts()
    {
        using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(170).Script;
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int n in new[]{0,1,2,3,4,255})
        {
            string symbol=n is 0 or 2?"ANIM_ATK_PARTNER":"ANIM_DEF_PARTNER";
            SourceBattleAnimation Make(string selector)=>new(Minimal(script,C("monbg",selector),C("delay","4"),C("clearmonbg",selector),C("delay","4"),C("end")),Cue(170,actor,actor^1),Bodies(doubles),doubles,new Audio(),TerrainPalette(resources));
            var a=Make(n.ToString());var b=Make(symbol);int frames=0;while(a.Active&&frames++<40){var x=a.Tick();var y=b.Tick();CollectionAssert.AreEqual(x.BackgroundSlots,y.BackgroundSlots);CollectionAssert.AreEqual(x.Offsets,y.Offsets);CollectionAssert.AreEqual(x.BattlerVisibility,y.BattlerVisibility);CollectionAssert.AreEqual(x.RestoredObjectSlots,y.RestoredObjectSlots);Assert.AreEqual(x.Active,y.Active);}Assert.IsFalse(a.Active);Assert.IsFalse(b.Active);
        }
        foreach(string value in new[]{"-1","256","unknown"})foreach(string op in new[]{"monbg","clearmonbg"})Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(Minimal(script,C(op,value),C("end")),Cue(170),Bodies(false),false,new Audio(),TerrainPalette(resources)));
    }
    [TestMethod]
    public void EveryCallbackPhaseCanCancelWithoutRetainedSpriteOrPaletteOwners()
    {
        using var resources=new SourceAnimationResources(Folder);
        foreach(int move in new[]{170,199,173,253})foreach(int stop in new[]{0,1,4,16,24,40,48,60,88,90,99,104,120,143,180})
        {var scene=new SourceBattleAnimation.SpriteScene();var r=new SourceBattleAnimation(resources.Resolve(move).Script,Cue(move),Bodies(true),true,new Audio(),TerrainPalette(resources),spriteScene:scene);for(int i=0;i<stop&&r.Active;i++)r.Tick();r.Cancel();r.Cancel();var f=r.Snapshot();Assert.AreEqual(0,r.ActiveVisualCount);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,f.Sprites.Length);Assert.IsFalse(f.BlendEnabled);Assert.IsNull(f.MoveBackground!.Id);Assert.AreEqual(0,f.PaletteBlends.Length);Assert.AreEqual(0,f.SpritePalettes.Count);Assert.IsTrue(f.Offsets.All(p=>p==default));}
    }
    [TestMethod]
    public void RangePaletteWritesRemainDetachedAndPreserveUntouchedEntries()
    {
        using var resources=new SourceAnimationResources(Folder);var s=resources.Resolve(199).Script;var r=new SourceBattleAnimation(Minimal(s,C("end")),Cue(199),Bodies(false),false,new Audio(),TerrainPalette(resources));Call(r,"LoadSpriteGraphics","ANIM_TAG_LOCK_ON");ushort[] normal=Enumerable.Range(0,16).Select(i=>(ushort)(i*971&32767)).ToArray();Call(r,"LoadSpritePaletteRange",4,0,normal);var before=r.Snapshot();Call(r,"BlendSpritePaletteRange",4,1,15,8,(ushort)31);var blended=r.Snapshot();Call(r,"LoadSpritePaletteRange",4,1,new ushort[]{normal[8],normal[9]});var after=r.Snapshot();
        CollectionAssert.AreEqual(normal,before.SpritePaletteSlots[4]);Assert.AreEqual(normal[0],blended.SpritePaletteSlots[4][0]);for(int i=1;i<16;i++)Assert.AreEqual(SourcePaletteMath.Blend(normal[i],8,31),blended.SpritePaletteSlots[4][i]);Assert.AreEqual(normal[8],after.SpritePaletteSlots[4][1]);Assert.AreEqual(normal[9],after.SpritePaletteSlots[4][2]);for(int i=3;i<16;i++)Assert.AreEqual(blended.SpritePaletteSlots[4][i],after.SpritePaletteSlots[4][i]);Assert.AreEqual(normal[8],((ushort[][])Field(r,"_spritePaletteUnfadedBanks"))[4][1]);CollectionAssert.AreEqual(normal,before.SpritePaletteSlots[4]);
        foreach(var args in new object[][]{[3,0,new ushort[]{1}],[16,0,new ushort[]{1}],[4,16,new ushort[]{1}],[4,0,new ushort[]{32768}]})Assert.Throws<TargetInvocationException>(()=>Call(r,"LoadSpritePaletteRange",args));r.Cancel();
    }
    [TestMethod]
    public void MissingMaterialsSoundsProgramsAndCompetingOwnersFailBeforePlayback()
    {
        using var resources=new SourceAnimationResources(Folder);
        foreach(int move in new[]{170,199,173,253}){var s=resources.Resolve(move).Script;Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s with{SpritePalettes=new Dictionary<int,ushort[]>()},Cue(move),Bodies(false),false,new Audio(),TerrainPalette(resources)));}
        var focus=resources.Resolve(170).Script;var sound=resources.Resolve(253).Script;var locking=resources.Resolve(199).Script;
        foreach(var extra in new[]{C("setalpha","8","8"),C("createsprite","gWhiteHaloSpriteTemplate","ANIM_TARGET","2")})Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(focus with{Commands=[..focus.Commands.Take(focus.Commands.Length-1),extra,C("end")]},Cue(170),Bodies(false),false,new Audio(),TerrainPalette(resources)));
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(locking with{SoundEffects=new Dictionary<string,int>()},Cue(199),Bodies(false),false,new Audio(),TerrainPalette(resources)));
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(locking with{Commands=locking.Commands.Where(c=>c.Op!="setarg").ToArray()},Cue(199),Bodies(false),false,new Audio(),TerrainPalette(resources)));
        var signal=locking.Commands.Single(c=>c.Op=="setarg");var noSignal=locking.Commands.Where(c=>c.Op!="setarg").ToArray();
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(locking with{Commands=[..noSignal.Take(noSignal.Length-1),signal,C("end")]},Cue(199),Bodies(false),false,new Audio(),TerrainPalette(resources)));
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(locking with{Commands=[..locking.Commands.Take(locking.Commands.Length-1),C("setarg","7","0"),C("end")]},Cue(199),Bodies(false),false,new Audio(),TerrainPalette(resources)));
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(sound with{Commands=[..Enumerable.Repeat(C("createvisualtask","AnimTask_UproarDistortion","2","0"),17),..sound.Commands]},Cue(253),Bodies(false),false,new Audio(),TerrainPalette(resources)));
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(sound with{TaskAffinePrograms=new Dictionary<string,SourceTaskAffineCommand[]>()},Cue(253),Bodies(false),false,new Audio(),TerrainPalette(resources)));
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(sound with{Commands=[..sound.Commands.Take(sound.Commands.Length-1),C("setalpha","8","8"),C("end")]},Cue(253),Bodies(false),false,new Audio(),TerrainPalette(resources)));
    }
}
