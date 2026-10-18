using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class RockFragmentAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    [TestMethod][DataRow(false)][DataRow(true)]
    public void OriginalCFragmentsAndPackedTranslationPreserveSignedArithmeticAndHandoffs(bool genericMigration)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        using var document=SourceVectorFixture.Read("rock-fragment-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_rock_fragment_vectors.py --reference content/raw/pokefirered/037335f4 --helper tests/Reference/reference_c.py --output tests/Game.Presentation.Tests/Fixtures/rock-fragment-source-vectors.json");
        var root=document.RootElement;int count=0;
        foreach(var item in root.GetProperty("cases").EnumerateObject())
        {
            var key=item.Name.Split(':').Select(int.Parse).ToArray();int kind=key[0],actor=key[1],target=key[2],config=key[3];
            if(genericMigration&&(kind!=1||config>1))continue;
            var source=resources.Resolve(genericMigration?52:kind==0?249:350).Script;
            var operands=kind==0?root.GetProperty("fragmentArgs")[config].EnumerateArray().Select(v=>v.GetInt32()).ToArray():new[]{16,-3,13,7,25,root.GetProperty("modes")[config].GetInt32()};
            string template=genericMigration?"gEmberSpriteTemplate":kind==0?"gRockFragmentSpriteTemplate":"gRockBlastRockSpriteTemplate",tag=genericMigration?"ANIM_TAG_SMALL_EMBER":"ANIM_TAG_ROCKS";
            string[] args=[template,"ANIM_ATTACKER","2",..operands.Select(v=>v.ToString())];
            var script=source with{Commands=[C("loadspritegfx",tag),C("createsprite",args),C("waitforvisualfinish"),C("end")],Labels=new Dictionary<string,int>()};
            var runtime=new SourceBattleAnimation(script,new("rock-vectors",1,script.MoveId,new(actor),new(target)),Battlers(true),true,new Audio());
            var rows=item.Value.EnumerateArray().Skip(1).Select(r=>r.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).Where(r=>r[15]==0).ToArray();int seen=0,ticks=0;
            var affine=kind==1&&!genericMigration?new SourceSpriteAffineProgram(source.SpriteRecipes[template].AffineAnimations[item.Value[0][12].GetInt32()]):null;
            while(runtime.Active&&ticks++<128)
            {
                var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;Assert.IsTrue(seen<rows.Length,item.Name);var row=rows[seen++];var sprite=frame.Sprites.Single();
                Assert.AreEqual(new PixelPoint(row[1],row[2]),sprite.Position,item.Name);Assert.AreEqual(new PixelPoint(row[3],row[4]),sprite.Offset,item.Name);Assert.AreEqual(row[10],frame.AnimationArguments[2],item.Name);
                if(kind==0)Assert.AreEqual(new[]{32,48,64}[row[11]],sprite.Tile);
                if(affine is not null){affine.Step();var m=affine.Matrix;Assert.IsTrue(sprite.Affine);Assert.AreEqual(m.A,sprite.MatrixA);Assert.AreEqual(m.B,sprite.MatrixB);Assert.AreEqual(m.C,sprite.MatrixC);Assert.AreEqual(m.D,sprite.MatrixD);}
            }
            Assert.IsFalse(runtime.Active,item.Name);Assert.AreEqual(rows.Length,seen,item.Name);count++;
        }
        Assert.AreEqual(genericMigration?24:168,count);
    }
    [TestMethod][DataRow(0,2)][DataRow(-1,2)][DataRow(14,3)]
    public void UndefinedFragmentDivisionAndFrameSelectionsAreRejected(int duration,int animation)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var source=resources.Resolve(249).Script;
        var script=source with{Commands=[C("loadspritegfx","ANIM_TAG_ROCKS"),C("createsprite","gRockFragmentSpriteTemplate","ANIM_TARGET","2","0","0","20","24",duration.ToString(),animation.ToString()),C("waitforvisualfinish"),C("end")],Labels=new Dictionary<string,int>()};
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script,new("invalid-fragment",1,249,new(0),new(1)),Battlers(true),true,new Audio()));
    }
    [TestMethod][DataRow(249)][DataRow(350)]
    public void CompleteRockRootsComposeAllSixActorLayouts(int move)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Battlers(doubles);var surface=resources.CreateBackgroundSurface(0);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));foreach(var battler in b)assets.Add(battler.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var runtime=new SourceBattleAnimation(resolved.Script,new("rock-root",1,move,new(actor),new(actor^1)),b,doubles,new Audio());var composer=AnimationCompositionFixture.Create(resources,resolved,assets,b,surface);int ticks=0;bool fragment=false;
            while(runtime.Active&&ticks++<1024){var frame=runtime.Tick();fragment|=frame.Sprites.Any(s=>s.Template=="gRockFragmentSpriteTemplate");Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length);}
            Assert.IsFalse(runtime.Active);Assert.IsTrue(fragment);Assert.AreEqual(0,runtime.Snapshot().Sprites.Length);
        }
    }
}
