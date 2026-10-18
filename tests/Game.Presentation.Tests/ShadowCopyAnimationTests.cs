using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ShadowCopyAnimationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound>Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects()=>Stops++;}
    private sealed class Occupied(int slot):SourceBattleAnimation.VisualSprite(slot,100)
    {public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"foreign",default,default,0,false,false,false,256,null,100);public override void Step(SourceBattleAnimation o){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static object? Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private|BindingFlags.Public)!.Invoke(o,args);
    private static int I(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static CommittedMoveAnimation Cue(int actor,int target)=>new("nightmare",1,171,new(actor),new(target));
    private static AnimationBattler[] Battlers(bool doubles,int hidden=-1,int absent=-1)=>Enumerable.Range(0,doubles?4:2)
        .Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,48){SourceVisibility=new(i!=absent,false,i==hidden)}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript script)=>script with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};

    [TestMethod]
    public void NativeNightmareMatchesHiddenRawCopiesFixedPointDriftAlternatingAlphaAndDeferredCleanup()
    {
        using var vectors=SourceVectorFixture.Read("shadow-copy-source-vectors.json", "python tests/Reference/ShadowCopy/generate.py content/raw/pokefirered/037335f4 --repo . --output tests/Game.Presentation.Tests/Fixtures/shadow-copy-source-vectors.json");
        using var resources=new SourceAnimationResources(Folder);var script=Primitive(resources.Resolve(171).Script);int cases=0,rows=0;
        foreach(var c in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {
            bool doubles=c.GetProperty("doubles").GetBoolean(),free=c.GetProperty("free").GetBoolean(),present=c.GetProperty("present").GetBoolean();int target=I(c,"target"),actor=I(c,"actor");
            var scene=new SourceBattleAnimation.SpriteScene();var bodies=Battlers(doubles,c.GetProperty("hidden").GetBoolean()?target:-1,present?-1:target);
            var o=new SourceBattleAnimation(script,Cue(actor,target),bodies,doubles,new Audio(),spriteScene:scene);
            for(int slot=doubles?4:2;slot<64;slot++)if(!free||slot!=4)scene.Sprites.Add(new Occupied(slot){Owner=o});
            var args=Field<short[]>(o,"_animationArgs");for(int i=0;i<args.Length;i++)args[i]=(short)(300+i);var before=(short[])args.Clone();
            Field<PixelPoint[]>(o,"_offsets")[target]=new(3,-2);
            Assert.IsTrue((bool)Invoke(o,"TryCreateShadowCopyTask",C("createvisualtask","AnimTask_NightmareClone","2"))!);
            var copy=scene.Sprites.SingleOrDefault(s=>s is not Occupied);
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int frame=I(row,"frame");if(frame>0){Invoke(o,"AdvanceSourceSprites");Invoke(o,"RunSourceTasks");}
                string why=c.GetProperty("id").GetString()+" frame "+frame;
                Assert.AreEqual(I(row,"alive"),Field<IEnumerable>(o,"_tasks").Cast<object>().Count(),why+" task lifetime");
                var blend=N(row.GetProperty("blend"));var f=o.Snapshot();Assert.AreEqual((blend[0]&64)!=0,f.BlendEnabled,why+" BLDCNT");Assert.AreEqual(blend[1]&255,f.AlphaA,why+" EVA");Assert.AreEqual(blend[1]>>8,f.AlphaB,why+" EVB");
                if(row.GetProperty("copy").ValueKind==JsonValueKind.Null)Assert.IsNull(copy);
                else
                {
                    Assert.IsNotNull(copy);var n=row.GetProperty("copy");var s=copy.Frame;
                    Assert.AreEqual(I(n,"alive")==0,copy.Finished,why+" child lifetime");CollectionAssert.AreEqual(N(n.GetProperty("position")),new[]{s.Position.X,s.Position.Y,s.Offset.X,s.Offset.Y},why+" signed fixed point");
                    Assert.AreEqual(I(n,"visible")!=0,s.Visible);Assert.IsTrue(s.Blend);Assert.AreEqual(I(n,"priority"),s.OamPriority);Assert.AreEqual(I(n,"subpriority"),s.Subpriority);Assert.AreEqual(target,s.BattlerCopySlot);Assert.IsFalse(copy.Counted);
                    Assert.AreEqual(I(n,"alive"),scene.Sprites.Count(x=>x is not Occupied));
                }
                CollectionAssert.AreEqual(before,args,why+" no retained argument mutation");Assert.IsNull(Field<object?>(o,"_visualRandom"));rows++;
            }
            o.Cancel();cases++;
        }
        Assert.AreEqual(36,cases);Assert.AreEqual(2052,rows);
    }

    [TestMethod]
    public void CompleteNightmareRootComposesBothSidesAndRestoresGhostBackgroundAndRawOwnership()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(171);
        Assert.AreEqual("Move_NIGHTMARE",resolved.Script.Label);Assert.IsTrue(resolved.Backgrounds.Count>0);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var bodies=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());assets.TryAdd("impact",new(32,32,new ushort[1024]));
            foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64>12&&i%64<51&&i/64>8&&i/64<55?0x83e0:0)).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var o=new SourceBattleAnimation(resolved.Script,Cue(actor,actor^1),bodies,doubles,audio,spriteScene:scene,spriteMaterials:assets);
            bool copy=false,clonePixels=false,ghost=false,alpha=false;int ticks=0;
            while(o.Active&&ticks++<500)
            {
                var f=o.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(o.Snapshot()));var pixels=composer.ComposeField(f);
                copy|=f.Sprites.Any(s=>s.Template=="AnimNightmareClone");alpha|=f.BlendEnabled;ghost|=f.MoveBackground is not null;
                if(f.Sprites.Length>0&&ticks%3==0)clonePixels|=!pixels.SequenceEqual(composer.ComposeField(f with{Sprites=[]}));
                foreach(var s in f.Sprites)Assert.IsNotNull(s.Material,"Clone material is detached from the scene's later body lookup.");
            }
            Assert.IsFalse(o.Active);Assert.IsTrue(copy&&clonePixels&&ghost&&alpha);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,o.ActiveVisualCount);Assert.IsFalse(o.HasResidualSprites);Assert.IsFalse(o.Snapshot().BlendEnabled);Assert.AreEqual(0,o.Snapshot().BackgroundSlots.Length);Assert.IsTrue(o.Snapshot().BattlerBackgroundEnabled.All(x=>x));Assert.IsFalse(o.GraphicsLoaded);Assert.IsNull(Field<object?>(o,"_visualRandom"));Assert.AreEqual(1,audio.Cues.Count(c=>c.Id=="SE_M_NIGHTMARE"));
            foreach(int cancel in new[]{0,1,16,32,50,82,110,150})
            {
                var s=new SourceBattleAnimation.SpriteScene();var sound=new Audio();var r=new SourceBattleAnimation(resolved.Script,Cue(actor,actor^1),bodies,doubles,sound,spriteScene:s,spriteMaterials:assets);
                for(int i=0;i<cancel&&r.Active;i++)r.Tick();r.Cancel();r.Cancel();Assert.AreEqual(0,s.Count);Assert.AreEqual(0,r.ActiveVisualCount);Assert.IsFalse(r.Snapshot().BlendEnabled);Assert.AreEqual(0,r.Snapshot().BackgroundSlots.Length);Assert.AreEqual(1,sound.Stops);
            }
        }
    }

    [TestMethod]
    public void UnreviewedCloneCompanionsAndMalformedOperandsFailBeforeSceneOrAudioEffects()
    {
        using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(171).Script;
        var bads=new[]{script with{Commands=[C("createvisualtask","AnimTask_NightmareClone","2","1"),C("end")]},script with{Commands=[C("createvisualtask","AnimTask_NightmareClone","256"),C("end")]},script with{Commands=[C("createvisualtask","AnimTask_NightmareClone","2"),C("createvisualtask","AnimTask_NightmareClone","2"),C("end")]},script with{Commands=[C("createvisualtask","AnimTask_NightmareClone","2"),C("createvisualtask","AnimTask_NightShadeClone","2","1"),C("end")]},script with{Commands=[C("invisible","ANIM_TARGET"),..script.Commands]}};
        foreach(var bad in bads){var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(bad,Cue(0,1),Battlers(true),true,audio,spriteScene:scene));Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,audio.Cues.Count);}
    }
}
