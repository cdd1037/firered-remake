using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class BatonPassAnimationTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private const string Template="gBatonPassPokeballSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound>Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects()=>Stops++;}
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Flags)!.GetValue(o)!;
    private static object? Call(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Flags)!.Invoke(o,args);
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static CommittedMoveAnimation Cue(int actor)=>new("baton-pass",1,226,new(actor),new(actor));
    private static AnimationBattler[] Bodies(bool doubles,int y=7,int elevation=0,int flags=0)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,y,elevation,64,64){SourceVisibility=new(true,(flags&1)!=0,(flags&2)!=0)}).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static int[] N(JsonElement row)=>row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static ushort[] Terrain(SourceAnimationResources r){var bg=r.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>bg.PaletteColor(2,i)).ToArray();}

    [TestMethod]
    public void ExactNativeFallthroughAndHiddenResetMatchCompactOracle()
    {
        using var doc=SourceVectorFixture.Read("baton-pass-source-vectors.json","python tests/Reference/BatonPass/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/baton-pass-source-vectors.json");
        Assert.AreEqual("firered-baton-pass-v1",doc.RootElement.GetProperty("schema").GetString());int cases=0,rows=0;
        var script=AnimationCompositionFixture.Script(226);
        foreach(var c in doc.RootElement.GetProperty("cases").EnumerateArray())
        {
            bool doubles=c.GetProperty("doubles").GetBoolean();int actor=c.GetProperty("actor").GetInt32(),flags=c.GetProperty("flags").GetInt32();
            var bodies=Bodies(doubles,c.GetProperty("yOffset").GetInt32(),c.GetProperty("elevation").GetInt32(),flags);
            var owner=new SourceBattleAnimation(Primitive(script),Cue(actor),bodies,doubles,new Audio());
            foreach(var b in bodies){var p=b.Base(doubles);Call(owner,"SetBattlerPosition",b.Slot.Value,new PixelPoint(p.X+17,p.Y+11));Field<PixelPoint[]>(owner,"_offsets")[b.Slot.Value]=new(7,9);}
            var sprite=(SourceBattleAnimation.VisualSprite)Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType("BatonPassBall",BindingFlags.NonPublic)!,Flags,null,[doubles?4:2,2,script.SpriteRecipes[Template],owner],CultureInfo.InvariantCulture)!;
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int tick=row.GetProperty("frame").GetInt32();if(tick>0){sprite.Step(owner);if(!sprite.Finished)sprite.AdvanceAnimation(owner);}
                string why=$"case{cases} actor{actor} frame{tick}";var p=N(row.GetProperty("position"));var f=sprite.Frame;
                Assert.AreEqual(new PixelPoint(p[0],p[1]),f.Position,why);Assert.AreEqual(new PixelPoint(p[2],p[3]),f.Offset,why);
                Assert.AreEqual(row.GetProperty("alive").GetInt32()!=0,!sprite.Finished,why);Assert.AreEqual(0,f.Tile,why);Assert.IsTrue(f.Visible,why);
                CollectionAssert.AreEqual(N(row.GetProperty("data")).Select(x=>(short)x).ToArray(),Field<short[]>(sprite,"_d"),why);
                var matrix=N(row.GetProperty("matrix"));CollectionAssert.AreEqual(matrix,new[]{Field<int[]>(owner,"_xScale")[actor],Field<int[]>(owner,"_matrixB")[actor],Field<int[]>(owner,"_matrixC")[actor],Field<int[]>(owner,"_yScale")[actor]},why);
                Assert.AreEqual(row.GetProperty("actorAffine").GetInt32()==3,Field<bool[]>(owner,"_affineMode")[actor],why);
                var visibility=owner.Snapshot().BattlerVisibility.Single(v=>v.Slot==actor);Assert.AreEqual(row.GetProperty("actorInvisible").GetInt32()!=0,visibility.SpriteInvisible,why);Assert.AreEqual((flags&1)!=0,visibility.LogicalInvisible,why);
                var ap=N(row.GetProperty("actorPosition"));Assert.AreEqual(new PixelPoint(ap[0],ap[1]),(PixelPoint)Call(owner,"BattlerPosition",actor)!,why);Assert.AreEqual(new PixelPoint(ap[2],ap[3]),Field<PixelPoint[]>(owner,"_offsets")[actor],why);
                if(tick==1){var writes=row.GetProperty("writes");CollectionAssert.AreEqual(new[]{352,230},N(writes[0]));CollectionAssert.AreEqual(new[]{448,278},N(writes[1]));}
                if(tick==6){var writes=row.GetProperty("writes");CollectionAssert.AreEqual(new[]{1120,466},N(writes[0]));CollectionAssert.AreEqual(new[]{256,256},N(writes[1]));Assert.IsTrue(visibility.SpriteInvisible);}
                rows++;
            }
            owner.Cancel();cases++;
        }
        Assert.AreEqual(12,cases);Assert.AreEqual(297,rows);
    }

    [TestMethod]
    public void FullRootSharesIndexedPaletteCompositionAndHoldsDepartingBodyUntilReplacement()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(226);int cases=0;
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);
            assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64 is >12 and <51&&i/64 is >8 and <55?0x83e0:0)).ToArray());
            Assert.AreEqual(256,assets["pokeball"].PaletteIndices.Length);Assert.AreEqual(16,assets["pokeball"].Palette.Length);
            var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var owner=new SourceBattleAnimation(resolved.Script,Cue(actor),bodies,doubles,audio,Terrain(resources),spriteScene:scene,spriteMaterials:assets);var initial=owner.Snapshot();var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);int ticks=0;bool pixels=false,affine=false,hidden=false;
            while(owner.Active&&ticks++<200)
            {
                var frame=owner.Tick();string before=JsonSerializer.Serialize(frame);var rgba=composer.ComposeField(frame);composer.ComposeField(frame);Assert.AreEqual(before,JsonSerializer.Serialize(frame));
                if(frame.Sprites.Any(s=>s.Template==Template))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));
                affine|=frame.BattlerAffine[actor].DoubleSize;hidden|=frame.BattlerVisibility[actor].SpriteInvisible;
            }
            Assert.IsTrue(ticks<200);Assert.IsTrue(pixels);Assert.IsTrue(affine);Assert.IsTrue(hidden);AssertClean(owner);Assert.AreEqual(0,scene.Count);Assert.AreEqual(1,audio.Cues.Count);Assert.AreEqual("SE_M_BATON_PASS",audio.Cues[0].Id);Assert.AreEqual(actor%2==0?-64:63,audio.Cues[0].Pan);
            var completed=owner.Snapshot();Assert.IsTrue(completed.BattlerVisibility[actor].SpriteInvisible);Assert.IsFalse(completed.BattlerAffine[actor].DoubleSize);
            var hold=new SourceCompletedBattlerState();Assert.IsTrue(hold.Capture(initial,completed));CollectionAssert.AreEqual(new[]{actor},hold.Slots);
            for(int wait=0;wait<4;wait++)Assert.IsTrue(scene.TickIdle(bodies,doubles,hold).BattlerVisibility[actor].SpriteInvisible);
            // Switch-entry owns the release; the incoming occupant must not inherit
            // the outgoing member's hidden state while the other slots stay intact.
            var incoming=(AnimationBattler[])bodies.Clone();incoming[actor]=incoming[actor] with{Asset="incoming"};hold.DropSlot(actor);var idle=scene.CaptureIdle(incoming,doubles,hold);
            Assert.IsFalse(hold.HasState);Assert.IsTrue(idle.BattlerVisibility.Length==0||!idle.BattlerVisibility[actor].SpriteInvisible);Assert.AreEqual("incoming",idle.Battlers[actor].Asset);Assert.AreEqual(default(PixelPoint),idle.Offsets[actor]);cases++;
        }
        Assert.AreEqual(6,cases);
    }
    private static void AssertClean(SourceBattleAnimation owner)
    {Assert.IsFalse(owner.Active);Assert.IsFalse(owner.GraphicsLoaded);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsFalse(owner.HasResidualSprites);var f=owner.Snapshot();Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.AreEqual(0,f.PaletteBlends.Length);Assert.IsFalse(f.BlendEnabled);Assert.IsTrue(f.Offsets.All(p=>p==default));}

    [TestMethod]
    public void CancellationAtEveryTickRestoresCapturedVisibilityAndAffineState()
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(226);
        foreach(int flags in new[]{0,1,2,3})
        {
            var bodies=Bodies(true,7,0,flags);var baseline=new SourceBattleAnimation(r.Script,Cue(2),bodies,true,new Audio(),Terrain(resources));int duration=0;while(baseline.Active&&duration++<200)baseline.Tick();AssertClean(baseline);
            for(int stop=0;stop<=duration;stop++)
            {
                var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var owner=new SourceBattleAnimation(r.Script,Cue(2),bodies,true,audio,Terrain(resources),spriteScene:scene);var initial=owner.Snapshot();for(int i=0;i<stop&&owner.Active;i++)owner.Tick();owner.Cancel();AssertClean(owner);Assert.AreEqual(0,scene.Count);
                CollectionAssert.AreEqual(initial.BattlerVisibility,owner.Snapshot().BattlerVisibility);CollectionAssert.AreEqual(initial.BattlerAffine,owner.Snapshot().BattlerAffine);Assert.AreEqual(1,audio.Stops);
            }
        }
    }

    [TestMethod]
    public void MalformedRecipeOperandsAbsentActorAndCompanionWritesFailBeforePlayback()
    {
        var script=AnimationCompositionFixture.Script(226);var recipe=script.SpriteRecipes[Template];var command=script.Commands.Single(c=>c.Op=="createsprite");
        void Reject(SourceAnimationScript s,AnimationBattler[]? bodies=null){var audio=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(0),bodies??Bodies(false),false,audio));Assert.AreEqual(0,audio.Cues.Count);}
        foreach(var bad in new[]{recipe with{Width=32},recipe with{Callback="UnknownBaton"},recipe with{Asset="spotlight"},recipe with{AffineDoubleSize=true},recipe with{Animations=[[new(1,1,false,false)]]}})
            Reject(script with{SpriteRecipes=script.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==Template?bad:p.Value)});
        foreach(var args in new[]{new[]{Template,"ANIM_TARGET","2"},new[]{Template,"ANIM_ATTACKER","3"},new[]{Template,"ANIM_ATTACKER","2","0"}})
            Reject(script with{Commands=script.Commands.Select(c=>ReferenceEquals(c,command)?c with{Args=args}:c).ToArray()});
        var absent=Bodies(false);absent[0]=absent[0] with{SourceVisibility=new(false,true,true)};Reject(script,absent);
        Reject(script with{Commands=[command,command,C("end")]});Reject(script with{Commands=[command,C("invisible","ANIM_TARGET"),C("end")]});
        Reject(script with{Commands=[command,C("createvisualtask","AnimTask_ShakeMon","2","0","2","0","8","1"),C("end")]});
    }
}
