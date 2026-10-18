using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class HealingItemAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    [TestMethod]
    public void OriginalCHealingItemsPreserveAffineLoopsRestartsAlphaAndSharedArgumentTermination()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        using var document=SourceVectorFixture.Read("healing-item-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_healing_item_vectors.py --reference content/raw/pokefirered/037335f4 --helper tests/Reference/reference_c.py --output tests/Game.Presentation.Tests/Fixtures/healing-item-source-vectors.json");int cases=0;
        foreach(var item in document.RootElement.GetProperty("cases").EnumerateObject())
        {
            var key=item.Name.Split(':').Select(int.Parse).ToArray();int kind=key[0],actor=key[1],target=key[2];bool egg=kind<2;var source=resources.Resolve(egg?135:208).Script;
            string template=egg?"gSoftBoiledEggSpriteTemplate":"gMilkBottleSpriteTemplate",tag=egg?"ANIM_TAG_BREAKING_EGG":"ANIM_TAG_MILK_BOTTLE";
            string[] args=egg?[template,"ANIM_ATTACKER","4","3","16",kind.ToString()]:[template,"ANIM_ATTACKER","2"];
            var script=source with{Commands=[C("loadspritegfx",tag),C("createsprite",args),C("delay","198"),C("setarg","7","0xFFFF"),C("waitforvisualfinish"),C("end")],Labels=new Dictionary<string,int>()};
            var runtime=new SourceBattleAnimation(script,new("healing-vectors",1,script.MoveId,new(actor),new(target)),Battlers(true),true,new Audio());
            var rows=item.Value.EnumerateArray().Skip(1).Select(r=>r.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).Where(r=>r[7]==0).ToArray();int seen=0,ticks=0;
            while(runtime.Active&&ticks++<512)
            {
                var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;Assert.IsTrue(seen<rows.Length,item.Name);var row=rows[seen++];var sprite=frame.Sprites.Single();
                Assert.AreEqual(new PixelPoint(row[1],row[2]),sprite.Position,item.Name);Assert.AreEqual(new PixelPoint(row[3],row[4]),sprite.Offset,item.Name);
                Assert.AreEqual(row[5],sprite.Tile,item.Name);Assert.AreEqual(row[6]==0,sprite.Visible,item.Name);Assert.AreEqual(row[18],frame.AnimationArguments[7],item.Name);
                Assert.AreEqual(row[8]!=0,frame.BlendEnabled,item.Name);Assert.AreEqual(row[9]&31,frame.AlphaA,item.Name);Assert.AreEqual((row[9]>>8)&31,frame.AlphaB,item.Name);
                var matrix=SourceAffineMatrix.FromScaleRotation(65536/row[14],65536/row[15],row[16]);Assert.AreEqual(matrix.A,sprite.MatrixA,item.Name);Assert.AreEqual(matrix.B,sprite.MatrixB,item.Name);Assert.AreEqual(matrix.C,sprite.MatrixC,item.Name);Assert.AreEqual(matrix.D,sprite.MatrixD,item.Name);
            }
            Assert.IsFalse(runtime.Active,item.Name);Assert.AreEqual(rows.Length,seen,item.Name);Assert.IsFalse(runtime.Snapshot().BlendEnabled);cases++;
        }
        Assert.AreEqual(48,cases);
    }
    [TestMethod][DataRow(135)][DataRow(208)]
    public void CompleteHealingRootsComposeSixSelfTargetLayoutsAndRestoreAlpha(int move)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(move);
        Assert.IsTrue(resolved.Assets.ContainsKey("thin-ring"));
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Battlers(doubles);var surface=resources.CreateBackgroundSurface(0);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));foreach(var battler in b)assets.Add(battler.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var runtime=new SourceBattleAnimation(resolved.Script,new("healing-root",1,move,new(actor),new(actor)),b,doubles,new Audio());var composer=AnimationCompositionFixture.Create(resources,resolved,assets,b,surface);int ticks=0;bool item=false,ring=false;
            while(runtime.Active&&ticks++<1024){var frame=runtime.Tick();item|=frame.Sprites.Any(s=>s.Template is "gSoftBoiledEggSpriteTemplate" or "gMilkBottleSpriteTemplate");ring|=frame.Sprites.Any(s=>s.Template=="gThinRingExpandingSpriteTemplate");Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length);}
            Assert.IsFalse(runtime.Active);Assert.IsTrue(item&&ring);Assert.IsFalse(runtime.Snapshot().BlendEnabled);Assert.AreEqual(0,runtime.Snapshot().Sprites.Length);
        }
    }
    [TestMethod][DataRow(-1)][DataRow(2)]
    public void UnreviewedEggHalfModesAreRejected(int mode)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var source=resources.Resolve(135).Script;
        var script=source with{Commands=[C("loadspritegfx","ANIM_TAG_BREAKING_EGG"),C("createsprite","gSoftBoiledEggSpriteTemplate","ANIM_ATTACKER","4","0","16",mode.ToString()),C("end")]};
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script,new("bad-egg",1,135,new(0),new(0)),Battlers(false),false,new Audio()));
    }
    [TestMethod]
    public void MalformedAffineLoopProgressAndJumpTargetsFailClosed()
    {
        static SourceSpriteAffineCommand Frame()=>new("frame",1,0,0,1,0);
        static SourceSpriteAffineCommand Loop(int n)=>new("loop",0,0,0,0,n);
        Assert.ThrowsExactly<InvalidDataException>(()=>new SourceSpriteAffineProgram([Loop(0),Loop(1)]));
        var falling=new SourceSpriteAffineProgram([Frame(),Loop(0)]);
        Assert.ThrowsExactly<InvalidDataException>(()=>falling.Step());
        var jumpMarker=new SourceSpriteAffineProgram([Frame(),new("jump",0,0,0,0,2),Loop(0),new("end",0,0,0,0,0)]);
        Assert.ThrowsExactly<InvalidDataException>(()=>jumpMarker.Step());
    }
    [TestMethod][DataRow(135,"ANIM_TAG_BREAKING_EGG")][DataRow(208,"ANIM_TAG_MILK_BOTTLE")]
    public void HealingGraphicsLoadsRequireResolvedSourceTagMetadata(int move,string tag)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var source=resources.Resolve(move).Script;
        Assert.IsTrue(source.SpriteTags.ContainsKey(tag));
        var tags=source.SpriteTags.Where(p=>p.Key!=tag).ToDictionary(p=>p.Key,p=>p.Value);
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpriteTags=tags},new("missing-tag",1,move,new(0),new(0)),Battlers(false),false,new Audio()));
    }
}
