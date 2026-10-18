using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class ChargePairAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound> Cues=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}public void SetPan(int pan,int frame){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],new[]{64,40,24,56}[i],new[]{64,48,32,56}[i])).ToArray();
    private static CommittedMoveAnimation Cue(int move,int turn,int actor=0,int target=1)=>new("charge-pair",1,move,new(actor),new(target)){MoveTurn=(byte)turn,VisualSeed=0x12345678};
    private static SourceAnimationCommand C(string op,params string[] a)=>new(op,a,1);
    private static SourceAnimationScript Minimal(SourceAnimationScript s,params SourceAnimationCommand[] commands)=>s with{Commands=commands,Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static Dictionary<string,SourcePixelAsset> Materials(SourceResolvedAnimation r,AnimationBattler[] battlers)
    {var a=r.Assets.ToDictionary(p=>p.Key,p=>p.Value);a["background"]=new(240,112,Enumerable.Repeat((ushort)0x801f,240*112).ToArray());foreach(var b in battlers)a.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));return a;}
    private static void Clean(SourceBattleAnimation r)
    {
        Assert.IsFalse(r.Active);Assert.AreEqual(0,r.ActiveVisualCount);var f=r.Snapshot();Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.BackgroundSlots.Length);Assert.IsTrue(f.Offsets.All(p=>p==default));Assert.IsFalse(f.BlendEnabled);Assert.AreEqual(0,f.PaletteBlends.Length);Assert.AreEqual(0,f.SpritePalettes.Count);Assert.IsTrue(f.BattlerAffine.All(a=>!a.DoubleSize&&a.XScale==256&&a.YScale==256&&a.MatrixB==0&&a.MatrixC==0));
        Assert.IsTrue(((PixelPoint[])typeof(SourceBattleAnimation).GetField("_battlerPositionOffsets",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(r)!).All(p=>p==default));
    }
    [TestMethod][DataRow(13,0)][DataRow(13,1)][DataRow(130,0)][DataRow(130,1)]
    public void BothAuthoredBranchesResolveComposeAndReplayAcrossEverySourcePosition(int move,int turn)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);CollectionAssert.AreEquivalent(move==13?new[]{"impact","gust","air-wave-2"}:new[]{"impact"},resolved.Assets.Keys.ToArray());
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in turn==0?(doubles?new[]{actor,actor^1,actor^2}:new[]{actor,actor^1}):(doubles?new[]{actor^1,actor^3}:new[]{actor^1}))
        {
            var battlers=Battlers(doubles);var assets=Materials(resolved,battlers);var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();var cue=Cue(move,turn,actor,target);var scene=new SourceBattleAnimation.SpriteScene();
            var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,audio,spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio(),spriteMaterials:assets);var neutral=composer.ComposeField(runtime.Snapshot());int ticks=0;bool changed=false;var templates=new HashSet<string>();
            while(runtime.Active&&ticks++<800)
            {
                var f=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));foreach(var s in f.Sprites){templates.Add(s.Template);if(s.Asset is not null)Assert.IsNotNull(s.Material);}
                var before=JsonSerializer.Serialize(runtime.Snapshot());changed|=!composer.ComposeField(f).SequenceEqual(neutral);Assert.AreEqual(before,JsonSerializer.Serialize(runtime.Snapshot()));
            }
            Clean(runtime);Clean(replay);Assert.AreEqual(0,scene.Count);Assert.IsTrue(changed);Assert.IsTrue(audio.Cues.Count>0);
            if(move==13){Assert.AreEqual(turn==0,templates.Contains("gRazorWindTornadoSpriteTemplate"));Assert.AreEqual(turn==1,templates.Contains("gAirWaveCrescentSpriteTemplate"));}
            else{Assert.AreEqual(turn==0,templates.Contains("gSlideMonToOffsetAndBackSpriteTemplate"));Assert.AreEqual(turn==1,audio.Cues.Any(s=>s.Id=="SE_BANG"&&!s.ApplyPanning));}
            runtime.Cancel();replay.Cancel();Clean(runtime);Clean(replay);
        }
    }
    [TestMethod][DataRow(13,0)][DataRow(13,1)][DataRow(130,0)][DataRow(130,1)]
    public void CancellationAtEveryTickRestoresAllFourParticipantsAndSceneOwnership(int move,int turn)
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(move);var b=Battlers(true);var assets=Materials(r,b);
        for(int actor=0;actor<4;actor++)
        {
            var cue=Cue(move,turn,actor,actor^1);var probe=new SourceBattleAnimation(r.Script,cue,b,true,new Audio(),spriteMaterials:assets);int length=0;while(probe.Active&&length++<800)probe.Tick();Clean(probe);probe.Cancel();
            for(int stop=0;stop<=length;stop++)
            {
                var scene=new SourceBattleAnimation.SpriteScene();var runtime=new SourceBattleAnimation(r.Script,cue,b,true,new Audio(),spriteScene:scene,spriteMaterials:assets);for(int n=0;n<stop&&runtime.Active;n++)runtime.Tick();runtime.Cancel();runtime.Cancel();Clean(runtime);Assert.AreEqual(0,scene.Count);Assert.IsTrue(runtime.Snapshot().BattlerVisibility.All(v=>v.Present&&!v.LogicalInvisible&&!v.SpriteInvisible));
            }
        }
    }
    [TestMethod]
    public void MissingTurnRecipeAndMalformedNativeOperandsFailClosed()
    {
        using var resources=new SourceAnimationResources(Folder);var b=Battlers(false);var razor=resources.Resolve(13).Script;var skull=resources.Resolve(130).Script;
        foreach(var s in new[]{razor,skull})Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(s,new("no-turn",1,s.MoveId,new(0),new(1)),b,false,new Audio()));
        var cases=new[]{(razor,C("createsprite","gRazorWindTornadoSpriteTemplate","ANIM_ATTACKER","2","32","0","16","16","256","7","40")),(razor,C("createsprite","gRazorWindTornadoSpriteTemplate","ANIM_ATTACKER","2","32","0","16","16","0","256","40")),(skull,C("createvisualtask","AnimTask_SkullBashPosition","2","2")),(skull,C("createsprite","gSlideMonToOffsetAndBackSpriteTemplate","ANIM_ATTACKER","2","ANIM_ATTACKER","-24","0","0","0","0"))};
        foreach(var (s,c) in cases)Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(Minimal(s,c,C("end")),Cue(s.MoveId,0),b,false,new Audio()));
        var recipes=razor.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes["gRazorWindTornadoSpriteTemplate"]=recipes["gRazorWindTornadoSpriteTemplate"] with{Width=64};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(razor with{SpriteRecipes=recipes},Cue(13,0),b,false,new Audio()));
        b[0]=b[0] with{SourceVisibility=new(false,false,false)};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(skull,Cue(130,1),b,false,new Audio()));
    }
}
