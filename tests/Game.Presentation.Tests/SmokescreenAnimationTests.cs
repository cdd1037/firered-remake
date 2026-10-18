using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SmokescreenAnimationTests
{
    private const string Impact="sSmokescreenImpactSpriteTemplate",Parent="SpriteCB_SmokescreenImpactMain",Smoke="gBlackSmokeSpriteTemplate",Task="AnimTask_SmokescreenImpact";
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static CommittedMoveAnimation Cue(int actor=0,int target=1)=>new("smokescreen",1,108,new(actor),new(target));
    private static AnimationBattler[] Bodies(bool doubles,int y=8,int elevation=0)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body"+i,y,(i&1)==1?elevation:0,64,64)).ToArray();
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound>Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private sealed class Occupied(int slot):SourceBattleAnimation.VisualSprite(slot,100)
    {public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"occupied",default,default,0,false,false,false,256,null,100);public override void Step(SourceBattleAnimation owner){}}
    private sealed class RetainedPrioritySprite(int slot):SourceBattleAnimation.VisualSprite(slot,0)
    {public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"retained-priority-two",new(120,80),default,0,true,false,false,256,"retained",0){OamPriority=2};public override void Step(SourceBattleAnimation owner){}}
    private static object? Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.Invoke(target,args);
    private static T Field<T>(object target,string name)=>(T)target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.GetValue(target)!;
    private static SourceAnimationScript Primitive(SourceAnimationScript source,bool retain=false)=>source with{Commands=retain?[C("createvisualtask",Task,"2"),C("end")]:[C("createvisualtask",Task,"2"),C("delay","20"),C("end")],Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("smokescreen-source-vectors.json","python tests/Reference/Smokescreen/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/smokescreen-source-vectors.json");

    [TestMethod]
    public void OriginalRawHelperMatchesFramesFlipsFragmentedAllocationAndOneTickParentFree()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(108);int cases=0,rows=0;
        foreach(var item in vectors.RootElement.GetProperty("cases").GetProperty("impact").EnumerateArray())
        {
            if(item.GetProperty("persist").GetBoolean()||item.GetProperty("preloaded").GetBoolean())continue;
            bool doubles=item.GetProperty("doubles").GetBoolean();var bodies=Bodies(doubles,item.GetProperty("yOffset").GetInt32(),item.GetProperty("elevation").GetInt32());var scene=new SourceBattleAnimation.SpriteScene();
            var occupied=item.GetProperty("occupied").EnumerateArray().Select(x=>new Occupied(x.GetInt32())).ToArray();foreach(var sprite in occupied)scene.Sprites.Add(sprite);
            var owner=new SourceBattleAnimation(Primitive(resolved.Script),Cue(item.GetProperty("actor").GetInt32(),item.GetProperty("target").GetInt32()),bodies,doubles,new Audio(),spriteScene:scene,spriteMaterials:resolved.Assets);foreach(var sprite in occupied)sprite.Owner=owner;
            foreach(var row in item.GetProperty("rows").EnumerateArray())
            {
                int[] state=row.GetProperty("state").EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(state[0]<0)continue;var frame=owner.Tick();string why=$"case {cases}, frame {state[0]}";
                var actual=frame.Sprites.Where(s=>s.Template is Impact or Parent).OrderBy(s=>s.Slot).ToArray();var expected=row.GetProperty("sprites").EnumerateArray().ToArray();Assert.AreEqual(expected.Length,actual.Length,why);Assert.AreEqual(0,owner.ActiveVisualCount,why);Assert.AreEqual(state[1]!=0,scene.HasDynamicSpriteGraphics(55019),why);Assert.AreEqual(state[2]!=0,frame.SpritePalettes.ContainsKey(55019),why);Assert.AreEqual(state[1]!=0,owner.GraphicsLoaded,why);
                for(int i=0;i<actual.Length;i++)
                {
                    int[] v=expected[i].EnumerateArray().Select(x=>x.GetInt32()).ToArray();var s=actual[i];Assert.AreEqual(v[0],s.Slot,why);Assert.AreEqual(v[1]==0?Parent:Impact,s.Template,why);Assert.AreEqual(new PixelPoint(v[2],v[3]),s.Position,why);Assert.AreEqual(new PixelPoint(v[4],v[5]),s.Offset,why);Assert.AreEqual(v[6],s.Tile,why);Assert.AreEqual(v[7]==0,s.Visible,why);Assert.AreEqual(v[8]!=0,s.HFlip,why);Assert.AreEqual(v[9]!=0,s.VFlip,why);Assert.AreEqual(v[10],s.Subpriority,why);Assert.AreEqual(v[11],s.OamPriority,why);
                    var part=scene.Sprites[s.Slot]!;if(v[1]==0)Assert.AreEqual(v[14],Field<int>(part,"LiveChildren"),why);else{var frames=Field<SourceSpriteFrameProgram>(part,"_frames");Assert.AreEqual(v[12]!=0,frames.Ended,why);Assert.AreEqual(v[13],frames.DelayCounter,why);Assert.AreEqual(55019,s.Material!.PaletteTag,why);CollectionAssert.AreEqual(resolved.Assets["smokescreen-impact"].Palette,s.MaterialPalette!,why);}
                }
                Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(owner.Snapshot()),why);rows++;
            }
            Assert.AreEqual(occupied.Length,scene.Count);Assert.IsFalse(scene.HasDynamicSpriteGraphics(55019));owner.Cancel();Assert.AreEqual(0,scene.Count);cases++;
        }
        Assert.AreEqual(6,cases);Assert.AreEqual(84,rows);
        var extra=vectors.RootElement.GetProperty("cases").GetProperty("impact").EnumerateArray().Where(c=>c.GetProperty("persist").GetBoolean()||c.GetProperty("preloaded").GetBoolean()).ToArray();Assert.AreEqual(2,extra.Length);
        foreach(var item in extra){var last=item.GetProperty("rows").EnumerateArray().Last();int[] s=last.GetProperty("state").EnumerateArray().Select(x=>x.GetInt32()).ToArray();Assert.AreEqual(0,s[1]);Assert.AreEqual(0,s[2]);Assert.AreEqual(1,s[5]);Assert.AreEqual(1,s[6]);Assert.AreEqual(item.GetProperty("preloaded").GetBoolean()?0:1,s[3]);Assert.AreEqual(item.GetProperty("persist").GetBoolean()?1:0,last.GetProperty("sprites").GetArrayLength());}
    }

    [TestMethod]
    public void OriginalSmokeSignedAccumulatorsBlinkAndDestructionMatchAllAuthoredVelocities()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);var source=resources.Resolve(108).Script;int cases=0,rows=0;
        foreach(var item in vectors.RootElement.GetProperty("cases").GetProperty("smoke").EnumerateArray())
        {
            int target=item.GetProperty("x").GetInt32()==72?0:1;string[] args=item.GetProperty("args").EnumerateArray().Select(x=>x.GetInt32().ToString(CultureInfo.InvariantCulture)).ToArray();var create=new SourceAnimationCommand("createsprite",new[]{Smoke,"ANIM_TARGET","4"}.Concat(args).ToArray(),1);
            var script=source with{Commands=[C("loadspritegfx","ANIM_TAG_BLACK_SMOKE"),create,C("waitforvisualfinish"),C("end")],Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};var scene=new SourceBattleAnimation.SpriteScene();var owner=new SourceBattleAnimation(script,Cue(target^1,target),Bodies(false),false,new Audio(),spriteScene:scene);
            for(int i=0;i<3;i++)Assert.AreEqual(0,owner.Tick().Sprites.Length);
            foreach(var row in item.GetProperty("rows").EnumerateArray())
            {
                int[] state=row.GetProperty("state").EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(state[0]<0)continue;var frame=owner.Tick();var expected=row.GetProperty("sprites").EnumerateArray().ToArray();Assert.AreEqual(expected.Length,frame.Sprites.Length);Assert.AreEqual(state[7],owner.ActiveVisualCount);
                if(expected.Length!=0){int[] v=expected[0].EnumerateArray().Select(x=>x.GetInt32()).ToArray();var s=frame.Sprites.Single();Assert.AreEqual(Smoke,s.Template);Assert.AreEqual(new PixelPoint(v[2],v[3]),s.Position);Assert.AreEqual(new PixelPoint(v[4],v[5]),s.Offset);Assert.AreEqual(v[7]==0,s.Visible);Assert.AreEqual((32,16),(s.Width,s.Height));var part=scene.Sprites[s.Slot]!;Assert.AreEqual((short)v[14],Field<short>(part,"_speed"));Assert.AreEqual((short)v[15],Field<short>(part,"_remaining"));Assert.AreEqual((short)v[16],Field<short>(part,"_sum"));}rows++;
            }
            owner.Cancel();cases++;
        }
        Assert.AreEqual(17,cases);Assert.AreEqual(927,rows);
    }

    [TestMethod]
    public void FullRootUsesCanonicalMaterialsAndFinishesAfterAllRawAndCountedChildren()
    {
        using var vectors=Vectors();using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(108);CollectionAssert.AreEquivalent(new[]{"impact","black-ball","black-smoke","smokescreen-impact"},resolved.Assets.Keys.ToArray());
        foreach(var (doubles,actor,target) in new[]{(false,0,1),(false,1,0),(true,0,3),(true,1,2),(true,2,1),(true,3,0)})
        {
            var native=vectors.RootElement.GetProperty("schedules").EnumerateArray().Single(s=>s.GetProperty("doubles").GetBoolean()==doubles&&s.GetProperty("actor").GetInt32()==actor&&s.GetProperty("target").GetInt32()==target);var nativeRows=native.GetProperty("rows").EnumerateArray().ToArray();var events=native.GetProperty("events").EnumerateArray().Select(r=>r.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).ToArray();
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var owner=new SourceBattleAnimation(resolved.Script,Cue(actor,target),bodies,doubles,audio,spriteScene:scene,spriteMaterials:resolved.Assets);var replay=new SourceBattleAnimation(resolved.Script,Cue(actor,target),bodies,doubles,new Audio(),spriteMaterials:resolved.Assets);
            int ticks=0,impactBirth=-1,impactFree=-1,smokeBirth=-1,smokeCount=0;bool impactPixels=false,smokePixels=false;var seen=new HashSet<SourceBattleAnimation.VisualSprite>();
            while(owner.Active&&ticks++<200)
            {
                var f=owner.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(owner.Snapshot()));
                var expected=nativeRows[f.Frame];int[] nativeState=expected.GetProperty("state").EnumerateArray().Select(x=>x.GetInt32()).ToArray();var nativeSprites=expected.GetProperty("sprites").EnumerateArray().Select(r=>r.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).ToArray();Assert.AreEqual(nativeState[7],owner.ActiveVisualCount);Assert.AreEqual(nativeState[1]!=0,scene.HasDynamicSpriteGraphics(55019));Assert.AreEqual(nativeSprites.Length,f.Sprites.Length);
                foreach(var v in nativeSprites){var actual=f.Sprites.Single(s=>s.Slot==v[0]);string template=v[1] switch{0=>Parent,1=>Impact,2=>Smoke,_=>"gBlackBallSpriteTemplate"};Assert.AreEqual(template,actual.Template);Assert.AreEqual(new PixelPoint(v[2],v[3]),actual.Position);Assert.AreEqual(new PixelPoint(v[4],v[5]),actual.Offset);Assert.AreEqual(v[6],actual.Tile);Assert.AreEqual(v[7]==0,actual.Visible);Assert.AreEqual(v[8]!=0,actual.HFlip);Assert.AreEqual(v[9]!=0,actual.VFlip);Assert.AreEqual(v[10],actual.Subpriority);Assert.AreEqual(v[11],actual.OamPriority);}
                foreach(var s in scene.Sprites.Where(s=>seen.Add(s))){if(s.Frame.Template==Parent)impactBirth=f.Frame;if(s.Frame.Template==Smoke){smokeBirth=smokeBirth<0?f.Frame:smokeBirth;smokeCount++;}}
                if(impactBirth>=0&&impactFree<0&&!scene.HasDynamicSpriteGraphics(55019))impactFree=f.Frame;
                var pixels=composer.ComposeField(f);if(f.Sprites.Any(s=>s.Template==Impact))impactPixels|=!pixels.SequenceEqual(composer.ComposeField(f with{Sprites=f.Sprites.Where(s=>s.Template!=Impact).ToArray()}));if(f.Sprites.Any(s=>s.Template==Smoke&&s.Visible))smokePixels|=!pixels.SequenceEqual(composer.ComposeField(f with{Sprites=f.Sprites.Where(s=>s.Template!=Smoke).ToArray()}));
            }
            Assert.AreEqual(nativeRows.Length,ticks);Assert.AreEqual(events.Single(e=>e[1]==1)[0],impactBirth);Assert.AreEqual(events.Single(e=>e[1]==3)[0],owner.Snapshot().Frame);CollectionAssert.AreEqual(events.Where(e=>e[1]==2).Select(e=>e[0]).ToArray(),audio.Cues.Select(c=>c.Frame).ToArray());Assert.AreEqual(12,smokeCount);Assert.AreEqual(13,impactFree-impactBirth);Assert.IsTrue(smokeBirth>impactBirth&&smokeBirth<impactFree);Assert.IsTrue(impactPixels&&smokePixels);Assert.IsFalse(owner.Active||owner.GraphicsLoaded||owner.HasResidualSprites);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.AreEqual(0,owner.Snapshot().SpritePalettes.Count);CollectionAssert.AreEqual(new[]{"SE_M_DOUBLE_TEAM","SE_M_SAND_ATTACK"},audio.Cues.Select(s=>s.Id).ToArray());Assert.IsFalse(scene.HasDynamicSpriteGraphics(55019));
        }
    }

    [TestMethod]
    public void RetiredParentKeepsLeaseOverlapFailsBeforeSideEffectsAndOwnerCancellationIsScoped()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(108);var bodies=Bodies(true);var scene=new SourceBattleAnimation.SpriteScene();var old=new SourceBattleAnimation(Primitive(resolved.Script,true),Cue(),bodies,true,new Audio(),spriteScene:scene,spriteMaterials:resolved.Assets);old.Tick();Assert.IsFalse(old.Active);Assert.IsTrue(old.HasResidualSprites&&old.GraphicsLoaded&&scene.HasDynamicSpriteGraphics(55019));Assert.AreEqual(5,scene.Count);
        var rejected=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(resolved.Script,Cue(),bodies,true,rejected,spriteScene:scene,spriteMaterials:resolved.Assets));Assert.AreEqual(0,rejected.Cues.Count);Assert.AreEqual(5,scene.Count);
        for(int tick=1;tick<=13;tick++){var frame=scene.TickIdle(bodies,true);Assert.AreEqual(tick<13,scene.HasDynamicSpriteGraphics(55019));if(tick<12)Assert.IsTrue(frame.Sprites.Where(s=>s.Template==Impact).All(s=>s.Material is not null&&s.MaterialPalette!.SequenceEqual(resolved.Assets["smokescreen-impact"].Palette)));}
        Assert.AreEqual(0,scene.Count);var replacement=new SourceBattleAnimation(Primitive(resolved.Script,true),Cue(),bodies,true,new Audio(),spriteScene:scene);replacement.Tick();Assert.IsTrue(scene.HasDynamicSpriteGraphics(55019));old.Cancel();Assert.IsTrue(scene.HasDynamicSpriteGraphics(55019));
        var colors=resolved.Script.SpritePalettes.ToDictionary(p=>p.Key,p=>p.Value);colors[60000]=colors[55019];var unrelated=new SourceBattleAnimation(resolved.Script with{Commands=[C("delay","50"),C("end")],SpritePalettes=colors},Cue(),bodies,true,new Audio(),spriteScene:scene);Invoke(unrelated,"AcquireDynamicSpriteGraphics",60000);Assert.IsTrue(scene.HasDynamicSpriteGraphics(60000));replacement.Cancel();Assert.IsFalse(scene.HasDynamicSpriteGraphics(55019));Assert.IsTrue(scene.HasDynamicSpriteGraphics(60000));scene.Cancel();Assert.IsFalse(scene.HasDynamicSpriteGraphics(60000));Assert.AreEqual(0,scene.Count);
    }

    [TestMethod]
    public void PriorityOneQuadrantsSortBeforeRetainedPriorityTwoAndInvisiblePriorityThreeParent()
    {
        using var resources=new SourceAnimationResources(Folder);var source=resources.Resolve(108).Script;var bodies=Bodies(true);var scene=new SourceBattleAnimation.SpriteScene();
        var retired=new SourceBattleAnimation(source with{Commands=[C("end")]},Cue(),bodies,true,new Audio(),spriteScene:scene);var retained=new RetainedPrioritySprite(4){Owner=retired};scene.Sprites.Add(retained);retired.Tick();Assert.IsFalse(retired.Active);Assert.IsTrue(retired.HasResidualSprites);
        var owner=new SourceBattleAnimation(Primitive(source),Cue(),bodies,true,new Audio(),spriteScene:scene);var frame=owner.Tick();var previous=frame.Sprites.Single(s=>s.Template=="retained-priority-two");var parent=frame.Sprites.Single(s=>s.Template==Parent);var children=frame.Sprites.Where(s=>s.Template==Impact).ToArray();
        Assert.AreEqual(4,children.Length);Assert.AreEqual(2,previous.OamPriority);Assert.AreEqual(0,previous.Subpriority);Assert.AreEqual(3,parent.OamPriority);Assert.AreEqual(14,parent.Subpriority);Assert.IsFalse(parent.Visible);Assert.IsNull(parent.Asset);
        foreach(var child in children){Assert.AreEqual(1,child.OamPriority);Assert.AreEqual(2,child.Subpriority);Assert.IsTrue(child.OamRank<previous.OamRank);}
        Assert.IsTrue(previous.OamRank<parent.OamRank);owner.Cancel();Assert.AreSame(retained,scene.Sprites[4]);retired.Cancel();Assert.AreEqual(0,scene.Count);
    }

    [TestMethod]
    public void CapacityAndDependencyFailuresPrecedeAudioAndCancellationReleasesEveryPhase()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(108);var source=resolved.Script;
        void Reject(SourceAnimationScript script){var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(),Bodies(true),true,audio,spriteScene:scene));Assert.AreEqual(0,audio.Cues.Count);Assert.AreEqual(0,scene.Count);Assert.IsFalse(scene.HasDynamicSpriteGraphics(55019));}
        Reject(source with{SpriteDependencies=[]});Reject(source with{SpritePalettes=new Dictionary<int,ushort[]>()});Reject(source with{SpriteTags=source.SpriteTags.Where(p=>p.Key!="TAG_SMOKESCREEN").ToDictionary(p=>p.Key,p=>p.Value)});Reject(source with{Commands=[C("createvisualtask",Task,"2"),..source.Commands]});Reject(source with{Commands=[C("createvisualtask",Task,"2","0"),..source.Commands]});
        var recipe=source.SpriteRecipes[Impact];foreach(var bad in new[]{recipe with{Width=32},recipe with{Blend=true},recipe with{LoopStarts=[0,0,0,0]},recipe with{Animations=[[new(0,4,false,false)]]},recipe with{AffineAnimations=[[new("end",0,0,0,0,0)]]}})Reject(source with{SpriteRecipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==Impact?bad:p.Value)});
        var full=new SourceBattleAnimation.SpriteScene();for(int i=4;i<50;i++)full.Sprites.Add(new Occupied(i));var audio2=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(source,Cue(),Bodies(true),true,audio2,spriteScene:full));Assert.AreEqual(46,full.Count);Assert.AreEqual(0,audio2.Cues.Count);
        foreach(int stop in Enumerable.Range(0,15)){var scene=new SourceBattleAnimation.SpriteScene();var owner=new SourceBattleAnimation(Primitive(source),Cue(),Bodies(true),true,new Audio(),spriteScene:scene);for(int tick=0;tick<stop;tick++)owner.Tick();owner.Cancel();owner.Cancel();Assert.AreEqual(0,scene.Count);Assert.IsFalse(scene.HasDynamicSpriteGraphics(55019));Assert.IsFalse(owner.GraphicsLoaded);Assert.AreEqual(0,owner.Snapshot().SpritePalettes.Count);}
    }
}
