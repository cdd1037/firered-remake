using Game.Presentation.BattleAnimations;
using System.Text.Json.Nodes;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class SourceAnimationResourceTests
{
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private sealed class Silent:ISourceAnimationAudio
    {
        private int _cryFrames;
        public bool IsPlaying=>false;
        public bool SupportsCries=>true;
        public bool IsCryPlaying=>_cryFrames>0;
        public void PlayCry(SourceAnimationCry cry)=>_cryFrames=3;
        public void Advance(){if(_cryFrames>0)_cryFrames--;}
        public void StopAnimationCries()=>_cryFrames=0;
        public void Play(SourceAnimationSound sound){}public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}
    }
    [TestMethod]
    public void CanonicalProgramsReuseResolvedDependenciesButCustomProgramsRemainValidated()
    {
        using var resources=new SourceAnimationResources(Folder);
        var move=resources.Resolve(160);int assets=resources.CachedAssetCount;
        for(int i=0;i<20;i++)Assert.AreSame(move,resources.Resolve(160));
        Assert.AreEqual(assets,resources.CachedAssetCount);
        var general=resources.ResolveGeneral("General_WishHeal");
        Assert.AreSame(general,resources.ResolveGeneral("General_WishHeal"));
        var custom=move.Script with{SpriteDependencies=["missing-dependency"]};
        Assert.ThrowsExactly<InvalidDataException>(()=>resources.Resolve(custom));
        Assert.AreSame(move,resources.Resolve(160));
        var unbound=move.Script with{ProgramReferences=["missing-program"]};
        Assert.ThrowsExactly<InvalidDataException>(()=>resources.Resolve(unbound));
        resources.Dispose();
        Assert.ThrowsExactly<ObjectDisposedException>(()=>resources.Resolve(160));
        Assert.ThrowsExactly<ObjectDisposedException>(()=>resources.ResolveGeneral("General_WishHeal"));
    }
    [TestMethod]
    public void SourceRegisteredWideTileSheetRetainsAllPixelsThroughSharedResolver()
    {
        using var resources=new SourceAnimationResources(Folder);
        var sheet=resources.Asset("torn-metal");
        Assert.AreEqual(128,sheet.Width);Assert.AreEqual(32,sheet.Height);
        var bytes=File.ReadAllBytes(Path.Combine(Folder,"physical-torn-metal.rgb555"));
        Assert.AreEqual(4+128*32*2,bytes.Length);
        for(int i=0;i<sheet.Pixels.Length;i++)
            Assert.AreEqual(System.Buffers.Binary.BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(4+i*2)),sheet.Pixels[i]);
        Assert.AreSame(sheet,resources.Asset("torn-metal"));
        Assert.IsNotNull(resources.Resolve(232).Metallic);
    }
    [TestMethod]
    public void SharedRecipesCoalesceDefaultsAndMaterialSelectionFollowsTaskNotMoveId()
    {
        using var resources=new SourceAnimationResources(Folder);Assert.AreEqual(0,resources.CachedAssetCount);
        var simple=resources.Resolve(1);Assert.IsNull(simple.Metallic);Assert.AreEqual(1,resources.CachedAssetCount);
        Assert.AreSame(simple.Script.SpriteRecipes,resources.Scripts[92].SpriteRecipes);
        Assert.IsTrue(simple.Script.SpriteRecipes.ContainsKey("gToxicBubbleSpriteTemplate"));
        var crossFamily=simple.Script with{MoveId=999,Commands=[new("createvisualtask",["AnimTask_MetallicShine","5","1","0","0"],1),new("end",[],2)]};
        var resolved=resources.Resolve(crossFamily);Assert.IsNotNull(resolved.Metallic);Assert.IsNull(resolved.StatMask);
        Assert.AreSame(resolved.Metallic,resources.Resolve(crossFamily).Metallic);
        Assert.IsNotNull(resources.Resolve(0).StatMask);
        Assert.AreSame(resources.Asset("explosion"),resources.Asset("explosion"));
        resources.Dispose();Assert.AreEqual(0,resources.CachedAssetCount);Assert.Throws<ObjectDisposedException>(()=>resources.Asset("impact"));
    }
    [TestMethod]
    public void EveryGeneratedMoveResolvesOnlyItsDependenciesAndComposesEveryFrame()
    {
        using var resources=new SourceAnimationResources(Folder);Assert.IsTrue(resources.Scripts.Count>1);
        var battlers=Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
        foreach(var script in resources.Scripts.Values.Where(s=>s.MoveId!=0))
        {
            var resolved=resources.Resolve(script.MoveId);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);
            assets.Add("background",new(240,112,new ushort[240*112]));foreach(var battler in battlers)assets.Add(battler.Asset,new(64,64,Enumerable.Repeat((ushort)0x801f,4096).ToArray()));
            var surface=resources.CreateBackgroundSurface(0);
            // Controlled invocation inputs exercise resource composition; receipt-derived
            // values and both branch thresholds are checked by dedicated family tests.
            var cue=new CommittedMoveAnimation("resources",1,script.MoveId,new(0),new(1)){Friendship=128,AnimationWeather=0,ActorHpQuarterTier=2,MovePower=100,FuryCutterCounter=3,RolloutCounter=3,SourceTerrain=SourceAnimationTerrain.Grass,MoveDamage=24,IsHealingMove=false,MoveTurn=script.Commands.Any(c=>c.Op is "choosetwoturnanim" or "jumpifmoveturn"||c.Op=="createvisualtask"&&c.Args[0]=="GetIsDoomDesireHitTurn")?(byte)0:null};
            cue=cue with{VisualSeed=SourceAnimationVisualSeed.For(cue)};
            var replacements=battlers.ToDictionary(b=>b.Slot.Value,b=>b with{Asset=b.Asset+"-replacement"});
            foreach(var replacement in replacements.Values)assets.Add(replacement.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var scene=new SourceBattleAnimation.SpriteScene();
            // Composition uses controlled finite cry liveness. Source-mode PCM
            // duration and task ordering have their own dedicated audio vectors.
            var audio=new Silent();
            var runtime=new SourceBattleAnimation(resolved.Script,cue,battlers,true,audio,Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray(),battlerReplacements:replacements,spriteScene:scene,spriteMaterials:assets,originalPartySpecies:Enumerable.Range(0,4).ToDictionary(i=>i,_=>4),additionalMonMaterial:new(battlers[0] with{Asset="additional-mon"},assets[battlers[1].Asset]));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers,surface);int ticks=0;
            while(runtime.Active&&ticks++<2048)
            {
                audio.Advance();var frame=runtime.Tick();
                foreach(var sprite in frame.Sprites.Where(s=>s.Visible&&s.Asset is not null))Assert.IsTrue(assets.ContainsKey(sprite.Asset!)||sprite.Material is {Width:64,Height:64},$"Move {script.MoveId} needs {sprite.Asset} through {sprite.Template}.");
                Assert.AreEqual(240*112*4,composer.ComposeField(frame).Length,$"Move {script.MoveId}");
            }
            Assert.IsFalse(runtime.Active,$"Move {script.MoveId} did not finish.");
            while(runtime.HasResidualSprites&&ticks++<4096)composer.ComposeField(scene.TickIdle(battlers,true));
            Assert.IsFalse(runtime.HasResidualSprites);
        }
    }
    [TestMethod]
    public void IndexedTerrainIdentityAndMoveMaterialFollowGeneratedDependencies()
    {
        using var resources=new SourceAnimationResources(Folder);
        var surface=resources.CreateBackgroundSurface(0);var pixels=new ushort[240*112];surface.Render(pixels,240,112);
        int terrain=resources.IdentifyTerrain(new(240,112,pixels));var matched=new ushort[pixels.Length];resources.CreateBackgroundSurface(terrain).Render(matched,240,112);CollectionAssert.AreEqual(pixels,matched);
        Assert.AreEqual(0,resources.Resolve(1).Backgrounds.Count);
        var dark=resources.Resolve(168);Assert.AreEqual(1,dark.Backgrounds.Count);Assert.AreSame(dark.Backgrounds[1],resources.Resolve(242).Backgrounds[1]);
        var assets=dark.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,pixels));
        var composer=new SourceBattleFrameComposer(assets,[],backgroundSurface:resources.CreateBackgroundSurface(terrain),moveBackgrounds:dark.Backgrounds);
        SourceAnimationFrame Frame(int revision,int? id,int darkness)=>new(revision,true,new PixelPoint[4],[],0,0,[],[]){MoveBackground=new(id,0,0,darkness){Revision=revision}};
        byte[] original=composer.ComposeField(Frame(0,null,0));byte[] black=composer.ComposeField(Frame(1,1,16));
        Assert.IsTrue(black.Where((_,i)=>i%4!=3).All(c=>c==0));Assert.IsTrue(black.Where((_,i)=>i%4==3).All(c=>c==255));
        byte[] changed=composer.ComposeField(Frame(1,1,0));Assert.IsFalse(original.SequenceEqual(changed));
        CollectionAssert.AreEqual(original,composer.ComposeField(Frame(2,null,0)));
    }
    [TestMethod]
    public void ProductionResolverLoadsEveryMoveFromGeneratedOnlyDirectory()
    {
        string folder=Path.Combine(Path.GetTempPath(),"animation-generated-"+Guid.NewGuid().ToString("N"));
        try
        {
            foreach(string file in Directory.EnumerateFiles(Folder,"*",SearchOption.AllDirectories))
            {string target=Path.Combine(folder,Path.GetRelativePath(Folder,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);}
            using var resources=new SourceAnimationResources(folder);
            foreach(int move in resources.Scripts.Keys)Assert.IsNotNull(resources.Resolve(move));
            Assert.IsTrue(resources.CachedAssetCount>1);
            Assert.IsFalse(Directory.Exists(Path.Combine(folder,"content/raw")));
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    [TestMethod]
    public void IndexedMaterialTakesPrecedenceOverSamePackRgbFallback()
    {
        string folder=Path.Combine(Path.GetTempPath(),"animation-indexed-"+Guid.NewGuid().ToString("N"));
        try
        {
            CopyManifestMetadata(folder);
            string path=Path.Combine(folder,"hit/manifest.json");var json=JsonNode.Parse(File.ReadAllText(path))!;
            json["assets"]!.AsArray().Single(row=>row!["id"]!.GetValue<string>()=="impact")!["path"]="absent-fallback.rgb555";
            var indexed=json["indexedAssets"]!.AsArray().Single(row=>row!["id"]!.GetValue<string>()=="impact")!;
            File.WriteAllText(path,json.ToJsonString());
            using var resources=new SourceAnimationResources(folder);Assert.AreEqual(0,resources.CachedAssetCount);
            // Lazy reads must need only detached metadata and subsequently available pixels.
            foreach(string file in Directory.EnumerateFiles(folder,"*.json",SearchOption.AllDirectories))File.Delete(file);
            foreach(string key in new[]{"indicesPath","palettePath"})
            {string file=indexed[key]!.GetValue<string>();File.Copy(Path.Combine(Folder,"hit",file),Path.Combine(folder,"hit",file));}
            using var original=new SourceAnimationResources(Folder);
            var expected=original.Asset("impact");var loaded=resources.Asset("impact");
            Assert.AreEqual(expected.Width,loaded.Width);Assert.AreEqual(expected.Height,loaded.Height);Assert.AreEqual(expected.PaletteTag,loaded.PaletteTag);
            CollectionAssert.AreEqual(expected.Pixels,loaded.Pixels);CollectionAssert.AreEqual(expected.PaletteIndices,loaded.PaletteIndices);CollectionAssert.AreEqual(expected.Palette,loaded.Palette);
            Assert.AreSame(expected:loaded,actual:resources.Asset("impact"));Assert.AreEqual(1,resources.CachedAssetCount);
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    [TestMethod]
    public void ConflictingMaterialFromDistinctPacksIsStillRejected()
    {
        string folder=Path.Combine(Path.GetTempPath(),"animation-conflict-"+Guid.NewGuid().ToString("N"));
        try
        {
            CopyManifestMetadata(folder);
            var bytes=File.ReadAllBytes(Path.Combine(Folder,"explosion/explosion.rgb555"));
            File.WriteAllBytes(Path.Combine(folder,"explosion/explosion.rgb555"),bytes);
            File.Copy(Path.Combine(Folder,"elemental-explosion.rgb555"),Path.Combine(folder,"elemental-explosion.rgb555"));
            bytes[4]^=1;File.WriteAllBytes(Path.Combine(folder,"conflicting-explosion.rgb555"),bytes);
            string path=Path.Combine(folder,"general.json");var json=JsonNode.Parse(File.ReadAllText(path))!;
            json["assets"]!.AsArray().Add(new JsonObject{["id"]="explosion",["path"]="conflicting-explosion.rgb555"});File.WriteAllText(path,json.ToJsonString());
            using var resources=new SourceAnimationResources(folder);
            var failure=Assert.ThrowsExactly<InvalidDataException>(()=>resources.Asset("explosion"));
            StringAssert.Contains(failure.Message,"Conflicting source sprite pixels explosion");Assert.AreEqual(0,resources.CachedAssetCount);
        }
        finally{if(Directory.Exists(folder))Directory.Delete(folder,true);}
    }
    private static void CopyManifestMetadata(string folder)
    {
        foreach(string file in Directory.EnumerateFiles(Folder,"*.json",SearchOption.AllDirectories))
        {string target=Path.Combine(folder,Path.GetRelativePath(Folder,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);}
    }
    [TestMethod]
    public void ConflictingDuplicateTemplateFailsWithoutReadingPixels()
    {
        string folder=Path.Combine(Path.GetTempPath(),"animation-recipes-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
        try
        {
            foreach(string file in new[]{"hit/manifest.json","physical.json","elemental.json","background-scripts.json","walls.json","flying.json","fog.json","horn.json","general.json","trace.json","explosion/manifest.json","stat-moves/manifest.json","stat-mask/manifest.json"})
            {string target=Path.Combine(folder,file);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(Path.Combine(Folder,file),target);}
            string path=Path.Combine(folder,"explosion/manifest.json");var json=JsonNode.Parse(File.ReadAllText(path))!;json["spriteRecipes"]![0]!["blend"]=true;File.WriteAllText(path,json.ToJsonString());
            Assert.Throws<InvalidDataException>(()=>new SourceAnimationResources(folder));
        }
        finally{Directory.Delete(folder,true);}
    }
}
