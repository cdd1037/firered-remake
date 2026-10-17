using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SharedSpriteSceneTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public bool IsPlaying=>false;public void Play(SourceAnimationSound cue){}public void StopAnimationEffects(){}}
    private static AnimationBattler[] Battlers()=>Enumerable.Range(0,4).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    private static Dictionary<string,SourcePixelAsset> Materials(SourceResolvedAnimation resolved)
    {
        var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);
        assets["background"]=new(240,112,new ushort[240*112]);
        foreach(var b in Battlers())assets[b.Asset]=new(64,64,new ushort[4096]);
        return assets;
    }
    private static SourceBattleAnimation RetireStars(SourceAnimationResources resources,SourceBattleAnimation.SpriteScene scene,bool indexed=false)
    {
        var script=resources.Scripts[309] with{Commands=[new("loadspritegfx",["ANIM_TAG_GOLD_STARS"],1),
            new("createsprite",["gMeteorMashStarSpriteTemplate","ANIM_TARGET","2","0","0","0","0","30"],2),
            new("waitforvisualfinish",[],3),new("end",[],4)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),Labels=new Dictionary<string,int>()};
        var resolved=resources.Resolve(script);var assets=Materials(resolved);
        if(indexed)
        {
            int tag=resolved.Script.SpriteTags["ANIM_TAG_GOLD_STARS"];ushort[] red=new ushort[16];red[1]=31;
            var gold=assets["gold-stars"];
            assets["gold-stars"]=gold with{PaletteTag=tag,PaletteIndices=Enumerable.Repeat((byte)1,gold.Pixels.Length).ToArray(),Palette=red};
            resolved=resolved with{Script=resolved.Script with{SpritePalettes=new Dictionary<int,ushort[]>{{tag,red}}}};
        }
        var runtime=new SourceBattleAnimation(resolved.Script,new("shared",1,309,new(0),new(1)){VisualSeed=0x12345678},Battlers(),true,new Audio(),spriteScene:scene,spriteMaterials:assets);
        int ticks=0;while(runtime.Active&&ticks++<50)runtime.Tick();
        Assert.IsFalse(runtime.Active);Assert.IsTrue(runtime.HasResidualSprites);Assert.AreEqual(0,runtime.ActiveVisualCount);
        return runtime;
    }
    private static SourceResolvedAnimation Hits(SourceAnimationResources resources,int count,int modifier=37)
    {
        var commands=new List<SourceAnimationCommand>{new("loadspritegfx",["ANIM_TAG_IMPACT"],1)};
        for(int i=0;i<count;i++)commands.Add(new("createsprite",["gBasicHitSplatSpriteTemplate","ANIM_TARGET",modifier.ToString(),"0","0","ANIM_TARGET","0"],i+2));
        commands.Add(new("waitforvisualfinish",[],count+2));commands.Add(new("end",[],count+3));
        return resources.Resolve(resources.Scripts[1] with{Commands=commands.ToArray(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),Labels=new Dictionary<string,int>()});
    }
    private static SourceAnimationFrame ReachSprite(SourceBattleAnimation runtime,string template)
    {
        // loadspritegfx has its own source wait; general roots build OAM before
        // running their script task. Never assume allocation is the first tick.
        for(int ticks=0;ticks<8&&runtime.Active;ticks++)
        {
            var frame=runtime.Tick();if(frame.Sprites.Any(s=>s.Template==template))return frame;
        }
        Assert.Fail("The source script did not reach the expected sprite within eight frames: "+template);
        throw new InvalidOperationException();
    }
    [TestMethod]
    public void NewInvocationSharesLiveSlotsAndOamWithRetiredSpritesAndRetainsMaterials()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var scene=new SourceBattleAnimation.SpriteScene();var old=RetireStars(resources,scene);
        var occupied=old.Snapshot().Sprites.Select(s=>s.Slot).ToHashSet();Assert.IsTrue(occupied.Count>0);
        var resolved=Hits(resources,1);var assets=Materials(resolved);
        assets["impact"]=new(32,32,Enumerable.Repeat((ushort)0x83e0,1024).ToArray());
        Assert.IsFalse(assets.ContainsKey("gold-stars"));
        var next=new SourceBattleAnimation(resolved.Script,new("shared",2,1,new(0),new(1)),Battlers(),true,new Audio(),spriteScene:scene,spriteMaterials:assets);
        var frame=ReachSprite(next,"gBasicHitSplatSpriteTemplate");var hit=frame.Sprites.Single(s=>s.Template=="gBasicHitSplatSpriteTemplate");
        Assert.IsFalse(occupied.Contains(hit.Slot));Assert.AreEqual(frame.Sprites.Length,frame.Sprites.Select(s=>s.Slot).Distinct().Count());
        var stars=frame.Sprites.Where(s=>s.Template=="gMiniTwinklingStarSpriteTemplate").ToArray();Assert.IsTrue(stars.Length>0);
        Assert.IsTrue(stars.All(s=>hit.OamRank<s.OamRank));Assert.IsTrue(stars.All(s=>s.Material is not null));
        var composer=AnimationCompositionFixture.Create(resources,resolved,assets,Battlers());
        var pixels=composer.ComposeField(frame);var center=hit.Position;int at=(center.Y*240+center.X)*4;
        Assert.AreEqual((byte)0,pixels[at]);Assert.AreEqual((byte)255,pixels[at+1]);
        Assert.Throws<InvalidOperationException>(()=>scene.TickIdle(Battlers(),true));
        Assert.Throws<InvalidOperationException>(()=>old.Tick());
        // The current script advances the old callbacks once, not a second overlay pass.
        int ticks=1;while(next.Active&&ticks++<40)next.Tick();Assert.IsFalse(next.Active);
        int tails=0;while(scene.HasResidualSprites&&tails++<80)composer.ComposeField(scene.TickIdle(Battlers(),true));
        Assert.IsFalse(scene.HasResidualSprites);Assert.AreEqual(0,scene.Count);
    }
    [TestMethod]
    public void ResidualOccupancyParticipatesInCapacityAndSceneCancellationReleasesEveryOwner()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var scene=new SourceBattleAnimation.SpriteScene();var old=RetireStars(resources,scene);int retained=scene.Count;
        var resolved=Hits(resources,61-retained);var assets=Materials(resolved);
        var next=new SourceBattleAnimation(resolved.Script,new("shared",2,1,new(0),new(1)),Battlers(),true,new Audio(),spriteScene:scene,spriteMaterials:assets);
        Assert.Throws<InvalidDataException>(()=>{for(int ticks=0;ticks<8&&next.Active;ticks++)next.Tick();});Assert.AreEqual(60,scene.Count);
        scene.Cancel();Assert.AreEqual(0,scene.Count);Assert.IsFalse(old.HasResidualSprites);Assert.IsFalse(next.Active);
        var full=Hits(resources,60);var fullAssets=Materials(full);
        var replacement=new SourceBattleAnimation(full.Script,new("shared",3,1,new(0),new(1)),Battlers(),true,new Audio(),spriteScene:scene,spriteMaterials:fullAssets);
        var frame=ReachSprite(replacement,"gBasicHitSplatSpriteTemplate");Assert.AreEqual(60,frame.Sprites.Length);
        CollectionAssert.AreEqual(Enumerable.Range(4,60).ToArray(),frame.Sprites.Select(s=>s.Slot).ToArray());scene.Cancel();
    }
    [TestMethod]
    public void CancellingOneInvocationDoesNotDeleteAnotherOwnersResiduals()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var scene=new SourceBattleAnimation.SpriteScene();var old=RetireStars(resources,scene);int retained=scene.Count;
        var resolved=Hits(resources,1);var next=new SourceBattleAnimation(resolved.Script,new("shared",2,1,new(0),new(1)),Battlers(),true,new Audio(),spriteScene:scene,spriteMaterials:Materials(resolved));
        var active=ReachSprite(next,"gBasicHitSplatSpriteTemplate");Assert.AreEqual(1,active.Sprites.Count(s=>s.Template=="gBasicHitSplatSpriteTemplate"));
        Assert.AreEqual(retained+1,scene.Count);next.Cancel();Assert.AreEqual(retained,scene.Count);Assert.IsTrue(old.HasResidualSprites);
        var idle=scene.TickIdle(Battlers(),true);Assert.IsTrue(idle.Sprites.All(s=>s.Template=="gMiniTwinklingStarSpriteTemplate"));
        scene.Cancel();Assert.AreEqual(0,scene.Count);
    }
    [TestMethod]
    public void ReleasedPaletteRemainsOwnedWhenNextFrameUsesTheSameTag()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var scene=new SourceBattleAnimation.SpriteScene();RetireStars(resources,scene,true);
        var resolved=Hits(resources,1);var assets=Materials(resolved);
        var next=new SourceBattleAnimation(resolved.Script,new("shared",2,1,new(0),new(1)),Battlers(),true,new Audio(),spriteScene:scene,spriteMaterials:assets);
        var frame=ReachSprite(next,"gBasicHitSplatSpriteTemplate");var star=frame.Sprites.First(s=>s.Template=="gMiniTwinklingStarSpriteTemplate"&&s.Visible);
        Assert.IsNotNull(star.Material);Assert.IsNotNull(star.MaterialPalette);Assert.AreEqual((ushort)31,star.MaterialPalette[1]);
        ushort[] blue=new ushort[16];blue[1]=31<<10;
        var changed=frame with{SpritePalettes=new Dictionary<int,ushort[]>{{star.Material.PaletteTag,blue}},
            Sprites=frame.Sprites.Select(s=>s with{Visible=s.Slot==star.Slot}).ToArray()};
        var pixels=AnimationCompositionFixture.Create(resources,resolved,assets,Battlers()).ComposeField(changed);
        int at=((star.Position.Y+star.Offset.Y)*240+star.Position.X+star.Offset.X)*4;
        Assert.AreEqual((byte)255,pixels[at]);Assert.AreEqual((byte)0,pixels[at+2]);scene.Cancel();
    }

    [TestMethod]
    public void GeneralTaskPaletteWritesAfterOamAffectTheSameComposedFrame()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        const string label="General_PaletteTiming";
        var script=resources.Scripts[47] with{MoveId=0,Label=label,Commands=[
            new("loadspritegfx",["ANIM_TAG_MUSIC_NOTES"],1),
            new("createsprite",["gWavyMusicNotesSpriteTemplate","ANIM_ATTACKER","2","0","0","0"],2),
            new("delay",["0"],3),new("createvisualtask",["AnimTask_MusicNotesRainbowBlend","2"],4),
            new("waitforvisualfinish",[],5),new("end",[],6)],Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),Labels=new Dictionary<string,int>()};
        var resolved=resources.Resolve(script);var assets=Materials(resolved);
        int tag=resolved.Script.SpriteTags["ANIM_TAG_MUSIC_NOTES"];
        script=resolved.Script with{SpritePalettes=new Dictionary<int,ushort[]>{{tag,new ushort[16]}}};
        var runtime=new SourceBattleAnimation(script,new("palette",1,0,new(0),new(1)){GeneralAnimationLabel=label},Battlers(),true,new Audio(),spriteMaterials:assets);
        Assert.AreEqual(0,runtime.Tick().Sprites.Length);
        var before=ReachSprite(runtime,"gWavyMusicNotesSpriteTemplate");var initial=before.Sprites.Single();Assert.IsTrue(initial.MaterialPalette!.All(c=>c==0));
        var changed=runtime.Tick();var note=changed.Sprites.Single();Assert.IsNotNull(note.PaletteSlot);
        CollectionAssert.AreEqual(changed.SpritePaletteSlots[note.PaletteSlot.Value],note.MaterialPalette);
        Assert.AreEqual((ushort)32767,note.MaterialPalette![1]);runtime.Cancel();
    }

}
