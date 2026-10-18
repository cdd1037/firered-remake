using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ExpulsionAnimationTests
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private sealed class Audio:ISourceAnimationAudio
    {
        public bool SupportsCries=>true;public bool IsCryPlaying{get;set;}public bool IsPlaying=>false;
        public List<SourceAnimationCry> Cries{get;}=[];public List<SourceAnimationSound> Sounds{get;}=[];public int Stops,CryStops;
        public void PlayCry(SourceAnimationCry c)=>Cries.Add(c);public void Play(SourceAnimationSound c)=>Sounds.Add(c);public void SetPan(int pan,int frame){}public void StopAnimationEffects()=>Stops++;public void StopAnimationCries()=>CryStops++;
    }
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("expulsion-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_expulsion_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/expulsion-vectors.json");
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static object? Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private|BindingFlags.Public)!.Invoke(o,args);
    private static object Nested(string name,params object[] args)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(name,BindingFlags.NonPublic)!,Private|BindingFlags.Public,null,args,null)!;
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static AnimationBattler[] Battlers(bool doubles,int height=7,int flags=0)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,height,0,64,64){SourceVisibility=new(true,(flags&1)!=0,(flags&2)!=0),Visible=(flags&3)!=3}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript script)=>script with{Commands=[C("delay","1"),C("end")],Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),Labels=new Dictionary<string,int>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static SourceBattleAnimation Owner(SourceAnimationScript full,bool doubles,int actor,int target,AnimationBattler[]? bodies=null,Audio? audio=null)=>new(Primitive(full),new("expulsion",1,full.MoveId,new(actor),new(target)),bodies??Battlers(doubles),doubles,audio??new Audio());

    [TestMethod]
    public void SlideMatchesExtractedTargetSidePresentHiddenPartnerSelectionAndSignedRawOffsets()
    {
        var full=AnimationCompositionFixture.Script(18);using var json=Vectors();int cases=0,rows=0,skipped=0;
        foreach(var group in json.RootElement.GetProperty("slide").EnumerateArray().Select(N).GroupBy(r=>(r[0],r[1],r[2],r[3],r[4],r[5])))
        {
            var(d,actor,target,part,visibility,variant)=group.Key;bool doubles=d!=0;int selected=part switch{0=>actor,1=>target,2=>actor^2,_=>target^2};var o=Owner(full,doubles,actor,target,Battlers(doubles,7,visibility));
            foreach(var b in o.Snapshot().Battlers)Field<PixelPoint[]>(o,"_offsets")[b.Slot.Value]=new(7,9);
            if(selected<(doubles?4:2)){var p=(PixelPoint)Invoke(o,"BattlerPosition",selected)!;Invoke(o,"SetBattlerPosition",selected,new PixelPoint(p.X+(variant==1?17:0),p.Y));Field<PixelPoint[]>(o,"_offsets")[selected]=new(variant==2?32760:7,9);}
            Assert.IsTrue((bool)Invoke(o,"TryCreateExpulsionTask",C("createvisualtask","AnimTask_SlideOffScreen","5",part.ToString(),(variant==0?2:variant==1?8:32767).ToString()))!);
            var tasks=Field<IEnumerable>(o,"_tasks").Cast<object>().ToArray();if(tasks.Length==0)skipped++;
            foreach(var r in group)
            {
                if(r[6]>=0)foreach(var t in tasks)if(!(bool)t.GetType().GetProperty("Finished")!.GetValue(t)!)Invoke(t,"Step",o);
                string why=$"{group.Key} tick {r[6]}";Assert.AreEqual(r[12]!=0,tasks.Any(t=>!(bool)t.GetType().GetProperty("Finished")!.GetValue(t)!),why);
                if(selected<(doubles?4:2)){Assert.AreEqual(r[9],((PixelPoint)Invoke(o,"BattlerPosition",selected)!).X,why);Assert.AreEqual(new PixelPoint(r[10],r[11]),Field<PixelPoint[]>(o,"_offsets")[selected],why);}
                rows++;
            }
            o.Cancel();cases++;
        }
        Assert.AreEqual(960,cases);Assert.IsTrue(rows>15000);Assert.IsTrue(skipped>0);
    }

    [TestMethod]
    public void TeleportMatchesEveryNativeFootOffsetTwentyAffineTicksSideLiftAndVisibilityReset()
    {
        var full=AnimationCompositionFixture.Script(100);using var json=Vectors();int cases=0;
        foreach(var group in json.RootElement.GetProperty("teleport").EnumerateArray().Select(N).GroupBy(r=>(r[0],r[1],r[2],r[3])))
        {
            var(d,actor,height,visibility)=group.Key;var o=Owner(full,d!=0,actor,actor^1,Battlers(d!=0,height,visibility));Field<PixelPoint[]>(o,"_offsets")[actor]=new(7,9);var task=Nested("TeleportTask",o,actor);
            foreach(var r in group)
            {
                if(r[4]>=0)Invoke(task,"Step",o);string why=$"{group.Key} tick {r[4]}";Assert.AreEqual(new PixelPoint(r[5],r[6]),(PixelPoint)Invoke(o,"BattlerPosition",actor)!,why);Assert.AreEqual(new PixelPoint(r[7],r[8]),Field<PixelPoint[]>(o,"_offsets")[actor],why);
                Assert.AreEqual(r[11]==0?256:r[9],Field<int[]>(o,"_xScale")[actor],why);Assert.AreEqual(r[11]==0?256:r[10],Field<int[]>(o,"_yScale")[actor],why);Assert.AreEqual(r[11]!=0,Field<bool[]>(o,"_affineMode")[actor],why);Assert.AreEqual(r[12]!=0,o.Snapshot().BattlerVisibility[actor].SpriteInvisible,why);Assert.AreEqual((visibility&1)!=0,o.Snapshot().BattlerVisibility[actor].LogicalInvisible,why);Assert.AreEqual(r[13]==0,(bool)task.GetType().GetProperty("Finished")!.GetValue(task)!,why);
            }
            Assert.IsTrue(o.Snapshot().BattlerVisibility[actor].SpriteInvisible);Assert.AreEqual(272,((PixelPoint)Invoke(o,"BattlerPosition",actor)!).X);o.Cancel();cases++;
        }
        Assert.AreEqual(768,cases);
    }

    [TestMethod]
    public void WhirlwindMatchesSourceSeekCreationAdvanceFrameEndRestartAndLifetimeAcrossPositions()
    {
        var full=AnimationCompositionFixture.Script(18);using var json=Vectors();int cases=0,rows=0;
        foreach(var group in json.RootElement.GetProperty("whirlwind").EnumerateArray().Select(N).GroupBy(r=>(r[0],r[1],r[2],r[3],r[4],r[5])))
        {
            var(d,actor,target,part,seek,life)=group.Key;var o=Owner(full,d!=0,actor,target);var sprite=Nested("WhirlwindLineSprite",4,28,full.SpriteRecipes["gWhirlwindLineSpriteTemplate"],new[]{-17,8,part,life!=0?60:0,seek},o);
            foreach(var r in group)
            {
                if(r[6]>=0){Invoke(sprite,"Step",o);if(!(bool)sprite.GetType().GetProperty("Finished")!.GetValue(sprite)!)Invoke(sprite,"AdvanceAnimation",o);}
                string why=$"{group.Key} tick {r[6]}";var f=(SourceVisualSpriteFrame)sprite.GetType().GetProperty("Frame")!.GetValue(sprite)!;Assert.AreEqual(new PixelPoint(r[7],r[8]),f.Position,why);Assert.AreEqual(r[9],f.Offset.X,why);Assert.AreEqual(r[10],f.Tile,why);Assert.AreEqual(r[11]!=0,f.HFlip,why);Assert.AreEqual(r[12]!=0,Field<SourceSpriteFrameProgram>(sprite,"_frames").Ended,why);Assert.AreEqual(r[13],(int)Field<short>(sprite,"_phase"),why);Assert.AreEqual(r[14],(int)Field<short>(sprite,"_remaining"),why);Assert.AreEqual(r[15]==0,(bool)sprite.GetType().GetProperty("Finished")!.GetValue(sprite)!,why);rows++;
            }
            o.Cancel();cases++;
        }
        Assert.AreEqual(400,cases);Assert.IsTrue(rows>10000);
    }

    [TestMethod]
    public void RoarAndGrowlSecondPhaseWaitOnGlobalCryAndKeepTheNativeMinimumVisits()
    {
        var full=AnimationCompositionFixture.Script(46);using var json=Vectors();int cases=0;
        foreach(var group in json.RootElement.GetProperty("cries").EnumerateArray().Select(N).GroupBy(r=>(r[0],r[1])))
        {
            var(kind,release)=group.Key;var audio=new Audio();var o=Owner(full,false,0,1,audio:audio);var task=Nested("VoiceCryTask",132,-64,kind==0?10:kind==3?8:6,kind==2);
            foreach(var r in group){audio.IsCryPlaying=r[2]<release;audio.Cries.Clear();Invoke(task,"Step",o);Assert.AreEqual(r[4]!=0,(bool)task.GetType().GetProperty("Finished")!.GetValue(task)!);Assert.AreEqual(r[5]==0?0:1,audio.Cries.Count);if(r[5]!=0)Assert.AreEqual(new SourceAnimationCry(r[6],r[5],r[7],-1),audio.Cries.Single());}o.Cancel();cases++;
        }
        Assert.AreEqual(36,cases);
    }

    [TestMethod][DataRow(18)][DataRow(46)][DataRow(100)]
    public void CompleteRootsComposeAllPositionsAndRetainSourceExitStateUntilSceneContinuation(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var full=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            int target=move==100?actor:actor^1;var b=Battlers(doubles);var assets=full.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var body in b)assets[body.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());var surface=resources.CreateBackgroundSurface(0);var palette=Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray();var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(full.Script,new("expulsion",1,move,new(actor),new(target)),b,doubles,audio,palette,spriteScene:scene,spriteMaterials:full.Assets,originalPartySpecies:b.ToDictionary(x=>x.Slot.Value,_=>132));var composer=AnimationCompositionFixture.Create(resources,full,assets,b,surface);int ticks=0,peak=0;bool psychic=false,affine=false,pixels=false;
            while(o.Active&&ticks++<1024){var f=o.Tick();var rgba=composer.ComposeField(f);Assert.AreEqual(240*112*4,rgba.Length);peak=Math.Max(peak,f.Sprites.Length);psychic|=f.MoveBackground?.Id==full.Script.BackgroundSymbols.GetValueOrDefault("BG_PSYCHIC",-1);affine|=f.BattlerAffine[actor].DoubleSize;if(f.Sprites.Length>0)pixels|=!rgba.SequenceEqual(composer.ComposeField(f with{Sprites=[]}));}
            Assert.IsFalse(o.Active);Assert.AreEqual(0,o.ActiveVisualCount);Assert.AreEqual(0,scene.Count);Assert.IsFalse(o.GraphicsLoaded);Assert.IsFalse(o.Snapshot().BlendEnabled);Assert.IsNull(Field<object?>(o,"_visualRandom"));
            if(move==100){Assert.IsTrue(psychic&&affine);Assert.IsTrue(o.Snapshot().BattlerVisibility[actor].SpriteInvisible);Assert.AreEqual(272,((PixelPoint)Invoke(o,"BattlerPosition",actor)!).X);Assert.IsNull(o.Snapshot().MoveBackground!.Id);Assert.AreEqual(1,audio.Sounds.Count(c=>c.Id=="SE_M_TELEPORT"));}
            else{Assert.IsTrue(peak>=3&&pixels);int x=((PixelPoint)Invoke(o,"BattlerPosition",target)!).X+Field<PixelPoint[]>(o,"_offsets")[target].X;Assert.IsTrue(x< -32||x>272);Assert.IsFalse(o.Snapshot().BattlerVisibility[target].SpriteInvisible);}
            if(move==46){CollectionAssert.AreEqual(new[]{7,8},audio.Cries.Select(c=>c.Mode).ToArray());Assert.IsTrue(audio.Cries.All(c=>c.Species==132&&c.Pan==((actor&1)==0?-64:63)));}
        }
    }

    [TestMethod]
    public void CancellationRestoresBothVisibilityFlagsAffineRawOffsetsAndAudioExactlyOnce()
    {
        using var resources=new SourceAnimationResources(Folder);foreach(int move in new[]{18,46,100})foreach(int ticks in new[]{0,1,15,20,24,28,40,60,100})
        {var full=resources.Resolve(move);var b=Battlers(true);var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(full.Script,new("cancel",1,move,new(2),new(move==100?2:3)),b,true,audio,new ushort[16],spriteScene:scene,spriteMaterials:full.Assets,originalPartySpecies:b.ToDictionary(x=>x.Slot.Value,_=>132));for(int i=0;i<ticks&&o.Active;i++)o.Tick();o.Cancel();o.Cancel();Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,o.ActiveVisualCount);Assert.IsTrue(o.Snapshot().Offsets.All(p=>p==default));Assert.IsTrue(Field<PixelPoint[]>(o,"_battlerPositionOffsets").All(p=>p==default));Assert.IsTrue(o.Snapshot().BattlerVisibility.All(v=>!v.SpriteInvisible&&!v.LogicalInvisible));Assert.IsTrue(o.Snapshot().BattlerAffine.All(a=>!a.DoubleSize&&a.XScale==256&&a.YScale==256));Assert.AreEqual(1,audio.Stops);Assert.AreEqual(1,audio.CryStops);Assert.IsFalse(o.GraphicsLoaded);Assert.IsNull(o.Snapshot().MoveBackground!.Id);}
    }

    [TestMethod]
    public void InvalidSeekAffineSourceDurationCrySelectorAndMissingPrimarySpriteFailClosed()
    {
        var line=AnimationCompositionFixture.Script(18);var teleport=AnimationCompositionFixture.Script(100);var roar=AnimationCompositionFixture.Script(46);
        void Reject(SourceAnimationScript s,AnimationBattler[]? b=null){var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,new("invalid",1,s.MoveId,new(0),new(1)),b??Battlers(false),false,audio,new ushort[16],originalPartySpecies:new Dictionary<int,int>{{0,132},{1,4}}));Assert.AreEqual(0,audio.Cries.Count);}
        Reject(Primitive(line) with{Commands=[C("createsprite","gWhirlwindLineSpriteTemplate","ANIM_ATTACKER","2","0","0","ANIM_TARGET","60","5"),C("end")]});
        Reject(Primitive(teleport) with{Commands=[C("createvisualtask","AnimTask_Teleport","2"),C("end")],TaskAffinePrograms=new Dictionary<string,SourceTaskAffineCommand[]>{{"sAffineAnim_Teleport",[new("frame",64,-4,0,19),new("frame",0,0,0,200),new("end")]}}});
        Reject(Primitive(roar) with{Commands=[C("createvisualtask","SoundTask_PlayDoubleCry","2","ANIM_ATTACKER","0"),C("end")]});
        var absent=Battlers(false);absent[0]=absent[0] with{SourceVisibility=new(false,true,true)};Reject(Primitive(teleport) with{Commands=[C("createvisualtask","AnimTask_Teleport","2"),C("end")]},absent);
        absent=Battlers(false);absent[1]=absent[1] with{SourceVisibility=new(false,true,true)};Reject(Primitive(line) with{Commands=[C("createvisualtask","AnimTask_SlideOffScreen","5","ANIM_TARGET","2"),C("end")]},absent);
    }
}
