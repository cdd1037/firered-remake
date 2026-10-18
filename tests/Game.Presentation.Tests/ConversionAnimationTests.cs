using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class ConversionAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio{public bool IsPlaying=>false;public void Play(SourceAnimationSound c){}public void SetPan(int p,int f){}public void StopAnimationEffects(){}}
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static AnimationBattler[] Battlers(bool doubles=true)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    private static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(target)!;
    private static object? Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.Invoke(target,args);
    private static SourceBattleAnimation Runtime(SourceAnimationScript script,int actor=0,int target=0)=>new(script with{Commands=[C("end")]},new("conversion-vectors",1,script.MoveId,new(actor),new(target)),Battlers(),true,new Audio());
    [TestMethod]
    public void SpriteCallbacksMatchOriginalCPositionPauseDelayLinearAndDestroyHandoffs()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        using var json=SourceVectorFixture.Read("conversion-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_conversion_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/conversion-vectors.json");
        var rows=json.RootElement.GetProperty("sprites").EnumerateArray().Select(r=>r.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).ToArray();int cases=0;
        foreach(var group in rows.GroupBy(r=>(Kind:r[0],Actor:r[1],Target:r[2],Delay:r[3])))
        {
            var key=group.Key;var script=resources.Resolve(key.Kind==0?160:176).Script;var o=Runtime(script,key.Actor,key.Target);Invoke(o,"LoadSpriteGraphics","ANIM_TAG_CONVERSION");
            var type=typeof(SourceBattleAnimation).GetNestedType("ConversionSprite",BindingFlags.NonPublic)!;
            var recipe=script.SpriteRecipes[key.Kind==0?"gConversionSpriteTemplate":"gConversion2SpriteTemplate"];
            var sprite=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[4,2,recipe,key.Kind==0?new[]{-24,24}:new[]{-24,24,key.Delay},o],null)!;
            foreach(var r in group)
            {
                if(key.Kind==0&&r[4]==72)((short[])Field(o,"_animationArgs"))[7]=-1;
                if(r[4]>=0)Invoke(sprite,"Step",o);
                bool finished=(bool)type.GetProperty("Finished")!.GetValue(sprite)!;Assert.AreEqual(r[5]==0,finished,$"{key} tick{r[4]}");
                var f=(SourceVisualSpriteFrame)type.GetProperty("Frame")!.GetValue(sprite)!;
                Assert.AreEqual(new PixelPoint(r[6],r[7]),f.Position);Assert.AreEqual(new PixelPoint(r[8],r[9]),f.Offset);
                Assert.AreEqual(128,f.MatrixA);Assert.AreEqual(128,f.MatrixD);
                if(key.Kind==1&&r[10]!=0)Assert.AreEqual(0,f.Tile,"Paused frame remains initial tile.");
            }
            cases++;
        }
        Assert.AreEqual(20,cases);
    }
    [TestMethod]
    public void AlphaTasksMatchOriginalCRegistersAndSharedTerminationExactly()
    {
        using var json=SourceVectorFixture.Read("conversion-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_conversion_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/conversion-vectors.json");
        var rows=json.RootElement.GetProperty("alpha").EnumerateArray().Select(r=>r.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).ToArray();
        foreach(int kind in new[]{0,1})
        {
            var o=Runtime(AnimationCompositionFixture.Script(kind==0?160:176));var type=typeof(SourceBattleAnimation).GetNestedType("ConversionAlphaTask",BindingFlags.NonPublic)!;
            var task=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[kind==1],null)!;
            typeof(SourceBattleAnimation).GetField("_alphaA",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(o,kind==0?16:0);typeof(SourceBattleAnimation).GetField("_alphaB",BindingFlags.Instance|BindingFlags.NonPublic)!.SetValue(o,kind==0?0:16);
            foreach(var r in rows.Where(r=>r[0]==kind)){Invoke(task,"Step",o);var f=o.Snapshot();Assert.AreEqual(r[6]&31,f.AlphaA);Assert.AreEqual((r[6]>>8)&31,f.AlphaB);Assert.AreEqual(r[7],f.AnimationArguments[7]);Assert.AreEqual(r[2]==0,(bool)type.GetProperty("Finished")!.GetValue(task)!);}
        }
    }
    [TestMethod]
    public void FlashTaskMatchesOriginalSourceRequestsAndActiveFadeRefusal()
    {
        using var json=SourceVectorFixture.Read("conversion-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_conversion_vectors.py --root . --output tests/Game.Presentation.Tests/Fixtures/conversion-vectors.json");
        var rows=json.RootElement.GetProperty("flash").EnumerateArray().Select(r=>r.EnumerateArray().Select(v=>v.GetInt32()).ToArray()).ToArray();
        foreach(int scenario in new[]{0,1})
        {
            var script=AnimationCompositionFixture.Script(160);var o=Runtime(script);Invoke(o,"LoadSpriteGraphics","ANIM_TAG_CONVERSION");
            var fadeField=typeof(SourceBattleAnimation).GetField("_fade",BindingFlags.Instance|BindingFlags.NonPublic)!;var fadeType=typeof(SourceBattleAnimation).GetNestedType("PaletteFade",BindingFlags.NonPublic)!;var type=typeof(SourceBattleAnimation).GetNestedType("SpritePaletteFlashTask",BindingFlags.NonPublic)!;object? task=null;
            foreach(var r in rows.Where(r=>r[0]==scenario))
            {
                object? busy=r[9]==0?null:Activator.CreateInstance(fadeType,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[1u,0,0,(ushort)0,0],null);fadeField.SetValue(o,busy);
                if(r[1]==0)task=Activator.CreateInstance(type,BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic,null,[new[]{script.SpriteTags["ANIM_TAG_CONVERSION"],1,1,14335,12,0,0},o],null);
                else Invoke(task!,"Step",o);
                Assert.AreEqual(r[2]==0,(bool)type.GetProperty("Finished")!.GetValue(task)!);Assert.AreEqual(r[3],Convert.ToInt32(Field(task!,"_delay")));Assert.AreEqual(r[4],Convert.ToInt32(Field(task!,"_toggle")));Assert.AreEqual(r[5],Convert.ToInt32(Field(task!,"_remaining")));
                var after=fadeField.GetValue(o);if(r[6]>0&&r[9]==0){Assert.IsNotNull(after);Assert.AreEqual(r[7],(int)Field(after,"_y"));Assert.AreEqual(r[8],Convert.ToInt32(Field(after,"_color")));}else Assert.AreSame(busy,after);
            }
        }
    }
    [TestMethod][DataRow(160)][DataRow(176)]
    public void CompleteSourceRootsComposeAllSixSelfLayoutsAndCleanAlpha(int move)
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(move);
        Assert.AreEqual(8,resolved.Assets["conversion"].Width);Assert.AreEqual(32,resolved.Assets["conversion"].Height);Assert.AreEqual(256,resolved.Assets["conversion"].PaletteIndices.Length);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,new ushort[240*112]);foreach(var mon in b)assets[mon.Asset]=new(64,64,Enumerable.Repeat((ushort)0xffff,4096).ToArray());
            var o=new SourceBattleAnimation(resolved.Script,new("conversion",1,move,new(actor),new(actor)),b,doubles,new Audio());var composer=AnimationCompositionFixture.Create(resources,resolved,assets,b);int ticks=0,peak=0;
            while(o.Active&&ticks++<1024){var f=o.Tick();peak=Math.Max(peak,f.Sprites.Length);Assert.AreEqual(240*112*4,composer.ComposeField(f).Length);}
            Assert.IsFalse(o.Active);Assert.AreEqual(16,peak);Assert.IsFalse(o.Snapshot().BlendEnabled);Assert.AreEqual(0,o.ActiveVisualCount);
        }
    }
    [TestMethod]
    public void NormalSpritePaletteFadeUsesUnfadedBaselineAfterDirectWritesAndRetainsFreedBytes()
    {
        var script=AnimationCompositionFixture.Script(160);var o=Runtime(script);Invoke(o,"LoadSpriteGraphics","ANIM_TAG_CONVERSION");int tag=script.SpriteTags["ANIM_TAG_CONVERSION"],slot=(int)Invoke(o,"FindSpritePalette",tag)!;var original=script.SpritePalettes[tag];
        Invoke(o,"WriteFadedSpritePalette",slot,1,new ushort[]{31});Invoke(o,"BlendPalettes",1u<<(slot+16),12,(ushort)14335);
        var first=o.Snapshot();CollectionAssert.AreEqual(original.Select(c=>SourcePaletteMath.Blend(c,12,14335)).ToArray(),first.SpritePaletteSlots[slot]);
        Invoke(o,"FreeSpritePalette",tag);Invoke(o,"AllocateSpritePalette",12345);Invoke(o,"BlendPalettes",1u<<(slot+16),0,(ushort)0);
        CollectionAssert.AreEqual(original,o.Snapshot().SpritePaletteSlots[slot]);Assert.AreNotEqual(first.SpritePaletteSlots[slot][1],o.Snapshot().SpritePaletteSlots[slot][1]);o.Cancel();Assert.IsTrue(o.Snapshot().SpritePaletteSlots.All(p=>p.All(c=>c==0)));
    }
    [TestMethod]
    public void MissingIndexedFlashPaletteAndNegativePauseAreRejected()
    {
        var script=AnimationCompositionFixture.Script(160);var noPalette=script with{SpritePalettes=new Dictionary<int,ushort[]>(),Commands=[C("createvisualtask","AnimTask_FlashAnimTagWithColor","2","ANIM_TAG_CONVERSION","1","1","14335","12","0","0"),C("end")]};
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(noPalette,new("bad",1,160,new(0),new(0)),Battlers(),true,new Audio()));
        var negative=AnimationCompositionFixture.Script(176) with{Commands=[C("createsprite","gConversion2SpriteTemplate","ANIM_ATTACKER","2","0","0","-1"),C("end")]};
        Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(negative,new("bad",1,176,new(0),new(0)),Battlers(),true,new Audio()));
    }
    [TestMethod]
    public void NewFadeDoesNotRecolorRetiredOwnersMaterialPalette()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var scene=new SourceBattleAnimation.SpriteScene();var b=Battlers();
        var oldScript=resources.Scripts[309] with{Commands=[C("loadspritegfx","ANIM_TAG_GOLD_STARS"),C("createsprite","gMeteorMashStarSpriteTemplate","ANIM_TARGET","2","0","0","0","0","30"),C("waitforvisualfinish"),C("end")],Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),Labels=new Dictionary<string,int>()};
        var resolved=resources.Resolve(oldScript);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);int tag=resolved.Script.SpriteTags["ANIM_TAG_GOLD_STARS"];ushort[] red=new ushort[16];red[1]=31;var gold=assets["gold-stars"];assets["gold-stars"]=gold with{PaletteTag=tag,PaletteIndices=Enumerable.Repeat((byte)1,gold.Pixels.Length).ToArray(),Palette=red};
        var old=new SourceBattleAnimation(resolved.Script with{SpritePalettes=new Dictionary<int,ushort[]>{{tag,red}}},new("palettes",1,309,new(0),new(1)){VisualSeed=0x12345678},b,true,new Audio(),spriteScene:scene,spriteMaterials:assets);
        int ticks=0;while(old.Active&&ticks++<50)old.Tick();Assert.IsFalse(old.Active);Assert.IsTrue(old.HasResidualSprites);
        var nextResolved=resources.Resolve(160);var next=new SourceBattleAnimation(nextResolved.Script with{Commands=[C("delay","100"),C("end")]},new("palettes",2,160,new(0),new(0)),b,true,new Audio(),spriteScene:scene,spriteMaterials:nextResolved.Assets);
        Invoke(next,"LoadSpriteGraphics","ANIM_TAG_CONVERSION");int slot=(int)Invoke(next,"FindSpritePalette",nextResolved.Script.SpriteTags["ANIM_TAG_CONVERSION"])!;Assert.AreEqual(4,slot);Invoke(next,"BlendPalettes",1u<<(slot+16),16,(ushort)(31<<10));
        var frame=next.Tick();var stars=frame.Sprites.Where(x=>x.Template=="gMiniTwinklingStarSpriteTemplate").ToArray();Assert.IsTrue(stars.Length>0);Assert.IsTrue(stars.All(x=>x.MaterialPalette![1]==31));Assert.AreEqual((ushort)(31<<10),frame.SpritePaletteSlots[4][1]);scene.Cancel();
    }
    [TestMethod]
    public void PaletteUpdateAfterOamBindsTheSameFrameAndKeepsEarlierSnapshotImmutable()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(160);var script=resolved.Script with{MoveId=0,Label="General_ConversionPalettePhase",Commands=[C("loadspritegfx","ANIM_TAG_CONVERSION"),C("createsprite","gConversionSpriteTemplate","ANIM_ATTACKER","2","0","0"),C("delay","0"),C("createvisualtask","AnimTask_FlashAnimTagWithColor","2","ANIM_TAG_CONVERSION","1","1","14335","12","0","0"),C("delay","100"),C("setarg","7","0xFFFF"),C("waitforvisualfinish"),C("end")]};
        var o=new SourceBattleAnimation(script,new("palette-phase",1,0,new(0),new(0)){GeneralAnimationLabel="General_ConversionPalettePhase"},Battlers(),true,new Audio(),spriteMaterials:resolved.Assets);SourceAnimationFrame? before=null;object? fade=null;
        for(int tick=0;tick<16;tick++){var f=o.Tick();fade=typeof(SourceBattleAnimation).GetField("_fade",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(o);if(f.Sprites.Length>0&&fade is not null&&(int)Field(fade,"_toggle")==1){before=f;break;}}
        Assert.IsNotNull(before);var original=resolved.Script.SpritePalettes[resolved.Script.SpriteTags["ANIM_TAG_CONVERSION"]];CollectionAssert.AreEqual(original,before.Sprites.Single().MaterialPalette);
        var after=o.Tick();var expected=original.Select(c=>SourcePaletteMath.Blend(c,12,14335)).ToArray();CollectionAssert.AreEqual(expected,after.Sprites.Single().MaterialPalette);CollectionAssert.AreEqual(expected,after.SpritePaletteSlots[4]);CollectionAssert.AreEqual(original,before.Sprites.Single().MaterialPalette);Assert.AreEqual(before.Sprites.Single().Position,after.Sprites.Single().Position);o.Cancel();
    }

}
