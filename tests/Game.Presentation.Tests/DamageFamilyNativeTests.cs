using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class DamageFamilyNativeTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly string[] Templates=["gRevengeSmallScratchSpriteTemplate","gRevengeBigScratchSpriteTemplate","gPersistHitSplatSpriteTemplate","gMonEdgeHitSplatSpriteTemplate","gRapidSpinSpriteTemplate","gAirCutterSliceSpriteTemplate","gSpeedDustSpriteTemplate","gAirWaveProjectileSpriteTemplate"];
    private static readonly string[] Tasks=["RapidElevationTask","SpeedStretchTask","SpeedImpactTask","SpeedReappearTask","DamageDustTask","AirProjectileTask"];
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound>Cues=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects(){} }
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags)!.GetValue(o)!;
    private static T Property<T>(object o,string n)=>(T)o.GetType().GetProperty(n,Flags)!.GetValue(o)!;
    private static object? Invoke(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags)!.Invoke(o,args);
    private static object Nested(string n,params object[] args)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(n,BindingFlags.NonPublic)!,Flags,null,args,CultureInfo.InvariantCulture)!;
    private static int[] V(JsonElement e,string n)=>e.GetProperty(n).EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static int I(JsonElement e,string n)=>e.GetProperty(n).GetInt32();
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[new("end",[],1)],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};

    [TestMethod]
    public void EveryExtractedNativeFrameMatchesPhaseBoundariesGeometryRetainedArgsAndRawOwnership()
    {
        using var doc=SourceVectorFixture.Read("damage-family-source-vectors.json","python tests/Reference/DamageFamily/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/damage-family-source-vectors.json");
        Assert.AreEqual("firered-damage-family-source-v1",doc.RootElement.GetProperty("schema").GetString());
        CollectionAssert.AreEqual(new[]{"O0","O2"},doc.RootElement.GetProperty("optimizationParity").EnumerateArray().Select(x=>x.GetString()).ToArray());
        var excluded=doc.RootElement.GetProperty("excludedUndefinedInputs").EnumerateArray().ToArray();Assert.AreEqual(2,excluded.Length);
        CollectionAssert.AreEqual(new[]{-29,0,512,0,1},V(excluded[0],"arguments"));Assert.AreEqual("Zero x difference makes data5 zero; original Q_8_8_inv(data5) divides by zero.",excluded[0].GetProperty("reason").GetString());
        CollectionAssert.AreEqual(new[]{-28,0,32766,0,1},V(excluded[1],"arguments"));Assert.AreEqual("Nonzero x difference1 truncates Q_8_8_mul(1,2) to zero; original Q_8_8_inv(data5) divides by zero.",excluded[1].GetProperty("reason").GetString());
        var nativeCases=doc.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        (int Kind,int[] Args)[] authored=[(4,[0,0,32,-32,40,-2]),(6,[0,2,0]),(6,[0,2,1]),(11,[32,-24,1536,2,128]),(5,[40,-32,0,2]),(2,[-10,-8,1,1,8]),(2,[10,8,1,1,8]),(0,[10,-10,0]),(1,[10,-10,4])];
        Assert.AreEqual(54,nativeCases.Count(c=>c.GetProperty("branch").GetString()=="authored-root-exact"));
        foreach(var input in authored){var exact=nativeCases.Where(c=>c.GetProperty("branch").GetString()=="authored-root-exact"&&I(c,"kind")==input.Kind&&V(c,"arguments").Take(input.Args.Length).SequenceEqual(input.Args)).ToArray();Assert.AreEqual(6,exact.Length,"Every authored callback recipe needs all six layouts.");Assert.AreEqual(6,exact.Select(c=>(c.GetProperty("doubles").GetBoolean(),I(c,"actor"))).Distinct().Count());Assert.IsTrue(exact.All(c=>I(c,"target")== (I(c,"actor")^1)));}
        foreach(int speed in new[]{2,3,4,32766})Assert.IsTrue(nativeCases.Any(c=>I(c,"kind")==11&&c.GetProperty("arguments")[2].GetInt32()==speed),"Required fixed-point speed boundary "+speed);
        foreach(int speed in new[]{2,3}){var boundary=nativeCases.First(c=>I(c,"kind")==11&&c.GetProperty("arguments")[2].GetInt32()==speed);Assert.IsTrue(boundary.GetProperty("rows")[0].GetProperty("task")[5].GetInt32()<0,"Signed reciprocal narrowing must remain executable.");}
        var cadence=nativeCases.Single(c=>c.GetProperty("branch").GetString()=="signed-reciprocal-max-safe-child-cadence");Assert.AreEqual(2,cadence.GetProperty("arguments")[2].GetInt32());Assert.AreEqual(19,cadence.GetProperty("arguments")[3].GetInt32());var cadenceRows=cadence.GetProperty("rows").EnumerateArray().ToArray();CollectionAssert.AreEqual(new[]{1,21,41},cadenceRows.Where(r=>r.GetProperty("sounds").GetArrayLength()>0).Select(r=>I(r,"frame")).ToArray());Assert.AreEqual(3,cadenceRows[41].GetProperty("sprites").GetArrayLength());Assert.IsTrue(cadenceRows[41].GetProperty("sprites").EnumerateArray().Any(s=>I(s,"instance")==1));Assert.IsFalse(cadenceRows[42].GetProperty("sprites").EnumerateArray().Any(s=>I(s,"instance")==1));Assert.AreEqual(150,I(cadenceRows[^1],"frame"));
        int cases=0,rows=0;var kinds=new HashSet<int>();var layouts=new HashSet<(bool,int)>();
        foreach(var c in nativeCases)
        {
            int kind=I(c,"kind"),actor=I(c,"actor"),target=I(c,"target"),id=I(c,"root"),hidden=I(c,"hiddenMask");bool doubles=c.GetProperty("doubles").GetBoolean(),shift=c.GetProperty("shift").GetBoolean();
            var script=AnimationCompositionFixture.Script(id);var bodies=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body"+i,I(c,"yOffset"),I(c,"elevation"),64,64){SourceVisibility=new(true,(hidden&(1<<i))!=0,(hidden&(1<<i))!=0)}).ToArray();
            var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var owner=new SourceBattleAnimation(Primitive(script),new("damage-native",1,id,new(actor),new(target)),bodies,doubles,audio,spriteScene:scene);
            var args=Field<short[]>(owner,"_animationArgs");for(int j=0;j<8;j++)args[j]=(short)c.GetProperty("arguments")[j].GetInt32();
            var offsets=Field<PixelPoint[]>(owner,"_offsets");if(shift)for(int j=0;j<bodies.Length;j++){offsets[j]=new((j&1)!=0?-5:5,-3);Invoke(owner,"SetBattlerPosition",j,new PixelPoint(bodies[j].Center(doubles).X+7,bodies[j].Center(doubles).Y-9));}
            var scan=Property<SourceScanlineState>(owner,"ScanlineState");if(I(c,"writeBuffer")==1){scan.SetParams(SourceScanlineDestination.Bg1Horizontal,false);scan.CaptureVBlank();scan.Stop();}
            var expectedBank=new ushort[1920];for(int b=0;b<2;b++)for(int j=0;j<960;j++){ushort v=c.GetProperty("seeded").GetBoolean()?unchecked((ushort)(b*4093+j*17)):(ushort)0;expectedBank[b*960+j]=v;scan.Write(b,j,v);}
            Invoke(owner,"SetScanlineBackgroundOffset",1,new PixelPoint(65501,27));Invoke(owner,"SetScanlineBackgroundOffset",2,new PixelPoint(65391,65515));
            var sprites=Field<object>(owner,"_sprites");SourceBattleAnimation.VisualSprite? single=kind<6?(SourceBattleAnimation.VisualSprite)Nested("DamageFamilyVisual",doubles?4:2,2,script.SpriteRecipes[Templates[kind]],owner):null;object? task=kind>=6?Nested(Tasks[kind-6],owner):null;
            var generations=new Dictionary<SourceBattleAnimation.VisualSprite,int>();var births=new Dictionary<SourceBattleAnimation.VisualSprite,int>();int generation=single is null?0:1;
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int tick=I(row,"frame");string why=$"damage case{cases} kind{kind} actor{actor} target{target} frame{tick} ({c.GetProperty("branch").GetString()})";
                if(tick>0){scan.CaptureVBlank();if(single is not null){single.Step(owner);if(!single.Finished)single.AdvanceAnimation(owner);}else{Invoke(owner,"AdvanceSourceSprites");Invoke(task!,"Step",owner);}}
                Assert.AreEqual(I(row,"alive")!=0,single is not null?!single.Finished:!Property<bool>(task!,"Finished"),why+" callback lifetime");
                CollectionAssert.AreEqual(V(row,"args").Select(v=>(short)v).ToArray(),args.Take(8).ToArray(),why+" retained arguments");Assert.IsTrue(args.Skip(8).All(v=>v==0),why+" untouched extended registers");
                if(kind==6){CollectionAssert.AreEqual(V(row,"task").Select(v=>(short)v).ToArray(),Field<short[]>(task!,"_d"),why+" scanline callback registers");Assert.AreEqual(I(row,"state"),scan.State,why);Assert.AreEqual(I(row,"writeBuffer"),scan.SourceBuffer,why);Assert.AreEqual(I(row,"rank"),scan.Destination==SourceScanlineDestination.Bg1Horizontal?1:2,why);foreach(var write in row.GetProperty("writes").EnumerateArray())expectedBank[write[0].GetInt32()]=(ushort)write[1].GetInt32();CollectionAssert.AreEqual(expectedBank,Enumerable.Range(0,1920).Select(i=>scan.Read(i/960,i%960)).ToArray(),why+" both retained banks");}
                if(kind==8){int[] d=V(row,"task");CollectionAssert.AreEqual(new[]{d[0],d[1],d[2],d[3],d[12]},new[]{(int)Field<short>(task!,"_state"),(int)Field<short>(task!,"_timer"),(int)Field<short>(task!,"_toggle"),(int)Field<short>(task!,"_count"),(int)Field<short>(task!,"_remaining")},why+" impact phases");}
                if(kind==9){int[] d=V(row,"task");CollectionAssert.AreEqual(new[]{d[1],d[2],d[3],d[4]},new[]{(int)Field<short>(task!,"_timer"),(int)Field<short>(task!,"_toggle"),(int)Field<short>(task!,"_count"),(int)Field<short>(task!,"_delay")},why+" blink phases");}
                if(kind==10)CollectionAssert.AreEqual(V(row,"task").Take(14).Select(v=>(short)v).ToArray(),Field<short[]>(task!,"_d").Take(14).ToArray(),why+" dust cadence and child counters");
                if(kind==11)CollectionAssert.AreEqual(V(row,"task").Select(v=>(short)v).ToArray(),Field<short[]>(task!,"Data"),why+" projectile shared registers and raw slots");
                var snapshot=owner.Snapshot();int bi=0;foreach(var b in row.GetProperty("battlers").EnumerateArray())
                {
                    int[] e=b.EnumerateArray().Select(v=>v.GetInt32()).ToArray();Assert.AreEqual(new PixelPoint(e[0],e[1]),(PixelPoint)Invoke(owner,"BattlerPosition",bi)!,why+" base position");Assert.AreEqual(new PixelPoint(e[2],e[3]),offsets[bi],why+" battler secondary offset");Assert.AreEqual(e[4]!=0,snapshot.BattlerVisibility[bi].SpriteInvisible,why+" sprite visibility");
                    var a=snapshot.BattlerAffine[bi];CollectionAssert.AreEqual(e.Skip(5).ToArray(),new[]{a.XScale,a.MatrixB,a.MatrixC,a.YScale},why+" affine endpoint and foot anchoring");bi++;
                }
                var actual=single is not null?new[]{single}:((IEnumerable)sprites).Cast<SourceBattleAnimation.VisualSprite>().OrderBy(s=>s.Slot).ToArray();var expected=row.GetProperty("sprites").EnumerateArray().ToArray();Assert.AreEqual(expected.Length,actual.Length,why+" raw owner count");
                for(int i=0;i<actual.Length;i++)
                {
                    var sprite=actual[i];if(!generations.TryGetValue(sprite,out int instance)){instance=single is not null?1:++generation;generations.Add(sprite,instance);births.Add(sprite,tick);}var e=expected[i];var f=sprite.Frame;
                    Assert.AreEqual(I(e,"slot"),f.Slot,why);Assert.AreEqual(I(e,"instance"),instance,why+" allocation reuse");Assert.AreEqual(Templates[I(e,"template")],f.Template,why);Assert.AreEqual(new PixelPoint(e.GetProperty("position")[0].GetInt32(),e.GetProperty("position")[1].GetInt32()),f.Position,why);Assert.AreEqual(new PixelPoint(e.GetProperty("offset")[0].GetInt32(),e.GetProperty("offset")[1].GetInt32()),f.Offset,why);Assert.AreEqual(I(e,"tile"),f.Tile,why);Assert.AreEqual(I(e,"visible")!=0,f.Visible,why);Assert.AreEqual(I(e,"priority"),f.OamPriority,why);Assert.AreEqual(I(e,"subpriority"),f.Subpriority,why);Assert.AreEqual(I(e,"hFlip")!=0,f.HFlip,why);Assert.AreEqual(I(e,"vFlip")!=0,f.VFlip,why);Assert.AreEqual(single is not null,sprite.Counted,why+" raw children never count as visual owners");
                    if(kind is 0 or 1 or 4 or 5 || kind==10&&tick>births[sprite]){var frames=Field<SourceSpriteFrameProgram>(sprite,"_frames");Assert.AreEqual(I(e,"animEnded")!=0,frames.Ended,why);Assert.AreEqual(I(e,"animDelay"),frames.DelayCounter,why);}
                    if(kind is 2 or 3){var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");Assert.AreEqual(I(e,"xScale"),affine.XScale,why);Assert.AreEqual(I(e,"yScale"),affine.YScale,why);Assert.AreEqual(I(e,"affineEnded")!=0,affine.Ended,why);}
                    if(kind==11){int[] d=V(e,"data");CollectionAssert.AreEqual(d.Take(7).Select(v=>(short)v).ToArray(),Field<short[]>(sprite,"_d").Take(7).ToArray(),why+" native-width child phases");}
                }
                var sounds=row.GetProperty("sounds").EnumerateArray().ToArray();Assert.AreEqual(sounds.Length,audio.Cues.Count,why+" callback sounds");for(int i=0;i<sounds.Length;i++){Assert.AreEqual(sounds[i][0].GetInt32(),script.SoundEffects[audio.Cues[i].Id],why);Assert.AreEqual(sounds[i][1].GetInt32(),audio.Cues[i].Pan,why);Assert.IsTrue(audio.Cues[i].ApplyPanning,why);}audio.Cues.Clear();rows++;
            }
            Assert.IsTrue(single?.Finished??Property<bool>(task!,"Finished"));if(task is not null)Assert.AreEqual(0,scene.Count,"Raw child owners must drain before their task finishes.");owner.Cancel();cases++;kinds.Add(kind);layouts.Add((doubles,actor));
        }
        Assert.AreEqual(12,kinds.Count);Assert.AreEqual(6,layouts.Count);Assert.IsTrue(cases>=268);Assert.IsTrue(rows>=8559);
    }
}
