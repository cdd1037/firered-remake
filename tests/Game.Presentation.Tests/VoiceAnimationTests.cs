using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class VoiceAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {
        public bool SupportsCries=>true;public bool IsCryPlaying{get;set;}public bool IsPlaying=>false;
        public List<SourceAnimationCry> Cries{get;}=[];
        public void PlayCry(SourceAnimationCry cue)=>Cries.Add(cue);
        public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}public void SetPan(int pan,int frame){}
    }
    private sealed class Silent:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,(i&1)==0?7:9,0,64,64)).ToArray();
    [TestMethod]
    public void NoiseLinesMatchExtractedSourcePositionSignedAccumulationAndArgumentMutation()
    {
        var script=AnimationCompositionFixture.Script(336);using var document=SourceVectorFixture.Read("voice-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_voice_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/voice-vectors.json");
        foreach(var group in document.RootElement.GetProperty("noise").EnumerateArray().Select(e=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).GroupBy(r=>(r[0],r[1],r[2])))
        {
            var(mode,actor,kind)=group.Key;var b=Battlers(mode!=0);var o=new SourceBattleAnimation(script with{Commands=[new("end",[],1)]},new("noise",1,336,new(actor),new(actor)),b,mode!=0,new Audio());
            var type=typeof(SourceBattleAnimation).GetNestedType("VoiceSprite",BindingFlags.NonPublic)!;
            var sprite=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public,null,[4,30,script.SpriteRecipes["gRoarNoiseLineSpriteTemplate"],new[]{24,kind==0?-8:kind==1?8:0,kind},o],null)!;
            foreach(var r in group)
            {
                if(r[3]>=0)type.GetMethod("Step")!.Invoke(sprite,[o]);
                var f=(SourceVisualSpriteFrame)type.GetProperty("Frame")!.GetValue(sprite)!;
                Assert.AreEqual(new PixelPoint(r[4],r[5]),f.Position);Assert.AreEqual(new PixelPoint(r[6],r[7]),f.Offset);Assert.AreEqual(r[8]!=0,f.HFlip);Assert.AreEqual(r[9]!=0,f.VFlip);Assert.AreEqual(r[12]!=0,(bool)type.GetProperty("Finished")!.GetValue(sprite)!);
                var args=(short[])typeof(SourceBattleAnimation).GetField("_animationArgs",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o)!;if((actor&1)!=0)Assert.AreEqual((short)r[11],args[0]);
            }
        }
    }
    [TestMethod]
    public void TerrainShakeMatchesSourceInitializerRegistersAndRestoration()
    {
        int[][] args=[[1,0,6,1],[0,2,3,0],[-3,-2,4,2],[32767,-32768,1,0]];var script=AnimationCompositionFixture.Script(304) with{Commands=[new("end",[],1)]};using var json=SourceVectorFixture.Read("voice-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_voice_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/voice-vectors.json");
        foreach(var group in json.RootElement.GetProperty("terrain").EnumerateArray().Select(e=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).GroupBy(r=>r[0]))
        {
            var o=new SourceBattleAnimation(script,new("terrain",1,304,new(0),new(1)),Battlers(false),false,new Audio());var type=typeof(SourceBattleAnimation).GetNestedType("VoiceTerrainShake",BindingFlags.NonPublic)!;
            var task=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public,null,[o,args[group.Key]],null)!;
            foreach(var r in group){if(r[1]>=0)type.GetMethod("Step")!.Invoke(task,[o]);Assert.AreEqual(r[2],o.Snapshot().MoveBackground!.ScrollX);Assert.AreEqual(r[3],o.Snapshot().MoveBackground!.ScrollY);Assert.AreEqual(r[6]!=0,(bool)type.GetProperty("Finished")!.GetValue(task)!);}
        }
    }
    [TestMethod]
    public void HyperVoiceMatchesSourceImmediateTranslationPartnerVisibilityAndBytePriority()
    {
        var script=AnimationCompositionFixture.Script(304);using var json=SourceVectorFixture.Read("hyper-voice-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_hyper_voice_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/hyper-voice-vectors.json");
        foreach(var group in json.RootElement.EnumerateArray().Select(e=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).GroupBy(r=>(r[0],r[1],r[2],r[3],r[4])))
        {
            var(mode,actor,target,mask,pic)=group.Key;var b=Battlers(mode!=0).Select(x=>x with{Visible=(mask&(1<<x.Slot.Value))!=0}).ToArray();
            var o=new SourceBattleAnimation(script with{Commands=[new("end",[],1)]},new("hyper",1,304,new(actor),new(target)),b,mode!=0,new Audio());var type=typeof(SourceBattleAnimation).GetNestedType("VoiceSprite",BindingFlags.NonPublic)!;
            var sprite=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public,null,[4,30,script.SpriteRecipes["gHyperVoiceRingSpriteTemplate"],new[]{45,0,0,0,0,0,pic},o],null)!;
            foreach(var r in group)
            {
                if(r[5]>=0)type.GetMethod("Step")!.Invoke(sprite,[o]);var f=(SourceVisualSpriteFrame)type.GetProperty("Frame")!.GetValue(sprite)!;
                Assert.AreEqual(new PixelPoint(r[6],r[7]),f.Position);Assert.AreEqual(new PixelPoint(r[8],r[9]),f.Offset);Assert.AreEqual(r[10],f.Subpriority);Assert.AreEqual(r[11]!=0,(bool)type.GetProperty("Finished")!.GetValue(sprite)!);
            }
        }
    }
    [TestMethod]
    public void DeepInhaleMatchesOriginalTaskAffineJitterAndEverySourceFootOffset()
    {
        var script=AnimationCompositionFixture.Script(336);using var json=SourceVectorFixture.Read("inhale-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_inhale_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/inhale-vectors.json");
        foreach(var group in json.RootElement.EnumerateArray().Select(e=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).GroupBy(r=>r[0]))
        {
            var b=Battlers(false);b[0]=b[0] with{YOffset=group.Key};var o=new SourceBattleAnimation(script with{Commands=[new("end",[],1)]},new("inhale",1,336,new(0),new(0)),b,false,new Audio());
            var type=typeof(SourceBattleAnimation).GetNestedType("DeepInhaleTask",BindingFlags.NonPublic)!;var task=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[o,new AnimationSlot(0),script.TaskAffinePrograms["sDeepInhaleAffineAnimCmds"]],null)!;
            foreach(var r in group)
            {
                type.GetMethod("Step")!.Invoke(task,[o]);
                var offsets=(PixelPoint[])typeof(SourceBattleAnimation).GetField("_offsets",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o)!;
                var xs=(int[])typeof(SourceBattleAnimation).GetField("_xScale",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o)!;var ys=(int[])typeof(SourceBattleAnimation).GetField("_yScale",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o)!;
                Assert.AreEqual(r[5],xs[0]);Assert.AreEqual(r[6],ys[0]);Assert.AreEqual(new PixelPoint(r[8],r[9]),offsets[0]);Assert.AreEqual(r[10]==0,(bool)type.GetProperty("Finished")!.GetValue(task)!);
            }
        }
    }
    [TestMethod]
    public void HowlUsesExplicitOriginalSpeciesAndCompletesWithSourceAffineAndMaterials()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(336);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,new ushort[240*112]);foreach(var mon in b)assets[mon.Asset]=new(64,64,Enumerable.Repeat((ushort)0xffff,4096).ToArray());
            var audio=new Audio{IsCryPlaying=true};var o=new SourceBattleAnimation(resolved.Script,new("howl",1,336,new(actor),new(actor)),b,doubles,audio,spriteMaterials:resolved.Assets,originalPartySpecies:b.ToDictionary(x=>x.Slot.Value,_=>132));var composer=AnimationCompositionFixture.Create(resources,resolved,assets,b);int ticks=0;
            while(o.Active&&ticks++<512)Assert.AreEqual(240*112*4,composer.ComposeField(o.Tick()).Length);
            Assert.IsFalse(o.Active);Assert.AreEqual(1,audio.Cries.Count);Assert.AreEqual(132,audio.Cries[0].Species);Assert.AreEqual(3,audio.Cries[0].Mode);Assert.AreEqual(0,o.ActiveVisualCount);
        }
    }
    [TestMethod][DataRow(45)][DataRow(304)]
    public void CryRootsComposeWithControlledLivenessAndFullResolvedDependencies(int move)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,new ushort[240*112]);foreach(var mon in b)assets[mon.Asset]=new(64,64,Enumerable.Repeat((ushort)0xffff,4096).ToArray());
            var audio=new Audio();var o=new SourceBattleAnimation(resolved.Script,new("voice",1,move,new(actor),new(actor^1)),b,doubles,audio,spriteMaterials:resolved.Assets,originalPartySpecies:b.ToDictionary(x=>x.Slot.Value,_=>132));var composer=AnimationCompositionFixture.Create(resources,resolved,assets,b);int ticks=0;
            while(o.Active&&ticks++<1024)Assert.AreEqual(240*112*4,composer.ComposeField(o.Tick()).Length);
            Assert.IsFalse(o.Active);Assert.AreEqual(move==45?2:4,audio.Cries.Count);Assert.IsTrue(audio.Cries.All(c=>c.Species==132));Assert.AreEqual(0,o.ActiveVisualCount);
        }
    }
    [TestMethod]
    public void SharedCryPhaseAndWaitTasksMatchSourceMinimumVisitsAndGlobalLiveness()
    {
        var script=AnimationCompositionFixture.Script(336) with{Commands=[new("end",[],1)]};using var json=SourceVectorFixture.Read("cry-task-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_cry_task_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/cry-task-vectors.json");
        foreach(var group in json.RootElement.EnumerateArray().Select(e=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).GroupBy(r=>(r[0],r[1])))
        {
            var(kind,release)=group.Key;var audio=new Audio();var o=new SourceBattleAnimation(script,new("cry-task",1,336,new(0),new(0)),Battlers(false),false,audio);var type=typeof(SourceBattleAnimation).GetNestedType("VoiceCryTask",BindingFlags.NonPublic)!;
            var task=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public,null,[132,-64,kind==0?10:6,kind==2],null)!;
            foreach(var r in group){audio.IsCryPlaying=r[2]<release;audio.Cries.Clear();type.GetMethod("Step")!.Invoke(task,[o]);Assert.AreEqual(r[4]!=0,(bool)type.GetProperty("Finished")!.GetValue(task)!);Assert.AreEqual(r[5]==0?0:1,audio.Cries.Count);if(r[5]!=0){Assert.AreEqual(r[5],audio.Cries[0].Mode);Assert.AreEqual(r[6],audio.Cries[0].Species);Assert.AreEqual(r[7],audio.Cries[0].Pan);}}
        }
    }
    [TestMethod]
    public void CryIdentityInputIsDetachedAndUnusedHelpersStillValidateOperandWidths()
    {
        var script=AnimationCompositionFixture.Script(336);var b=Battlers(false);var audio=new Audio();var species=new Dictionary<int,int>{{0,132},{1,1}};
        var o=new SourceBattleAnimation(script,new("identity",1,336,new(0),new(0)),b,false,audio,originalPartySpecies:species);species[0]=1;species.Clear();
        for(int tick=0;o.Active&&tick<512;tick++)o.Tick();Assert.AreEqual(132,audio.Cries.Single().Species);
        var invalid=script with{Commands=[new("end",[],1)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"UnusedCry",[new("createvisualtask",["SoundTask_PlayCryHighPitch","256","ANIM_ATTACKER","3"],2),new("return",[],3)]}}};
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(invalid,new("unused",1,336,new(0),new(0)),b,false,new Silent()));
    }
    [TestMethod]
    public void InvokedCryRequiresBackendAndOriginalIdentityWithoutWeakeningUnusedValidation()
    {
        var script=AnimationCompositionFixture.Script(336);var b=Battlers(false);var cue=new CommittedMoveAnimation("missing",1,336,new(0),new(0));
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,cue,b,false,new Silent(),originalPartySpecies:new Dictionary<int,int>{{0,132}}));
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,cue,b,false,new Audio()));
        var unused=script with{Commands=[new("end",[],1)]};var o=new SourceBattleAnimation(unused,cue,b,false,new Silent());Assert.IsFalse(o.Tick().Active);
    }
}
