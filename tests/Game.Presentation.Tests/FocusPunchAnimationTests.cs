using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FocusPunchAnimationTests
{
    private const string Template="gFocusPunchFistSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int actor,int target)=>new("focus-punch",1,264,new(actor),new(target));
    private static SourceAnimationScript Isolated(SourceAnimationScript source,string priorityOwner="ANIM_TARGET")=>source with{Labels=new Dictionary<string,int>(),Commands=[
        new("loadspritegfx",["ANIM_TAG_HANDS_AND_FEET"],1),new("setarg",["0","123"],2),new("setarg",["7","777"],3),
        new("createsprite",[Template,priorityOwner,"2"],4),new("waitforvisualfinish",[],5),new("end",[],6)]};

    [TestMethod]
    public void OriginalCallbackAndSharedAffineEndOrderMatchAtAllTargetCoordinates()
    {
        var source=AnimationCompositionFixture.Script(264);
        using var vectors=SourceVectorFixture.Read("focus-punch-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_focus_punch_vectors.py --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/focus-punch-source-vectors.json");
        foreach(int actor in Enumerable.Range(0,4))foreach(int target in Enumerable.Range(0,4))foreach(string priorityOwner in new[]{"ANIM_ATTACKER","ANIM_TARGET"})
        {
            var battlers=Battlers(true);var runtime=new SourceBattleAnimation(Isolated(source,priorityOwner),Cue(actor,target),battlers,true,new Audio());
            SourceAnimationFrame? frame=null;for(int i=0;i<10;i++){frame=runtime.Tick();if(frame.Sprites.Length!=0)break;}
            Assert.IsNotNull(frame);Assert.AreEqual(1,frame.Sprites.Length);int tick=0,visible=0;
            foreach(var row in vectors.RootElement.GetProperty("rows").EnumerateArray())
            {
                int[] v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[0]<0)continue;
                while(tick<v[0]){frame=runtime.Tick();tick++;}string message=$"{actor}/{target}/{priorityOwner}/{tick}";
                Assert.AreEqual(123,(int)frame.AnimationArguments[0],message);Assert.AreEqual(777,(int)frame.AnimationArguments[7],message);
                if(v[1]==0){Assert.AreEqual(0,frame.Sprites.Length,message);continue;}
                visible++;var sprite=frame.Sprites.Single();Assert.AreEqual(battlers[target].Center(true,true),sprite.Position,message);
                Assert.AreEqual(new PixelPoint(v[4],v[5]),sprite.Offset,message);Assert.AreEqual(v[6],sprite.Tile,message);
                CollectionAssert.AreEqual(new[]{v[15],0,0,v[16]},new[]{sprite.MatrixA,sprite.MatrixB,sprite.MatrixC,sprite.MatrixD},message);
                Assert.IsTrue(sprite.Affine&&sprite.DoubleSize&&sprite.Visible);Assert.IsFalse(sprite.Blend||sprite.HFlip||sprite.VFlip);
                Assert.AreEqual(32,sprite.Width);Assert.AreEqual(32,sprite.Height);Assert.AreEqual("hands-and-feet",sprite.Asset);
            }
            Assert.AreEqual(49,visible);for(int i=0;runtime.Active&&i<8;i++)runtime.Tick();Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);
        }
    }

    [TestMethod][DataRow(false,0)][DataRow(false,1)][DataRow(true,0)][DataRow(true,1)][DataRow(true,2)][DataRow(true,3)]
    public void CompleteRootComposesInAllSixLayoutsAndRestoresBackground(bool doubles,int actor)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(264);
        CollectionAssert.AreEquivalent(new[]{"impact","hands-and-feet"},resolved.Assets.Keys.ToArray());CollectionAssert.AreEquivalent(new[]{4,5},resolved.Backgrounds.Keys.ToArray());
        foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));
            foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(actor,target),battlers,doubles,audio);
            int ticks=0,fistFrames=0;bool fistPixels=false,impact=false,background=false,shake=false;
            while(runtime.Active&&ticks++<400)
            {
                var frame=runtime.Tick();var rgba=composer.ComposeField(frame);Assert.AreEqual(240*112*4,rgba.Length);
                if(frame.Sprites.Any(s=>s.Template==Template))
                {fistFrames++;fistPixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Template!=Template).ToArray()}));}
                impact|=frame.Sprites.Any(s=>s.Asset=="impact");shake|=frame.Offsets[target].X!=0;
                if(frame.MoveBackground?.Id is {} id){Assert.AreEqual((target&1)==0?5:4,id);background=true;}
            }
            Assert.IsFalse(runtime.Active);Assert.AreEqual(49,fistFrames);Assert.IsTrue(fistPixels&&impact&&background&&shake);
            CollectionAssert.AreEqual(new[]{"SE_M_SWAGGER","SE_M_VITAL_THROW2","SE_M_VITAL_THROW2","SE_M_MEGA_KICK2"},audio.Cues.Select(c=>c.Id).ToArray());
            var final=runtime.Snapshot();Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,final.Sprites.Length);Assert.AreEqual(0,final.BackgroundSlots.Length);
            Assert.IsFalse(final.BlendEnabled);Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.IsNull(final.MoveBackground!.Id);Assert.AreEqual(0,final.MoveBackground.Darkening);
        }
    }

    [TestMethod]
    public void CancelAtShrinkShakeAndFadeStagesRestoresCapturedState()
    {
        var source=AnimationCompositionFixture.Script(264);var baseline=new SourceBattleAnimation(source,Cue(3,1),Battlers(true),true,new Audio());int duration=0;
        while(baseline.Active&&duration<400){baseline.Tick();duration++;}Assert.IsFalse(baseline.Active);
        foreach(int requested in new[]{1,5,20,35,50,70,duration-1,duration,duration+5}.Distinct())
        {
            var runtime=new SourceBattleAnimation(source,Cue(3,1),Battlers(true),true,new Audio());int reached=0;while(runtime.Active&&reached<requested){runtime.Tick();reached++;}
            Assert.AreEqual(Math.Min(duration,requested),reached);runtime.Cancel();runtime.Cancel();var frame=runtime.Snapshot();
            Assert.IsFalse(runtime.Active||runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.AreEqual(0,frame.PaletteBlends.Length);
            Assert.IsFalse(frame.BlendEnabled);Assert.IsTrue(frame.Offsets.All(p=>p==default));Assert.IsNull(frame.MoveBackground!.Id);Assert.AreEqual(0,frame.MoveBackground.Darkening);
        }
    }

    [TestMethod]
    public void MissingMaterialBadAbiAndMutatedAffineRecipesFailClosed()
    {
        var source=AnimationCompositionFixture.Script(264);var script=Isolated(source);var recipe=source.SpriteRecipes[Template];
        void Reject(SourceAnimationScript bad)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(bad,Cue(0,1),Battlers(false),false,new Audio()));
        foreach(var args in new[]{new[]{Template,"ANIM_TARGET","2","0"},new[]{Template,"ANIM_TARGET","128"},new[]{Template,"2","2"}})
            Reject(script with{Commands=[new("createsprite",args,1),new("end",[],2)]});
        foreach(var bad in new[]{recipe with{Width=16},recipe with{Blend=true},recipe with{AffineDoubleSize=false},recipe with{Callback="UnknownFocusPunch"},recipe with{Animations=[]},recipe with{AffineAnimations=[]},recipe with{AffineAnimations=[[new("frame",512,512,0,0,0),new("frame",-16,-16,0,8,0),new("end",0,0,0,0,0)]]}})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[Template]=bad;Reject(script with{SpriteRecipes=recipes});}
        var missing=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);missing.Remove(Template);Reject(script with{SpriteRecipes=missing});Reject(script with{SineTable=[]});
        var unloaded=new SourceBattleAnimation(script with{Commands=[new("createsprite",[Template,"ANIM_TARGET","2"],1),new("end",[],2)]},Cue(0,1),Battlers(false),false,new Audio());
        Assert.Throws<SourceAnimationException>(()=>unloaded.Tick());
    }
}
