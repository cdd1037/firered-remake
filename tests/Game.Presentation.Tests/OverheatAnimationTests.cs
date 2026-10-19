using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class OverheatAnimationTests
{
    private const BindingFlags Private=BindingFlags.NonPublic|BindingFlags.Instance;
    private const string Template="gOverheatFlameSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Sounds{get;}=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Sounds.Add(cue);public void StopAnimationEffects()=>Stops++;}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("overheat-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_overheat_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/overheat-source-vectors.json");
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static object? Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,Private|BindingFlags.Public)!.Invoke(o,args);
    private static object Nested(string name,params object[] args)=>Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(name,BindingFlags.NonPublic)!,Private|BindingFlags.Public,null,args,null)!;
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,7,0,64,64)).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static SourceBattleAnimation Owner(SourceAnimationScript s,bool doubles,int actor)=>new(Primitive(s),new("overheat",1,s.MoveId,new(actor),new(actor^1)),Battlers(doubles),doubles,new Audio());
    private static void PaletteTask(SourceBattleAnimation owner,string name,int selector=0,int index=0)
    {var args=Field<short[]>(owner,"_animationArgs");args[0]=(short)selector;args[1]=(short)index;Assert.IsTrue((bool)Invoke(owner,"TryRunPaletteBackupTask",C("createvisualtask",name,"5"))!);}
    private static ushort Faded(SourceBattleAnimation owner,int bank,ushort color)
    {var frame=owner.Snapshot();return SourcePaletteMath.Apply(color,frame.PaletteBlends.SingleOrDefault(p=>p.Palette==bank),frame.InvertedPalettes.Contains(bank),bank>=16&&frame.GreyscaleSlots.Contains(bank-16));}
    private static ushort Normal(SourceBattleAnimation owner,int bank,ushort color)
    {var normal=Field<Dictionary<int,SourcePaletteState>>(owner,"_unfadedPaletteStates").GetValueOrDefault(bank);return normal is null?color:SourcePaletteMath.Apply(color,normal.Blend,normal.Inverted,normal.Greyscale);}

    [TestMethod]
    public void SharedSmallEmberHasOneIndexedOwnerAndRetainsEveryExistingPixelAndConsumer()
    {
        using var elemental=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Folder,"elemental.json")));
        using var physical=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(Folder,"physical.json")));
        Assert.AreEqual(1,elemental.RootElement.GetProperty("indexedAssets").EnumerateArray().Count(a=>a.GetProperty("id").GetString()=="small-ember"));
        foreach(string key in new[]{"assets","indexedAssets"})Assert.IsFalse(physical.RootElement.GetProperty(key).EnumerateArray().Any(a=>a.GetProperty("id").GetString()=="small-ember"));
        using var resources=new SourceAnimationResources(Folder);var shared=resources.Asset("small-ember");
        var row=elemental.RootElement.GetProperty("indexedAssets").EnumerateArray().Single(a=>a.GetProperty("id").GetString()=="small-ember");
        Assert.IsFalse(elemental.RootElement.GetProperty("assets").EnumerateArray().Any(a=>a.GetProperty("id").GetString()=="small-ember"));
        IndexedSpriteExpectations.AssertMatches(row,Folder,shared);
        Assert.AreEqual(shared.Width*shared.Height,shared.PaletteIndices.Length);Assert.AreEqual(16,shared.Palette.Length);
        var physicalIds=physical.RootElement.GetProperty("scripts").EnumerateArray().Select(s=>s.GetProperty("moveId").GetInt32()).ToHashSet();int physicalConsumers=0,elementalConsumers=0;
        foreach(var script in resources.Scripts.Values)
        {
            var commands=script.Commands.Concat(script.Subroutines.Values.SelectMany(c=>c));
            if(!commands.Any(c=>c.Op=="createsprite"&&script.SpriteRecipes.TryGetValue(c.Args[0],out var recipe)&&recipe.Asset=="small-ember"))continue;
            var resolved=resources.Resolve(script.MoveId);Assert.AreSame(shared,resolved.Assets["small-ember"]);Assert.AreEqual(resolved.Script.SpriteTags["ANIM_TAG_SMALL_EMBER"],shared.PaletteTag);CollectionAssert.AreEqual(shared.Palette,resolved.Script.SpritePalettes[shared.PaletteTag]);
            if(physicalIds.Contains(script.MoveId))physicalConsumers++;else elementalConsumers++;
        }
        Assert.IsTrue(physicalConsumers>0);Assert.IsTrue(elementalConsumers>0);
    }

    [TestMethod][DataRow(299)][DataRow(337)]
    public void ExistingPhysicalRootsComposeThroughTheSharedIndexedSmallEmberOwner(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);var shared=resources.Asset("small-ember");Assert.AreSame(shared,resolved.Assets["small-ember"]);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,new ushort[240*112]);foreach(var b in battlers)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0xded3,4096).ToArray());
            var cue=new CommittedMoveAnimation("shared-small-ember",1,move,new(actor),new(actor^1));cue=cue with{VisualSeed=SourceAnimationVisualSeed.For(cue)};
            var scene=new SourceBattleAnimation.SpriteScene();var owner=new SourceBattleAnimation(resolved.Script,cue,battlers,doubles,new Audio(),spriteScene:scene,spriteMaterials:resolved.Assets);var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);int ticks=0;bool pixels=false,indexed=false;
            while(owner.Active&&ticks++<500)
            {
                var frame=owner.Tick();var rgba=composer.ComposeField(frame);Assert.AreEqual(240*112*4,rgba.Length);
                var flames=frame.Sprites.Where(s=>s.Asset=="small-ember").ToArray();if(flames.Length==0)continue;
                // These source callbacks keep the ordinary tag-selected palette; explicit
                // slots belong only to callbacks that alter oam.paletteNum themselves.
                foreach(var flame in flames)
                {
                    Assert.IsNull(flame.PaletteSlot);Assert.AreSame(shared,flame.Material);Assert.IsNotNull(flame.MaterialPalette);
                    CollectionAssert.AreEqual(shared.Palette,flame.MaterialPalette);CollectionAssert.AreEqual(frame.SpritePalettes[shared.PaletteTag],flame.MaterialPalette);Assert.AreNotSame(frame.SpritePalettes[shared.PaletteTag],flame.MaterialPalette);
                }
                indexed=true;
                pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Asset!="small-ember").ToArray()}));
            }
            Assert.IsFalse(owner.Active);Assert.IsTrue(indexed);Assert.IsTrue(pixels);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsFalse(owner.GraphicsLoaded);
        }
    }

    [TestMethod]
    public void FlameMatchesSourceSignedAccumulatorsDivisionFramesLifetimeAndEveryNativePosition()
    {
        var full=AnimationCompositionFixture.Script(315);using var json=Vectors();int cases=0,rows=0;
        foreach(var group in json.RootElement.GetProperty("flame").EnumerateArray().Select(N).GroupBy(r=>(r[0],r[1],r[2],r[3],r[4])))
        {
            var first=group.First();var owner=Owner(full,first[0]!=0,first[1]);var sprite=Nested("OverheatFlameSprite",4,28,full.SpriteRecipes[Template],first.Skip(6).Take(5).ToArray(),owner);
            foreach(var r in group)
            {
                if(r[5]>=0){Invoke(sprite,"Step",owner);if(!(bool)sprite.GetType().GetProperty("Finished")!.GetValue(sprite)!)Invoke(sprite,"AdvanceAnimation",owner);}
                var f=(SourceVisualSpriteFrame)sprite.GetType().GetProperty("Frame")!.GetValue(sprite)!;string why=$"{group.Key} tick {r[5]}";
                Assert.AreEqual(new PixelPoint(r[11],r[12]),f.Position,why);Assert.AreEqual(new PixelPoint(r[13],r[14]),f.Offset,why);Assert.AreEqual(r[15]==0,(bool)sprite.GetType().GetProperty("Finished")!.GetValue(sprite)!,why);Assert.AreEqual(r[16],f.Tile,why);Assert.AreEqual(r[17],Field<SourceSpriteFrameProgram>(sprite,"_frames").DelayCounter,why);
                Assert.AreEqual(r[18],(int)Field<short>(sprite,"_ticks"),why);Assert.AreEqual(r[19],(int)Field<short>(sprite,"_vx"),why);Assert.AreEqual(r[20],(int)Field<short>(sprite,"_vy"),why);Assert.AreEqual(r[21],(int)Field<short>(sprite,"_duration"),why);Assert.AreEqual(r[22],(int)Field<short>(sprite,"_x"),why);Assert.AreEqual(r[23],(int)Field<short>(sprite,"_y"),why);rows++;
            }
            owner.Cancel();cases++;
        }
        Assert.AreEqual(336,cases);Assert.AreEqual(11232,rows);
    }

    [TestMethod]
    public void BackupMatchesNativeNormalFadedIsolationSourceSelectorsAndSequentialRounding()
    {
        var full=AnimationCompositionFixture.Script(315);using var json=Vectors();int cases=0,rows=0;bool sequentialDiffers=false;
        foreach(var group in json.RootElement.GetProperty("palette").EnumerateArray().Select(N).GroupBy(r=>(Actor:r[0],Selector:r[1],Pass:r[2])))
        {
            var key=group.Key;var owner=Owner(full,true,key.Actor);int bank=group.First()[4],index=key.Pass!=0?255:0;var originals=group.First().Skip(7).Take(16).Select(x=>(ushort)x).ToArray();Field<HashSet<int>>(owner,"_invertedPalettes").Add(bank);
            foreach(var r in group)
            {
                switch(r[3])
                {
                    case 1:PaletteTask(owner,"AnimTask_AllocBackupPalBuffer");break;
                    case 2:PaletteTask(owner,"AnimTask_CopyPalUnfadedToBackup",key.Selector,index);break;
                    case 3:Invoke(owner,"BlendPalettes",1u<<bank,13,(ushort)28);break;
                    case 4:PaletteTask(owner,"AnimTask_CopyPalFadedToUnfaded",key.Selector);break;
                    case 5:Invoke(owner,"BlendPalettes",1u<<bank,13,(ushort)(18|(18<<5)|(18<<10)));break;
                    case 6:PaletteTask(owner,"AnimTask_CopyPalUnfadedFromBackup",key.Selector,index);break;
                    case 7:Invoke(owner,"BlendPalettes",1u<<bank,0,(ushort)(18|(18<<5)|(18<<10)));break;
                    case 8:PaletteTask(owner,"AnimTask_FreeBackupPalBuffer");break;
                }
                Assert.AreEqual(r[6]!=0,Field<object?>(owner,"_paletteBackup") is not null);
                for(int i=0;i<16;i++)
                {
                    Assert.AreEqual((ushort)r[7+i],Normal(owner,bank,originals[i]),$"normal {key}, phase {r[3]}, index {i}");Assert.AreEqual((ushort)r[23+i],Faded(owner,bank,originals[i]),$"faded {key}, phase {r[3]}, index {i}");
                    if(r[3]==5)sequentialDiffers|=r[23+i]!=SourcePaletteMath.Blend(originals[i],13,(ushort)(18|(18<<5)|(18<<10)));
                }
                Assert.AreEqual(0,owner.ActiveVisualCount);rows++;
            }
            owner.Cancel();cases++;
        }
        Assert.AreEqual(24,cases);Assert.AreEqual(216,rows);Assert.IsTrue(sequentialDiffers);
    }

    [TestMethod]
    public void SequentialPaletteTransformsMatchIndependentCForEveryRgb555ColorAndPreserveOpacity()
    {
        using var json=Vectors();var first=new SourcePaletteBlend(16,13,28);var second=new SourcePaletteBlend(16,13,19026){Unfaded=new(first,false,false)};int rows=0;
        foreach(var r in json.RootElement.GetProperty("exhaustive").EnumerateArray().Select(N))
        {Assert.AreEqual(rows,r[0]);Assert.AreEqual((ushort)r[1],SourcePaletteMath.Apply((ushort)r[0],first,false));Assert.AreEqual((ushort)r[2],SourcePaletteMath.Apply((ushort)r[0],second,false));Assert.AreEqual((ushort)(r[2]|0x8000),SourcePaletteMath.Apply((ushort)(r[0]|0x8000),second,false));rows++;}
        Assert.AreEqual(32768,rows);
    }

    [TestMethod]
    public void MaterialReloadClearsBothPaletteStatesWhileGreyscaleReadsCurrentNormal()
    {
        var full=AnimationCompositionFixture.Script(315);var bodies=Battlers(false);var replacement=bodies[0] with{Asset="replacement"};var owner=new SourceBattleAnimation(Primitive(full),new("reload",1,315,new(0),new(1)),bodies,false,new Audio(),battlerReplacements:new Dictionary<int,AnimationBattler>{{0,replacement}});const ushort source=0x5ed3;
        PaletteTask(owner,"AnimTask_AllocBackupPalBuffer");Invoke(owner,"BlendPalettes",1u<<16,13,(ushort)28);PaletteTask(owner,"AnimTask_CopyPalFadedToUnfaded",1);ushort normal=Normal(owner,16,source);Invoke(owner,"SetBattlerGreyscaleOrOriginal",0,false);int average=((normal&31)+((normal>>5)&31)+((normal>>10)&31))/3;Assert.AreEqual((ushort)(average|(average<<5)|(average<<10)),Faded(owner,16,source));Assert.AreEqual(normal,Normal(owner,16,source));Invoke(owner,"SetBattlerGreyscaleOrOriginal",0,true);Assert.AreEqual(normal,Faded(owner,16,source));Invoke(owner,"SwapBattlerMaterial",0);Assert.AreEqual(source,Normal(owner,16,source));Assert.AreEqual(source,Faded(owner,16,source));Assert.AreEqual("replacement",owner.Snapshot().Battlers[0].Asset);
        var baselines=Field<Dictionary<int,SourcePaletteState>>(owner,"_unfadedPaletteStates");var state=new SourcePaletteState(new(16,13,28),false,false);foreach(int bank in new[]{2,3,4,20})baselines[bank]=state;
        Invoke(owner,"LoadBackgroundPalette",new object[]{null!});Assert.IsFalse(new[]{2,3,4}.Any(baselines.ContainsKey));Invoke(owner,"LoadSpriteGraphics","ANIM_TAG_SMALL_EMBER");Assert.IsFalse(baselines.ContainsKey(20));owner.Cancel();
    }

    [TestMethod]
    public void BackupsAreIndependentAndStaticCopiesFreezeNormalWhileLiveMonbgFollowsFaded()
    {
        var full=AnimationCompositionFixture.Script(315);var owner=Owner(full,true,2);const ushort source=0x5ed3;PaletteTask(owner,"AnimTask_AllocBackupPalBuffer");PaletteTask(owner,"AnimTask_CopyPalUnfadedToBackup",0,1);PaletteTask(owner,"AnimTask_CopyPalUnfadedToBackup",1,0);
        Invoke(owner,"BlendPalettes",1u<<18,13,(ushort)28);PaletteTask(owner,"AnimTask_CopyPalFadedToUnfaded",1);Invoke(owner,"MoveBattlerToStaticBackground",2);var frozen=owner.Snapshot();var copied=frozen.StaticBackgroundCopies.Single();Assert.IsNotNull(copied.Palette);Assert.AreEqual(SourcePaletteMath.Blend(source,13,28),SourcePaletteMath.Apply(source,copied.Palette,false));
        Invoke(owner,"BlendPalettes",1u<<18,13,(ushort)19026);var twice=owner.Snapshot();Assert.AreEqual(SourcePaletteMath.Blend(SourcePaletteMath.Blend(source,13,28),13,19026),Faded(owner,18,source));Assert.AreEqual(SourcePaletteMath.Blend(source,13,28),SourcePaletteMath.Apply(source,copied.Palette,false));
        PaletteTask(owner,"AnimTask_CopyPalUnfadedFromBackup",0,1);Assert.AreNotEqual(source,Normal(owner,18,source));PaletteTask(owner,"AnimTask_CopyPalUnfadedFromBackup",1,0);Assert.AreEqual(source,Normal(owner,18,source));Assert.AreNotEqual(source,Faded(owner,18,source));Invoke(owner,"BlendPalettes",1u<<18,0,(ushort)0);Assert.AreEqual(source,Faded(owner,18,source));
        Assert.AreEqual(SourcePaletteMath.Blend(SourcePaletteMath.Blend(source,13,28),13,19026),SourcePaletteMath.Apply(source,twice.PaletteBlends.Single(p=>p.Palette==18),false));PaletteTask(owner,"AnimTask_FreeBackupPalBuffer");owner.Cancel();Assert.AreEqual(SourcePaletteMath.Blend(source,13,28),SourcePaletteMath.Apply(source,copied.Palette,false));
    }

    [TestMethod]
    public void DetachedClonePaletteKeepsItsCompleteNormalPrefixAfterLiveOwnerChanges()
    {
        const ushort source=0xded3;var first=new SourcePaletteBlend(16,13,28);var detached=new SourcePaletteBlend(16,13,19026){Unfaded=new(first,false,false)};
        var assets=new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,Enumerable.Repeat((ushort)0x8000,240*112).ToArray())},{"impact",new(32,32,new ushort[1024])},{"copy",new(64,64,Enumerable.Repeat(source,4096).ToArray())}};
        var sprite=new SourceVisualSpriteFrame(4,"detached-palette-copy",new(120,56),default,0,true,false,false,256,"copy",1){Width=64,Height=64,BattlerCopySlot=0,BattlerCopyPalette=detached};var frame=new SourceAnimationFrame(0,true,new PixelPoint[4],[],0,0,[],[]){Sprites=[sprite],PaletteBlends=[new(16,16,31)]};var composer=new SourceBattleFrameComposer(assets,[]);var pixels=composer.ComposeField(frame);ushort expected=SourcePaletteMath.Apply(source,detached,false);int at=(56*240+120)*4;Assert.AreEqual((byte)((expected&31)*255/31),pixels[at]);Assert.AreEqual((byte)(((expected>>5)&31)*255/31),pixels[at+1]);Assert.AreEqual((byte)(((expected>>10)&31)*255/31),pixels[at+2]);CollectionAssert.AreEqual(pixels,composer.ComposeField(frame with{PaletteBlends=[]}));
    }

    [TestMethod]
    public void CompleteRootComposesAllPositionsThreeFlameRingsAndRestoresPaletteOwnership()
    {
        using var resources=new SourceAnimationResources(Folder);var full=resources.Resolve(315);Assert.AreEqual("Move_OVERHEAT",full.Script.Label);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var b=Battlers(doubles);var assets=full.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());foreach(var body in b)assets[body.Asset]=new(64,64,Enumerable.Repeat((ushort)0xded3,4096).ToArray());var surface=resources.CreateBackgroundSurface(0);var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var owner=new SourceBattleAnimation(full.Script,new("overheat",1,315,new(actor),new(target)),b,doubles,audio,Enumerable.Range(0,16).Select(i=>surface.PaletteColor(2,i)).ToArray(),spriteScene:scene,spriteMaterials:full.Assets);var composer=AnimationCompositionFixture.Create(resources,full,assets,b,surface);int ticks=0,peak=0;bool pixels=false,sequential=false;SourceAnimationFrame? retained=null;
            while(owner.Active&&ticks++<600)
            {
                var frame=owner.Tick();var rgba=composer.ComposeField(frame);Assert.AreEqual(240*112*4,rgba.Length);peak=Math.Max(peak,frame.Sprites.Count(s=>s.Template==Template));sequential|=frame.PaletteBlends.Any(p=>p.Palette==16+actor&&p.Unfaded is not null);
                if(frame.Sprites.Any(s=>s.Template==Template)){pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));retained=frame;}
            }
            Assert.IsFalse(owner.Active);Assert.IsTrue(pixels&&sequential);Assert.AreEqual(24,peak);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsFalse(owner.GraphicsLoaded);Assert.IsNull(Field<object?>(owner,"_paletteBackup"));Assert.AreEqual(0,Field<Dictionary<int,SourcePaletteState>>(owner,"_unfadedPaletteStates").Count);var final=owner.Snapshot();Assert.AreEqual(0,final.PaletteBlends.Length);Assert.AreEqual(0,final.InvertedPalettes.Length);Assert.AreEqual(0,final.BackgroundSlots.Length);Assert.IsFalse(final.BlendEnabled);Assert.IsTrue(final.Offsets.All(p=>p==default));Assert.AreEqual(4,audio.Sounds.Count(s=>s.Id=="SE_M_FIRE_PUNCH"));Assert.AreEqual(actor%2==0?-64:63,audio.Sounds.Single(s=>s.Id=="SE_M_DRAGON_RAGE").Pan);Assert.IsNotNull(retained);Assert.IsTrue(retained.Sprites.Any(s=>s.Template==Template));
        }
    }

    [TestMethod]
    public void CancelAcrossNormalMutationFlamesAndFinalRecoveryReleasesStateExactlyOnce()
    {
        var full=AnimationCompositionFixture.Script(315);foreach(int tick in new[]{0,1,15,25,45,70,100,140,180,220})
        {
            var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var owner=new SourceBattleAnimation(full,new("cancel",1,315,new(3),new(0)),Battlers(true),true,audio,spriteScene:scene);
            for(int i=0;i<tick&&owner.Active;i++)owner.Tick();var before=owner.Snapshot();int sounds=audio.Sounds.Count;owner.Cancel();owner.Cancel();var after=owner.Snapshot();Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,owner.ActiveVisualCount);Assert.IsNull(Field<object?>(owner,"_paletteBackup"));Assert.AreEqual(0,Field<Dictionary<int,SourcePaletteState>>(owner,"_unfadedPaletteStates").Count);Assert.AreEqual(0,after.PaletteBlends.Length);Assert.AreEqual(0,after.BackgroundSlots.Length);Assert.IsFalse(after.BlendEnabled);Assert.IsTrue(after.Offsets.All(p=>p==default));Assert.IsFalse(owner.GraphicsLoaded);Assert.AreEqual(1,audio.Stops);Assert.AreEqual(sounds,audio.Sounds.Count);Assert.IsFalse(after.Active);
        }
    }

    [TestMethod]
    public void MalformedRecipesMissingPaletteAndUnbalancedOrCrossBankBackupsFailClosed()
    {
        var full=AnimationCompositionFixture.Script(315);var recipe=full.SpriteRecipes[Template];
        void Reject(SourceAnimationScript s){var audio=new Audio();Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(s,new("bad",1,315,new(0),new(1)),Battlers(false),false,audio));Assert.AreEqual(0,audio.Sounds.Count);}
        foreach(var r in new[]{recipe with{Callback="SpriteCallbackDummy"},recipe with{Width=16},recipe with{Blend=true},recipe with{LoopStarts=[-1]},recipe with{AffineDoubleSize=true},recipe with{Animations=[[new(0,1,false,false)]]}}){var recipes=full.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[Template]=r;Reject(full with{SpriteRecipes=recipes});}
        Reject(full with{SpritePalettes=new Dictionary<int,ushort[]>()});Reject(full with{Commands=full.Commands.Where(c=>c.Args.FirstOrDefault()!="AnimTask_FreeBackupPalBuffer").ToArray()});
        SourceAnimationCommand Task(string name,params string[] args)=>C("createvisualtask",new[]{name,"5"}.Concat(args).ToArray());var alloc=Task("AnimTask_AllocBackupPalBuffer");var free=Task("AnimTask_FreeBackupPalBuffer");
        foreach(var commands in new[]{new[]{free,C("end")},new[]{alloc,alloc,free,C("end")},new[]{alloc,Task("AnimTask_CopyPalUnfadedFromBackup","1","0"),free,C("end")},new[]{alloc,Task("AnimTask_CopyPalUnfadedToBackup","1","0"),Task("AnimTask_CopyPalUnfadedFromBackup","2","0"),free,C("end")},new[]{alloc,Task("AnimTask_CopyPalFadedToUnfaded","1"),free,C("end")},new[]{alloc,Task("AnimTask_CopyPalUnfadedToBackup","1","256"),free,C("end")}})Reject(Primitive(full) with{Commands=commands});
        var badFlame=full.Commands.First(c=>c.Args.FirstOrDefault()==Template);foreach((int operand,string value) in new[]{(4,"256"),(6,"32767")}){var args=(string[])badFlame.Args.Clone();args[operand]=value;Reject(Primitive(full) with{Commands=[badFlame with{Args=args},C("end")]});}
    }
}
