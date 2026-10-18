using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ReactionAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}public void SetPan(int pan,int frame){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],new[]{64,40,24,56}[i],new[]{64,48,32,56}[i])).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor=0,int target=1)=>new("reaction-root",1,move,new(actor),new(target)){VisualSeed=0x12345678};
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static void Clean(SourceBattleAnimation runtime)
    {
        Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);var f=runtime.Snapshot();Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.IsTrue(f.Offsets.All(p=>p==default));Assert.IsFalse(f.BlendEnabled);Assert.IsTrue(f.BattlerAffine.All(a=>!a.DoubleSize&&a.XScale==256&&a.YScale==256));Assert.AreEqual(0,f.PaletteBlends.Length);Assert.AreEqual(0,f.InvertedPalettes.Length);
    }
    [TestMethod][DataRow(161)][DataRow(263)][DataRow(265)][DataRow(348)]
    public void WholeSourceRootsResolveRenderReplayAcrossSidesAndDoubles(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        string[] expected=move switch{161=>["impact","tri-attack-triangle","fire","lightning","ice-crystals"],263=>["impact","sweat-drop"],265=>["impact","tag-hand","smellingsalt-effect"],_=>["impact","leaf","cross-impact"]};
        CollectionAssert.AreEquivalent(expected,resolved.Assets.Keys.ToArray());
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x801f,240*112).ToArray()));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();var cue=Cue(move,actor,target);var scene=new SourceBattleAnimation.SpriteScene();
            var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio,spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio(),spriteMaterials:assets);
            int ticks=0,peak=0;bool visible=false,pixels=false,palette=false;var templates=new HashSet<string>();
            while(runtime.Active&&ticks++<600)
            {
                var f=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));
                peak=Math.Max(peak,f.Sprites.Count(s=>s.Template==(move==348?"gLeafBladeSpriteTemplate":"gFacadeSweatDropSpriteTemplate")));
                foreach(var sprite in f.Sprites){templates.Add(sprite.Template);if(sprite.Asset is not null)Assert.IsNotNull(sprite.Material);}
                visible|=f.Sprites.Any(s=>s.Visible);palette|=f.PaletteBlends.Any(p=>p.Palette==actor+16&&p.Coefficient==8);
                if((ticks&7)==0&&f.Sprites.Any(s=>s.Visible))
                {var before=JsonSerializer.Serialize(runtime.Snapshot());var rendered=composer.ComposeField(f);pixels|=!rendered.SequenceEqual(composer.ComposeField(f with{Sprites=[]}));Assert.AreEqual(before,JsonSerializer.Serialize(runtime.Snapshot()));}
            }
            Clean(runtime);Clean(replay);Assert.AreEqual(0,scene.Count);Assert.IsTrue(visible);Assert.IsTrue(pixels);Assert.IsTrue(audio.Cues.Count>0);
            string[] required=move switch{161=>["gTriAttackTriangleSpriteTemplate","gLargeFlameScatterSpriteTemplate","gLightningSpriteTemplate","gIceCrystalHitLargeSpriteTemplate"],263=>["gFacadeSweatDropSpriteTemplate"],265=>["gSmellingSaltsHandSpriteTemplate","gSmellingSaltExclamationSpriteTemplate"],_=>["gLeafBladeSpriteTemplate","gCrossImpactSpriteTemplate"]};
            foreach(string template in required)Assert.IsTrue(templates.Contains(template),template);
            if(move==263){Assert.IsTrue(palette);Assert.AreEqual(4,peak);}if(move==348)Assert.AreEqual(12,peak);
        }
    }
    [TestMethod][DataRow(161)][DataRow(263)][DataRow(265)][DataRow(348)]
    public void CancellationAtEveryTickReleasesOwnedChildrenAffineAndPalette(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);var battlers=Battlers(true);var length=new SourceBattleAnimation(resolved.Script,Cue(move),battlers,true,new Audio(),spriteMaterials:resolved.Assets);int ticks=0;while(length.Active&&ticks++<600)length.Tick();Assert.IsFalse(length.Active);
        for(int stop=0;stop<=ticks;stop++)
        {
            var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(resolved.Script,Cue(move),battlers,true,new Audio(),spriteScene:scene,spriteMaterials:resolved.Assets);
            for(int t=0;t<stop&&runtime.Active;t++)runtime.Tick();runtime.Cancel();runtime.Cancel();Clean(runtime);Assert.AreEqual(0,scene.Count);Assert.IsTrue(runtime.Snapshot().SpritePaletteSlots.All(p=>p.All(c=>c==0)));
        }
    }
    [TestMethod]
    public void RawChildDependenciesAndSourceRecipesFailClosed()
    {
        foreach(int move in new[]{263,348})
        {
            var source=AnimationCompositionFixture.Script(move);Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpriteDependencies=[]},Cue(move),Battlers(false),false,new Audio()));
            string name=move==263?"gFacadeSweatDropSpriteTemplate":"gLeafBladeSpriteTemplate";var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[name]=recipes[name] with{Width=32};
            Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpriteRecipes=recipes},Cue(move),Battlers(false),false,new Audio()));
        }
        var triangle=AnimationCompositionFixture.Script(161);var changed=triangle.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);changed["gTriAttackTriangleSpriteTemplate"]=changed["gTriAttackTriangleSpriteTemplate"] with{AffineDoubleSize=false};Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(triangle with{SpriteRecipes=changed},Cue(161),Battlers(false),false,new Audio()));
        var facade=AnimationCompositionFixture.Script(263);int tag=facade.SpriteTags["ANIM_TAG_SWEAT_DROP"];var palettes=facade.SpritePalettes.Where(p=>p.Key!=tag).ToDictionary(p=>p.Key,p=>p.Value);Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(facade with{SpritePalettes=palettes},Cue(263),Battlers(false),false,new Audio()));
    }
    [TestMethod]
    public void GeometryRepeatAndTaskAffineOperandBoundariesAreExplicit()
    {
        foreach(int move in new[]{263,265})
        {
            var source=AnimationCompositionFixture.Script(move);string task=move==263?"AnimTask_SquishAndSweatDroplets":"AnimTask_SmellingSaltsSquish";
            foreach(string count in new[]{"0","-1","32768"})
            {
                var commands=source.Commands.Select(c=>c.Op=="createvisualtask"&&c.Args[0]==task?c with{Args=[c.Args[0],c.Args[1],c.Args[2],count]}:c).ToArray();Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{Commands=commands},Cue(move),Battlers(false),false,new Audio()));
            }
            var programs=source.TaskAffinePrograms.ToDictionary(p=>p.Key,p=>p.Value);programs[move==263?"sFacadeSquishAffineAnimCmds":"sSmellingSaltsSquishAffineAnimCmds"]=[new("frame",0,0,0,1),new("end")];Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{TaskAffinePrograms=programs},Cue(move),Battlers(false),false,new Audio()));
        }
        foreach(int move in new[]{265,348}){var battlers=Battlers(false);battlers[1]=battlers[1] with{SourceWidth=0};Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(AnimationCompositionFixture.Script(move),Cue(move),battlers,false,new Audio()));}
    }
    [TestMethod][DataRow(263,"AnimTask_SquishAndSweatDroplets")][DataRow(348,"AnimTask_LeafBlade")]
    public void EmitterClosureIsInvocationScopedAndRejectsRepeatedOrCalledEmitters(int move,string task)
    {
        var source=AnimationCompositionFixture.Script(move);var emitter=source.Commands.Single(c=>c.Op=="createvisualtask"&&c.Args[0]==task);var helpers=new Dictionary<string,SourceAnimationCommand[]>{{"UnusedEmitter",[emitter,C("return")]}};
        var unused=source with{Commands=[C("end")],Subroutines=helpers};var empty=new SourceBattleAnimation(unused,Cue(move),Battlers(false),false,new Audio());empty.Tick();Assert.IsFalse(empty.Active);
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(unused with{Commands=[C("call","UnusedEmitter"),C("end")]},Cue(move),Battlers(false),false,new Audio()));
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{Commands=[emitter,..source.Commands]},Cue(move),Battlers(false),false,new Audio()));
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{Commands=[emitter,C("goto","Loop"),C("end")],Labels=new Dictionary<string,int>{{"Loop",0}}},Cue(move),Battlers(false),false,new Audio()));
    }
    [TestMethod][DataRow(263)][DataRow(348)]
    public void RetainedNonAffineOwnerSurvivesEmitterCancellation(int move)
    {
        var bolt=AnimationCompositionFixture.Script(84);var battlers=Battlers(true);var scene=new SourceBattleAnimation.SpriteScene();var script=bolt with{Commands=[C("loadspritegfx","ANIM_TAG_SPARK"),C("createvisualtask","AnimTask_ElectricBolt","5","0","-44","0"),C("waitforvisualfinish"),C("end")],Subroutines=new Dictionary<string,SourceAnimationCommand[]>()};
        var old=new SourceBattleAnimation(script,Cue(84),battlers,true,new Audio(),spriteScene:scene);int ticks=0;while(old.Active&&ticks++<40)old.Tick();Assert.IsTrue(old.HasResidualSprites);
        var current=new SourceBattleAnimation(AnimationCompositionFixture.Script(move),Cue(move),battlers,true,new Audio(),spriteScene:scene);current.Tick();current.Cancel();Assert.IsTrue(old.HasResidualSprites);Assert.AreEqual(0,current.ActiveVisualCount);
        ticks=0;while(old.HasResidualSprites&&ticks++<40)scene.TickIdle(battlers,true);Assert.AreEqual(0,scene.Count);
    }
}
