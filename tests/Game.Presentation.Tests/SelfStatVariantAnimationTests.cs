using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SelfStatVariantAnimationTests
{
    private const string Question="gQuestionMarkSpriteTemplate",Meditate="sAffineAnim_MeditateStretchAttacker";
    private const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound>Cues=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects(){}}
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("self-stat-variant-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_self_stat_variant_vectors.py --repo . --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/self-stat-variant-vectors.json");
    private static int[] Numbers(JsonElement row)=>row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();
    private static object? Field(object value,string name)=>value.GetType().GetField(name,Instance)!.GetValue(value);
    private static T Field<T>(object value,string name)=>(T)Field(value,name)!;
    private static void Set(object value,string name,object data)=>value.GetType().GetField(name,Instance)!.SetValue(value,data);
    private static T Property<T>(object value,string name)=>(T)value.GetType().GetProperty(name,Instance)!.GetValue(value)!;
    private static object? Invoke(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Instance)!.Invoke(value,args);
    private static AnimationBattler[] Battlers(bool doubles,int width=64,int height=64,int yOffset=8)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,yOffset,0,width,height)).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript source,params SourceAnimationCommand[] commands)=>source with{Commands=commands,Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),Labels=new Dictionary<string,int>()};
    private static object NewQuestion(SourceBattleAnimation owner,SourceAnimationScript script)
    {
        var type=typeof(SourceBattleAnimation).GetNestedType("QuestionMark",BindingFlags.NonPublic)!;
        var sprite=Activator.CreateInstance(type,Instance,null,new object[]{4,20,script.SpriteRecipes[Question],owner},CultureInfo.InvariantCulture)!;
        Invoke(Field(owner,"_sprites")!,"Add",sprite);return sprite;
    }
    [TestMethod]
    public void QuestionMarkMatchesExactSourceFramesDynamicAffineLoopAndDelayedDestruction()
    {
        using var vectors=Vectors();var source=AnimationCompositionFixture.Script(133);int count=0;
        int[][] dimensions=[[64,64],[1,1],[63,63],[16,8],[64,128]];
        foreach(var item in vectors.RootElement.GetProperty("question").EnumerateObject())
        {
            int key=int.Parse(item.Name,CultureInfo.InvariantCulture),actor=key/5;var dimensionsForCase=dimensions[key%5];
            var b=Battlers(true,dimensionsForCase[0],dimensionsForCase[1]);var script=Primitive(source,C("end"));var scene=new SourceBattleAnimation.SpriteScene();
            var owner=new SourceBattleAnimation(script,new("question-source",1,133,new(actor),new(actor)),b,true,new Audio(),spriteScene:scene);
            owner.Tick();var sprite=NewQuestion(owner,script);
            foreach(var element in item.Value.EnumerateArray())
            {
                var v=Numbers(element);string context=$"case {key} tick {v[0]}";
                if(v[0]>=0){Invoke(owner,"StepSourceSprite",sprite);if(!Property<bool>(sprite,"Finished"))Invoke(sprite,"AdvanceAnimation",owner);}
                Assert.AreEqual(v[1]==0,Property<bool>(sprite,"Finished"),context);Assert.AreEqual(v[1],scene.Count,context);
                var frame=Property<SourceVisualSpriteFrame>(sprite,"Frame");
                Assert.AreEqual(new PixelPoint(v[2],v[3]),frame.Position,context);Assert.AreEqual(v[4],frame.Tile,context);Assert.AreEqual(v[9]!=0,frame.Affine,context);
                Assert.IsFalse(frame.DoubleSize);Assert.IsTrue(frame.Visible);Assert.AreEqual(32,frame.Width);Assert.AreEqual(32,frame.Height);
                var frames=Field<SourceSpriteFrameProgram>(sprite,"_frames");Assert.AreEqual(v[5]!=0,frames.Ended,context);Assert.AreEqual(v[19],frames.DelayCounter,context);
                int phase=v[6]<2?v[6]:v[7]==0?2:3;Assert.AreEqual(phase,Field<int>(sprite,"_phase"),context);Assert.AreEqual(v[8],(int)Field<short>(sprite,"_delay"),context);
                if(v[9]!=0)
                {
                    var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");Assert.AreEqual(v[10]!=0,affine.Ended,context);
                    CollectionAssert.AreEqual(new[]{v[13],v[14],v[15]},new[]{affine.XScale,affine.YScale,affine.Rotation},context);
                    var matrix=SourceAffineMatrix.FromScaleRotation(65536/v[13],65536/v[14],v[15]);
                    CollectionAssert.AreEqual(new[]{matrix.A,matrix.B,matrix.C,matrix.D},new[]{frame.MatrixA,frame.MatrixB,frame.MatrixC,frame.MatrixD},context);
                }
                else Assert.IsNull(Field(sprite,"_affine"),context);
            }
            scene.Cancel();count++;
        }
        Assert.AreEqual(20,count);
    }
    [TestMethod]
    public void QuestionMarkSignedCountdownBoundaryMatchesSourceCallbacks()
    {
        using var vectors=Vectors();var source=AnimationCompositionFixture.Script(133);var delays=Numbers(vectors.RootElement.GetProperty("probeDelays"));int count=0;
        foreach(var item in vectors.RootElement.GetProperty("probes").EnumerateObject())
        {
            int key=int.Parse(item.Name,CultureInfo.InvariantCulture);var script=Primitive(source,C("end"));var scene=new SourceBattleAnimation.SpriteScene();
            var owner=new SourceBattleAnimation(script,new("question-probe",1,133,new(0),new(0)),Battlers(true),true,new Audio(),spriteScene:scene);owner.Tick();var sprite=NewQuestion(owner,script);
            Set(sprite,"_phase",3);Set(sprite,"_delay",(short)delays[key]);
            foreach(var element in item.Value.EnumerateArray())
            {
                var v=Numbers(element);Invoke(owner,"StepSourceSprite",sprite);Assert.AreEqual(v[8],(int)Field<short>(sprite,"_delay"));Assert.AreEqual(v[1]==0,Property<bool>(sprite,"Finished"));Assert.AreEqual(v[1],scene.Count);
            }
            scene.Cancel();count++;
        }
        Assert.AreEqual(5,count);
    }
    [TestMethod]
    public void MeditateUsesSharedTaskPlayerAndSourceFootAnchoringForEveryActor()
    {
        using var vectors=Vectors();var source=AnimationCompositionFixture.Script(96);
        foreach(var item in vectors.RootElement.GetProperty("meditate").EnumerateObject())for(int actor=0;actor<4;actor++)
        {
            int y=int.Parse(item.Name,CultureInfo.InvariantCulture);var script=Primitive(source,C("createvisualtask","AnimTask_MeditateStretchAttacker","2"),C("waitforvisualfinish"),C("end"));
            var runtime=new SourceBattleAnimation(script,new("meditate-source",1,96,new(actor),new(actor)),Battlers(true,yOffset:y),true,new Audio());var program=new SourceTaskAffineProgram(source.TaskAffinePrograms[Meditate]);
            foreach(var element in item.Value.EnumerateArray())
            {
                var v=Numbers(element);var frame=runtime.Tick();var affine=frame.BattlerAffine[actor];bool running=program.Run();
                CollectionAssert.AreEqual(v.Skip(1).Take(7).ToArray(),new[]{running?1:0,program.CommandIndex,program.FrameTicks,program.LoopRemaining,program.XScale,program.YScale,program.Rotation});
                Assert.AreEqual(v[1]!=0?v[5]:256,affine.XScale);Assert.AreEqual(v[1]!=0?v[6]:256,affine.YScale);Assert.AreEqual(v[8],frame.Offsets[actor].Y);Assert.AreEqual(v[1],runtime.ActiveVisualCount);
                Assert.IsTrue(frame.Offsets.Where((_,i)=>i!=actor).All(p=>p==default));
            }
            runtime.Tick();Assert.IsFalse(runtime.Active);Assert.IsTrue(runtime.Snapshot().BattlerAffine.All(a=>!a.DoubleSize));
        }
    }
    [TestMethod][DataRow(96)][DataRow(133)]
    public void CompleteSelfStatRootsComposeEveryLayoutRestorePsychicBackgroundAndCancelCleanly(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Battlers(doubles);var surface=resources.CreateBackgroundSurface(0);var palette=Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray();
            var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,new ushort[240*112]);foreach(var mon in b)assets[mon.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());
            var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(resolved.Script,new("self-stat",1,move,new(actor),new(actor)),b,doubles,audio,palette,spriteScene:scene,spriteMaterials:assets);
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,b,surface);bool background=false,changed=false,turned=false;int ticks=0;
            while(runtime.Active&&ticks++<512)
            {
                var frame=runtime.Tick();Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length);background|=frame.MoveBackground?.Id is not null;
                if(move==96)changed|=frame.BattlerAffine[actor].XScale!=256;
                else foreach(var sprite in frame.Sprites.Where(s=>s.Asset=="amnesia")){changed=true;turned|=sprite.Affine&&sprite.MatrixB!=0;Assert.IsNotNull(sprite.MaterialPalette);}
            }
            Assert.IsFalse(runtime.Active);Assert.IsTrue(background&&changed);if(move==133)Assert.IsTrue(turned);
            Assert.IsNull(runtime.Snapshot().MoveBackground?.Id);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,scene.Count);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsTrue(runtime.Snapshot().Offsets.All(p=>p==default));
            CollectionAssert.AreEqual(move==96?new[]{"SE_M_HEADBUTT","SE_M_TAKE_DOWN"}:Enumerable.Repeat("SE_M_METRONOME",4).ToArray(),audio.Cues.Select(c=>c.Id).ToArray());Assert.IsTrue(audio.Cues.All(c=>c.Pan==(actor%2==0?-64:63)));
            foreach(int cancelAt in move==96?new[]{1,32,52}:new[]{1,40,100,150})
            {
                var cancelled=new SourceBattleAnimation(resolved.Script,new("cancel-self-stat",1,move,new(actor),new(actor)),b,doubles,new Audio(),palette,spriteScene:scene,spriteMaterials:assets);
                for(int i=0;i<cancelAt&&cancelled.Active;i++)cancelled.Tick();scene.Cancel();Assert.IsFalse(cancelled.Active);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,cancelled.ActiveVisualCount);Assert.IsFalse(cancelled.GraphicsLoaded);Assert.IsTrue(cancelled.Snapshot().Offsets.All(p=>p==default));
            }
        }
    }
    [TestMethod]
    public void MissingTaskProgramMalformedDynamicRecipeAndUnloadedGraphicsFailClosed()
    {
        var meditate=AnimationCompositionFixture.Script(96);var amnesia=AnimationCompositionFixture.Script(133);var b=Battlers(false);
        void Reject(SourceAnimationScript script)=>Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script,new("bad-self-stat",1,script.MoveId,new(0),new(0)),b,false,new Audio()));
        Reject(Primitive(meditate,C("createvisualtask","AnimTask_MeditateStretchAttacker","2"),C("end")) with{TaskAffinePrograms=new Dictionary<string,SourceTaskAffineCommand[]>()});
        var question=Primitive(amnesia,C("createsprite",Question,"ANIM_ATTACKER","20"),C("end"));
        Reject(question with{SpritePalettes=new Dictionary<int,ushort[]>()});Reject(Primitive(amnesia,C("createsprite",Question,"ANIM_ATTACKER","20","0"),C("end")));
        Reject(Primitive(amnesia,C("loadspritegfx","ANIM_TAG_AMNESIA"),C("end")) with{SpriteTags=new Dictionary<string,int>()});
        Reject(Primitive(amnesia,C("playsewithpan","SE_M_METRONOME","SOUND_PAN_ATTACKER"),C("end")) with{SoundEffects=new Dictionary<string,int>()});
        Reject(Primitive(amnesia,C("loopsewithpan","SE_M_METRONOME","SOUND_PAN_ATTACKER","16","3"),C("end")) with{SoundEffects=new Dictionary<string,int>()});
        foreach(var recipe in new[]{amnesia.SpriteRecipes[Question] with{Width=16},amnesia.SpriteRecipes[Question] with{AffineAnimations=[]},amnesia.SpriteRecipes[Question] with{Blend=true}})
        {var recipes=amnesia.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[Question]=recipe;Reject(question with{SpriteRecipes=recipes});}
        var runtime=new SourceBattleAnimation(question,new("unloaded",1,133,new(0),new(0)),b,false,new Audio());Assert.ThrowsExactly<SourceAnimationException>(()=>runtime.Tick());runtime.Cancel();
    }
}
