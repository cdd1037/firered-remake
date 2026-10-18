using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ChargePairSourceVectorTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio{public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static T Field<T>(object o,string name)=>(T)(o.GetType().GetField(name,Flags)?.GetValue(o)??throw new InvalidOperationException(name));
    private static object? Invoke(object o,string name,params object[] args)=>(o.GetType().GetMethod(name,Flags)??throw new InvalidOperationException(name)).Invoke(o,args);
    private static int[] Numbers(JsonElement e)=>e.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static SourceVisualSpriteFrame Frame(object sprite)=>(SourceVisualSpriteFrame)sprite.GetType().GetProperty("Frame",Flags)!.GetValue(sprite)!;
    private static object[] Sprites(SourceBattleAnimation owner)=>Field<IEnumerable>(owner,"_sprites").Cast<object>().ToArray();
    private static void Battler(SourceBattleAnimation owner,int slot,int[] e,string why)
    {
        Assert.AreEqual(new PixelPoint(e[0],e[1]),(PixelPoint)Invoke(owner,"BattlerPosition",slot)!,why+" raw x/y");
        Assert.AreEqual(new PixelPoint(e[2],e[3]),Field<PixelPoint[]>(owner,"_offsets")[slot],why+" pos2");
        Assert.AreEqual(e[4]!=0,owner.Snapshot().BattlerVisibility.Single(v=>v.Slot==slot).SpriteInvisible,why+" invisible");
        CollectionAssert.AreEqual(e[5..9],new[]{Field<int[]>(owner,"_xScale")[slot],Field<int[]>(owner,"_matrixB")[slot],Field<int[]>(owner,"_matrixC")[slot],Field<int[]>(owner,"_yScale")[slot]},why+" matrix");
        Assert.AreEqual(e[9]==3,Field<bool[]>(owner,"_affineMode")[slot],why+" affine mode");
    }
    [TestMethod]
    public void OriginalCCallbacksAndAffinePlayerMatchEveryPhaseWrapDirectionAndTerminalRow()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        using var doc=SourceVectorFixture.Read("charge-pair-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_charge_pair_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");var root=doc.RootElement;int cases=0,rows=0;
        foreach(var entry in root.GetProperty("cases").EnumerateArray())
        {
            bool doubles=entry.GetProperty("doubles").GetBoolean();int actor=entry.GetProperty("actor").GetInt32(),target=entry.GetProperty("target").GetInt32(),index=entry.GetProperty("config").GetInt32();var c=Numbers(root.GetProperty("configs")[index]);int kind=c[0],slot=kind==1&&c[1]!=0?target:actor;
            var battlers=Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],64,new[]{64,48,32,56}[i])).ToArray();battlers[slot]=battlers[slot] with{SourceVisibility=new(true,false,c[10]!=0)};
            var script=resources.Resolve(kind==0?13:130).Script with{Commands=[new("end",[],1)],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
            var owner=new SourceBattleAnimation(script,new("charge-source",1,script.MoveId,new(actor),new(target)),battlers,doubles,new Audio());var args=Field<short[]>(owner,"_animationArgs");for(int i=0;i<7;i++)args[i]=(short)c[i+1];var extension=args.Skip(8).ToArray();Field<PixelPoint[]>(owner,"_offsets")[slot]=new(c[8],c[9]);
            if(kind==3){Invoke(owner,"PrepareBattlerRotScale",slot,0);Invoke(owner,"SetBattlerRotScale",slot,256,256,(slot&1)!=0?0x600:-0x600);Invoke(owner,"SetSourceBattlerSpriteInvisible",slot,c[10]!=0);}
            object? task=null;var tasks=Field<IList>(owner,"_tasks");
            if(kind<2)
            {
                if(kind==0)Invoke(owner,"LoadSpriteGraphics","ANIM_TAG_GUST");
                string template=kind==0?"gRazorWindTornadoSpriteTemplate":"gSlideMonToOffsetAndBackSpriteTemplate";
                Assert.AreEqual(true,Invoke(owner,"TryCreateChargePairSprite",new SourceAnimationCommand("createsprite",[template],1),doubles?4:2,battlers[actor].Subpriority-2));
            }
            else{Assert.AreEqual(true,Invoke(owner,"TryCreateChargePairTask",new SourceAnimationCommand("createvisualtask",["AnimTask_SkullBashPosition"],1)));task=tasks[tasks.Count-1];}
            int tick=-2;
            foreach(var row in entry.GetProperty("rows").EnumerateArray())
            {
                Assert.AreEqual(++tick,row.GetProperty("tick").GetInt32());string why=$"case {cases} kind {kind} frame {tick}";
                if(tick>=0)Invoke(owner,"AdvanceSourceSprites");Battler(owner,slot,Numbers(row.GetProperty("beforeTask")),why+" before task");
                if(tick>=0)Invoke(owner,"RunSourceTasks");Battler(owner,slot,Numbers(row.GetProperty("afterTask")),why+" after task");
                CollectionAssert.AreEqual(Numbers(row.GetProperty("args")),args.Take(8).Select(x=>(int)x).ToArray(),why+" argument writes");
                CollectionAssert.AreEqual(extension,args.Skip(8).ToArray(),why+" untouched bounded extension");
                Assert.AreEqual(row.GetProperty("count").GetInt32(),owner.ActiveVisualCount,why+" counted lifetime");Assert.AreEqual(row.GetProperty("taskAlive").GetInt32(),tasks.Count,why+" task lifetime");
                if(task is not null)CollectionAssert.AreEqual(Numbers(row.GetProperty("taskData")),Field<short[]>(task,"_data").Select(x=>(int)x).ToArray(),why+" exact signed task state");
                var e=Numbers(row.GetProperty("effect"));var sprites=Sprites(owner);Assert.AreEqual(e.Length==0?0:1,sprites.Length,why+" effect lifetime");
                if(e.Length!=0)
                {
                    var sprite=sprites.Single();var f=Frame(sprite);Assert.AreEqual(new PixelPoint(e[1],e[2]),f.Position,why+" effect x/y");Assert.AreEqual(new PixelPoint(e[3],e[4]),f.Offset,why+" effect pos2");Assert.AreEqual(e[5]==0,f.Visible,why+" effect visible");Assert.AreEqual(e[6],f.Tile,why+" tile");
                    CollectionAssert.AreEqual(e[18..24],Field<short[]>(sprite,"_data").Take(6).Select(x=>(int)x).ToArray(),why+" signed effect data");
                    if(kind==0)
                    {
                        CollectionAssert.AreEqual(e[7..11],new[]{f.MatrixA,f.MatrixB,f.MatrixC,f.MatrixD},why+" effect matrix");var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");
                        CollectionAssert.AreEqual(e[12..15],new[]{affine.XScale,affine.YScale,affine.Rotation},why+" native player state");Assert.AreEqual(e[15]!=0,affine.Ended,why+" affine end");
                    }
                }
                rows++;
            }
            owner.Cancel();owner.Cancel();Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsTrue(owner.Snapshot().Offsets.All(p=>p==default));cases++;
        }
        Assert.AreEqual(104,cases);Assert.AreEqual(2432,rows);Assert.AreEqual(cases,root.GetProperty("coverage").GetProperty("caseCount").GetInt32());Assert.AreEqual(rows,root.GetProperty("coverage").GetProperty("rowCount").GetInt32());
    }
}
