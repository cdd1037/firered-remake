using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FangAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static string Template(int move)=>move==162?"gSuperFangSpriteTemplate":"gFangSpriteTemplate";
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor,int target)=>new("fang",1,move,new(actor),new(target));
    private static SourceAnimationScript Isolated(SourceAnimationScript source,string createdAt)=>source with{Commands=[
        new("loadspritegfx",["ANIM_TAG_FANG_ATTACK"],1),new("setarg",["0","123"],2),new("setarg",["7","777"],3),
        new("createsprite",[Template(source.MoveId),createdAt,"2"],4),new("waitforvisualfinish",[],5),new("end",[],6)]};

    [TestMethod][DataRow(162)][DataRow(305)]
    public void SharedPlayersAndDistinctEndHandoffsMatchExtractedCForEveryPosition(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var source=resources.Scripts[move];
        using var vectors=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(TestAssets.Root,"tests/Game.Presentation.Tests/Fixtures/fang-source-vectors.json")));
        foreach(int actor in Enumerable.Range(0,4))foreach(int target in Enumerable.Range(0,4))foreach(string createdAt in new[]{"ANIM_ATTACKER","ANIM_TARGET"})
        {
            var battlers=Battlers(true);var runtime=new SourceBattleAnimation(Isolated(source,createdAt),Cue(move,actor,target),battlers,true,new Audio());
            SourceAnimationFrame? frame=null;for(int i=0;i<10;i++){frame=runtime.Tick();if(frame.Sprites.Length!=0)break;}
            Assert.IsNotNull(frame);Assert.AreEqual(1,frame.Sprites.Length);int tick=0,visible=0;
            foreach(var row in vectors.RootElement.GetProperty("cases").GetProperty(move==162?"super":"fang").EnumerateArray())
            {
                int[] v=row.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[0]<0)continue;
                while(tick<v[0]){frame=runtime.Tick();tick++;}
                string message=$"{move}/{actor}/{target}/{createdAt}/{tick}";
                Assert.AreEqual(123,(int)frame.AnimationArguments[0],message);Assert.AreEqual(777,(int)frame.AnimationArguments[7],message);
                if(v[1]==0){Assert.AreEqual(0,frame.Sprites.Length,message);continue;}
                visible++;var sprite=frame.Sprites.Single();Assert.AreEqual(battlers[target].Center(true,true),sprite.Position,message);Assert.AreEqual(default(PixelPoint),sprite.Offset,message);Assert.AreEqual(v[2],sprite.Tile,message);
                var matrix=SourceAffineMatrix.FromScaleRotation(65536/v[5],65536/v[6],v[7]);CollectionAssert.AreEqual(new[]{matrix.A,matrix.B,matrix.C,matrix.D},new[]{sprite.MatrixA,sprite.MatrixB,sprite.MatrixC,sprite.MatrixD},message);
                Assert.AreEqual(move!=162,sprite.Affine);Assert.AreEqual(move!=162,sprite.DoubleSize);Assert.AreEqual("fang-attack",sprite.Asset);Assert.AreEqual(32,sprite.Width);Assert.AreEqual(32,sprite.Height);Assert.IsTrue(sprite.Visible);Assert.IsFalse(sprite.Blend);
            }
            Assert.AreEqual(move==162?9:32,visible);while(runtime.Active)runtime.Tick();Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);
        }
    }
    [TestMethod][DataRow(158)][DataRow(162)][DataRow(305)]
    public void CompleteRootsComposeSharedSheetAndRestoreSideSelectedImpactAndPalette(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,new ushort[240*112]));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),battlers,doubles,audio);
            int ticks=0,fangFrames=0;bool pixels=false,background=false,bubbles=false,blend=false;
            while(runtime.Active&&ticks++<600)
            {
                var frame=runtime.Tick();var rgba=composer.ComposeField(frame);Assert.AreEqual(240*112*4,rgba.Length);fangFrames+=frame.Sprites.Count(s=>s.Template==Template(move));
                if(frame.Sprites.Any(s=>s.Template==Template(move)))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Template!=Template(move)).ToArray()}));
                if(frame.MoveBackground?.Id is {} id){Assert.AreEqual(158,move);Assert.AreEqual((target&1)==0?5:4,id);background=true;}
                bubbles|=frame.Sprites.Any(s=>s.Asset=="poison-bubble");blend|=frame.PaletteBlends.Length!=0;
            }
            Assert.IsFalse(runtime.Active);Assert.IsTrue(pixels);Assert.AreEqual(move==162?9:32,fangFrames);Assert.AreEqual(move==158,background);Assert.AreEqual(move==305,bubbles);Assert.IsTrue(move==158||blend);
            Assert.AreEqual(1,audio.Cues.Count(s=>s.Id=="SE_M_BITE"));var final=runtime.Snapshot();Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,final.Sprites.Length);Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsFalse(final.BlendEnabled);Assert.IsNull(final.MoveBackground!.Id);Assert.AreEqual(0,final.MoveBackground.Darkening);
        }
    }
    [TestMethod][DataRow(158)][DataRow(162)][DataRow(305)]
    public void CancellationAtActiveAndCompletedStagesReleasesOwnedState(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var source=resources.Scripts[move];var baseline=new SourceBattleAnimation(source,Cue(move,3,1),Battlers(true),true,new Audio());int duration=0;
        while(baseline.Active&&duration<600){baseline.Tick();duration++;}Assert.IsFalse(baseline.Active);
        foreach(int requested in new[]{1,5,20,35,60,duration-1,duration,duration+5}.Distinct())
        {
            var runtime=new SourceBattleAnimation(source,Cue(move,3,1),Battlers(true),true,new Audio());int reached=0;while(runtime.Active&&reached<requested){runtime.Tick();reached++;}Assert.AreEqual(Math.Min(duration,requested),reached);Assert.AreEqual(requested<duration,runtime.Active);
            runtime.Cancel();runtime.Cancel();var frame=runtime.Snapshot();Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.AreEqual(0,frame.PaletteBlends.Length);Assert.IsFalse(frame.BlendEnabled);Assert.IsTrue(frame.Offsets.All(p=>p==default));Assert.IsNull(frame.MoveBackground!.Id);Assert.AreEqual(0,frame.MoveBackground.Darkening);
        }
    }
    [TestMethod][DataRow(162)][DataRow(305)]
    public void UnknownDependenciesIgnoredOperandWritesAndIncorrectRecipesFailClosed(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var source=resources.Scripts[move];string template=Template(move);var script=Isolated(source,"ANIM_TARGET");
        void Reject(SourceAnimationScript bad)=>Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(bad,Cue(move,0,1),Battlers(false),false,new Audio()));
        foreach(var args in new[]{new[]{template,"ANIM_TARGET","2","0"},new[]{template,"ANIM_TARGET","128"},new[]{template,"2","2"}})Reject(script with{Commands=[new("createsprite",args,1),new("end",[],2)]});
        foreach(var bad in new[]{source.SpriteRecipes[template] with{Width=16},source.SpriteRecipes[template] with{Blend=true},source.SpriteRecipes[template] with{AffineDoubleSize=move==162},source.SpriteRecipes[template] with{Callback="UnknownFang"},source.SpriteRecipes[template] with{Animations=[]}})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[template]=bad;Reject(script with{SpriteRecipes=recipes});}
        var missing=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);missing.Remove(template);Reject(script with{SpriteRecipes=missing});
        Reject(script with{Commands=[new("loadspritegfx",["ANIM_TAG_UNKNOWN_FANG"],1),new("end",[],2)]});Reject(script with{Commands=[new("playsewithpan",["SE_UNKNOWN_FANG","63"],1),new("end",[],2)]});
        var unloaded=new SourceBattleAnimation(script with{Commands=[new("createsprite",[template,"ANIM_TARGET","2"],1),new("end",[],2)]},Cue(move,0,1),Battlers(false),false,new Audio());Assert.Throws<SourceAnimationException>(()=>unloaded.Tick());
    }
}
