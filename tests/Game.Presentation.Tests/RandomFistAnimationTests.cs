using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class RandomFistAnimationTests
{
    private const string Parent="gFistFootRandomPosSpriteTemplate",Impact="gBasicHitSplatSpriteTemplate";
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound>Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static SourceAnimationScript Script()=>AnimationCompositionFixture.Script(24);
    private static CommittedMoveAnimation Cue(int actor=0,int target=1,uint? seed=0,long sequence=1)
        =>new("random-fist",sequence,24,new(actor),new(target)){VisualSeed=seed,MoveTurn=0};
    private static SourceAnimationCommand Create(int selector=1,int duration=20,int frame=1)=>new("createsprite",[Parent,"ANIM_ATTACKER","3",selector.ToString(),duration.ToString(),frame.ToString()],3);
    private static SourceAnimationScript Primitive(int selector=1,int duration=20,int frame=1)=>Script() with{Commands=[
        new("loadspritegfx",["ANIM_TAG_HANDS_AND_FEET"],1),new("loadspritegfx",["ANIM_TAG_IMPACT"],2),Create(selector,duration,frame),
        new("waitforvisualfinish",[],4),new("end",[],5)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),Labels=new Dictionary<string,int>()};
    private static T Field<T>(object instance,string name)=>(T)(instance.GetType().GetField(name,Private)?.GetValue(instance)??throw new InvalidOperationException(name));
    private static uint? RandomState(SourceBattleAnimation runtime)=>typeof(SourceBattleAnimation).GetField("_visualRandom",Private)!.GetValue(runtime) is SourceVisualRandom random?Field<uint>(random,"_state"):null;
    private static SourceAnimationFrame FirstParent(SourceBattleAnimation runtime)
    {
        for(int i=0;i<12&&runtime.Active;i++){var frame=runtime.Tick();if(frame.Sprites.Any(s=>s.Template==Parent))return frame;}
        throw new InvalidOperationException("Source random parent never appeared.");
    }
    private static void Finish(SourceBattleAnimation runtime)
    {for(int i=0;i<128&&runtime.Active;i++)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.HasResidualSprites);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.Snapshot().Sprites.Length);}

    [TestMethod]
    public void FourDrawRootAndOptionalFifthDrawRetainGlobalArgumentAndIdentitySeedSemantics()
    {
        for(int actor=0;actor<4;actor++)foreach(int target in new[]{actor^1,actor^3,actor^2})foreach(int selected in new[]{0,1})foreach(int animation in new[]{-32768,-1,0,1,4})
        {
            var cue=Cue(actor,target);var random=new SourceVisualRandom(cue.VisualSeed!.Value);int expectedAnimation=animation<0?random.Next()%5:animation;
            var battlers=Battlers(true);var b=battlers[selected==0?actor:target];var center=b.Center(true,true);
            int x=random.Next()%(b.SourceWidth/2),y=random.Next()%(b.SourceHeight/4);if((random.Next()&1)!=0)x=-x;if((random.Next()&1)!=0)y=-y;if(b.Slot.IsPlayer)y-=16;
            var runtime=new SourceBattleAnimation(Primitive(selected,20,animation),cue,battlers,true,new Audio());var frame=FirstParent(runtime);
            var parent=frame.Sprites.Single(s=>s.Template==Parent);var impact=frame.Sprites.Single(s=>s.Template==Impact);
            Assert.AreEqual(new PixelPoint(center.X+x,center.Y+y),parent.Position);Assert.AreEqual(parent.Position,impact.Position);
            Assert.AreEqual(expectedAnimation,(int)frame.AnimationArguments[2]);Assert.AreEqual(expectedAnimation<4?expectedAnimation*16:48,parent.Tile);Assert.AreEqual(expectedAnimation==4,parent.HFlip);
            Assert.AreEqual(Field<uint>(random,"_state"),RandomState(runtime));Assert.AreEqual(parent.Subpriority+1,impact.Subpriority);
            Assert.AreEqual(1,runtime.ActiveVisualCount);Assert.IsTrue(impact.Affine);Assert.IsTrue(impact.Blend);Assert.IsFalse(impact.DoubleSize);
            var saved=RandomState(runtime);runtime.Snapshot();runtime.Snapshot();Assert.AreEqual(saved,RandomState(runtime));Finish(runtime);Assert.AreEqual(saved,RandomState(runtime));
        }
        var original=Cue();original=original with{VisualSeed=SourceAnimationVisualSeed.For(original)};
        var changed=original with{Sequence=original.Sequence+1,VisualSeed=null};changed=changed with{VisualSeed=SourceAnimationVisualSeed.For(changed)};
        var a=new SourceBattleAnimation(Primitive(),original,Battlers(false),false,new Audio());var bRuntime=new SourceBattleAnimation(Primitive(),changed,Battlers(false),false,new Audio());
        Assert.AreNotEqual(FirstParent(a).Sprites.Single(s=>s.Template==Parent).Position,FirstParent(bRuntime).Sprites.Single(s=>s.Template==Parent).Position);
        a.Cancel();bRuntime.Cancel();
    }

    [TestMethod]
    public void FormAndTransformedMaterialDimensionsChooseTheExactSourceRandomBounds()
    {
        var forms=new SourceBattleFormCatalog(Path.Combine(TestAssets.Root,"content/generated/battle/art/campaign-hit-art"));
        for(int form=0;form<4;form++)foreach(bool transformed in new[]{false,true})for(int actor=0;actor<4;actor++)foreach(int target in new[]{actor^1,actor^3,actor^2})
        {
            var battlers=Enumerable.Range(0,4).Select(i=>forms.Resolve(385,new(i),form,false,transformed).Geometry with{Asset="b"+i,DisplayCenter=new PixelPoint(120,60)}).ToArray();
            var targetGeometry=battlers[target];var center=targetGeometry.Center(true,true);var rng=new SourceVisualRandom(0x12345678);
            int x=rng.Next()%(targetGeometry.SourceWidth/2),y=rng.Next()%(targetGeometry.SourceHeight/4);if((rng.Next()&1)!=0)x=-x;if((rng.Next()&1)!=0)y=-y;if(targetGeometry.Slot.IsPlayer)y-=16;
            var runtime=new SourceBattleAnimation(Primitive(),Cue(actor,target,0x12345678),battlers,true,new Audio());
            Assert.AreEqual(new PixelPoint(center.X+x,center.Y+y),FirstParent(runtime).Sprites.Single(s=>s.Template==Parent).Position);
            Finish(runtime);
        }
    }

    [TestMethod][DataRow(false)][DataRow(true)]
    public void CompleteCommittedHitRootComposesExactlyOnePairAndCleansUp(bool doubles)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(24);
        CollectionAssert.AreEquivalent(new[]{"impact","hands-and-feet"},resolved.Assets.Keys.ToArray());
        for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^3,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,new ushort[240*112]);
            foreach(var b in battlers)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());
            var scene=new SourceBattleAnimation.SpriteScene();var cue=Cue(actor,target);cue=cue with{VisualSeed=SourceAnimationVisualSeed.For(cue)};
            var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio,spriteScene:scene,spriteMaterials:assets);
            var repeat=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio(),spriteMaterials:assets);var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);
            int count=0,visible=0;bool pixels=false;uint? state=null;
            while(runtime.Active&&count++<128)
            {
                var frame=runtime.Tick();var other=repeat.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(other));
                var sprites=frame.Sprites.Where(s=>s.Template is Parent or Impact).ToArray();
                if(sprites.Length!=0){Assert.AreEqual(2,sprites.Length);visible++;state??=RandomState(runtime);Assert.AreEqual(state,RandomState(runtime));}
                string before=JsonSerializer.Serialize(runtime.Snapshot());var rgba=composer.ComposeField(frame);composer.ComposeField(frame);Assert.AreEqual(before,JsonSerializer.Serialize(runtime.Snapshot()));
                if(sprites.Length!=0)pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(repeat.Active);Assert.AreEqual(20,visible);Assert.IsTrue(pixels);Assert.AreEqual(0,scene.Count);
            Assert.AreEqual(1,audio.Cues.Count);Assert.AreEqual("SE_M_VITAL_THROW2",audio.Cues[0].Id);
            var after=runtime.Snapshot();Assert.AreEqual(0,after.Sprites.Length);Assert.AreEqual(0,after.BackgroundSlots.Length);Assert.IsFalse(after.BlendEnabled);Assert.IsTrue(after.Offsets.All(p=>p==default));Assert.IsFalse(runtime.GraphicsLoaded);
        }
    }

    [TestMethod]
    public void ZeroDurationCreatesThenDestroysBothObjectsBeforeTheFirstPublicOamPass()
    {
        var runtime=new SourceBattleAnimation(Primitive(duration:0),Cue(),Battlers(false),false,new Audio());
        for(int i=0;runtime.Active&&i<32;i++){var frame=runtime.Tick();Assert.AreEqual(0,frame.Sprites.Length);}
        Assert.IsFalse(runtime.Active);var random=new SourceVisualRandom(0);for(int i=0;i<4;i++)random.Next();
        Assert.AreEqual(Field<uint>(random,"_state"),RandomState(runtime));Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.HasResidualSprites);
    }

    private static object AddInert(SourceBattleAnimation owner,int slot)
    {
        var type=typeof(SourceBattleAnimation).GetNestedType("BubbleHelper",BindingFlags.NonPublic)!;
        var sprite=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,new object[]{slot},null)!;
        var owned=Field<object>(owner,"_sprites");owned.GetType().GetMethod("Add")!.Invoke(owned,[sprite]);return sprite;
    }
    [TestMethod]
    public void RawChildAllocationFailureKeepsTheParentTimerAndAnotherOwnersReservations()
    {
        var scene=new SourceBattleAnimation.SpriteScene();var baseScript=Primitive() with{Commands=[new("end",[],1)]};
        var old=new SourceBattleAnimation(baseScript,Cue(sequence:1),Battlers(true),true,new Audio(),spriteScene:scene);old.Tick();
        for(int slot=4;slot<63;slot++)AddInert(old,slot);Assert.AreEqual(59,scene.Count);
        foreach(bool cancel in new[]{false,true})
        {
            var next=new SourceBattleAnimation(Primitive(duration:2),Cue(sequence:cancel?3:2),Battlers(true),true,new Audio(),spriteScene:scene);
            var frame=FirstParent(next);Assert.AreEqual(60,scene.Count);Assert.AreEqual(1,next.ActiveVisualCount);
            Assert.AreEqual(63,frame.Sprites.Single(s=>s.Template==Parent).Slot);Assert.IsFalse(frame.Sprites.Any(s=>s.Template==Impact));
            if(cancel)next.Cancel();else{next.Tick();var gone=next.Tick();Assert.IsFalse(gone.Sprites.Any(s=>s.Template==Parent));for(int i=0;next.Active&&i<12;i++)next.Tick();Assert.IsFalse(next.Active);}
            Assert.AreEqual(59,scene.Count);Assert.AreEqual(0,next.ActiveVisualCount);Assert.IsTrue(old.HasResidualSprites);
        }
        scene.Cancel();Assert.AreEqual(0,scene.Count);
    }

    [TestMethod]
    public void CancellationRemovesTheRawChildButPreservesAnotherSceneOwnersLiveSprite()
    {
        var scene=new SourceBattleAnimation.SpriteScene();var old=new SourceBattleAnimation(Primitive() with{Commands=[new("end",[],1)]},Cue(sequence:1),Battlers(true),true,new Audio(),spriteScene:scene);old.Tick();
        AddInert(old,5);
        var next=new SourceBattleAnimation(Primitive(),Cue(sequence:2),Battlers(true),true,new Audio(),spriteScene:scene);
        var frame=FirstParent(next);Assert.AreEqual(3,scene.Count);Assert.AreEqual(1,next.ActiveVisualCount);
        Assert.AreEqual(4,frame.Sprites.Single(s=>s.Template==Parent).Slot);Assert.AreEqual(6,frame.Sprites.Single(s=>s.Template==Impact).Slot);
        next.Cancel();Assert.AreEqual(1,scene.Count);Assert.IsTrue(old.HasResidualSprites);Assert.IsFalse(next.HasResidualSprites);Assert.AreEqual(0,next.ActiveVisualCount);
        var retained=scene.TickIdle(Battlers(true),true).Sprites.Single();Assert.AreEqual(5,retained.Slot);Assert.IsNull(retained.Asset);scene.Cancel();Assert.AreEqual(0,scene.Count);
    }

    [TestMethod]
    public void CancellationAtEveryLifetimePhaseReleasesOnlyOwnedStateAndNeverDrawsAgain()
    {
        foreach(int stop in new[]{0,1,4,6,7,9,25,60})
        {
            var gate=new SourceBattleAnimationGate();var cue=Cue();var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();
            Assert.IsTrue(gate.TryStart(cue,()=>new(Primitive(),cue,Battlers(false),false,audio,spriteScene:scene)));int tick=0;
            while(gate.IsBlocking&&tick<stop){gate.Current!.Tick();tick++;}Assert.IsTrue(tick==stop||!gate.IsBlocking);
            int sounds=audio.Cues.Count;gate.Current!.Cancel();gate.Current.Cancel();Assert.IsFalse(gate.IsBlocking);Assert.AreEqual(0,scene.Count);Assert.IsNull(RandomState(gate.Current));Assert.AreEqual(sounds,audio.Cues.Count);
            Assert.IsFalse(gate.TryStart(cue,()=>throw new InvalidOperationException("Receipt replay must not recreate children.")));
        }
    }

    [TestMethod]
    public void InvalidOperandsRecipesAndRequiredContextFailClosedButUnusedContextDoesNot()
    {
        var source=Primitive();void Reject(SourceAnimationScript script,CommittedMoveAnimation? cue=null,AnimationBattler[]? battlers=null)=>
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,cue??Cue(),battlers??Battlers(false),false,new Audio()));
        foreach(var args in new[]{new[]{2,20,1},new[]{1,-1,1},new[]{1,32768,1},new[]{1,20,5},new[]{1,20,-32769}})
            Reject(source with{Commands=[source.Commands[0],source.Commands[1],Create(args[0],args[1],args[2]),source.Commands[3],source.Commands[4]]});
        Reject(source,Cue(seed:null));var badBattlers=Battlers(false);badBattlers[1]=badBattlers[1] with{SourceWidth=0};Reject(source,battlers:badBattlers);
        foreach(string template in new[]{Parent,Impact}){var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes.Remove(template);Reject(source with{SpriteRecipes=recipes});}
        Reject(source with{SpriteDependencies=[]});
        foreach(var bad in new[]{source.SpriteRecipes[Parent] with{Blend=true},source.SpriteRecipes[Parent] with{Width=16},source.SpriteRecipes[Parent] with{Callback="UnknownRandomFist"}})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[Parent]=bad;Reject(source with{SpriteRecipes=recipes});}
        foreach(var bad in new[]{source.SpriteRecipes[Impact] with{Blend=false},source.SpriteRecipes[Impact] with{Width=16},source.SpriteRecipes[Impact] with{AffineDoubleSize=true},source.SpriteRecipes[Impact] with{AffineAnimations=[source.SpriteRecipes[Impact].AffineAnimations[0]]}})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[Impact]=bad;Reject(source with{SpriteRecipes=recipes});}
        var helper=new Dictionary<string,SourceAnimationCommand[]>{{"unused",[Create(),new("return",[],2)]}};
        var unused=source with{Commands=[new("end",[],1)],Subroutines=helper};var runtime=new SourceBattleAnimation(unused,Cue(seed:null),badBattlers,false,new Audio());runtime.Tick();Assert.IsFalse(runtime.Active);
        Reject(unused with{Commands=[new("call",["unused"],1),new("end",[],2)]},Cue(seed:null));
        Reject(unused with{Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"unused",[Create(frame:5),new("return",[],2)]}}},Cue(seed:null));
        var malformed=unused.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);var impact=malformed[Impact];var affine=impact.AffineAnimations.Select(p=>p.ToArray()).ToArray();affine[3][0]=affine[3][0] with{X=127};malformed[Impact]=impact with{AffineAnimations=affine};
        Reject(unused with{SpriteRecipes=malformed},Cue(seed:null));
    }
}
