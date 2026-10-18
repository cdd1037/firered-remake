using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ChargedRootAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio{public List<SourceAnimationSound>Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],64,new[]{64,48,32,56}[i])).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor,int target,int turn)=>new("charged-roots",1,move,new(actor),new(target)){MoveTurn=(byte)turn,MovePower=move==255?(ushort)100:null};
    private static SourceAnimationScript Primitive(SourceAnimationScript s,params SourceAnimationCommand[] commands)=>s with{Commands=commands,Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static void Clean(SourceBattleAnimation o)
    {Assert.IsFalse(o.Active);Assert.AreEqual(0,o.ActiveVisualCount);var f=o.Snapshot();Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);var background=f.MoveBackground;Assert.IsNotNull(background);Assert.IsNull(background.Id);Assert.IsFalse(f.BlendEnabled);Assert.IsTrue(f.Offsets.All(x=>x==default));Assert.IsTrue(f.BattlerAffine.All(x=>!x.DoubleSize&&x.XScale==256&&x.YScale==256));Assert.IsTrue(f.BattlerVisibility.All(v=>!v.SpriteInvisible));}
    [TestMethod,DataRow(143,0),DataRow(143,1),DataRow(255,1),DataRow(255,2),DataRow(255,3)]
    public void CompleteBranchesRenderAndRestoreAcrossSinglesAndAllDoublesTargets(int move,int turn)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2,actor^3}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x801f,240*112).ToArray()));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var cue=Cue(move,actor,target,turn);var o=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio,spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio(),spriteMaterials:assets);
            bool pixels=false,background=false,deformed=false;int ticks=0,created=0;HashSet<int> prior=[];string template=move==143?"gSkyAttackBirdSpriteTemplate":"gSpitUpOrbSpriteTemplate";
            while(o.Active&&ticks++<700)
            {
                var f=o.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));var current=f.Sprites.Where(s=>s.Template==template).Select(s=>s.Slot).ToHashSet();created+=current.Except(prior).Count();prior=current;background|=f.MoveBackground?.Id is not null;deformed|=f.BattlerAffine[actor].XScale!=256||f.BattlerAffine[actor].YScale!=256;
                foreach(var s in f.Sprites.Where(s=>s.Asset is not null))Assert.IsNotNull(s.Material);
                if(!pixels&&current.Count>0)pixels=!composer.ComposeField(f).SequenceEqual(composer.ComposeField(f with{Sprites=f.Sprites.Where(s=>s.Template!=template).ToArray()}));
            }
            Clean(o);Clean(replay);Assert.AreEqual(0,scene.Count);Assert.IsTrue(audio.Cues.Count>0);
            if(move==255){Assert.IsTrue(pixels);Assert.IsTrue(deformed);Assert.AreEqual(turn==1?8:turn==2?12:16,created);Assert.AreEqual(1,audio.Cues.Count(s=>s.Id=="SE_M_SPIT_UP"));}
            else if(turn==1){Assert.IsTrue(pixels);Assert.IsTrue(background);Assert.AreEqual(1,created);Assert.AreEqual(1,audio.Cues.Count(s=>s.Id=="SE_M_SKY_UPPERCUT"));}
            else{Assert.AreEqual(0,created);Assert.AreEqual(target==(actor^2)?1:8,audio.Cues.Count(s=>s.Id=="SE_M_STAT_INCREASE"));}
        }
    }
    [TestMethod,DataRow(143,0),DataRow(143,1),DataRow(255,1),DataRow(255,2),DataRow(255,3)]
    public void EveryCancellationFrameReleasesSharedSceneAndSourceState(int move,int turn)
    {
        var script=AnimationCompositionFixture.Script(move);var battlers=Battlers(true);var cue=Cue(move,0,2,turn);var full=new SourceBattleAnimation(script,cue,battlers,true,new Audio());int frames=0;while(full.Active&&frames++<700)full.Tick();Clean(full);
        for(int stop=0;stop<=frames;stop++){var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(script,cue,battlers,true,new Audio(),spriteScene:scene);for(int t=0;t<stop&&o.Active;t++)o.Tick();o.Cancel();o.Cancel();Clean(o);Assert.AreEqual(0,scene.Count);}
    }
    [TestMethod]
    public void StrongOrbBranchRetainsPreviousDurationRatherThanZeroingOmittedArgument()
    {
        var s=AnimationCompositionFixture.Script(255);var script=Primitive(s,new("loadspritegfx",["ANIM_TAG_RED_ORB_2"],1),new("createsprite",["gSpitUpOrbSpriteTemplate","ANIM_ATTACKER","2","0","12"],2),new("createsprite",["gSpitUpOrbSpriteTemplate","ANIM_ATTACKER","2","64"],3),new("waitforvisualfinish",[],4),new("end",[],5));
        var o=new SourceBattleAnimation(script,Cue(255,0,1,2),Battlers(false),false,new Audio());int overlap=0;while(o.Active){var f=o.Tick();if(f.Sprites.Length==2){overlap++;Assert.AreEqual((short)12,f.AnimationArguments[1]);}}Assert.IsTrue(overlap>=11);Clean(o);
    }
    [TestMethod]
    public void MissingInputsMalformedRecipesAndDegenerateBirdMotionFailClosed()
    {
        foreach(int move in new[]{143,255})
        {
            var s=AnimationCompositionFixture.Script(move);string template=move==143?"gSkyAttackBirdSpriteTemplate":"gSpitUpOrbSpriteTemplate";var r=s.SpriteRecipes[template];
            foreach(var bad in new[]{r with{Width=16},r with{Blend=true},r with{AffineDoubleSize=false},r with{Callback="WrongCallback"},r with{Animations=[[new(0,2,false,false)]]}})
            {var recipes=s.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[template]=bad;Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(s with{SpriteRecipes=recipes},Cue(move,0,1,1),Battlers(false),false,new Audio()));}
            Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(s,Cue(move,0,1,1) with{MoveTurn=null},Battlers(false),false,new Audio()));
        }
        var spit=AnimationCompositionFixture.Script(255);Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(spit,Cue(255,0,1,1) with{MovePower=null},Battlers(false),false,new Audio()));
        var programs=spit.TaskAffinePrograms.ToDictionary(p=>p.Key,p=>p.Value);programs["sSpitUpDeformMonAffineAnimCmds"]=[new("frame",0,6,0,19),new("end")];Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(spit with{TaskAffinePrograms=programs},Cue(255,0,1,1),Battlers(false),false,new Audio()));
        var bird=AnimationCompositionFixture.Script(143);Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(bird,Cue(143,0,0,1),Battlers(false),false,new Audio()));
    }
}
