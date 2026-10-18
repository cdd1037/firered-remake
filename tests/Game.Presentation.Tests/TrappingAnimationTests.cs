using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class TrappingAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Calls{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Calls.Add(c);public void SetPan(int p,int f){}public void StopAnimationEffects(){}}
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,(i&1)==0?7:9,0,64,64)).ToArray();
    private static object? Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic)!.Invoke(target,args);
    [TestMethod]
    public void BothCallbacksMatchNativeAffineRestartPauseAlphaSubpriorityAndSoundVectors()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));using var json=SourceVectorFixture.Read("trapping-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_trapping_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/trapping-vectors.json");int cases=0,rows=0;
        foreach(var c in json.RootElement.GetProperty("cases").EnumerateArray())
        {
            int kind=c.GetProperty("kind").GetInt32(),actor=c.GetProperty("actor").GetInt32(),target=c.GetProperty("target").GetInt32();bool doubles=c.GetProperty("mode").GetInt32()!=0;
            var b=Battlers(doubles);var script=resources.Resolve(kind==0?212:335).Script with{Commands=[C("end")]};var audio=new Audio();var o=new SourceBattleAnimation(script,new("trapping-vector",1,script.MoveId,new(actor),new(target)),b,doubles,audio);
            var recipe=script.SpriteRecipes[kind==0?"gMeanLookEyeSpriteTemplate":"gBlockXSpriteTemplate"];var type=typeof(SourceBattleAnimation).GetNestedType("TrappingSprite",BindingFlags.NonPublic)!;
            int priority=kind==0?Math.Max(3,b[actor].Subpriority-2):b[target].Subpriority+2;
            var sprite=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[4,priority,recipe,o],null)!;
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int[] r=row.EnumerateArray().Select(v=>v.GetInt32()).ToArray();audio.Calls.Clear();
                if(r[0]>=0){Invoke(sprite,"Step",o);if(!(bool)type.GetProperty("Finished")!.GetValue(sprite)!)Invoke(sprite,"AdvanceAnimation",o);}
                var f=(SourceVisualSpriteFrame)type.GetProperty("Frame")!.GetValue(sprite)!;var state=o.Snapshot();
                Assert.AreEqual(r[1]==0,(bool)type.GetProperty("Finished")!.GetValue(sprite)!, $"case {cases} tick {r[0]}");Assert.AreEqual(new PixelPoint(r[2],r[3]),f.Position);Assert.AreEqual(new PixelPoint(r[4],r[5]),f.Offset);Assert.AreEqual(r[6]==0,f.Visible);Assert.AreEqual(r[8],f.Subpriority);Assert.AreEqual(r[9]!=0,state.BlendEnabled);Assert.AreEqual(r[10]&255,state.AlphaA);Assert.AreEqual(r[10]>>8,state.AlphaB);
                var data=(short[])type.GetField("_data",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(sprite)!;CollectionAssert.AreEqual(r.Skip(21).Take(8).Select(v=>(short)v).ToArray(),data);
                if(kind==0){Assert.AreEqual(r[29],f.MatrixA);Assert.AreEqual(r[30],f.MatrixB);Assert.AreEqual(r[31],f.MatrixC);Assert.AreEqual(r[32],f.MatrixD);Assert.AreEqual(r[17]!=0,(bool)type.GetField("_paused",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(sprite)!);}
                if(r[19]==0)Assert.AreEqual(0,audio.Calls.Count);else{Assert.AreEqual(1,audio.Calls.Count);Assert.AreEqual(script.SoundEffects.Single(p=>p.Value==r[19]).Key,audio.Calls[0].Id);Assert.AreEqual(r[20],audio.Calls[0].Pan);}rows++;
            }
            cases++;
        }
        Assert.AreEqual(40,cases);Assert.IsTrue(rows>5000);
    }
    [TestMethod][DataRow(212)][DataRow(335)]
    public void CompleteRootsComposeEveryFrameAcrossAllActorsAndSelectedAllies(int move)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var b=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,new ushort[240*112]);foreach(var mon in b)assets[mon.Asset]=new(64,64,Enumerable.Repeat((ushort)0xffff,4096).ToArray());var audio=new Audio();
            var o=new SourceBattleAnimation(resolved.Script,new("trapping",1,move,new(actor),new(target)),b,doubles,audio,spriteMaterials:resolved.Assets);var composer=AnimationCompositionFixture.Create(resources,resolved,assets,b);int ticks=0,peak=0;
            while(o.Active&&ticks++<1024){var f=o.Tick();peak=Math.Max(peak,f.Sprites.Length);Assert.AreEqual(240*112*4,composer.ComposeField(f).Length);}
            Assert.IsFalse(o.Active);Assert.IsTrue(peak>0);Assert.AreEqual(0,o.ActiveVisualCount);Assert.IsFalse(o.Snapshot().BlendEnabled);Assert.AreEqual(0,o.Snapshot().BackgroundSlots.Length);
            if(move==335){Assert.AreEqual(2,audio.Calls.Count(c=>c.Id=="SE_M_SKETCH"));Assert.AreEqual(1,audio.Calls.Count(c=>c.Id=="SE_M_LEER"));}
        }
    }
    [TestMethod]
    public void MissingCallbackSoundAndMalformedRecipeFailClosed()
    {
        var script=AnimationCompositionFixture.Script(335);var bad=script with{SoundEffects=script.SoundEffects.Where(p=>p.Key!="SE_M_SKETCH").ToDictionary(p=>p.Key,p=>p.Value)};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(bad,new("trapping-bad",1,335,new(0),new(1)),Battlers(true),true,new Audio()));
        var recipes=script.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes["gBlockXSpriteTemplate"]=recipes["gBlockXSpriteTemplate"] with{Width=32};bad=script with{SpriteRecipes=recipes};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(bad,new("trapping-bad",1,335,new(0),new(1)),Battlers(true),true,new Audio()));
    }
}
