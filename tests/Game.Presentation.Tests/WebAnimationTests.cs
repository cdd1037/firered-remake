using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class WebAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio{public bool IsPlaying=>false;public void Play(SourceAnimationSound c){}public void SetPan(int p,int f){}public void StopAnimationEffects(){}}
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static AnimationBattler[] Battlers(bool doubles,int hiddenMask=0)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64){Visible=(hiddenMask&(1<<i))==0}).ToArray();
    private static object? Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(target,args);
    [TestMethod]
    public void CallbacksMatchOriginalCGeometryVisibilityLifetimeAlphaAndAffineSteps()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        using var json=SourceVectorFixture.Read("web-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_web_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/web-vectors.json");
        int cases=0,rows=0;
        foreach(var c in json.RootElement.GetProperty("cases").EnumerateArray())
        {
            int kind=c.GetProperty("kind").GetInt32(),actor=c.GetProperty("actor").GetInt32(),target=c.GetProperty("target").GetInt32(),mode=c.GetProperty("mode").GetInt32(),average=c.GetProperty("average").GetInt32(),y=c.GetProperty("yOffset").GetInt32();bool doubles=mode!=0;
            int hidden=mode==2?1<<(target^2):0;var script=resources.Resolve(kind==2?169:81).Script with{Commands=[C("end")]};
            var o=new SourceBattleAnimation(script,new("web-vectors",1,script.MoveId,new(actor),new(target)),Battlers(doubles,hidden),doubles,new Audio());
            string template=kind==0?"gWebThreadSpriteTemplate":kind==1?"gStringWrapSpriteTemplate":"gSpiderWebSpriteTemplate";var recipe=script.SpriteRecipes[template];
            var type=typeof(SourceBattleAnimation).GetNestedType("WebSprite",BindingFlags.NonPublic)!;
            int[] args=kind==0?[20,0,512,20,average]:kind==1?[0,y]:[];
            var sprite=Activator.CreateInstance(type,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,[4,2,recipe,args,o],null)!;
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int[] r=row.EnumerateArray().Select(v=>v.GetInt32()).ToArray();if(r[0]>=0)Invoke(sprite,"Step",o);
                var f=(SourceVisualSpriteFrame)type.GetProperty("Frame")!.GetValue(sprite)!;bool finished=(bool)type.GetProperty("Finished")!.GetValue(sprite)!;
                Assert.AreEqual(r[1]==0,finished,$"case {cases} tick {r[0]}");Assert.AreEqual(new PixelPoint(r[2],r[3]),f.Position);Assert.AreEqual(new PixelPoint(r[4],r[5]),f.Offset);Assert.AreEqual(r[6]==0,f.Visible);
                if(kind==2){var state=o.Snapshot();Assert.AreEqual(r[8]!=0,state.BlendEnabled);Assert.AreEqual(r[9]&255,state.AlphaA);Assert.AreEqual(r[9]>>8,state.AlphaB);Assert.AreEqual(65536/r[10],f.MatrixA);Assert.AreEqual(65536/r[11],f.MatrixD);Assert.AreEqual(0,f.MatrixB);Assert.AreEqual(0,f.MatrixC);}rows++;
            }
            cases++;
        }
        Assert.IsTrue(cases>=100);Assert.IsTrue(rows>5000);
    }
    [TestMethod][DataRow(81)][DataRow(169)]
    public void CompleteRootsUseResolvedMaterialsAcrossAllSixActorsAndHiddenPartners(int move)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(bool hiddenPartner in doubles?new[]{false,true}:new[]{false})
        {
            int target=actor^1;var b=Battlers(doubles,hiddenPartner?1<<(target^2):0);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,new ushort[240*112]);foreach(var mon in b)assets[mon.Asset]=new(64,64,Enumerable.Repeat((ushort)0xffff,4096).ToArray());
            var o=new SourceBattleAnimation(resolved.Script,new("web",1,move,new(actor),new(target)),b,doubles,new Audio(),spriteMaterials:resolved.Assets);var composer=AnimationCompositionFixture.Create(resources,resolved,assets,b);int ticks=0,peak=0;
            while(o.Active&&ticks++<1024){var f=o.Tick();peak=Math.Max(peak,f.Sprites.Length);Assert.AreEqual(240*112*4,composer.ComposeField(f).Length);}
            Assert.IsFalse(o.Active);Assert.IsTrue(peak>0);Assert.AreEqual(0,o.ActiveVisualCount);Assert.IsFalse(o.Snapshot().BlendEnabled);Assert.AreEqual(0,o.Snapshot().BackgroundSlots.Length);
        }
    }
    [TestMethod]
    public void ThreadRejectsZeroSpeedAndUnsupportedAverageModeAndMissingMaterials()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var script=resources.Resolve(81).Script;
        foreach(var args in new[]{new[]{"20","0","0","20","1"},new[]{"20","0","512","20","2"}})
        {var bad=script with{Commands=[C("createsprite",["gWebThreadSpriteTemplate","ANIM_TARGET","2",..args]),C("end")]};Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(bad,new("web-bad",1,81,new(0),new(1)),Battlers(true),true,new Audio()));}
        var absent=script with{SpriteDependencies=["gUnknownWebSpriteTemplate"]};Assert.Throws<InvalidDataException>(()=>resources.Resolve(absent));
    }
    [TestMethod]
    public void SourceSpeedToSignedDurationRejectsZeroAndWrappedNegativeWithoutClamping()
    {
        var script=AnimationCompositionFixture.Script(81) with{Commands=[C("end")]};
        var type=typeof(SourceBattleAnimation).GetNestedType("WebSprite",BindingFlags.NonPublic)!;var recipe=script.SpriteRecipes["gWebThreadSpriteTemplate"];
        foreach(int speed in new[]{32767,1})
        {
            // 20px self travel /32767 truncates to0; 148px /1 wraps37888 to signed16 negative.
            var o=new SourceBattleAnimation(script,new("web-speed",1,81,new(0),new(speed==32767?0:1)),Battlers(true),true,new Audio());
            var error=Assert.Throws<TargetInvocationException>(()=>Activator.CreateInstance(type,BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance,null,[4,2,recipe,new[]{20,0,speed,20,0},o],null));
            Assert.IsInstanceOfType<InvalidDataException>(error.InnerException);
        }
    }

}
