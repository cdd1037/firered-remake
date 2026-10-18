using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class OrbDefenseAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],new[]{64,40,24,56}[i],new[]{64,48,32,56}[i])).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor,int turn)=>new("orb-defense",1,move,new(actor),new(actor)){MoveTurn=move==256?checked((byte)turn):null};
    private static SourceAnimationScript Minimal(SourceAnimationScript source,params SourceAnimationCommand[] commands)=>source with{Commands=commands,Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static void Clean(SourceBattleAnimation runtime)
    {
        Assert.IsFalse(runtime.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);var f=runtime.Snapshot();Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.IsTrue(f.Offsets.All(p=>p==default));Assert.IsFalse(f.BlendEnabled);Assert.IsTrue(f.BattlerAffine.All(a=>!a.DoubleSize&&a.XScale==256&&a.YScale==256));Assert.AreEqual(0,f.PaletteBlends.Length);Assert.AreEqual(0,f.InvertedPalettes.Length);Assert.AreEqual(0,f.GreyscaleSlots.Length);
    }
    [TestMethod,DataRow(254,0),DataRow(256,1),DataRow(256,2),DataRow(256,3)]
    public void CompleteSourceBranchesRenderAndReplayAcrossAllSixLayouts(int move,int turn)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);string template=move==254?"gStockpileAbsorptionOrbSpriteTemplate":"gSwallowBlueOrbSpriteTemplate";
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x801f,240*112).ToArray()));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();var cue=Cue(move,actor,turn);var scene=new SourceBattleAnimation.SpriteScene();
            var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio,spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio(),spriteMaterials:assets);
            int ticks=0,created=0;bool pixels=false,deformed=false;HashSet<int> previous=[];
            while(runtime.Active&&ticks++<700)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(replay.Tick()));var current=frame.Sprites.Where(s=>s.Template==template).Select(s=>s.Slot).ToHashSet();created+=current.Except(previous).Count();previous=current;
                foreach(var sprite in frame.Sprites.Where(s=>s.Asset is not null))Assert.IsNotNull(sprite.Material);
                deformed|=frame.BattlerAffine[actor].XScale!=256||frame.BattlerAffine[actor].YScale!=256;
                if(!pixels&&frame.Sprites.Any(s=>s.Template==template))
                {var before=JsonSerializer.Serialize(runtime.Snapshot());pixels=!composer.ComposeField(frame).SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Template!=template).ToArray()}));Assert.AreEqual(before,JsonSerializer.Serialize(runtime.Snapshot()));}
            }
            Clean(runtime);Clean(replay);Assert.AreEqual(0,scene.Count);Assert.IsTrue(pixels);Assert.IsTrue(deformed);Assert.AreEqual(move==254?16:5*turn,created);Assert.IsTrue(audio.Cues.Count>0);
            if(move==256)Assert.AreEqual(1,audio.Cues.Count(c=>c.Id=="SE_M_ABSORB_2"),"Healing helper executes once after all swallowed-orb branches.");
        }
    }
    [TestMethod,DataRow(254,0),DataRow(256,1),DataRow(256,2),DataRow(256,3)]
    public void EveryCancellationBoundaryReleasesScenePaletteAndAffineState(int move,int turn)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);var battlers=Battlers(true);var cue=Cue(move,0,turn);var length=new SourceBattleAnimation(resolved.Script,cue,battlers,true,new Audio());int ticks=0;while(length.Active&&ticks++<700)length.Tick();Clean(length);
        for(int stop=0;stop<=ticks;stop++)
        {var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,true,new Audio(),spriteScene:scene);for(int t=0;t<stop&&runtime.Active;t++)runtime.Tick();runtime.Cancel();runtime.Cancel();Clean(runtime);Assert.AreEqual(0,scene.Count);Assert.IsTrue(runtime.Snapshot().SpritePaletteSlots.All(p=>p.All(c=>c==0)));}
    }
    [TestMethod,DataRow(254),DataRow(256)]
    public void AlteredOrbRecipesAndTaskProgramsFailBeforePlayback(int move)
    {
        var source=AnimationCompositionFixture.Script(move);string template=move==254?"gStockpileAbsorptionOrbSpriteTemplate":"gSwallowBlueOrbSpriteTemplate";var recipe=source.SpriteRecipes[template];var cue=Cue(move,0,1);
        foreach(var bad in new[]{recipe with{Width=16},recipe with{Callback="UnknownOrb"},recipe with{Blend=true},recipe with{Animations=[[new(0,2,false,false)]]},recipe with{AffineDoubleSize=!recipe.AffineDoubleSize}})
        {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[template]=bad;Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{SpriteRecipes=recipes},cue,Battlers(false),false,new Audio()));}
        string name=move==254?"sStockpileDeformMonAffineAnimCmds":"sSwallowDeformMonAffineAnimCmds";var programs=source.TaskAffinePrograms.ToDictionary(p=>p.Key,p=>p.Value);var altered=programs[name].ToArray();altered[0]=altered[0] with{Duration=19};programs[name]=altered;
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source with{TaskAffinePrograms=programs},cue,Battlers(false),false,new Audio()));
    }
    [TestMethod]
    public void AbsorptionZeroDurationAndSwallowMissingTurnAreRejected()
    {
        var source=AnimationCompositionFixture.Script(254);var malformed=new SourceAnimationCommand("createsprite",["gStockpileAbsorptionOrbSpriteTemplate","ANIM_ATTACKER","2","55","55","0"],1);
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(Minimal(source,malformed,new("end",[],2)),Cue(254,0,0),Battlers(false),false,new Audio()));
        var swallow=AnimationCompositionFixture.Script(256);Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(swallow,Cue(256,0,1) with{MoveTurn=null},Battlers(false),false,new Audio()));
    }
}
