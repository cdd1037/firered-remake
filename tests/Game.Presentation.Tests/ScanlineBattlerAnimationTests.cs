using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ScanlineBattlerAnimationTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound>Cues=[];public int Stops,Pans;public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects()=>Stops++;public void SetPan(int pan,int frame)=>Pans++;}
    private sealed class Occupied(int slot):SourceBattleAnimation.VisualSprite(slot,100){public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"external",default,default,0,false,false,false,256,null,100);public override void Step(SourceBattleAnimation o){}}
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags)!.GetValue(o)!;
    private static T Property<T>(object o,string n)=>(T)o.GetType().GetProperty(n,Flags)!.GetValue(o)!;
    private static object? Invoke(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Flags)!.Invoke(o,args);
    private static object Nested(string n,params object[] args)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(n,BindingFlags.NonPublic)!,Flags,null,args,CultureInfo.InvariantCulture)!;
    private static readonly string[] Names=["AnimTask_DigDownMovement","AnimTask_DigUpMovement","AnimTask_SketchDrawMon","AnimTask_DragonDanceWaver","AnimTask_AcidArmor","gDirtPlumeSpriteTemplate","gDirtMoundSpriteTemplate","gPencilSpriteTemplate","gDragonDanceOrbSpriteTemplate"];
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static AnimationBattler[] Bodies(bool doubles,int height=64,int width=64,int yoff=0,int elevation=0,bool hidden=false)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body"+i,yoff,elevation,width,height){SourceVisibility=new(true,false,hidden)}).ToArray();
    private static int[] Values(JsonElement row,string key)=>row.GetProperty(key).EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static ushort[] Terrain(SourceAnimationResources r){var b=r.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>b.PaletteColor(2,i)).ToArray();}
    [TestMethod]
    public void EveryNativeCallbackFrameMatchesRetainedBanksStopHandoffDigVisibilityAndPencilAudio()
    {
        using var doc=SourceVectorFixture.Read("scanline-battler-source-vectors.json","python tests/Reference/ScanlineBattlers/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/scanline-battler-source-vectors.json");int cases=0,rows=0;
        foreach(var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            int kind=c.GetProperty("kind").GetInt32(),actor=c.GetProperty("actor").GetInt32(),target=c.GetProperty("target").GetInt32(),id=kind is 0 or 1 or 5 or 6?91:kind is 2 or 7?166:kind==4?151:349;bool doubles=c.GetProperty("doubles").GetBoolean();var s=AnimationCompositionFixture.Script(id);var audio=new Audio();
            var o=new SourceBattleAnimation(Primitive(s),new("scanline-native",1,id,new(actor),new(target)),Bodies(doubles,c.GetProperty("height").GetInt32(),c.GetProperty("width").GetInt32(),c.GetProperty("yOffset").GetInt32(),c.GetProperty("elevation").GetInt32(),c.GetProperty("hidden").GetBoolean()),doubles,audio);
            var state=Property<SourceScanlineState>(o,"ScanlineState");if(c.GetProperty("writeBuffer").GetInt32()==1){state.SetParams(SourceScanlineDestination.Bg1Horizontal,false);state.CaptureVBlank();state.Stop();}
            var expected=new ushort[1920];for(int b=0;b<2;b++)for(int i=0;i<960;i++){ushort value=c.GetProperty("seeded").GetBoolean()?unchecked((ushort)(b*4093+i*17)):(ushort)0;expected[b*960+i]=value;state.Write(b,i,value);}
            Invoke(o,"SetScanlineBackgroundOffset",1,new PixelPoint(65501,27));Invoke(o,"SetScanlineBackgroundOffset",2,new PixelPoint(65391,65515));Invoke(o,"SetAlphaBlend",0,63,15,0);
            var args=Field<short[]>(o,"_animationArgs");int j=0;foreach(int a in Values(c,"arguments"))args[j++]=(short)a;
            var unfaded=Field<ushort[][]>(o,"_spritePaletteUnfadedBanks");var faded=Field<ushort[][]>(o,"_spritePaletteBanks");for(int i=0;i<16;i++)unfaded[4][i]=faded[4][i]=(ushort)(i*173);
            object callback=kind<5?Nested("BattlerScanlineTask",Names[kind],o):kind==9?Nested("BlendMonInAndOutTask",4,(ushort)args[1],(int)args[2],(int)args[3],(int)args[4]):Nested("BattlerScanlineSprite",8,2,s.SpriteRecipes[Names[kind]],o);
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int frame=row.GetProperty("frame").GetInt32();string why=$"case {cases}, kind {kind}, actor {actor}, target {target}, frame {frame}";
                if(frame>0){state.CaptureVBlank();Invoke(callback,"Step",o);}
                Assert.AreEqual(row.GetProperty("alive").GetInt32()!=0,!Property<bool>(callback,"Finished"),why);
                Assert.AreEqual(row.GetProperty("state").GetInt32(),state.State,why);Assert.AreEqual(row.GetProperty("writeBuffer").GetInt32(),state.SourceBuffer,why);
                if(state.State!=0){Assert.AreEqual(row.GetProperty("paired").GetInt32()!=0,state.Word32,why);Assert.AreEqual(row.GetProperty("rank").GetInt32(),state.Destination==SourceScanlineDestination.Bg1Horizontal?1:2,why);}
                foreach(var write in row.GetProperty("writes").EnumerateArray())expected[write[0].GetInt32()]=(ushort)write[1].GetInt32();
                CollectionAssert.AreEqual(expected,Enumerable.Range(0,1920).Select(i=>state.Read(i/960,i%960)).ToArray(),why);
                CollectionAssert.AreEqual(Values(row,"args").Select(v=>(short)v).ToArray(),args.Take(8).ToArray(),why);Assert.IsTrue(args.Skip(8).All(v=>v==0),why);
                var bg=Values(row,"bg");Assert.AreEqual(new PixelPoint(bg[0],bg[1]),Invoke(o,"ScanlineBackgroundOffset",1),why);Assert.AreEqual(new PixelPoint(bg[2],bg[3]),Invoke(o,"ScanlineBackgroundOffset",2),why);
                Assert.AreEqual(row.GetProperty("alpha").GetInt32(),Field<int>(o,"_alphaA")|(Field<int>(o,"_alphaB")<<8),why);
                if(kind<5)CollectionAssert.AreEqual(Values(row,"data").Select(v=>(short)v).ToArray(),Field<short[]>(callback,"_d"),why);
                if(kind is 7 or 8)CollectionAssert.AreEqual(Values(row,"data").Select(v=>(short)v).ToArray(),Field<short[]>(callback,"_d"),why);
                var e=Values(row,"sprite");
                if(kind<5){Assert.AreEqual(new PixelPoint(e[0],e[1]),Invoke(o,"BattlerPosition",actor),why);Assert.AreEqual(new PixelPoint(e[2],e[3]),Field<PixelPoint[]>(o,"_offsets")[actor],why);Assert.AreEqual(e[5]!=0,Invoke(o,"SourceBattlerSpriteInvisible",actor),why);}
                else if(kind<9){var f=((SourceBattleAnimation.VisualSprite)callback).Frame;Assert.AreEqual(new PixelPoint(e[0],e[1]),f.Position,why);Assert.AreEqual(new PixelPoint(e[2],e[3]),f.Offset,why);Assert.AreEqual(e[4],f.Tile,why);Assert.AreEqual(e[5]==0,f.Visible,why);}
                if(kind==9)CollectionAssert.AreEqual(Values(row,"palette").Select(v=>(ushort)v).ToArray(),faded[4],why);
                var sounds=Values(row,"sounds");Assert.AreEqual(sounds[0],audio.Cues.Count,why);if(sounds[0]>0){Assert.AreEqual("SE_M_SKETCH",audio.Cues[^1].Id,why);Assert.AreEqual(sounds[1],audio.Cues[^1].Pan,why);}
                rows++;
            }
            o.Cancel();cases++;
        }
        Assert.IsTrue(cases>=83);Assert.IsTrue(rows>=6993);
    }
    [TestMethod,DataRow(91),DataRow(151),DataRow(166),DataRow(349)]
    public void CompleteRootsComposeEachPositionAndReleaseOnlySourceOwnedVisibility(int id)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var r=resources.Resolve(id);
        foreach(bool doubles in new[]{false,true})foreach(int actor in Enumerable.Range(0,doubles?4:2))foreach(byte turn in id==91?new byte[]{0,1}:new byte[]{0})
        {
            int target=id is 151 or 349?actor:actor^1;var bodies=Bodies(doubles);if(id==91&&turn==1)bodies[actor]=bodies[actor] with{SourceVisibility=new(true,false,true)};
            var assets=r.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64 is >6 and <57&&i/64 is >4 and <59?0x83e0:0)).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,r,assets,bodies);var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(r.Script,new("scanline-full",1,id,new(actor),new(target)){MoveTurn=id==91?turn:null},bodies,doubles,audio,Terrain(resources),spriteScene:scene,spriteMaterials:assets);int ticks=0,scanlineFrames=0;bool pixelDifference=false;
            while(o.Active&&ticks++<1400){var f=o.Tick();var rgba=composer.ComposeField(f);Assert.AreEqual(240*112*4,rgba.Length);if(f.Scanline is not null){scanlineFrames++;pixelDifference|=!rgba.SequenceEqual(composer.ComposeField(f with{Scanline=null}));}Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(o.Snapshot()));}
            string why=$"move {id}, doubles {doubles}, actor {actor}, turn {turn}";
            // Dig masks only rows at/below raw Y+32. Player cutoffs 112/120 are
            // outside this 112-row field, so normal clipping already hides them.
            var actorBody=bodies[actor];int cutoff=((actorBody.Base(doubles).Y-(actorBody.Slot.IsPlayer?0:actorBody.Elevation))&255)+32;
            bool expectsVisibleMask=id!=91||cutoff<112;
            Assert.IsFalse(o.Active,why);Assert.IsTrue(scanlineFrames>0,why);Assert.AreEqual(expectsVisibleMask,pixelDifference,why);Assert.AreEqual(0,o.ActiveVisualCount);Assert.AreEqual(0,scene.Count);Assert.IsFalse(o.GraphicsLoaded);var end=o.Snapshot();Assert.AreEqual(0,end.BackgroundSlots.Length);Assert.IsNull(end.Scanline);Assert.AreEqual(id==91&&turn==0,end.BattlerVisibility.Single(v=>v.Slot==actor).SpriteInvisible);Assert.IsTrue(audio.Cues.Count>0);o.Cancel();
        }
    }
    [TestMethod,DataRow(1),DataRow(3)]
    public void DigOpponentCutoffMasksOriginalTilesBelowGroundBeforeFieldClipping(int slot)
    {
        var bodies=Bodies(true).Select(b=>b with{Visible=b.Slot.Value==slot,SourceVisibility=null}).ToArray();var body=bodies[slot];var center=body.Base(true);int cutoff=center.Y+32;
        var assets=new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,Enumerable.Repeat((ushort)0x801f,240*112).ToArray())},{"impact",new(32,32,new ushort[1024])}};
        foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());
        // The source BG has sunk eight pixels; its final eight rows now cross
        // the original ground line. A +240 HOFS masks those rows, not the tile above.
        var scroll=new PixelPoint(32-center.X,32-center.Y-8);var registers=new PixelPoint[2];registers[body.BackgroundRank-1]=scroll;
        var words=Enumerable.Range(0,161).Select(y=>unchecked((ushort)(scroll.X+(y>=cutoff?240:0)))).ToArray();
        var frame=new SourceAnimationFrame(0,true,new PixelPoint[4],[slot],16,0,[],[]){Battlers=bodies,Doubles=true,BackgroundScrollOffsets=registers,Scanline=new(body.BackgroundRank==1?SourceScanlineDestination.Bg1Horizontal:SourceScanlineDestination.Bg2Horizontal,false,words)};
        var composer=new SourceBattleFrameComposer(assets,bodies);var masked=composer.ComposeField(frame);var unmasked=composer.ComposeField(frame with{Scanline=null});
        byte[] Pixel(byte[] pixels,int row)=>pixels.AsSpan((row*240+center.X)*4,4).ToArray();
        CollectionAssert.AreEqual(new byte[]{0,255,0,255},Pixel(masked,cutoff-1));
        foreach(int row in new[]{cutoff,cutoff+7}){CollectionAssert.AreEqual(new byte[]{0,255,0,255},Pixel(unmasked,row));CollectionAssert.AreEqual(new byte[]{255,0,0,255},Pixel(masked,row));}
        CollectionAssert.AreEqual(new byte[]{255,0,0,255},Pixel(unmasked,cutoff+8));
    }
    [TestMethod,DataRow(91),DataRow(151),DataRow(166),DataRow(349)]
    public void CancelAtEachLifecycleBoundaryStopsDmaWithoutClearingRetainedBanks(int id)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var r=resources.Resolve(id);
        foreach(int stop in new[]{0,1,3,22,35,68,90,140,220})
        {
            var bodies=Bodies(true);bodies[2]=bodies[2] with{SourceVisibility=new(true,false,true)};var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(r.Script,new("scanline-cancel",1,id,new(0),new(id is 151 or 349?0:1)){MoveTurn=id==91?(byte)0:null},bodies,true,audio,Terrain(resources),spriteScene:scene,spriteMaterials:r.Assets);var initial=o.Snapshot();var state=Property<SourceScanlineState>(o,"ScanlineState");for(int i=0;i<stop&&o.Active;i++)o.Tick();var memory=Enumerable.Range(0,1920).Select(i=>state.Read(i/960,i%960)).ToArray();int cues=audio.Cues.Count;o.Cancel();o.Cancel();Assert.AreEqual(0,state.State);CollectionAssert.AreEqual(memory,Enumerable.Range(0,1920).Select(i=>state.Read(i/960,i%960)).ToArray());Assert.IsNull(o.Snapshot().Scanline);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,o.ActiveVisualCount);Assert.AreEqual(1,audio.Stops);Assert.AreEqual(cues,audio.Cues.Count);Assert.AreEqual(JsonSerializer.Serialize(initial.BattlerVisibility),JsonSerializer.Serialize(o.Snapshot().BattlerVisibility));
        }
    }
    [TestMethod]
    public void DuplicateUnboundedOrMalformedScanlineOwnersFailBeforeAudio()
    {
        void Reject(SourceAnimationScript s,int id,int occupied=0){var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var registry=Field<object>(scene,"Sprites");for(int i=0;i<occupied;i++)Invoke(registry,"Add",new Occupied(63-i));Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,new("bad",1,id,new(0),new(id is 151 or 349?0:1)){MoveTurn=id==91?(byte)0:null},Bodies(true),true,audio,spriteScene:scene));Assert.AreEqual(0,audio.Cues.Count);Assert.AreEqual(occupied,scene.Count);}
        foreach(int id in new[]{91,151,166,349}){var s=AnimationCompositionFixture.Script(id);var own=s.Commands.First(c=>c.Op=="createvisualtask"&&Names.Take(5).Contains(c.Args[0]));Reject(Primitive(s) with{Commands=[own,own,C("end")]},id);Reject(s,id,43);}
        var dance=AnimationCompositionFixture.Script(349);var orb=C("createsprite",Names[8],"0","2","0");Reject(Primitive(dance) with{Commands=[..Enumerable.Repeat(orb,43),C("end")]},349);
        foreach(string value in new[]{"-1","256"})Reject(Primitive(dance) with{Commands=[C("createsprite",Names[8],"0","2",value),C("end")]},349);
        var dig=AnimationCompositionFixture.Script(91);Reject(Primitive(dig) with{Commands=[C("createsprite",Names[5],"0","2","0","0","12","4","-16","0"),C("end")]},91);
        foreach(var (id,name) in new[]{(91,Names[6]),(166,Names[7]),(349,Names[8])}){var s=AnimationCompositionFixture.Script(id);var recipes=s.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[name]=recipes[name] with{Width=128};Reject(s with{SpriteRecipes=recipes},id);}
    }
}
