using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FeatherDanceAnimationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private const string Template="gFallingFeatherSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects()=>Stops++;}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceAnimationScript Primitive(SourceAnimationScript s,params SourceAnimationCommand[] commands)=>s with{Commands=commands,Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static AnimationBattler[] Battlers(bool d)=>Enumerable.Range(0,d?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,11+i,7+i,16+i*8,32+i*8)).ToArray();
    private static CommittedMoveAnimation Cue(int actor,int target)=>new("feather",1,297,new(actor),new(target));
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static int[] Numbers(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    [TestMethod]
    public void FallingFeatherMatchesOriginalCallbackAndFrameInterpreterAcrossEveryGeometryAndQuadrant()
    {
        using var doc=SourceVectorFixture.Read("feather-dance-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_feather_dance_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/feather-dance-source-vectors.json");var source=AnimationCompositionFixture.Script(297);var configs=doc.RootElement.GetProperty("configs").EnumerateArray().Select(Numbers).ToArray();int cases=0,rows=0;
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateObject())
        {
            var k=entry.Name.Split(':').Select(int.Parse).ToArray();bool doubles=k[0]!=0;var args=configs[k[3]];
            var owner=new SourceBattleAnimation(Primitive(source,C("delay","1"),C("end")),Cue(k[1],k[2]),Battlers(doubles),doubles,new Audio());
            var registers=Field<short[]>(owner,"_animationArgs");for(int i=0;i<8;i++)registers[i]=unchecked((short)args[i]);var priorities=Field<int[]>(owner,"_battlerBackgroundPriorities");priorities[0]=k[4];priorities[1]=k[4]+1;
            var sprite=(SourceBattleAnimation.VisualSprite)Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType("FallingFeatherSprite",BindingFlags.NonPublic)!,BindingFlags.Public|Private,null,new object[]{4,40,source.SpriteRecipes[Template],args,owner},null)!;
            foreach(var element in entry.Value.EnumerateArray())
            {
                var r=Numbers(element);if(r[0]>=0){sprite.Step(owner);if(!sprite.Finished)sprite.AdvanceAnimation(owner);}string why=$"{entry.Name} tick {r[0]}";var f=sprite.Frame;
                Assert.AreEqual(r[1]==0,sprite.Finished,why);Assert.AreEqual(new PixelPoint(r[2],r[3]),f.Position,why);Assert.AreEqual(new PixelPoint(r[4],r[5]),f.Offset,why);Assert.AreEqual(r[6],f.Tile,why);Assert.AreEqual(r[7]!=0,f.HFlip,why);Assert.AreEqual(r[8],f.OamPriority,why);CollectionAssert.AreEqual(r.Skip(9).Take(4).ToArray(),new[]{f.MatrixA,f.MatrixB,f.MatrixC,f.MatrixD},why);
                Assert.AreEqual(r[13]!=0,Field<SourceSpriteFrameProgram>(sprite,"_frames").Ended,why);Assert.AreEqual(r[23],(int)registers[0],why);Assert.IsTrue(f.Affine&&!f.DoubleSize&&!f.Blend,why);
                if(r[22]==0){Assert.AreEqual(r[15]!=0,Field<bool>(sprite,"_pause"),why);Assert.AreEqual(r[16],Field<int>(sprite,"_amplitude"),why);Assert.AreEqual(r[17]!=0,Field<bool>(sprite,"_flipPending"),why);Assert.AreEqual(r[18],Field<int>(sprite,"_quadrant"),why);}
                Assert.AreEqual(r[19],(int)Field<ushort>(sprite,"_angle"),why);Assert.AreEqual(r[20],(int)Field<ushort>(sprite,"_vertical"),why);Assert.AreEqual(r[21]!=0,Field<bool>(sprite,"_front"),why);Assert.AreEqual(r[22]!=0,Field<bool>(sprite,"_destroyNext"),why);rows++;
            }
            Assert.IsTrue(sprite.Finished);Assert.IsNull(Field<object?>(owner,"_visualRandom"));owner.Cancel();cases++;
        }
        Assert.AreEqual(960,cases);Assert.AreEqual(120480,rows);
    }
    private static void Clean(SourceBattleAnimation runtime,SourceBattleAnimation.SpriteScene scene)
    {Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,scene.Count);Assert.IsFalse(runtime.GraphicsLoaded);var f=runtime.Snapshot();Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.IsTrue(f.Offsets.All(p=>p==default));Assert.IsFalse(f.BlendEnabled);Assert.IsTrue(f.BattlerAffine.All(a=>!a.DoubleSize&&a.XScale==256&&a.YScale==256));}
    [TestMethod]
    public void DedicatedRootResolvesOneExistingIndexedMaterialRendersReplaysAndDrainsAcrossSides()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(297);Assert.AreEqual("Move_FEATHER_DANCE",resolved.Script.Label);CollectionAssert.AreEquivalent(new[]{"impact","white-feather"},resolved.Assets.Keys.ToArray());var material=resolved.Assets["white-feather"];Assert.AreEqual(32,material.Width);Assert.AreEqual(64,material.Height);Assert.AreEqual(2048,material.PaletteIndices.Length);Assert.AreEqual(16,material.Palette.Length);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var b=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var mon in b)assets[mon.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,b);var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(resolved.Script,Cue(actor,target),b,doubles,audio,spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,Cue(actor,target),b,doubles,new Audio(),spriteMaterials:assets);int ticks=0,peak=0;bool pixels=false;var tiles=new HashSet<int>();var priorities=new HashSet<int>();
            while(runtime.Active&&ticks++<500)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(replay.Tick()));peak=Math.Max(peak,frame.Sprites.Length);foreach(var s in frame.Sprites){tiles.Add(s.Tile);priorities.Add(s.OamPriority);Assert.IsNotNull(s.Material);}
                if(!pixels&&frame.Sprites.Length>0)pixels=!composer.ComposeField(frame).SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));
            }
            Clean(runtime,scene);Assert.IsFalse(replay.Active);Assert.IsTrue(pixels);Assert.AreEqual(8,peak);CollectionAssert.AreEquivalent(new[]{0,16},tiles.ToArray());Assert.AreEqual(2,priorities.Count);Assert.AreEqual("SE_M_PETAL_DANCE",audio.Cues.Single().Id);Assert.AreEqual(actor%2==0?(target%2==0?-64:63):(target%2==0?-63:63),audio.Cues.Single().Pan);Assert.IsNull(Field<object?>(runtime,"_visualRandom"));
        }
    }
    [TestMethod]
    public void CancellationAtEveryRootTickReleasesMatrixSpritesBackgroundAndSoundOnce()
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(297);var b=Battlers(true);int length=0;var full=new SourceBattleAnimation(r.Script,Cue(3,0),b,true,new Audio());while(full.Active&&length++<500)full.Tick();Assert.IsFalse(full.Active);
        for(int stop=0;stop<=length;stop++){var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(r.Script,Cue(3,0),b,true,audio,spriteScene:scene);for(int t=0;t<stop&&runtime.Active;t++)runtime.Tick();runtime.Cancel();runtime.Cancel();Clean(runtime,scene);Assert.AreEqual(1,audio.Stops);}
    }
    [TestMethod]
    public void MissingMaterialsAlteredRecipesUnboundedFallAndUnreviewedCompanionsFailClosed()
    {
        var source=AnimationCompositionFixture.Script(297);var r=source.SpriteRecipes[Template];
        foreach(var bad in new[]{r with{Width=16},r with{Callback="Other"},r with{Blend=true},r with{AffineDoubleSize=true},r with{Animations=[[new(0,0,false,false)],[new(16,1,true,false)]]},r with{Asset="orbs"}})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[Template]=bad;Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpriteRecipes=recipes},Cue(0,1),Battlers(false),false,new Audio()));}
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpritePalettes=new Dictionary<int,ushort[]>()},Cue(0,1),Battlers(false),false,new Audio()));
        var original=source.Commands.First(c=>c.Op=="createsprite");foreach((int index,string value) in new[]{(4,"-33"),(7,"0"),(7,"4097"),(9,"0"),(9,"97")}){var args=(string[])original.Args.Clone();args[index]=value;var script=Primitive(source,original with{Args=args},C("end"));Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(0,1),Battlers(false),false,new Audio()));}
        var companion=Primitive(source,original,C("createvisualtask","AnimTask_ShakeMon","5","1","1","0","3","1"),C("end"));Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(companion,Cue(0,1),Battlers(false),false,new Audio()));
        var unloaded=Primitive(source,original,C("waitforvisualfinish"),C("end"));var runtime=new SourceBattleAnimation(unloaded,Cue(0,1),Battlers(false),false,new Audio());Assert.ThrowsExactly<SourceAnimationException>(()=>runtime.Tick());runtime.Cancel();
    }
}
