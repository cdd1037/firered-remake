using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class GhostSongAnimationTests
{
    private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound> Cues=[];public readonly List<(int Pan,int Frame)> Pans=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static ushort[] TerrainPalette(SourceAnimationResources resources){var surface=resources.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray();}
    private static AnimationBattler[] Bodies(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body-"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],new[]{56,40,32,64}[i],new[]{64,48,32,56}[i])).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor=0,int target=1)=>new("ghost-song",1,move,new(actor),new(target));
    private static SourceAnimationCommand C(string op,params string[] a)=>new(op,a,1);
    private static SourceAnimationScript Minimal(SourceAnimationScript s,params SourceAnimationCommand[] commands)=>s with{Commands=commands,Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static object? Call(SourceBattleAnimation r,string method,params object[] args)=>typeof(SourceBattleAnimation).GetMethod(method,Hidden)!.Invoke(r,args);
    private static object Field(object obj,string field)=>obj.GetType().GetField(field,Hidden)!.GetValue(obj)!;
    private static object[] Sprites(SourceBattleAnimation r)=>((IEnumerable)Field(r,"_sprites")).Cast<object>().ToArray();
    private static SourceVisualSpriteFrame Sprite(object s)=>(SourceVisualSpriteFrame)s.GetType().GetProperty("Frame")!.GetValue(s)!;
    private static bool Finished(object s)=>(bool)s.GetType().GetProperty("Finished")!.GetValue(s)!;
    private static int[] Row(SourceVisualSpriteFrame f)=>[f.Slot,f.Position.X,f.Position.Y,f.Offset.X,f.Offset.Y,f.Visible?1:0,f.Tile,f.HFlip?1:0,f.VFlip?1:0,f.OamPriority,f.Subpriority,f.MatrixA,f.MatrixB,f.MatrixC,f.MatrixD,f.Affine?1:0,f.DoubleSize?1:0,f.ObjectWindow?1:0];
    private static object[] Tasks(SourceBattleAnimation r)=>((IEnumerable)Field(r,"_tasks")).Cast<object>().ToArray();
    private static int[] Numbers(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("ghost-song-source-vectors.json","python tests/Game.Presentation.Tests/Reference/generate_ghost_song_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");
    private delegate void PaletteRange(int bank,int start,ReadOnlySpan<ushort> colors);
    private static void BackgroundLoad(SourceBattleAnimation r,int bank,int start,ushort[] colors)=>typeof(SourceBattleAnimation).GetMethod("LoadBackgroundPaletteRange",Hidden)!.CreateDelegate<PaletteRange>(r)(bank,start,colors);
    private static object? OptionalField(object obj,string field)=>obj.GetType().GetField(field,Hidden)!.GetValue(obj);
    private static void AdvancePrimitives(SourceBattleAnimation r)
    {
        Call(r,"AdvanceSourceSprites");var fade=OptionalField(r,"_fade");fade?.GetType().GetMethod("Update")!.Invoke(fade,[r]);foreach(var task in Tasks(r).Where(t=>!Finished(t)))task.GetType().GetMethod("Step")!.Invoke(task,[r]);
    }
    [TestMethod]
    public void EverySelectedWholeSourceCaseMatchesAllSpritesRegistersPalettesAndCleanup()
    {
        using var resources=new SourceAnimationResources(Folder);using var vectors=Vectors();var root=vectors.RootElement;int cases=0,frames=0,rows=0;
        foreach(var entry in root.GetProperty("cases").EnumerateObject())
        {
            int[] key=entry.Name.Split(':').Select(int.Parse).ToArray();int kind=key[0],actor=key[2],target=key[3],move=kind<2?195:kind==4?194:174;bool doubles=key[1]!=0;int slots=doubles?4:2;
            int[] args=Numbers(root.GetProperty("configs")[kind][key[4]]);string template=root.GetProperty("templates")[kind].GetString()!;var resolved=resources.Resolve(move);var bodies=Bodies(doubles).Select(b=>b with{SourceVisibility=new(true,(key[5]&(1<<b.Slot.Value))!=0,(key[5]&(1<<b.Slot.Value))!=0)}).ToArray();
            var r=new SourceBattleAnimation(Minimal(resolved.Script,C("end")),Cue(move,actor,target),bodies,doubles,new Audio(),TerrainPalette(resources));
            if(kind<5){string asset=kind==4?"white-shadow":resolved.Script.SpriteRecipes[template].Asset;Call(r,"LoadSpriteGraphics","ANIM_TAG_"+asset.ToUpperInvariant().Replace('-','_'));}
            ushort[] colors=Enumerable.Range(320,16).Select(i=>(ushort)(i*997&32767)).ToArray();Call(r,"LoadSpritePaletteRange",4,0,colors);BackgroundLoad(r,8,0,Enumerable.Range(128,16).Select(i=>(ushort)(i*997&32767)).ToArray());
            short[] registers=(short[])Field(r,"_animationArgs");for(int i=0;i<args.Length;i++)registers[i]=unchecked((short)args[i]);
            if(kind==6&&args.Length>0){((bool[])Field(r,"_affineMode"))[actor]=args[0]==3;((int[])Field(r,"_xScale"))[actor]=args[1];((int[])Field(r,"_matrixB"))[actor]=args[2];((int[])Field(r,"_matrixC"))[actor]=args[3];((int[])Field(r,"_yScale"))[actor]=args[4];((PixelPoint[])Field(r,"_offsets"))[actor]=new(5,-7);}
            if(kind<4)Assert.AreEqual(true,Call(r,"TryCreateGhostSongSprite",C("createsprite",template,"ANIM_TARGET","2"),slots,28));else Assert.AreEqual(true,Call(r,"TryCreateGhostSongTask",C("createvisualtask",template,"5")));
            foreach(var expected in entry.Value.EnumerateArray())
            {
                int[] state=Numbers(expected.GetProperty("state"));int tick=state[0];string where=entry.Name+" tick "+tick;if(tick>=0)AdvancePrimitives(r);
                var actual=Sprites(r).Select(Sprite).OrderBy(s=>s.Slot).Select(Row).ToArray();var wanted=expected.GetProperty("sprites").EnumerateArray().Select(Numbers).ToArray();Assert.AreEqual(wanted.Length,actual.Length,where);for(int i=0;i<wanted.Length;i++){CollectionAssert.AreEqual(wanted[i],actual[i],where+" sprite "+i);rows++;}
                Assert.AreEqual(state[1],Tasks(r).Count(t=>!Finished(t))+Sprites(r).Count(s=>(bool)s.GetType().GetProperty("Counted")!.GetValue(s)!),where+" visual count");Assert.AreEqual(state[2]!=0,Tasks(r).Any(t=>!Finished(t)),where+" task");
                var f=r.Snapshot();Assert.AreEqual((state[3]&0xc0)==0x40,f.BlendEnabled,where+" alpha enable");
                if(kind>=2&&(kind!=2||state[3]!=0||Sprites(r).Length==0)){Assert.AreEqual(state[3]&63,f.BlendFirstTargets,where);Assert.AreEqual((state[3]>>8)&63,f.BlendSecondTargets,where);Assert.AreEqual(state[4]&31,f.AlphaA,where);Assert.AreEqual((state[4]>>8)&31,f.AlphaB,where);}
                if(kind==5){Assert.AreEqual((ushort)state[5],f.Window0!.Horizontal,where);Assert.AreEqual((ushort)state[6],f.Window0.Vertical,where);Assert.AreEqual(state[7]&63,f.Window0.InsideMask,where);Assert.AreEqual(state[8]&63,f.Window0.OutsideMask,where);Assert.AreEqual((state[3]&0xc0)==0xc0,f.Brightness is not null,where);if(f.Brightness is {} brightness){Assert.AreEqual(SourceBrightnessMode.Darken,brightness.Mode,where);Assert.AreEqual(state[9],brightness.Coefficient,where);}Assert.AreEqual(state[14]!=0,r.PaletteFadeActive,where);}
                if(kind==6){Assert.AreEqual((state[13]&0x8000)!=0,f.ObjectWindow?.Enabled==true,where);if(f.ObjectWindow is {} window){Assert.AreEqual((state[8]>>8)&63,window.InsideMask,where);Assert.AreEqual(state[8]&63,window.OutsideMask,where);}if(f.Overlay is {} overlay){Assert.AreEqual(state[10],(int)unchecked((ushort)overlay.ScrollX),where);Assert.AreEqual(state[11],(int)unchecked((ushort)overlay.ScrollY),where);}else Assert.AreEqual(0,state[2],where);for(int i=0;i<slots;i++)Assert.AreEqual(state[15+i],Call(r,"BattlerObjectPriority",i),where+" body priority "+i);Assert.AreEqual(state[12],f.BattlerBackgroundPriorities[0],where);}
                var normal=((ushort[][])Field(r,"_spritePaletteUnfadedBanks"))[4];var unfaded=(Dictionary<int,ushort>)Field(r,"_unfadedBackgroundEntries");var faded=f.BackgroundPaletteEntries.ToDictionary(x=>x.Index,x=>x.Color);int[] palette=Enumerable.Range(0,16).SelectMany(i=>new[]{(int)normal[i],(int)f.SpritePaletteSlots[4][i]}).Concat(Enumerable.Range(128,16).SelectMany(i=>new[]{(int)unfaded[i],(int)faded[i]})).ToArray();CollectionAssert.AreEqual(Numbers(expected.GetProperty("palette")),palette,where+" all source buffer entries");frames++;
            }
            r.Cancel();cases++;
        }
        var coverage=root.GetProperty("coverage");Assert.AreEqual(coverage.GetProperty("caseCount").GetInt32(),cases);Assert.AreEqual(coverage.GetProperty("frames").GetInt32(),frames);Assert.AreEqual(coverage.GetProperty("spriteRows").GetInt32(),rows);Assert.IsTrue(cases>0&&frames>cases&&rows>0);
    }
    [TestMethod]
    [DataRow(194,0)] [DataRow(195,0)] [DataRow(174,0)] [DataRow(174,1)]
    public void CompleteRootsComposeEveryFrameAcrossAllSixLayoutsAndRestoreScene(int move,int turn)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());assets.TryAdd("impact",new(32,32,new ushort[1024]));foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>i%64 is >4 and <59&&i/64 is >4 and <59?(ushort)0x83e0:(ushort)0).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var cue=Cue(move,actor,actor^1) with{MoveTurn=(byte)turn};
            var r=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,audio,TerrainPalette(resources),spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,new Audio(),TerrainPalette(resources),spriteMaterials:assets);byte[] baseline=composer.ComposeField(r.Snapshot());int ticks=0;bool changed=false,particles=false;
            while(r.Active&&ticks++<700){var f=r.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));var rgba=composer.ComposeField(f);Assert.AreEqual(240*112*4,rgba.Length);changed|=!rgba.SequenceEqual(baseline);particles|=f.Sprites.Any(s=>s.Asset is not null);}
            Assert.IsFalse(r.Active);Assert.IsFalse(replay.Active);Assert.AreEqual(0,r.ActiveVisualCount);Assert.AreEqual(0,scene.Count);Assert.IsFalse(r.GraphicsLoaded);Assert.IsTrue(changed);if(move!=174||turn==0)Assert.IsTrue(particles);var last=r.Snapshot();Assert.IsFalse(last.BlendEnabled);Assert.IsFalse(last.Window0?.Enabled==true);Assert.IsNull(last.ObjectWindow);Assert.IsNull(last.Brightness);Assert.IsNull(last.Overlay);CollectionAssert.AreEqual(new[]{2,2,2,2},last.BattlerObjectPriorities);Assert.IsNull(last.MoveBackground!.Id);Assert.AreEqual(0,last.BackgroundSlots.Length);Assert.IsTrue(last.Offsets.All(p=>p==default));Assert.AreEqual(0,last.PaletteBlends.Length);CollectionAssert.AreEqual(baseline,composer.ComposeField(last));Assert.IsTrue(audio.Cues.Count>0);
            if(move==195){var song=audio.Cues.Single();Assert.AreEqual("SE_M_PERISH_SONG",song.Id);bool player=(actor&1)==0;Assert.AreEqual(player?-64:63,song.Pan);var expected=Enumerable.Range(0,player?64:63).Select(i=>(player?Math.Min(63,-64+2*(i+1)):Math.Max(-63,63-2*(i+1)),song.Frame+i)).ToArray();CollectionAssert.AreEqual(expected,audio.Pans.ToArray());}
        }
    }
    [TestMethod]
    public void PartialBackgroundLoadAndGreyscaleReadUnfadedDataAndKeepEarlierSnapshotsDetached()
    {
        using var resources=new SourceAnimationResources(Folder);var r=new SourceBattleAnimation(Minimal(resources.Resolve(174).Script,C("end")),Cue(174),Bodies(false),false,new Audio(),TerrainPalette(resources));
        ushort[] normal=Enumerable.Range(0,16).Select(i=>(ushort)(i*997&32767)).ToArray();BackgroundLoad(r,8,0,normal);Call(r,"BlendPalettes",1u<<8,8,(ushort)31);var first=r.Snapshot();
        Call(r,"BeginOverlayMaterial","curse");BackgroundLoad(r,8,1,new ushort[]{32767});var loaded=r.Snapshot();var entries=loaded.BackgroundPaletteEntries.ToDictionary(p=>p.Index,p=>p.Color);
        Assert.AreEqual((ushort)32767,entries[129]);for(int i=0;i<16;i++)if(i!=1)Assert.AreEqual(SourcePaletteMath.Blend(normal[i],8,31),entries[128+i]);
        Call(r,"BlendPalettes",1u<<8,4,(ushort)992);var faded=r.Snapshot();for(int i=0;i<16;i++)Assert.AreEqual(SourcePaletteMath.Blend(i==1?(ushort)32767:normal[i],4,992),faded.BackgroundPaletteEntries.Single(p=>p.Index==128+i).Color);
        for(int i=0;i<16;i++)Assert.AreEqual(SourcePaletteMath.Blend(normal[i],8,31),first.BackgroundPaletteEntries.Single(p=>p.Index==128+i).Color);Assert.AreEqual((ushort)32767,loaded.BackgroundPaletteEntries.Single(p=>p.Index==129).Color);
        Call(r,"LoadSpritePaletteRange",4,0,normal);Call(r,"BlendSpritePaletteRange",4,0,16,16,(ushort)31);Call(r,"SetSpriteGreyscaleOrOriginal",4,false);var grey=r.Snapshot();for(int i=0;i<16;i++){int c=((normal[i]&31)+((normal[i]>>5)&31)+(normal[i]>>10))/3;Assert.AreEqual((ushort)(c|(c<<5)|(c<<10)),grey.SpritePaletteSlots[4][i]);}
        Call(r,"SetSpriteGreyscaleOrOriginal",4,true);CollectionAssert.AreEqual(normal,r.Snapshot().SpritePaletteSlots[4]);Assert.AreNotEqual(grey.SpritePaletteSlots[4][1],r.Snapshot().SpritePaletteSlots[4][1]);
        foreach((int bank,int start,ushort[] colors) in new (int,int,ushort[])[]{(-1,0,[1]),(16,0,[1]),(8,16,[1])})Assert.Throws<ArgumentOutOfRangeException>(()=>BackgroundLoad(r,bank,start,colors));Assert.Throws<InvalidDataException>(()=>BackgroundLoad(r,8,0,[32768]));
        r.Cancel();Assert.AreEqual(0,r.Snapshot().BackgroundPaletteEntries.Length);
    }
    [TestMethod]
    public void CursePartnerPriorityWritesLatchAfterTasksAndRestoreWithoutChangingOtherSlots()
    {
        using var resources=new SourceAnimationResources(Folder);var r=new SourceBattleAnimation(Minimal(resources.Resolve(174).Script,C("delay","100"),C("end")),Cue(174),Bodies(true),true,new Audio(),TerrainPalette(resources));
        Call(r,"SortSourceSprites",null!,false,null!);var initial=r.Snapshot();Call(r,"TryCreateGhostSongTask",C("createvisualtask","AnimTask_DrawFallingWhiteLinesOnAttacker","5"));
        CollectionAssert.AreEqual(new[]{2,2,2,2},r.Snapshot().BattlerObjectPriorities);Assert.AreEqual(1,Call(r,"BattlerObjectPriority",2));Call(r,"SortSourceSprites",null!,false,null!);CollectionAssert.AreEqual(new[]{2,2,1,2},r.Snapshot().BattlerObjectPriorities);
        for(int i=0;i<64;i++)AdvancePrimitives(r);Assert.AreEqual(2,Call(r,"BattlerObjectPriority",2));CollectionAssert.AreEqual(new[]{2,2,1,2},r.Snapshot().BattlerObjectPriorities);Call(r,"SortSourceSprites",null!,false,null!);CollectionAssert.AreEqual(new[]{2,2,2,2},r.Snapshot().BattlerObjectPriorities);CollectionAssert.AreEqual(new[]{2,2,2,2},initial.BattlerObjectPriorities);r.Cancel();
    }
    [TestMethod]
    public void LatchedBattlerPrioritySelectsLayerBeforeAlphaWithIndependentPixelExpectation()
    {
        var bodies=Bodies(false);var assets=new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,Enumerable.Repeat((ushort)0xfc00,240*112).ToArray())},{"impact",new(32,32,new ushort[1024])},{"body-0",new(64,64,Enumerable.Repeat((ushort)0x801f,4096).ToArray())},{"body-1",new(64,64,new ushort[4096])}};
        var overlayPalette=new ushort[16];overlayPalette[1]=992;var map=new byte[2048];for(int i=1;i<map.Length;i+=2)map[i]=128;
        var composer=new SourceBattleFrameComposer(assets,bodies,overlays:new Dictionary<string,SourceOverlayAsset>{{"test",new(Enumerable.Repeat((byte)0x11,32).ToArray(),map,overlayPalette)}});
        var frame=new SourceAnimationFrame(0,true,new PixelPoint[4],[],8,8,[],[]){Overlay=new("test",0,0),BattlerBackgroundPriorities=[1,1],BattlerObjectPriorities=[2,2,2,2],BlendEnabled=true,BlendFirstTargets=2,BlendSecondTargets=16};
        int x=bodies[0].Center(false).X,y=Math.Min(100,bodies[0].Center(false).Y);int at=(y*240+x)*4;CollectionAssert.AreEqual(new byte[]{123,123,0,255},composer.ComposeField(frame).AsSpan(at,4).ToArray());
        CollectionAssert.AreEqual(new byte[]{255,0,0,255},composer.ComposeField(frame with{BattlerObjectPriorities=[0,2,2,2]}).AsSpan(at,4).ToArray());
        CollectionAssert.AreEqual(new byte[]{123,123,0,255},composer.ComposeField(frame).AsSpan(at,4).ToArray());
    }
    [TestMethod]
    public void EveryOwnedPhaseCanCancelAndInvalidDependenciesOrCompetingRegisterOwnersFailBeforePlayback()
    {
        using var resources=new SourceAnimationResources(Folder);
        foreach(int move in new[]{194,195,174})foreach(byte turn in new byte[]{0,1})foreach(int stop in new[]{0,1,16,24,48,60,100,120,160,200,280})
        {var scene=new SourceBattleAnimation.SpriteScene();var r=new SourceBattleAnimation(resources.Resolve(move).Script,Cue(move) with{MoveTurn=turn},Bodies(true),true,new Audio(),TerrainPalette(resources),spriteScene:scene);for(int i=0;i<stop&&r.Active;i++)r.Tick();r.Cancel();r.Cancel();var f=r.Snapshot();Assert.AreEqual(0,r.ActiveVisualCount);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,f.Sprites.Length);Assert.IsFalse(f.BlendEnabled);Assert.IsNull(f.Overlay);Assert.IsNull(f.Window0);Assert.IsNull(f.ObjectWindow);Assert.IsNull(f.Brightness);Assert.IsNull(f.MoveBackground!.Id);Assert.AreEqual(0,f.PaletteBlends.Length);Assert.AreEqual(0,f.SpritePalettes.Count);CollectionAssert.AreEqual(new[]{2,2,2,2},f.BattlerObjectPriorities);}
        void Reject(SourceAnimationScript s,int move)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(move) with{MoveTurn=0},Bodies(false),false,new Audio(),TerrainPalette(resources)));
        foreach(int move in new[]{194,195,174}){var s=resources.Resolve(move).Script;Reject(s with{SpriteRecipes=new Dictionary<string,SourceSpriteRecipe>()},move);Reject(s with{SoundEffects=new Dictionary<string,int>()},move);Reject(s with{SpritePalettes=new Dictionary<int,ushort[]>()},move);}
        var song=resources.Resolve(195).Script;Reject(song with{Commands=[..song.Commands.Take(song.Commands.Length-1),C("setalpha","8","8"),C("end")]},195);
        var destiny=resources.Resolve(194).Script;Reject(destiny with{SpriteDependencies=[]},194);Reject(destiny with{Commands=[..Enumerable.Repeat(C("createvisualtask","AnimTask_DestinyBondWhiteShadow","5","0","48"),17),..destiny.Commands]},194);
        var curse=resources.Resolve(174).Script;Reject(curse with{OverlayDependencies=[]},174);Reject(curse with{OverlayPalettes=new Dictionary<string,ushort[]>()},174);Reject(curse with{Commands=curse.Commands.Where(c=>c.Op!="waitforvisualfinish").ToArray()},174);
    }
}
