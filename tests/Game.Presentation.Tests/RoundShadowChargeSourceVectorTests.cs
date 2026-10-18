using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class RoundShadowChargeSourceVectorTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private static readonly string[] Templates=["gFlyBallUpSpriteTemplate","gFlyBallAttackSpriteTemplate","gBounceBallShrinkSpriteTemplate","gBounceBallLandSpriteTemplate","gDiveBallSpriteTemplate","gDiveWaterSplashSpriteTemplate"];
    private sealed class Audio:ISourceAnimationAudio{public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static T Field<T>(object o,string name)=>(T)(o.GetType().GetField(name,Flags)?.GetValue(o)??throw new InvalidOperationException(name));
    private static object? Invoke(object o,string name,params object[] args)=>(o.GetType().GetMethod(name,Flags)??throw new InvalidOperationException(name)).Invoke(o,args);
    private static int[] Numbers(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    [TestMethod]
    public void OriginalCallbacksAndSpriteDriverMatchEveryRetainedNumericRowAtBothNativeOptimizationLevels()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        using var doc=SourceVectorFixture.Read("round-shadow-charge-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_round_shadow_charge_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");var root=doc.RootElement;
        CollectionAssert.AreEqual(new[]{"O0","O2"},root.GetProperty("optimizationParity").EnumerateArray().Select(e=>e.GetString()).ToArray());
        int cases=0,rows=0;var pool=root.GetProperty("rowPool");var paths=root.GetProperty("trajectories");
        foreach(var entry in root.GetProperty("cases").EnumerateArray())
        {
            int[] layout=Numbers(entry),c=Numbers(root.GetProperty("configs")[layout[3]]);bool doubles=layout[0]!=0;int actor=layout[1],target=layout[2],kind=c[0];bool hidden=kind is 1 or 3;
            var battlers=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],64,new[]{64,48,32,56}[i])).ToArray();battlers[actor]=battlers[actor] with{SourceVisibility=new(true,hidden,hidden)};
            var script=resources.Resolve(kind<2?19:kind<4?340:291).Script with{Commands=[new("end",[],1)],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
            var scene=new SourceBattleAnimation.SpriteScene();var owner=new SourceBattleAnimation(script,new("round-shadow-source",1,script.MoveId,new(actor),new(target)),battlers,doubles,new Audio(),spriteScene:scene);
            var args=Field<short[]>(owner,"_animationArgs");for(int i=0;i<4;i++)args[i]=(short)c[i+1];short[] expectedArgs=(short[])args.Clone();
            Invoke(owner,"LoadSpriteGraphics",kind==5?"ANIM_TAG_SPLASH":"ANIM_TAG_ROUND_SHADOW");Assert.AreEqual(true,Invoke(owner,"TryCreateRoundShadowChargeSprite",new SourceAnimationCommand("createsprite",[Templates[kind]],1),doubles?4:2,battlers[target].Subpriority-2));
            int tick=-1;
            foreach(int index in Numbers(paths[layout[4]]))
            {
                if(tick>=0)Invoke(owner,"AdvanceSourceSprites");var row=Numbers(pool[index]);string why=$"case {cases} kind {kind} frame {tick}";
                Assert.AreEqual(row[0]!=0,owner.Snapshot().BattlerVisibility.Single(v=>v.Slot==actor).SpriteInvisible,why+" actor sprite flag");
                Assert.AreEqual(row[1],owner.ActiveVisualCount,why+" counted lifetime");Assert.AreEqual(row[2],scene.Count,why+" matrix owner lifetime");CollectionAssert.AreEqual(expectedArgs,args,why+" retained argument registers");
                var sprites=Field<IEnumerable>(owner,"_sprites").Cast<object>().ToArray();int[] e=row[3..];Assert.AreEqual(e.Length==0?0:1,sprites.Length,why+" exact destroyed row");
                if(e.Length!=0)
                {
                    var sprite=sprites.Single();var f=(SourceVisualSpriteFrame)sprite.GetType().GetProperty("Frame",Flags)!.GetValue(sprite)!;
                    Assert.AreEqual(new PixelPoint(e[1],e[2]),f.Position,why+" x/y");Assert.AreEqual(new PixelPoint(e[3],e[4]),f.Offset,why+" x2/y2");Assert.AreEqual(e[5]==0,f.Visible,why+" effect invisible");Assert.AreEqual(e[6],f.Tile,why+" frame tile");
                    CollectionAssert.AreEqual(e[7..11],new[]{f.MatrixA,f.MatrixB,f.MatrixC,f.MatrixD},why+" affine matrix");Assert.IsTrue(f.Affine);Assert.AreEqual(e[11]==3,f.DoubleSize,why+" affine bounds");
                    if(kind==1)
                    {
                        var linear=Field<object>(sprite,"_linear");int[] data=[Field<int>(linear,"_remaining"),unchecked((short)Field<int>(linear,"_vx")),unchecked((short)Field<int>(linear,"_vy")),unchecked((short)Field<int>(linear,"_x")),unchecked((short)Field<int>(linear,"_y")),0,0,0];
                        CollectionAssert.AreEqual(e[18..26],data,why+" shared unsigned linear state and recycle");
                    }
                    else CollectionAssert.AreEqual(e[18..26],Field<short[]>(sprite,"_data").Select(x=>(int)x).ToArray(),why+" signed callback words");
                    if(kind!=5)
                    {
                        var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");CollectionAssert.AreEqual(e[12..15],new[]{affine.XScale,affine.YScale,affine.Rotation},why+" affine state");
                        Assert.AreEqual(e[15]!=0,affine.Ended,why+" affine END");// sprite.c AffineAnimCmd_end decrements animCmdIndex back to the
                        // preceding FRAME; the shared managed player parks on END.
                        // Compare that explicit encoding, without changing oracle rows.
                        int pc=Field<int>(affine,"_pc");if(affine.Ended)Assert.AreEqual("end",Field<SourceSpriteAffineCommand[]>(affine,"_commands")[pc].Op,why+" managed END encoding");
                        Assert.AreEqual(e[16],pc-(affine.Ended?1:0),why+" source affine pc");Assert.AreEqual(e[17],Field<int>(affine,"_delay"),why+" affine delay");
                    }
                }
                tick++;rows++;
            }
            owner.Cancel();owner.Cancel();Assert.AreEqual(0,scene.Count);Assert.AreEqual(hidden,owner.Snapshot().BattlerVisibility.Single(v=>v.Slot==actor).SpriteInvisible);cases++;
        }
        Assert.AreEqual(210,cases);Assert.AreEqual(15050,rows);Assert.AreEqual(cases,root.GetProperty("coverage").GetProperty("caseCount").GetInt32());Assert.AreEqual(rows,root.GetProperty("coverage").GetProperty("rowCount").GetInt32());
    }
}
