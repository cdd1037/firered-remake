using System.Text.Json;
using System.Reflection;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class HailAnimationTests
{
    private const string Particle="sHailParticleSpriteTemplate",Impact="gIceCrystalHitLargeSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static SourceBattleFormCatalog Forms()=>new(Path.Combine(TestAssets.Root,"content/generated/battle/art/campaign-hit-art"));
    private static AnimationBattler[] Battlers(bool doubles,int form,int visible)
    {
        var forms=Forms();return Enumerable.Range(0,doubles?4:2).Select(i=>(form<4?forms.Resolve(385,new(i),form,false).Geometry:new AnimationBattler(new(i),"",0,0,(i&1)==0?32:64,(i&1)==0?40:64)) with{Asset="b"+i,Visible=(visible&(1<<i))!=0}).ToArray();
    }
    private static CommittedMoveAnimation Cue(int actor=0)=>new("hail",1,258,new(actor),new(actor));
    private static SourceAnimationScript Isolated(SourceAnimationScript script)=>script with{Commands=[new("loadspritegfx",["ANIM_TAG_HAIL"],1),new("loadspritegfx",["ANIM_TAG_ICE_CRYSTALS"],2),new("createvisualtask",["AnimTask_Hail","5"],3),new("waitforvisualfinish",[],4),new("end",[],5)]};
    [TestMethod]
    public void ExtractedTaskParticlesAndCounterTransfersMatchAllVisibilityAndFormVectors()
    {
        using var resources=new SourceAnimationResources(Folder);var script=Isolated(resources.Resolve(258).Script);
        using var doc=SourceVectorFixture.Read("hail-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_hail_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/hail-source-vectors.json");
        bool lowerSlot=false,samePass=false;
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateObject())
        {
            var value=entry.Value;var config=value.GetProperty("config");bool doubles=config[0].GetInt32()!=0;int form=config[1].GetInt32(),visible=config[2].GetInt32();
            // The extracted source reserves every battler sprite, including hidden ones.
            // The renderer reserves visible battlers. Hail never consumes numeric IDs:
            // map ascending free-cell ranks, preserving allocation and callback order.
            int reserved=doubles?4:2;
            var freeCells=Enumerable.Range(0,64).Where(i=>i>=reserved||(visible&(1<<i))==0).ToArray();
            int RuntimeSlot(int sourceSlot)=>freeCells[sourceSlot-reserved];
            var runtime=new SourceBattleAnimation(script,Cue(),Battlers(doubles,form,visible),doubles,new Audio());SourceAnimationFrame frame=runtime.Snapshot();
            for(int i=0;i<12;i++){frame=runtime.Tick();if(frame.Trace.Contains("3:createvisualtask"))break;}
            Assert.IsTrue(frame.Trace.Contains("3:createvisualtask"));
            var rendered=value.GetProperty("rendered").EnumerateArray().Select(r=>r.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).GroupBy(r=>r[0]).ToDictionary(g=>g.Key,g=>g.OrderBy(r=>r[1]).ToArray());
            var tasks=value.GetProperty("tasks").EnumerateArray().ToArray();
            var allocations=value.GetProperty("allocations").EnumerateArray().Select(r=>r.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).ToArray();
            for(int tick=0;tick<tasks.Length;tick++)
            {
                if(tick>0)frame=runtime.Tick();var expected=rendered.GetValueOrDefault(tick,[]);var actual=frame.Sprites.OrderBy(s=>s.Slot).ToArray();
                Assert.AreEqual(expected.Length,actual.Length,$"{entry.Name} frame{tick}");
                for(int i=0;i<expected.Length;i++)
                {
                    var row=expected[i];var sprite=actual[i];bool hit=row[2]==1;Assert.AreEqual(RuntimeSlot(row[1]),sprite.Slot);Assert.AreEqual(hit?Impact:Particle,sprite.Template);Assert.AreEqual(new PixelPoint(row[3],row[4]),sprite.Position);
                    Assert.AreEqual(hit?4:0,sprite.Tile);Assert.AreEqual(hit?8:16,sprite.Width);Assert.AreEqual(16,sprite.Height);Assert.IsTrue(sprite.Affine);Assert.IsFalse(sprite.DoubleSize);Assert.AreEqual(hit,sprite.Blend);Assert.AreEqual(18,sprite.Subpriority);
                    int scale=row[7]==0?256:hit?206+5*Math.Min(Math.Max(0,row[6]-1),10):256-row[5]*16;
                    Assert.AreEqual(65536/scale,sprite.MatrixA,$"{entry.Name} frame{tick} slot{sprite.Slot}");Assert.AreEqual(sprite.MatrixA,sprite.MatrixD);Assert.AreEqual(0,sprite.MatrixB);Assert.AreEqual(0,sprite.MatrixC);
                    if(hit){lowerSlot|=row[7]==0;samePass|=row[6]==1&&allocations.Any(a=>a[0]==tick&&a[1]==row[1]&&a[2]==1);}
                }
                Assert.AreEqual(tasks[tick][4].GetInt32(),runtime.ActiveVisualCount);
                var snapshot=runtime.Snapshot();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(snapshot));
            }
            Assert.AreEqual(0,runtime.ActiveVisualCount);while(runtime.Active)runtime.Tick();Assert.IsFalse(runtime.HasResidualSprites);Assert.IsFalse(runtime.GraphicsLoaded);
        }
        Assert.IsTrue(lowerSlot,"A lower reused impact slot renders source tile4 and identity before Begin.");Assert.IsTrue(samePass);
    }
    [TestMethod,DataRow(false),DataRow(true)]
    public void CompleteWeatherRootComposesAndRestoresPaletteWithoutResidualChildren(bool doubles)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(258);var forms=Forms();
        for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var battlers=Battlers(doubles,3,15);var assets=resolved.Assets.ToDictionary(x=>x.Key,x=>x.Value);
            assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in battlers)assets[b.Asset]=forms.Resolve(385,b.Slot,3,false).Pixels;
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(actor),battlers,doubles,audio);bool pixels=false,darkened=false;int tick=0;
            while(runtime.Active&&tick++<250)
            {
                var frame=runtime.Tick();var image=composer.ComposeField(frame);darkened|=frame.PaletteBlends.Any(p=>p.Coefficient==6);
                if(frame.Sprites.Length>0)pixels|=!image.SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));
            }
            Assert.IsFalse(runtime.Active);Assert.IsTrue(pixels);Assert.IsTrue(darkened);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.HasResidualSprites);Assert.IsFalse(runtime.GraphicsLoaded);
            Assert.AreEqual(10,audio.Cues.Count);Assert.IsTrue(runtime.Snapshot().PaletteBlends.All(p=>p.Coefficient==0));Assert.IsFalse(runtime.Snapshot().BlendEnabled);
        }
    }
    [TestMethod]
    public void AllocationFailuresPreserveSourceCounterOwnership()
    {
        using var resources=new SourceAnimationResources(Folder);var script=Isolated(resources.Resolve(258).Script);
        var runtime=new SourceBattleAnimation(script,Cue(),Battlers(true,0,15),true,new Audio());
        var type=typeof(SourceBattleAnimation);var taskType=type.GetNestedType("HailTask",BindingFlags.NonPublic)!;var particleType=type.GetNestedType("HailParticle",BindingFlags.NonPublic)!;
        var task=Activator.CreateInstance(taskType,true)!;var registry=type.GetField("_sprites",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(runtime)!;
        var add=registry.GetType().GetMethod("Add")!;var count=taskType.GetField("_active",BindingFlags.Instance|BindingFlags.NonPublic)!;
        object? parent=null;
        for(int slot=4;slot<64;slot++)
        {
            var particle=Activator.CreateInstance(particleType,slot,script.SpriteRecipes[Particle],task,new PixelPoint(80,80),0,true,false)!;
            add.Invoke(registry,[particle]);if(slot==4)parent=particle;
        }
        var generate=taskType.GetMethod("Generate",BindingFlags.Instance|BindingFlags.NonPublic)!;
        Assert.AreEqual(false,generate.Invoke(task,[runtime]));Assert.AreEqual(0,count.GetValue(task));
        count.SetValue(task,1);var step=particleType.GetMethod("Step")!;
        for(int i=0;i<11;i++)step.Invoke(parent,[runtime]);
        Assert.AreEqual(1,count.GetValue(task),"Failed impact allocation does not invent a decrement or child.");
        Assert.AreEqual(true,particleType.GetProperty("Finished")!.GetValue(parent));
        runtime.Cancel();Assert.IsFalse(runtime.HasResidualSprites);
    }
    [TestMethod]
    public void RawDependenciesResolveBothMaterialsAndRejectMissingOrUnknownRecipes()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(258);CollectionAssert.AreEquivalent(new[]{Particle,Impact},resolved.Script.SpriteDependencies);
        Assert.IsTrue(resolved.Assets.ContainsKey("hail"));Assert.IsTrue(resolved.Assets.ContainsKey("ice-crystals"));
        Assert.ThrowsExactly<InvalidDataException>(()=>resources.Resolve(resolved.Script with{SpriteDependencies=["unknown-raw-child"]}));
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(Isolated(resolved.Script with{SpriteDependencies=[]}),Cue(),Battlers(true,0,15),true,new Audio()));
        var commands=Isolated(resolved.Script).Commands.ToList();commands.Insert(3,new("createvisualtask",["AnimTask_Hail","5"],7));
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(resolved.Script with{Commands=commands.ToArray()},Cue(),Battlers(true,0,15),true,new Audio()));
    }
}
