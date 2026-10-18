using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class RecoveryVisualSourceOracleTests
{
    private static readonly string[] Templates=["gSleepLetterZSpriteTemplate","gGreenStarSpriteTemplate","gMoonSpriteTemplate","gMoonlightSparkleSpriteTemplate","gAuroraBeamRingSpriteTemplate"];
    private static readonly string[] Tags=["ANIM_TAG_LETTER_Z","ANIM_TAG_GREEN_STAR","ANIM_TAG_MOON","ANIM_TAG_GREEN_SPARKLE","ANIM_TAG_RAINBOW_RINGS"];
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound> Cues=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects(){}}
    private static JsonDocument Vectors(string kind)=>JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,$"tests/Game.Presentation.Tests/Fixtures/recovery-{kind}-source-vectors.json")));
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static string Text(int n)=>n.ToString(CultureInfo.InvariantCulture);
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static T Field<T>(object o,string name){var f=o.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic);Assert.IsNotNull(f,name);return(T)f.GetValue(o)!;}
    private static object? Invoke(object o,string name,params object[] args){var m=o.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);Assert.IsNotNull(m,name);return m.Invoke(o,args);}
    private static object[] Tasks(SourceBattleAnimation r)=>Field<IEnumerable>(r,"_tasks").Cast<object>().ToArray();
    private static AnimationBattler[] Battlers(int mask=15)=>Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64){Visible=(mask&(1<<i))!=0}).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor=0,int target=1)=>new("recovery-source",1,move,new(actor),new(target)){VisualSeed=0x12345678};
    private static SourceAnimationScript Primitive(int move,IEnumerable<SourceAnimationCommand> commands)=>AnimationCompositionFixture.Script(move) with{Commands=[..commands,C("waitforvisualfinish"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static SourceAnimationFrame Begin(SourceBattleAnimation r,int loads=0){for(int i=0;i<loads*3;i++)r.Tick();Invoke(r,"RunScript");return r.Snapshot();}
    private static ushort Original(int i)=>(ushort)(((i+3)&31)|(((i*7+5)&31)<<5)|(((i*11+9)&31)<<10));

    [TestMethod]
    public void EverySourceInitializerCallbackFrameRngAndRawChildLifetimeMatchesNativePlayers()
    {
        using var document=Vectors("motion");int cases=0,rows=0;
        foreach(var c in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            int kind=c.GetProperty("kind").GetInt32(),type=kind==5?1:kind,actor=c.GetProperty("attacker").GetInt32(),target=c.GetProperty("target").GetInt32(),signal=c.GetProperty("signalFrame").GetInt32();bool doubles=c.GetProperty("doubles").GetBoolean();int move=type==0?156:type==1?234:type==4?62:236;
            var battlers=Battlers().Take(doubles?4:2).ToArray();int relative=actor==2?1:0,modifier=battlers[relative==0?actor:target].Subpriority-28;
            string[] args=type switch{0=>["4","-10","16","0","0"],1=>["30","640"],2=>["120","56"],3=>["-12","0"],_=>["20","0","0","0","17"]};
            List<SourceAnimationCommand> commands=[C("loadspritegfx",Tags[type])];if(type==4)commands.Add(C("setarg","7",signal==0?"-1":"0"));
            for(int i=0;i<(kind==5?15:1);i++){commands.Add(C("createsprite",[Templates[type],Text(relative),Text(modifier),..args]));if(kind==5&&i<14)commands.Add(C("delay","5"));}
            var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(Primitive(move,commands),Cue(move,actor,target),battlers,doubles,new Audio(),spriteScene:scene);Begin(runtime,1);
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int frame=row.GetProperty("frame").GetInt32();if(frame!=0){if(type==4&&frame==signal)Field<short[]>(runtime,"_animationArgs")[7]=-1;if(type is 2 or 3&&frame==16)foreach(var s in scene.Sprites)s.GetType().GetProperty("DestroySignaled")!.SetValue(s,true);runtime.Tick();}
                string at=$"kind={kind} doubles={doubles} actor={actor} signal={signal} callback={frame}";var expected=row.GetProperty("sprites").EnumerateArray().Select(N).ToArray();var actual=scene.Sprites.OrderBy(s=>s.Slot).ToArray();Assert.AreEqual(expected.Length,actual.Length,at);Assert.AreEqual(row.GetProperty("visualCount").GetInt32(),runtime.ActiveVisualCount,at);
                var randomField=typeof(SourceBattleAnimation).GetField("_visualRandom",BindingFlags.Instance|BindingFlags.NonPublic)!;uint rng=randomField.GetValue(runtime) is SourceVisualRandom random?Field<uint>(random,"_state"):0x12345678;Assert.AreEqual(row.GetProperty("rng").GetUInt32(),rng,at);
                for(int i=0;i<actual.Length;i++)
                {
                    int[] e=expected[i];var s=actual[i];var f=s.Frame;Assert.AreEqual(e[0],f.Slot,at);Assert.AreEqual(Templates[e[2]],f.Template,at);Assert.AreEqual(new PixelPoint(e[3],e[4]),f.Position,at);Assert.AreEqual(new PixelPoint(e[5],e[6]),f.Offset,at);Assert.AreEqual(e[7],f.Tile,at);Assert.AreEqual(e[8]!=0,f.Visible,at);Assert.AreEqual(e[9]!=0,f.Affine,at);Assert.AreEqual(e[10]!=0,f.DoubleSize,at);Assert.AreEqual(e[14],f.Subpriority,at);
                    Assert.AreEqual(type==2,f.Blend,at);Assert.AreSame(runtime,s.Owner,at);Assert.AreEqual(!(type==1&&e[25]==-1),s.Counted,at);
                    if(f.Affine){var affine=Field<SourceSpriteAffineProgram>(s,"_affine");Assert.AreEqual(e[11],affine.XScale,at);Assert.AreEqual(e[12],affine.YScale,at);Assert.AreEqual(e[13],affine.Rotation,at);}
                    var program=s.GetType().GetField("_frames",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(s) as SourceSpriteFrameProgram;
                    if(type!=2&&program is not null){Assert.AreEqual(e[16],program.DelayCounter,at);Assert.AreEqual(e[17]!=0,program.Ended,at);}
                    if(program is null){Assert.AreEqual(0,frame,at);Assert.AreEqual(0,f.Tile,at);Assert.IsFalse(f.Visible,at);}
                    if(type==0){Assert.AreEqual((short)e[18],Field<short>(s,"_sum"),at);Assert.AreEqual((short)e[19],Field<short>(s,"_age"),at);Assert.AreEqual((short)e[22],Field<short>(s,"_drift"),at);}
                    if(type==1){Assert.AreEqual(e[19],Field<int>(s,"_remaining"),at);Assert.AreEqual(e[21],Field<int>(s,"_fraction"),at);}
                    if(type==3){Assert.AreEqual(e[19],Field<int>(s,"_tick"),at);Assert.AreEqual(e[20],Field<int>(s,"_distance"),at);}
                    if(type==4)Assert.AreEqual(e[18],Field<int>(Field<object>(s,"_linear"),"_remaining"),at);
                }
                rows++;
            }
            Assert.AreEqual(0,scene.Count);runtime.Cancel();cases++;
        }
        Assert.AreEqual(48,cases);Assert.AreEqual(1950,rows);
    }

    [TestMethod]
    public void EverySourceTaskRegisterDelayGlobalFadeAndPaletteEntryMatchesNative()
    {
        using var document=Vectors("tasks");int cases=0,rows=0;int[] banks=[1,2,3,16,17,18,19,20,21,22];
        foreach(var c in document.RootElement.GetProperty("cases").EnumerateArray())
        {
            int kind=c.GetProperty("kind").GetInt32(),actor=c.GetProperty("attacker").GetInt32(),mask=c.GetProperty("visibleMask").GetInt32(),move=kind==0?234:kind==3?62:236;bool prior=c.GetProperty("preexistingFade").GetBoolean();var audio=new Audio();
            SourceAnimationCommand task=kind switch{0=>C("createvisualtask","AnimTask_MorningSunLightBeam","5"),1=>C("createvisualtask","AnimTask_MoonlightEndFade","2"),2=>C("createvisualtask","AnimTask_AlphaFadeIn","3","0","16","16","0","1"),_=>C("createvisualtask","AnimTask_RotateAuroraRingColors","10","130")};
            var script=Primitive(move,[task]);
            // Palette slots are explicit native fixture inputs, independent of the
            // authored loading sequence; production loading is exercised below.
            var runtime=new SourceBattleAnimation(script,Cue(move,actor,actor^1),Battlers(mask),true,audio,Enumerable.Range(32,16).Select(Original).ToArray());
            var tags=Field<int[]>(runtime,"_spritePaletteTags");var loaded=Field<HashSet<string>>(runtime,"_loaded");var original=Field<ushort[][]>(runtime,"_spritePaletteUnfadedBanks");var faded=Field<ushort[][]>(runtime,"_spritePaletteBanks");
            foreach(var pair in new[]{("ANIM_TAG_MOON",4),("ANIM_TAG_GREEN_SPARKLE",5),("ANIM_TAG_RAINBOW_RINGS",6)})if(script.SpriteTags.TryGetValue(pair.Item1,out int tag)){tags[pair.Item2]=tag;loaded.Add(pair.Item1);}
            for(int slot=0;slot<16;slot++)for(int i=0;i<16;i++)original[slot][i]=faded[slot][i]=Original((slot+16)*16+i);
            if(kind==1)Invoke(runtime,"BlendPalettes",0xeu,16,(ushort)0);
            if(prior){var t=typeof(SourceBattleAnimation).GetNestedType("PaletteFade",BindingFlags.NonPublic)!;var fade=Activator.CreateInstance(t,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[1u<<16,0,16,(ushort)31,0],CultureInfo.InvariantCulture)!;typeof(SourceBattleAnimation).GetField("_fade",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(runtime,fade);Invoke(fade,"Update",runtime);}
            var frame=Begin(runtime);int soundCount=0;SourcePaletteEntry[]? early=null;
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int f=row.GetProperty("frame").GetInt32();if(f!=0)frame=runtime.Tick();string at=$"kind={kind} actor={actor} mask={mask} prior={prior} frame={f}";int alive=row.GetProperty("alive").GetInt32();Assert.AreEqual(alive,runtime.ActiveVisualCount,at);
                if(kind is 0 or 2){int[] registers=N(row.GetProperty("registers"));Assert.AreEqual(registers[1]&255,frame.AlphaA,at);Assert.AreEqual(registers[1]>>8,frame.AlphaB,at);}
                if(kind==0)
                {
                    int[] bg=N(row.GetProperty("bg1"));Assert.AreEqual(bg[0],(int)unchecked((ushort)(frame.Overlay?.ScrollX??0)),at);Assert.AreEqual(bg[1],(int)unchecked((ushort)(frame.Overlay?.ScrollY??0)),at);Assert.AreEqual(alive!=0,frame.BlendEnabled,at);
                    if(alive!=0){Assert.AreEqual("morning-sun",frame.Overlay!.MaterialId);Assert.AreEqual(2,frame.BlendFirstTargets,at);Assert.AreEqual(63,frame.BlendSecondTargets,at);Assert.AreEqual(1,frame.BattlerBackgroundPriorities[0],at);}
                    soundCount+=row.GetProperty("events").EnumerateArray().Count(e=>e[0].GetInt32()==7);Assert.AreEqual(soundCount,audio.Cues.Count,at);foreach(var e in row.GetProperty("events").EnumerateArray().Where(e=>e[0].GetInt32()==7)){Assert.AreEqual("SE_M_MORNING_SUN",audio.Cues.Last().Id);Assert.AreEqual(e[2].GetInt32(),audio.Cues.Last().Pan,at);}
                }
                if(kind==1)
                {
                    Assert.AreEqual(row.GetProperty("fade")[0].GetInt32()!=0,runtime.PaletteFadeActive,at);
                    if(f==0){early=frame.BackgroundPaletteEntries;Assert.AreEqual(45,early.Length);Assert.IsFalse(early.Any(e=>e.Index%16==0));}
                    if(f==1)Assert.AreEqual((ushort)(1|(1<<5)|(1<<10)),early![0].Color,at);
                }
                if(kind is 1 or 3)for(int b=0;b<banks.Length;b++)
                {
                    int bank=banks[b];int[] values=Enumerable.Range(0,16).Select(i=>bank>=20?(int)frame.SpritePaletteSlots[bank-16][i]:(int)(frame.BackgroundPaletteEntries.FirstOrDefault(e=>e.Index==bank*16+i) is var entry&&entry.Index==bank*16+i?entry.Color:SourcePaletteMath.Apply(Original(bank*16+i),frame.PaletteBlends.SingleOrDefault(p=>p.Palette==bank),false))).ToArray();
                    CollectionAssert.AreEqual(N(row.GetProperty("palettes")[b]),values,at+" bank="+bank);
                }
                if(alive!=0)
                {
                    int[] d=N(row.GetProperty("data"));var actual=Tasks(runtime).Single();
                    if(kind==0){Assert.AreEqual(d[0],Field<int>(actual,"_state"),at);Assert.AreEqual(d[1],Field<int>(actual,"_alpha"),at);Assert.AreEqual(d[2],Field<int>(actual,"_pass"),at);Assert.AreEqual(d[3],Field<int>(actual,"_wait"),at);Assert.AreEqual(d[4],Field<int>(actual,"_tick"),at);}
                    if(kind==1){Assert.AreEqual(d[0],Field<int>(actual,"_state"),at);Assert.AreEqual(d[2],Field<int>(actual,"_colorStep"),at);Assert.AreEqual(d[1],Field<int>(actual,"_wait"),at);}
                    if(kind==2){Assert.AreEqual(d[0],Field<int>(actual,"_tick"),at);Assert.AreEqual(d[2],Field<int>(actual,"_alternate"),at);}
                    if(kind==3){Assert.AreEqual(d[10],Field<int>(actual,"_tick"),at);Assert.AreEqual(d[11],Field<int>(actual,"_age"),at);}
                }
                rows++;
            }
            if(kind==1)Assert.AreEqual(0,runtime.Snapshot().BackgroundPaletteEntries.Length);runtime.Cancel();cases++;
        }
        Assert.AreEqual(28,cases);Assert.AreEqual(2632,rows);
    }
}
