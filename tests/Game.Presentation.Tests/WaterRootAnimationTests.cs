using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class WaterRootAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound> Cues=[];public readonly List<(int Pan,int Frame)> Pans=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void SetPan(int pan,int frame)=>Pans.Add((pan,frame));public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static AnimationBattler[] Battlers(bool doubles,int visible=15)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,0,0,64,64){Visible=(visible&(1<<i))!=0}).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor,int target)=>new("water-roots",1,move,new(actor),new(target)){VisualSeed=0x12345678};
    [TestMethod]
    public void OriginalCallbackFrameAffineAndDestructionRowsMatchEveryReachableSourceVector()
    {
        using var fixture=SourceVectorFixture.Read("elemental-four-motion-source-vectors.json", "python tests/Reference/ElementalFour/generate_motion.py content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/elemental-four-motion-source-vectors.json");
        using var resources=new SourceAnimationResources(Folder);var type=typeof(SourceBattleAnimation).GetNestedType("WaterRootSprite",BindingFlags.NonPublic)!;
        string[] templates=["gPoisonGasCloudSpriteTemplate","gGuardRingSpriteTemplate","gIngrainRootSpriteTemplate","gIngrainOrbSpriteTemplate","gHydroCannonChargeSpriteTemplate","gHydroCannonBeamSpriteTemplate"];
        int[] oracleSub=[30,20,28,18];int cases=0,rows=0,sourceOnly=0;
        foreach(var source in fixture.RootElement.GetProperty("cases").EnumerateArray())
        {
            int Get(string name)=>source.GetProperty(name).GetInt32();
            // The oracle also records three deliberately shifted BASE coordinates.
            // Product geometry has fixed BASE coordinates; retain those as source
            // arithmetic evidence rather than invent a new runtime coordinate input.
            if(Get("attackerYOffset")!=0){sourceOnly++;continue;}
            int kind=Get("kindId"),actor=Get("attacker"),target=Get("target"),move=Get("root");bool doubles=source.GetProperty("doubles").GetBoolean();
            var battlers=Battlers(doubles,Get("visibleMask"));var full=resources.Resolve(move).Script;
            var script=full with{Commands=[C("delay","1"),C("end")],Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
            var owner=new SourceBattleAnimation(script,Cue(move,actor,target),battlers,doubles,new Audio());
            var bg=(int[])typeof(SourceBattleAnimation).GetField("_battlerBackgroundPriorities",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(owner)!;bg[0]=1;bg[1]=2;
            var args=(short[])typeof(SourceBattleAnimation).GetField("_animationArgs",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(owner)!;
            var initial=source.GetProperty("initialArgs").EnumerateArray().Select(v=>(short)v.GetInt32()).ToArray();Assert.AreEqual(8,initial.Length);Array.Copy(initial,args,8);
            // The native callbacks own eight registers. Later runtime extensions
            // must remain untouched, including nonzero values owned by other tasks.
            for(int i=8;i<args.Length;i++)args[i]=(short)(0x5a00+i);
            var extension=args.Skip(8).ToArray();
            var sprite=(SourceBattleAnimation.VisualSprite)Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[4,28,full.SpriteRecipes[templates[kind]],owner],null)!;
            // Poison saves the target BG priority at initialization. Later writes
            // must not retarget an already existing cloud.
            if(kind==0){bg[0]=0;bg[1]=0;}
            foreach(var row in source.GetProperty("rows").EnumerateArray())
            {
                int frame=row.GetProperty("frame").GetInt32();string why=source.GetProperty("id").GetString()+" frame "+frame;
                if(frame>0){sprite.Step(owner);if(!sprite.Finished)sprite.AdvanceAnimation(owner);}
                var f=sprite.Frame;int[] position=row.GetProperty("position").EnumerateArray().Select(v=>v.GetInt32()).ToArray();
                Assert.AreEqual(row.GetProperty("alive").GetInt32()==0,sprite.Finished,why+" life");
                CollectionAssert.AreEqual(position,new[]{f.Position.X,f.Position.Y,f.Offset.X,f.Offset.Y},why+" position");
                Assert.AreEqual(row.GetProperty("tile").GetInt32(),f.Tile,why+" tile");Assert.AreEqual(row.GetProperty("visible").GetInt32()!=0,f.Visible,why+" visibility");
                // Native base hFlip/vFlip differ from effective non-affine OAM flips.
                // SetSpriteOamFlipBits writes the rendered XOR result into matrixNum.
                bool nonAffine=(row.GetProperty("affineMode").GetInt32()&1)==0;int matrixNum=row.GetProperty("oamMatrixNum").GetInt32();
                Assert.AreEqual(nonAffine&&(matrixNum&8)!=0,f.HFlip,why+" hFlip");Assert.AreEqual(nonAffine&&(matrixNum&16)!=0,f.VFlip,why+" vFlip");
                Assert.AreEqual(row.GetProperty("priority").GetInt32(),f.OamPriority,why+" priority");
                int expectedSub=row.GetProperty("subpriority").GetInt32();
                // The oracle supplies explicit battler priorities. Preserve its
                // measured relative ordering when using the scene's own ranks.
                if(kind==4)expectedSub+=battlers[actor].Subpriority-oracleSub[actor];
                if(kind==0&&(target&1)==0&&(actor&1)==0&&battlers[actor].Base(doubles).X<battlers[target].Base(doubles).X)expectedSub+=battlers[target].Subpriority-oracleSub[target];
                Assert.AreEqual(expectedSub,f.Subpriority,why+" subpriority");
                CollectionAssert.AreEqual(row.GetProperty("args").EnumerateArray().Select(v=>(short)v.GetInt32()).ToArray(),args.Take(8).ToArray(),why+" shared args");
                CollectionAssert.AreEqual(extension,args.Skip(8).ToArray(),why+" untouched extension args");
                var affine=(SourceSpriteAffineProgram?)type.GetField("_affine",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(sprite);
                if(affine is not null){Assert.AreEqual(row.GetProperty("xScale").GetInt32(),affine.XScale,why+" xScale");Assert.AreEqual(row.GetProperty("yScale").GetInt32(),affine.YScale,why+" yScale");Assert.AreEqual(row.GetProperty("rotation").GetInt32(),affine.Rotation,why+" rotation");Assert.AreEqual(row.GetProperty("affineEnded").GetInt32()!=0,affine.Ended,why+" affine end");}
                rows++;
            }
            Assert.IsTrue(sprite.Finished);owner.Cancel();cases++;
        }
        Assert.AreEqual(fixture.RootElement.GetProperty("profile").GetString()=="full"?110:35,cases);Assert.AreEqual(fixture.RootElement.GetProperty("profile").GetString()=="full"?3:1,sourceOnly);Assert.AreEqual(fixture.RootElement.GetProperty("profile").GetString()=="full"?8360:2656,rows);
    }
    [TestMethod][DataRow(139)][DataRow(219)][DataRow(275)][DataRow(308)]
    public void CompleteScriptsReplayBothSidesWithVisiblePixelsAndCleanLifecycle(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            int target=move is 219 or 275?actor:actor^1;var bodies=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);
            assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());assets.TryAdd("impact",new(32,32,new ushort[1024]));foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();
            var runtime=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),bodies,doubles,audio,spriteScene:scene,spriteMaterials:resolved.Assets);
            var replay=new SourceBattleAnimation(resolved.Script,Cue(move,actor,target),bodies,doubles,new Audio(),spriteMaterials:resolved.Assets);
            var objects=new HashSet<SourceBattleAnimation.VisualSprite>();var counts=new Dictionary<string,int>();int ticks=0,peak=0;bool pixels=false;
            while(runtime.Active&&ticks++<600)
            {
                runtime.Tick();replay.Tick();var f=runtime.Snapshot();var other=replay.Snapshot();
                CollectionAssert.AreEqual(f.Offsets,other.Offsets);CollectionAssert.AreEqual(f.AnimationArguments,other.AnimationArguments);Assert.AreEqual(f.Sprites.Length,other.Sprites.Length);
                peak=Math.Max(peak,scene.Count);foreach(var s in scene.Sprites)if(objects.Add(s))counts[s.Frame.Template]=counts.GetValueOrDefault(s.Frame.Template)+1;
                if(f.Sprites.Length>0&&ticks%11==0)pixels|=!composer.ComposeField(f).SequenceEqual(composer.ComposeField(f with{Sprites=[]}));
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.IsTrue(pixels);Assert.IsTrue(audio.Cues.Count>0);Assert.IsTrue(peak>0);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.HasResidualSprites);
            var last=runtime.Snapshot();Assert.IsFalse(last.BlendEnabled);Assert.AreEqual(0,last.BackgroundSlots.Length);Assert.IsTrue(last.Offsets.All(o=>o==default));
            if(move==139)Assert.AreEqual(6,counts["gPoisonGasCloudSpriteTemplate"]);
            if(move==219)Assert.AreEqual(3,counts["gGuardRingSpriteTemplate"]);
            if(move==275){Assert.AreEqual(4,counts["gIngrainRootSpriteTemplate"]);Assert.AreEqual(3,counts["gIngrainOrbSpriteTemplate"]);}
            if(move==308){Assert.IsTrue(audio.Pans.Count>0,"Hydro Cannon must execute its source pan sweep.");Assert.AreEqual(1,counts["gHydroCannonChargeSpriteTemplate"]);Assert.AreEqual(30,counts["gHydroCannonBeamSpriteTemplate"]);Assert.AreEqual(6,counts["gBasicHitSplatSpriteTemplate"]);Assert.AreEqual(0,last.InvertedPalettes.Length);}
        }
    }
    [TestMethod]
    public void CancellationAndChangedRecipesFailClosed()
    {
        using var resources=new SourceAnimationResources(Folder);
        foreach(int move in new[]{139,219,275,308})
        {
            var s=resources.Resolve(move).Script;
            foreach(int tick in new[]{0,5,25,70,135,200})
            {
                var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(s,Cue(move,0,move is 219 or 275?0:1),Battlers(true),true,audio,spriteScene:scene);
                for(int i=0;i<tick&&runtime.Active;i++)runtime.Tick();int sounds=audio.Cues.Count,pans=audio.Pans.Count;runtime.Cancel();runtime.Cancel();Assert.AreEqual(0,scene.Count);Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.IsFalse(runtime.Snapshot().BlendEnabled);Assert.AreEqual(sounds,audio.Cues.Count);Assert.AreEqual(pans,audio.Pans.Count);
            }
            string template=s.Commands.First(c=>c.Op=="createsprite").Args[0];var recipe=s.SpriteRecipes[template];
            foreach(var bad in new[]{recipe with{Width=recipe.Width+8},recipe with{Blend=!recipe.Blend},recipe with{LoopStarts=[]}})
            {
                var changed=s with{SpriteRecipes=s.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Key==template?bad:p.Value)};
                Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(changed,Cue(move,0,move is 219 or 275?0:1),Battlers(true),true,new Audio()));
            }
        }
    }

    [TestMethod]
    public void SourceProfileRetainsExplicitSemanticAndBoundaryCases()
    {
        using var vectors=SourceVectorFixture.Read("elemental-four-motion-source-vectors.json", "python tests/Reference/ElementalFour/generate_motion.py content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/elemental-four-motion-source-vectors.json");
        Assert.IsTrue(vectors.RootElement.GetProperty("profile").GetString() is "representative" or "full");
        var cases=vectors.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        CollectionAssert.AreEquivalent(Enumerable.Range(0,6).ToArray(), cases.Select(c=>c.GetProperty("kindId").GetInt32()).Distinct().ToArray());
        foreach(string variant in new[]{"pic-offset-target-and-mutated-x","selected-ally","hidden-partner","y-greater-than120","coordinate-flag-and-mutated-x"})
            Assert.IsTrue(cases.Any(c=>c.GetProperty("variant").GetString()==variant),variant);
        CollectionAssert.AreEquivalent(new[]{0,1,256},cases.Where(c=>c.GetProperty("variant").GetString()=="coordinate-flag-and-mutated-x").Select(c=>c.GetProperty("initialArgs")[5].GetInt32()).Distinct().ToArray());
    }
}
