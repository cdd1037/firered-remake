using System.Globalization;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class NeedleSpriteAnimationTests
{
    private const string Stinger="gLinearStingerSpriteTemplate",InvertHit="gHandleInvertHitSplatSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static SourceAnimationScript Script(int move)=>AnimationCompositionFixture.Script(move);
    private static SourceAnimationScript Projectile(SourceAnimationScript source,int[] args)=>source with{Commands=[
        new("loadspritegfx",["ANIM_TAG_NEEDLE"],1),new("createsprite",new[]{Stinger,"ANIM_TARGET","2"}.Concat(args.Select(v=>v.ToString(CultureInfo.InvariantCulture))).ToArray(),2),
        new("waitforvisualfinish",[],3),new("end",[],4)]};
    private static CommittedMoveAnimation Cue(int move,int actor,int target)=>new("needle",1,move,new(actor),new(target)){MoveTurn=move==41?(byte)1:move==131?(byte)4:null};

    [TestMethod]
    public void IntegerBiosAnglesMatchAllSignedQuadrantsZeroNarrowingAndNegativeRoundingVectors()
    {
        var rows=File.ReadAllLines(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/bios-angle-vectors.csv"));Assert.AreEqual(61,rows.Length);
        foreach(string row in rows.Skip(1))
        {
            var v=row.Split(',');int rawX=int.Parse(v[0],CultureInfo.InvariantCulture),rawY=int.Parse(v[1],CultureInfo.InvariantCulture);
            short x=unchecked((short)rawX),y=unchecked((short)rawY);
            Assert.AreEqual(int.Parse(v[2],CultureInfo.InvariantCulture),(int)x);Assert.AreEqual(int.Parse(v[3],CultureInfo.InvariantCulture),(int)y);
            int expected=int.Parse(v[4][2..],NumberStyles.HexNumber,CultureInfo.InvariantCulture),negative=int.Parse(v[5][2..],NumberStyles.HexNumber,CultureInfo.InvariantCulture);
            Assert.AreEqual(expected,(int)SourceBiosAngles.ArcTan2(x,y),row);Assert.AreEqual(negative,(int)SourceBiosAngles.ArcTan2Neg(x,y),row);
            int angle=unchecked((ushort)(SourceBiosAngles.ArcTan2Neg(x,y)+0xc000));Assert.AreEqual(int.Parse(v[6][2..],NumberStyles.HexNumber,CultureInfo.InvariantCulture),angle);
            Assert.AreEqual(int.Parse(v[7],CultureInfo.InvariantCulture),angle>>8);
        }
    }
    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void StingerMatchesExtractedCallbacksIncludingAllyMirroringAndU16NumeratorWrap(int actor)
    {
        using var doc=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/needle-sprite-original-vectors.json")));
        var configs=doc.RootElement.GetProperty("configs").EnumerateArray().ToArray();Assert.AreEqual(13,configs.Length);
        foreach(int target in new[]{actor^1,actor^2})for(int config=0;config<configs.Length;config++)
        {
            int[] args=configs[config].EnumerateArray().Select(v=>v.GetInt32()).ToArray();
            var runtime=new SourceBattleAnimation(Projectile(Script(40),args),Cue(40,actor,target),Battlers(true),true,new Audio());
            for(int i=0;i<3;i++)Assert.AreEqual(0,runtime.Tick().Sprites.Length);
            string key=$"{actor}:{target}:{config}";
            foreach(var row in doc.RootElement.GetProperty("cases").GetProperty(key).EnumerateArray())
            {
                var v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[0]<0)continue;
                var frame=runtime.Tick();string message=key+" tick "+v[0];
                CollectionAssert.AreEqual(v[5..9],frame.AnimationArguments.Take(4).Select(x=>(int)x).ToArray(),message);
                if(v[14]==0){Assert.AreEqual(0,frame.Sprites.Length,message);continue;}
                var sprite=frame.Sprites.Single();Assert.AreEqual(new PixelPoint(v[1],v[2]),sprite.Position,message);Assert.AreEqual(new PixelPoint(v[3],v[4]),sprite.Offset,message);
                CollectionAssert.AreEqual(v[10..14],new[]{sprite.MatrixA,sprite.MatrixB,sprite.MatrixC,sprite.MatrixD},message);
                Assert.AreEqual(Stinger,sprite.Template);Assert.AreEqual("needle",sprite.Asset);Assert.IsTrue(sprite.Affine);Assert.IsFalse(sprite.DoubleSize);Assert.IsFalse(sprite.Blend);
                Assert.AreEqual(16,sprite.Width);Assert.AreEqual(16,sprite.Height);Assert.AreEqual(0,sprite.Tile);
            }
            while(runtime.Active)runtime.Tick();Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);
        }
    }
    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)][DataRow(3)]
    public void InvertedHitWrapperMutatesSharedArgumentAndUsesExistingHitLifecycle(int actor)
    {
        var source=Script(41);
        foreach(int target in new[]{actor^1,actor^2})foreach(int affine in new[]{2,3})
        {
            var script=source with{Commands=[new("loadspritegfx",["ANIM_TAG_IMPACT"],1),new("setarg",["7","777"],2),
                new("createsprite",[InvertHit,"ANIM_ATTACKER","3","10","12","1",affine.ToString(CultureInfo.InvariantCulture)],3),new("waitforvisualfinish",[],4),new("end",[],5)]};
            var runtime=new SourceBattleAnimation(script,Cue(41,actor,target),Battlers(true),true,new Audio());int visible=0;
            while(runtime.Active)
            {
                var frame=runtime.Tick();if(frame.Sprites.Length==0)continue;visible++;
                var sprite=frame.Sprites.Single();var center=Battlers(true)[target].Center(true,true);int direction=Battlers(true)[actor].Base(true).X>center.X?-1:1;
                Assert.AreEqual(new PixelPoint(center.X+direction*10,center.Y+(actor%2==0?12:-12)),sprite.Position);
                Assert.AreEqual(InvertHit,sprite.Template);Assert.AreEqual("impact",sprite.Asset);Assert.AreEqual(affine==2?176:128,sprite.Scale);
                Assert.AreEqual(actor%2==0?12:-12,(int)frame.AnimationArguments[1]);Assert.AreEqual(777,(int)frame.AnimationArguments[7]);
            }
            Assert.AreEqual(10,visible,"Existing affine hit chain includes frame completion, callback handoff and next-frame destruction.");
        }
    }
    [TestMethod][DataRow(40,1)][DataRow(41,2)][DataRow(131,3)]
    public void CompleteRootsPreserveAuthoredParticleMultiplicityAndComposeRealMaterials(int move,int needles)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x5294,240*112).ToArray()));
            foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),battlers,doubles,audio);
            var composer=new SourceBattleFrameComposer(assets,battlers,resolved.StatMask,resolved.Metallic);int ticks=0,needleFrames=0;bool pixels=false,impact=false,bubble=false;
            while(runtime.Active&&ticks++<500)
            {
                var frame=runtime.Tick();var rgba=composer.ComposeField(frame);Assert.AreEqual(240*112*4,rgba.Length);
                needleFrames+=frame.Sprites.Count(s=>s.Template==Stinger);
                if(frame.Sprites.Any(s=>s.Template==Stinger))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Template!=Stinger).ToArray()}));
                impact|=frame.Sprites.Any(s=>s.Asset=="impact");bubble|=frame.Sprites.Any(s=>s.Asset=="poison-bubble");
            }
            Assert.IsFalse(runtime.Active);Assert.AreEqual(21*needles,needleFrames);Assert.IsTrue(pixels);Assert.IsTrue(impact);Assert.AreEqual(move==40,bubble);
            Assert.AreEqual(needles,audio.Cues.Count(c=>c.Id=="SE_M_RAZOR_WIND2"));Assert.AreEqual(move==40?1:needles,audio.Cues.Count(c=>c.Id=="SE_M_HORN_ATTACK"));
            Assert.AreEqual(move==40?6:0,audio.Cues.Count(c=>c.Id=="SE_M_TOXIC"));
            var final=runtime.Snapshot();Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,final.Sprites.Length);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsFalse(final.BlendEnabled);Assert.IsTrue(final.Offsets.All(p=>p==default));
        }
    }
    [TestMethod][DataRow(40)][DataRow(41)][DataRow(131)]
    public void CancellationAtActiveOrCompletedStagesClearsAllOwnedState(int move)
    {
        var source=Script(move);var baseline=new SourceBattleAnimation(source,Cue(move,3,1),Battlers(true),true,new Audio());int duration=0;
        while(baseline.Active&&duration<500){baseline.Tick();duration++;}Assert.IsFalse(baseline.Active);
        foreach(int requested in new[]{1,5,15,25,45,75,duration-1,duration,duration+5}.Distinct())
        {
            var runtime=new SourceBattleAnimation(source,Cue(move,3,1),Battlers(true),true,new Audio());int reached=0;
            while(runtime.Active&&reached<requested){runtime.Tick();reached++;}Assert.AreEqual(Math.Min(duration,requested),reached);Assert.AreEqual(requested<duration,runtime.Active);
            runtime.Cancel();runtime.Cancel();var frame=runtime.Snapshot();Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.IsFalse(frame.BlendEnabled);Assert.IsTrue(frame.Offsets.All(p=>p==default));
        }
    }
    [TestMethod]
    public void InvalidDurationShapeCoordinatesAndUnknownDependenciesFailClosed()
    {
        var source=Script(40);
        void Reject(SourceAnimationScript script)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(40,0,1),Battlers(false),false,new Audio()));
        foreach(int duration in new[]{0,-1,256,32768})Reject(Projectile(source,[20,0,-8,0,duration]));
        Reject(Projectile(source,[32768,0,0,0,20]));Reject(Projectile(source,[128,0,0,0,20]));Reject(Projectile(source,[0,-129,0,0,20]));
        foreach(var bad in new[]{source.SpriteRecipes[Stinger] with{Width=32},source.SpriteRecipes[Stinger] with{Blend=true},source.SpriteRecipes[Stinger] with{AffineDoubleSize=true},source.SpriteRecipes[Stinger] with{Callback="UnknownStinger"},source.SpriteRecipes[Stinger] with{Animations=[[new(4,1,false,false)]]},source.SpriteRecipes[Stinger] with{LoopStarts=[-1]}})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[Stinger]=bad;Reject(Projectile(source,[20,0,-8,0,20]) with{SpriteRecipes=recipes});}
        Assert.IsTrue(source.SpriteTags.ContainsKey("ANIM_TAG_NEEDLE"));Assert.IsTrue(source.SoundEffects.ContainsKey("SE_M_RAZOR_WIND2"));
        Reject(source with{Commands=[new("loadspritegfx",["ANIM_TAG_UNREGISTERED_NEEDLE"],1),new("end",[],2)],SpriteTags=new Dictionary<string,int>()});
        Reject(source with{Commands=[new("playsewithpan",["SE_UNREGISTERED_NEEDLE","SOUND_PAN_ATTACKER"],1),new("end",[],2)],SoundEffects=new Dictionary<string,int>()});
        var missing=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);missing.Remove(Stinger);
        Reject(Projectile(source,[20,0,-8,0,20]) with{SpriteRecipes=missing});
    }
}
