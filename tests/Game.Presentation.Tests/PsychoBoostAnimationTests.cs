using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class PsychoBoostAnimationTests
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private const string Template="gPsychoBoostOrbSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound> Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects()=>Stops++;}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static ushort[] TerrainPalette(SourceAnimationResources resources){var surface=resources.CreateBackgroundSurface(0);return Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray();}
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("psycho-boost-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_psycho_boost_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/psycho-boost-source-vectors.json");
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static void Set(object o,string name,object value)=>o.GetType().GetField(name,Private)!.SetValue(o,value);
    private static object Nested(string name,params object[] args)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(name,BindingFlags.NonPublic)!,BindingFlags.Public|Private,null,args,null)!;
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,12,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int actor,int target)=>new("psycho-boost",1,354,new(actor),new(target));
    private static SourceAnimationScript Primitive(SourceAnimationScript source)=>source with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static void Step(SourceBattleAnimation.VisualSprite sprite,SourceBattleAnimation owner)
    {sprite.Step(owner);if(!sprite.Finished)sprite.AdvanceAnimation(owner);}

    [TestMethod]
    public void OrbMatchesExtractedNativeCallbackLoopsAffineChangeAlphaMotionAndSound()
    {
        using var vectors=Vectors();var script=AnimationCompositionFixture.Script(354);int cases=0,rows=0;
        foreach(var group in vectors.RootElement.GetProperty("orb").EnumerateArray().Select(N).GroupBy(r=>(Doubles:r[0]!=0,Actor:r[1])))
        {
            var key=group.Key;var audio=new Audio();var owner=new SourceBattleAnimation(Primitive(script),Cue(key.Actor,key.Actor^1),Battlers(key.Doubles),key.Doubles,audio);
            var sprite=(SourceBattleAnimation.VisualSprite)Nested("PsychoBoostSprite",4,2,script.SpriteRecipes[Template],owner);
            foreach(var r in group)
            {
                Set(owner,"_frame",r[2]);if(r[2]>=0)Step(sprite,owner);string why=$"{key} tick {r[2]}";var f=sprite.Frame;var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");var frame=owner.Snapshot();
                Assert.AreEqual(r[3]==0,sprite.Finished,why);Assert.AreEqual(new PixelPoint(r[4],r[5]),f.Position,why);Assert.AreEqual(new PixelPoint(r[6],r[7]),f.Offset,why);Assert.AreEqual(r[8]==0,f.Visible,why);
                Assert.AreEqual((short)r[9],Field<short>(sprite,"_phase"),why);Assert.AreEqual((short)r[10],Field<short>(sprite,"_blend"),why);Assert.AreEqual((short)r[11],Field<short>(sprite,"_timer"),why);Assert.AreEqual((short)r[12],Field<short>(sprite,"_verticalFraction"),why);
                Assert.AreEqual(r[13],affine.XScale,why);Assert.AreEqual(r[14],affine.YScale,why);Assert.AreEqual(r[15],affine.Rotation,why);Assert.AreEqual(r[16]!=0,affine.Ended,why);Assert.AreEqual(r[17]!=0,frame.BlendEnabled,why);Assert.AreEqual(r[18]&31,frame.AlphaA,why);Assert.AreEqual((r[18]>>8)&31,frame.AlphaB,why);
                Assert.AreEqual(r[19],audio.Cues.Count,why);if(r[19]>0){Assert.AreEqual("SE_M_TELEPORT",audio.Cues.Single().Id,why);Assert.AreEqual(r[20],audio.Cues.Single().Pan,why);Assert.AreEqual(198,audio.Cues.Single().Frame,why);}
                Assert.AreEqual(r[21],f.Tile,why);Assert.IsTrue(f.Affine&&f.DoubleSize&&f.Blend,why);Assert.IsNull(Field<object?>(owner,"_visualRandom"),why);rows++;
            }
            Assert.IsTrue(sprite.Finished);owner.Cancel();cases++;
        }
        Assert.AreEqual(6,cases);Assert.AreEqual(1350,rows);
    }

    [TestMethod]
    public void AffineChangeRetainsNativeSignedScalesAndRotationAcrossRelativeFirstFrames()
    {
        using var vectors=Vectors();var recipe=AnimationCompositionFixture.Script(354).SpriteRecipes[Template];int[][] states=[[304,304,0],[256,256,256],[512,-512,65077],[-300,400,32768]];int rows=0,cases=0;
        foreach(var group in vectors.RootElement.GetProperty("change").EnumerateArray().Select(N).GroupBy(r=>r[0]))
        {
            var state=states[group.Key];var program=new SourceSpriteAffineProgram(recipe.AffineAnimations[1],state[0],state[1],state[2]);
            foreach(var r in group){if(r[1]>0)program.Step();Assert.AreEqual(r[2],program.XScale);Assert.AreEqual(r[3],program.YScale);Assert.AreEqual(r[4],program.Rotation);Assert.AreEqual(r[5]!=0,program.Ended);rows++;}
            Assert.IsTrue(program.Ended);cases++;
        }
        Assert.AreEqual(4,cases);Assert.AreEqual(64,rows);
    }

    [TestMethod]
    public void BothPsychicPaletteModesMatchNativeRotationStopOrderAndUnfadedWrites()
    {
        using var vectors=Vectors();var script=AnimationCompositionFixture.Script(354);int cases=0,rows=0;
        foreach(var group in vectors.RootElement.GetProperty("palette").EnumerateArray().Select(N).GroupBy(r=>(Kind:r[0],Stop:r[1])))
        {
            var owner=new SourceBattleAnimation(Primitive(script),Cue(0,1),Battlers(false),false,new Audio());Set(owner,"_fadedBackgroundPalette",Enumerable.Range(132,16).Select(x=>(ushort)x).ToArray());Set(owner,"_unfadedBackgroundPalette",Enumerable.Range(1032,16).Select(x=>(ushort)x).ToArray());
            object task=Nested("PsychicBackgroundTask",group.Key.Kind!=0);Assert.IsFalse((bool)task.GetType().GetProperty("Counted")!.GetValue(task)!);
            foreach(var r in group)
            {
                if(r[2]==9)Set(owner,"_fadedBackgroundPalette",Field<ushort[]>(owner,"_unfadedBackgroundPalette").Select(x=>(ushort)(x+500)).ToArray());if(r[2]==group.Key.Stop)Field<short[]>(owner,"_animationArgs")[7]=-1;
                task.GetType().GetMethod("Step")!.Invoke(task,[owner]);string why=$"{group.Key} tick {r[2]}";
                Assert.AreEqual(r[3]==0,(bool)task.GetType().GetProperty("Finished")!.GetValue(task)!,why);Assert.AreEqual(r[4],Field<int>(task,"_counter"),why);Assert.AreEqual(0,r[5],why);
                CollectionAssert.AreEqual(r.Skip(6).Take(16).Select(x=>(ushort)x).ToArray(),Field<ushort[]>(owner,"_fadedBackgroundPalette"),why);CollectionAssert.AreEqual(r.Skip(22).Take(16).Select(x=>(ushort)x).ToArray(),Field<ushort[]>(owner,"_unfadedBackgroundPalette"),why);rows++;
            }
            owner.Cancel();cases++;
        }
        Assert.AreEqual(12,cases);Assert.AreEqual(98,rows);
    }

    [TestMethod]
    public void CompleteRootUsesExistingCircleMaterialComposesAllSidesAndRestoresState()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(354);Assert.AreEqual("Move_PSYCHO_BOOST",resolved.Script.Label);var circle=resolved.Assets["circle-of-light"];Assert.AreEqual(64,circle.Width);Assert.AreEqual(64,circle.Height);Assert.AreEqual(4096,circle.PaletteIndices.Length);Assert.AreEqual(16,circle.Palette.Length);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var bodies=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(resolved.Script,Cue(actor,target),bodies,doubles,audio,TerrainPalette(resources),spriteScene:scene,spriteMaterials:resolved.Assets);int ticks=0,peak=0;bool pixels=false,psychic=false,thin=false,shake=false;
            while(runtime.Active&&ticks++<600)
            {
                var frame=runtime.Tick();var rgba=composer.ComposeField(frame);Assert.AreEqual(240*112*4,rgba.Length);peak=Math.Max(peak,frame.Sprites.Length);psychic|=frame.MoveBackground?.Id==resolved.Script.BackgroundSymbols["BG_PSYCHIC"];shake|=frame.Offsets[actor].X!=0;
                if(frame.Sprites.Any(s=>s.Template==Template&&s.Visible)){pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));thin|=frame.Sprites.Any(s=>s.MatrixA==16384&&s.MatrixD==98);}
            }
            Assert.IsFalse(runtime.Active);Assert.IsTrue(pixels&&psychic&&thin&&shake);Assert.AreEqual(1,peak);Assert.AreEqual(1,audio.Cues.Count(s=>s.Id=="SE_M_TELEPORT"));Assert.AreEqual(actor%2==0?-64:63,audio.Cues.Single(s=>s.Id=="SE_M_TELEPORT").Pan);
            var final=runtime.Snapshot();Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(final.BlendEnabled);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.IsNull(final.MoveBackground!.Id);Assert.AreEqual(0,final.MoveBackground.Darkening);Assert.IsNull(Field<object?>(runtime,"_visualRandom"));
        }
    }

    [TestMethod]
    public void CancellationAcrossFadeGrowthPulsesAndRiseReleasesSceneAndAudioOnce()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(354);
        foreach(int tick in new[]{0,1,20,40,80,150,240,275,310,400})
        {
            var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(3,0),Battlers(true),true,audio,TerrainPalette(resources),spriteScene:scene,spriteMaterials:resolved.Assets);for(int i=0;i<tick&&runtime.Active;i++)runtime.Tick();int sounds=audio.Cues.Count;runtime.Cancel();runtime.Cancel();var frame=runtime.Snapshot();
            Assert.IsFalse(runtime.Active);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(frame.BlendEnabled);Assert.IsTrue(frame.Offsets.All(p=>p==default));Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.IsNull(frame.MoveBackground!.Id);Assert.AreEqual(0,frame.MoveBackground.FadedPalette.Length);Assert.AreEqual(sounds,audio.Cues.Count);Assert.AreEqual(1,audio.Stops);
        }
    }

    [TestMethod]
    public void MissingMaterialsSoundsMalformedAffineProgramsAndOperandsFailBeforeEffects()
    {
        var full=AnimationCompositionFixture.Script(354);var recipe=full.SpriteRecipes[Template];
        void Reject(SourceAnimationScript script){var audio=new Audio();Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(0,1),Battlers(false),false,audio,new ushort[16]));Assert.AreEqual(0,audio.Cues.Count);}
        foreach(var bad in new[]{recipe with{Width=32},recipe with{Blend=false},recipe with{AffineDoubleSize=false},recipe with{Callback="SpriteCallbackDummy"},recipe with{AffineAnimations=[]},recipe with{LoopStarts=[]},recipe with{Animations=[[new(0,2,false,false)]]},recipe with{AffineAnimations=[recipe.AffineAnimations[0],[new("frame",-20,24,0,14,0),new("end",0,0,0,0,0)]]}})
        {var recipes=full.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[Template]=bad;Reject(full with{SpriteRecipes=recipes});}
        Reject(full with{SpritePalettes=new Dictionary<int,ushort[]>()});Reject(full with{SoundEffects=full.SoundEffects.Where(p=>p.Key!="SE_M_TELEPORT").ToDictionary(p=>p.Key,p=>p.Value)});
        Reject(Primitive(full) with{Commands=[C("createsprite",Template,"ANIM_ATTACKER","2","0"),C("end")]});
        Reject(Primitive(full) with{Commands=Enumerable.Repeat(C("createsprite",Template,"ANIM_ATTACKER","2"),29).Append(C("end")).ToArray()});
    }

    [TestMethod]
    public void RetainedAffineConstructorRejectsForeignWidthsAndAbsoluteFramesStillReplaceState()
    {
        SourceSpriteAffineCommand[] relative=[new("frame",1,1,0,1,0),new("end",0,0,0,0,0)];
        foreach(var values in new[]{new[]{32768,256,0},new[]{256,-32769,0},new[]{256,256,-1},new[]{256,256,65536}})Assert.ThrowsExactly<InvalidDataException>(()=>new SourceSpriteAffineProgram(relative,values[0],values[1],values[2]));
        var zero=new SourceSpriteAffineProgram([new("frame",0,0,0,0,0),new("end",0,0,0,0,0)]);Assert.AreEqual(new SourceAffineMatrix(0,0,0,0),zero.Matrix);
        var zeroX=new SourceSpriteAffineProgram([new("frame",0,256,0,0,0),new("end",0,0,0,0,0)]);Assert.AreEqual(new SourceAffineMatrix(0,0,0,256),zeroX.Matrix);
        var zeroY=new SourceSpriteAffineProgram([new("frame",256,0,0,0,0),new("end",0,0,0,0,0)]);Assert.AreEqual(new SourceAffineMatrix(256,0,0,0),zeroY.Matrix);
        var source=AnimationCompositionFixture.Script(354).SpriteRecipes[Template].AffineAnimations[0];var ordinary=new SourceSpriteAffineProgram(source);var retained=new SourceSpriteAffineProgram(source,-100,400,32768);Assert.AreEqual(ordinary.XScale,retained.XScale);Assert.AreEqual(ordinary.YScale,retained.YScale);Assert.AreEqual(ordinary.Rotation,retained.Rotation);
    }
}
