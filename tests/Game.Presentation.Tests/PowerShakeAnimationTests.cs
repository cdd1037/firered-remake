using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class PowerShakeAnimationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const string Horizontal="AnimTask_HorizontalShake",Target="AnimTask_ShakeTargetBasedOnMovePowerOrDmg",Predicate="AnimTask_IsPowerOver99";
    private static readonly int[][] HorizontalInputs=[[0,10,4,0],[1,0,3,99],[2,-1,1,0],[3,0,2,100],[4,0,2,150],[5,10,4,0],[4,0,2,0],[5,0,2,65535]];
    private static readonly int[] Powers=[0,11,12,23,24,35,36,191,192,65535];
    private static readonly int[] Damages=[int.MinValue,-786444,-393216,-13,-1,0,11,12,23,24,36,191,192,393216,786420,786432,int.MaxValue];
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public List<SourceAnimationSound> Sounds{get;}=[];public void Play(SourceAnimationSound cue)=>Sounds.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceAnimationScript Script(int move)=>AnimationCompositionFixture.Script(move);
    private static AnimationBattler[] Battlers(bool doubles=true,int mask=15)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64){Visible=(mask&(1<<i))!=0}).ToArray();
    private static SourceAnimationScript Primitive(string callback,params int[] args)=>Script(89) with{
        Commands=[C("createvisualtask",new[]{callback,"5"}.Concat(args.Select(v=>v.ToString())).ToArray()),C("delay","255"),C("end")],Labels=new Dictionary<string,int>()};
    private static CommittedMoveAnimation Cue(int move,int actor,ushort? power=null,int? damage=null,byte? turn=null)=>new("power-shake",1,move,new(actor),new(actor^1)){MovePower=power,MoveDamage=damage,MoveTurn=turn};
    private static SourceBattleAnimation Runtime(SourceAnimationScript script,CommittedMoveAnimation cue,int mask=15)=>new(script,cue,Battlers(mask:mask),true,new Audio());
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("power-shake-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_power_shake_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/power-shake-source-vectors.json");
    private static void Initialize(SourceBattleAnimation runtime,bool initial)
    {
        if(!initial)return;
        typeof(SourceBattleAnimation).GetProperty("BackgroundScrollX",Private)!.SetValue(runtime,(ushort)65534);
        var offsets=(PixelPoint[])typeof(SourceBattleAnimation).GetField("_offsets",Private)!.GetValue(runtime)!;
        for(int i=0;i<4;i++)offsets[i]=new(i%2!=0?32766:-32767,i%2!=0?4:-6);
    }
    private static void Match(SourceBattleAnimation runtime,JsonElement rows,int mask,string context)
    {
        SourceAnimationFrame frame=runtime.Snapshot();int count=0;
        foreach(var row in rows.EnumerateArray())
        {
            int[] v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            while(frame.Frame<v[0])frame=runtime.Tick();
            Assert.AreEqual(v[1],runtime.ActiveVisualCount,context+" active "+v[0]);
            Assert.AreEqual(v[2],frame.MoveBackground!.ScrollX,context+" terrain "+v[0]);
            for(int slot=0;slot<4;slot++)
            {
                int c=3+slot*4;Assert.AreEqual(new PixelPoint(v[c],v[c+1]),frame.Offsets[slot],context+" offset "+slot+" tick "+v[0]);
                PixelPoint expected=default;
                if((mask&(1<<slot))!=0)
                {
                    var center=frame.Battlers[slot].Center(true);int x=((center.X+v[c+2]-32)&511)-center.X+32;if(x>255)x-=512;
                    int y=((center.Y+v[c+3]-32)&255)-center.Y+32;expected=new(x,y);
                }
                Assert.AreEqual(expected,frame.ObjectOffsets[slot],context+" pre-task OAM "+slot+" tick "+v[0]);
            }
            count++;
        }
        Assert.IsGreaterThan(0,count);runtime.Cancel();
    }
    [TestMethod]
    public void HorizontalShakeSelectorsVisibilityOddHalvesTerrainWrapDecayAndOamPhaseMatchPinnedC()
    {
        using var vectors=Vectors();int id=0;
        for(int actor=0;actor<4;actor++)for(int presence=0;presence<2;presence++)for(int initial=0;initial<2;initial++)for(int c=0;c<HorizontalInputs.Length;c++)
        {
            if(c==7&&(actor!=0||presence!=0||initial!=1))continue;
            var a=HorizontalInputs[c];int mask=presence!=0?5:15;
            var runtime=Runtime(Primitive(Horizontal,a.Take(3).ToArray()),Cue(89,actor,(ushort)a[3]),mask);Initialize(runtime,initial!=0);
            Match(runtime,vectors.RootElement.GetProperty("horizontal").GetProperty((id++).ToString()),mask,"horizontal "+id);
        }
        Assert.AreEqual(113,id);
    }
    [TestMethod]
    public void TargetPowerDamageDivisionSignedNarrowingClampsAxesRestorationAndOamPhaseMatchPinnedC()
    {
        using var vectors=Vectors();int id=0;
        for(int actor=0;actor<4;actor++)for(int initial=0;initial<2;initial++)for(int mode=0;mode<4;mode++)for(int c=0;c<27;c++)
        {
            bool damage=c>=Powers.Length;var runtime=Runtime(Primitive(Target,damage?1:0,c%3,7,mode&1,(mode>>1)&1),Cue(89,actor,damage?null:(ushort)Powers[c],damage?Damages[c-Powers.Length]:null));Initialize(runtime,initial!=0);
            Match(runtime,vectors.RootElement.GetProperty("target").GetProperty((id++).ToString()),15,"target "+id);
        }
        Assert.AreEqual(864,id);
    }
    [TestMethod]
    public void PredicateUsesAuthoredRegister15ThroughBoundedSafeRemakeStorage()
    {
        // Pinned source declares only eight words yet addresses word15. This is
        // explicitly a safe VM policy test, not evidence of native OOB parity.
        using var vectors=Vectors();int cases=0;
        foreach(var expected in vectors.RootElement.GetProperty("predicate").EnumerateArray())
        {
            int power=expected[0].GetInt32(),answer=expected[1].GetInt32();
            var script=Script(89) with{Commands=[C("setarg","7","123"),C("createvisualtask",Predicate,"2"),C("jumpargeq","15","1","large"),C("setarg","0","11"),C("goto","done"),C("setarg","0","22"),C("delay","255"),C("end")],Labels=new Dictionary<string,int>{{"large",5},{"done",6}}};
            var runtime=Runtime(script,Cue(89,0,(ushort)power));var frame=runtime.Tick();Assert.AreEqual(16,frame.AnimationArguments.Length);Assert.AreEqual((short)answer,frame.AnimationArguments[15]);Assert.AreEqual((short)123,frame.AnimationArguments[7]);Assert.AreEqual((short)(answer==1?22:11),frame.AnimationArguments[0]);Assert.AreEqual(0,runtime.ActiveVisualCount);runtime.Cancel();Assert.IsTrue(runtime.Snapshot().AnimationArguments.All(v=>v==0));cases++;
        }
        Assert.AreEqual(5,cases);
    }
    [TestMethod]
    public void ContextRequirementsAreScopedToInvokedProgramsWhileEveryHelperKeepsItsAbiValidation()
    {
        var noContext=Cue(89,0);var idle=Script(89) with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>()};
        SourceAnimationCommand Task(string callback,params string[] values)=>C("createvisualtask",new[]{callback,"5"}.Concat(values).ToArray());
        foreach(var missing in new[]{Task(Horizontal,"4","0","2"),Task(Target,"0","1","3","1","0"),Task(Target,"1","1","3","1","0"),Task(Predicate)})
        {
            var helpers=idle.Subroutines.ToDictionary(p=>p.Key,p=>p.Value);helpers["PowerHelper"]=[missing,C("return")];var script=idle with{Subroutines=helpers};
            Runtime(script,noContext).Cancel();
            Assert.Throws<SourceAnimationException>(()=>Runtime(script with{Commands=[C("call","PowerHelper"),C("end")]},noContext));
        }
        Runtime(Primitive(Horizontal,4,10,2),noContext).Cancel();
        Runtime(Primitive(Target,0,1,3,1,0),Cue(89,0,power:40)).Cancel();
        Runtime(Primitive(Target,1,1,3,1,0),Cue(89,0,damage:24)).Cancel();
        foreach(var malformed in new[]{Task(Horizontal,"6","10","2"),Task(Horizontal,"4","-2","2"),Task(Target,"0","1","3"),Task(Target,"0","32767","3","1","0"),Task(Target,"0","1","0","1","0"),Task(Predicate,"0")})
        {
            var helpers=idle.Subroutines.ToDictionary(p=>p.Key,p=>p.Value);helpers["BadHelper"]=[malformed,C("return")];
            Assert.Throws<SourceAnimationException>(()=>Runtime(idle with{Subroutines=helpers},Cue(89,0,40,24)));
        }
    }
    [TestMethod]
    [DataRow(89,0,0,0)][DataRow(99,0,24,0)][DataRow(99,0,191,0)]
    [DataRow(222,99,0,0)][DataRow(222,100,0,0)][DataRow(222,150,0,0)]
    [DataRow(228,40,0,0)][DataRow(228,40,0,1)]
    public void CompleteRootsComposeEveryActorAndFinishWithSourceRegisterCleanup(int move,int power,int damage,int turn)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var battlers=Battlers(doubles);var surface=resources.CreateBackgroundSurface(0);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(move,actor,move is 222 or 228?(ushort)power:null,move==99?damage:null,move==228?(byte)turn:null),battlers,doubles,audio,Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray());var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers,surface);int ticks=0;bool shake=false,palette=false,dark=false;
            while(runtime.Active&&ticks++<1024){var frame=runtime.Tick();shake|=frame.Offsets.Any(p=>p.X!=0)||frame.MoveBackground!.ScrollX!=0;palette|=frame.PaletteBlends.Length!=0;dark|=frame.MoveBackground!.Id==1;Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length);}
            Assert.IsFalse(runtime.Active);Assert.IsTrue(shake);Assert.IsGreaterThan(0,audio.Sounds.Count);Assert.AreEqual(move==228,dark);if(move==222)Assert.AreEqual(power>99,palette);
            var final=runtime.Snapshot();Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,final.MoveBackground!.ScrollX);Assert.IsNull(final.MoveBackground.Id);Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.AreEqual(0,final.Sprites.Length);
        }
    }
    [TestMethod]
    public void CancelDuringInitialAndDecayPhasesClearsTasksRegistersOamAndArgumentsIdempotently()
    {
        foreach(int tick in new[]{0,1,9,40,150})foreach(int move in new[]{89,99,222,228})
        {
            var runtime=Runtime(Script(move),Cue(move,0,150,191,1));for(int i=0;i<tick&&runtime.Active;i++)runtime.Tick();runtime.Cancel();runtime.Cancel();var frame=runtime.Snapshot();
            Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.MoveBackground!.ScrollX);Assert.IsNull(frame.MoveBackground.Id);Assert.IsTrue(frame.Offsets.All(p=>p==default));Assert.IsTrue(frame.ObjectOffsets.All(p=>p==default));Assert.IsTrue(frame.AnimationArguments.All(v=>v==0));
        }
    }
}
