using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SolarPincerAnimationTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound>Cues=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags)!.GetValue(o)!;
    private static object? Invoke(object o,string n,params object[] a)=>o.GetType().GetMethod(n,Flags)!.Invoke(o,a);
    private static object Nested(string n,params object[] a)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(n,BindingFlags.NonPublic)!,Flags,null,a,CultureInfo.InvariantCulture)!;
    private static AnimationBattler[] Bodies(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],64,new[]{64,48,32,56}[i])).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor,int target,int turn=1)=>new("solar-pincer",1,move,new(actor),new(target)){MoveTurn=(byte)turn};
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[new("end",[],1)],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    [TestMethod]
    public void NativeOrbTaskAndPincerFramesRetainEveryCallbackAndPlayerPhase()
    {
        using var doc=SourceVectorFixture.Read("solar-pincer-source-vectors.json","python tests/Game.Presentation.Tests/Reference/generate_solar_pincer_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");
        int cases=0,rows=0;foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateObject())
        {
            var c=entry.Value.GetProperty("config").EnumerateArray().Select(v=>v.GetInt32()).ToArray();int kind=c[0],actor=c[2],target=c[3];bool doubles=c[1]!=0;var bodies=Bodies(doubles);var script=AnimationCompositionFixture.Script(kind==1?12:76);
            var owner=new SourceBattleAnimation(Primitive(script),Cue(script.MoveId,actor,target),bodies,doubles,new Audio());var args=Field<short[]>(owner,"_animationArgs");for(int i=0;i<4;i++)args[i]=(short)c[i+4];var sprites=Field<object>(owner,"_sprites");var tasks=Field<IList>(owner,"_tasks");
            if(kind==2){var task=Nested("SolarSecondaryTask");Invoke(task,"Step",owner);tasks.Add(task);}
            else{var recipe=script.SpriteRecipes[kind==1?"gGuillotineSpriteTemplate":"gSolarBeamBigOrbSpriteTemplate"];int priority=bodies[kind==1?actor:target].Subpriority-(kind==1?2:3);var sprite=kind==1?Nested("GuillotineSprite",bodies.Length,priority,recipe,owner):Nested("SolarOrbSprite",bodies.Length,priority,recipe,owner,false);Invoke(sprites,"Add",sprite);}
            int tick=-2;foreach(var row in entry.Value.GetProperty("rows").EnumerateArray())
            {
                int t=row[0].GetInt32();Assert.AreEqual(++tick,t);if(t>=0){Invoke(owner,"AdvanceSourceSprites");Invoke(owner,"RunSourceTasks");}string why=entry.Name+" tick "+t;
                Assert.AreEqual(row[1].GetInt32(),owner.ActiveVisualCount,why);Assert.AreEqual(row[2].GetInt32(),tasks.Count,why);CollectionAssert.AreEqual(row[3].EnumerateArray().Select(v=>(short)v.GetInt32()).ToArray(),args.Take(4).ToArray(),why);
                var actual=((IEnumerable)sprites).Cast<SourceBattleAnimation.VisualSprite>().OrderBy(s=>s.Frame.Slot).ToArray();var expected=row[4].EnumerateArray().ToArray();Assert.AreEqual(expected.Length,actual.Length,why);
                for(int i=0;i<actual.Length;i++)
                {
                    var e=expected[i].EnumerateArray().Select(v=>v.GetInt32()).ToArray();var f=actual[i].Frame;Assert.AreEqual(e[0],f.Slot,why);Assert.AreEqual(new PixelPoint(e[1],e[2]),f.Position,why);Assert.AreEqual(new PixelPoint(e[3],e[4]),f.Offset,why);Assert.AreEqual(e[5],f.Tile,why);Assert.AreEqual(e[6]!=0,f.HFlip,why);Assert.AreEqual(e[7]!=0,f.VFlip,why);Assert.AreEqual(e[8],f.Subpriority,why);
                    var program=Field<SourceSpriteFrameProgram>(actual[i],"_frames");Assert.AreEqual(e[9],Field<int>(program,"_index"),why);Assert.AreEqual(e[10],program.DelayCounter,why);Assert.AreEqual(e[11]!=0,program.Ended,why);Assert.AreEqual(e[12]!=0,kind==1&&Field<bool>(actual[i],"_paused"),why);
                }
                rows++;
            }
            Assert.AreEqual(0,owner.ActiveVisualCount);owner.Cancel();cases++;
        }
        Assert.AreEqual(doc.RootElement.GetProperty("coverage").GetProperty("caseCount").GetInt32(),cases);Assert.AreEqual(doc.RootElement.GetProperty("coverage").GetProperty("rowCount").GetInt32(),rows);Assert.IsTrue(cases>=23);
    }
    [TestMethod,DataRow(76,0),DataRow(76,1),DataRow(12,1)]
    public void CompleteSourceBranchesComposeBothSidesAndReleaseTheirScene(int move,int turn)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(move);
        foreach(int actor in new[]{0,1,2,3})
        {
            var bodies=Bodies(true);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x801f,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var owner=new SourceBattleAnimation(resolved.Script,Cue(move,actor,actor^1,turn),bodies,true,audio,spriteScene:scene,spriteMaterials:assets);
            int ticks=0,rawBirths=0,bigBirths=0;HashSet<int> prior=[],priorBig=[];bool pixels=false;var backgrounds=new HashSet<int>();while(owner.Active&&ticks++<900)
            {
                var f=owner.Tick();if(f.MoveBackground?.Id is int bg)backgrounds.Add(bg);var current=f.Sprites.Where(s=>s.Template=="gSolarBeamSmallOrbSpriteTemplate").Select(s=>s.Slot).ToHashSet();rawBirths+=current.Except(prior).Count();prior=current;var big=f.Sprites.Where(s=>s.Template=="gSolarBeamBigOrbSpriteTemplate").Select(s=>s.Slot).ToHashSet();bigBirths+=big.Except(priorBig).Count();priorBig=big;var rgba=composer.ComposeField(f);Assert.AreEqual(240*112*4,rgba.Length);if(f.Sprites.Length>0)pixels|=!rgba.SequenceEqual(composer.ComposeField(f with{Sprites=[]}));
            }
            Assert.IsFalse(owner.Active);Assert.IsTrue(pixels);Assert.AreEqual(move==76&&turn==1?15:0,rawBirths);Assert.AreEqual(move==76&&turn==1?21:0,bigBirths);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsFalse(owner.GraphicsLoaded);Assert.IsFalse(owner.Snapshot().BlendEnabled);Assert.IsNull(owner.Snapshot().MoveBackground!.Id);Assert.IsTrue(audio.Cues.Count>0);
            if(move==12)CollectionAssert.AreEqual(new[]{(actor&1)==0?12:13},backgrounds.ToArray());else if(turn==1)CollectionAssert.AreEqual(new[]{(actor&1)==0?24:25},backgrounds.ToArray());else Assert.AreEqual(0,backgrounds.Count);
        }
    }
    [TestMethod]
    public void CancellationAndMalformedSourceDependenciesDoNotLeakOwnedSprites()
    {
        foreach(int move in new[]{12,76})
        {
            var script=AnimationCompositionFixture.Script(move);
            foreach(int stop in new[]{0,4,20,50,90,130,180})
            {
                var scene=new SourceBattleAnimation.SpriteScene();var owner=new SourceBattleAnimation(script,Cue(move,0,1),Bodies(true),true,new Audio(),spriteScene:scene);
                for(int n=0;n<stop&&owner.Active;n++)owner.Tick();owner.Cancel();owner.Cancel();Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsFalse(owner.GraphicsLoaded);Assert.IsNull(owner.Snapshot().MoveBackground!.Id);Assert.IsFalse(owner.Snapshot().BlendEnabled);
            }
            var names=move==12?new[]{"gGuillotineSpriteTemplate"}:new[]{"gSolarBeamBigOrbSpriteTemplate","gSolarBeamSmallOrbSpriteTemplate"};
            foreach(string name in names){var recipes=script.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes.Remove(name);Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script with{SpriteRecipes=recipes},Cue(move,0,1),Bodies(false),false,new Audio()));}
        }
        var pincer=AnimationCompositionFixture.Script(12);var commands=pincer.Commands.Select(c=>c.Op=="fadetobgfromset"?c with{Args=c.Args.Take(2).ToArray()}:c).ToArray();Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(pincer with{Commands=commands},Cue(12,0,1),Bodies(false),false,new Audio()));
    }

}
