using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;
[TestClass,TestCategory("Game")]
public sealed class MusicPaletteTests
{
    private sealed class Audio:ISourceAnimationAudio{public bool IsPlaying=>false;public void Play(SourceAnimationSound c){}public void SetPan(int p,int f){}public void StopAnimationEffects(){}}
    private static object Field(object target,string name)=>target.GetType().GetField(name,BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(target)!;
    private static object? Invoke(object target,string name,params object[] args)=>target.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance)!.Invoke(target,args);
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceBattleAnimation Runtime(SourceAnimationScript script)=>new(script,new("music-palettes",1,script.MoveId,new(0),new(1)),[new(new(0),"a",8,0,64,64),new(new(1),"b",8,0,64,64)],false,new Audio());
    [TestMethod]
    public void OriginalCAllocationFreeAndRainbowWritesPreserveDuplicateTagsExhaustionAndRetainedBytes()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var script=resources.Resolve(47).Script;
        using var json=SourceVectorFixture.Read("music-palette-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_music_palette_vectors.py --reference content/raw/pokefirered/037335f4 --helper tests/Reference/reference_c.py --output tests/Game.Presentation.Tests/Fixtures/music-palette-source-vectors.json");
        var rows=json.RootElement.GetProperty("rows").EnumerateArray().Select(r=>r.EnumerateArray().Select(x=>x.GetInt32()).ToArray()).ToArray();
        Assert.AreEqual(20,rows.Length);
        for(int scenario=0;scenario<5;scenario++)
        {
            var runtime=Runtime(script);var tags=(int[])Field(runtime,"_spritePaletteTags");var banks=(ushort[][])Field(runtime,"_spritePaletteBanks");var initial=rows.Single(r=>r[0]==scenario&&r[1]==0);
            Array.Copy(initial,3,tags,0,16);for(int bank=0;bank<16;bank++)for(int i=0;i<16;i++)banks[bank][i]=(ushort)initial[19+bank*16+i];
            for(int step=1;step<4;step++)
            {
                if(step<3)Invoke(runtime,"TryCreateMusicTask",C("createvisualtask",step==1?"AnimTask_MusicNotesRainbowBlend":"AnimTask_MusicNotesClearRainbowBlend","2"));
                int result=step==3?(int)Invoke(runtime,"AllocateSpritePalette",12345)!:(int)Invoke(runtime,"FindSpritePalette",script.SpriteTags["ANIM_TAG_BENT_SPOON"])!;
                var expected=rows.Single(r=>r[0]==scenario&&r[1]==step);Assert.AreEqual(expected[2],result);CollectionAssert.AreEqual(expected.Skip(3).Take(16).ToArray(),tags);
                CollectionAssert.AreEqual(expected.Skip(19).Select(v=>(ushort)v).ToArray(),banks.SelectMany(x=>x).ToArray());
            }
        }
    }
    [TestMethod]
    public void LoadedAliasesReserveOneBankAndSourceEndFreesFirstMatchingEntriesOnly()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var original=resources.Resolve(47).Script;
        var tags=new Dictionary<string,int>(original.SpriteTags){{"ALIAS",original.SpriteTags["ANIM_TAG_MUSIC_NOTES"]},{"RGB_ONLY",12345}};
        var runtime=Runtime(original with{SpriteTags=tags,Commands=[C("end")]});
        Invoke(runtime,"LoadSpriteGraphics","RGB_ONLY");Invoke(runtime,"LoadSpriteGraphics","ANIM_TAG_MUSIC_NOTES");Invoke(runtime,"LoadSpriteGraphics","ALIAS");
        Assert.AreEqual(4,(int)Invoke(runtime,"FindSpritePalette",12345)!);int tag=tags["ALIAS"];Assert.AreEqual(5,(int)Invoke(runtime,"FindSpritePalette",tag)!);
        CollectionAssert.AreEqual(original.SpritePalettes[tag],runtime.Snapshot().SpritePalettes[tag]);
        Assert.AreEqual(6,(int)Invoke(runtime,"AllocateSpritePalette",tag)!);Assert.AreEqual(7,(int)Invoke(runtime,"AllocateSpritePalette",54321)!);
        var before=runtime.Snapshot();runtime.Tick();var after=runtime.Snapshot();Assert.IsFalse(after.SpritePalettes.ContainsKey(tag));Assert.IsFalse(after.SpritePalettes.ContainsKey(12345));Assert.IsTrue(after.SpritePalettes.ContainsKey(54321));
        for(int slot=0;slot<16;slot++)CollectionAssert.AreEqual(before.SpritePaletteSlots[slot],after.SpritePaletteSlots[slot]);runtime.Cancel();Assert.IsTrue(runtime.Snapshot().SpritePaletteSlots.All(bank=>bank.All(c=>c==0)));
    }
    [TestMethod]
    public void ExplicitSlotSamplingSurvivesFreeAndObservesReallocationWithTransparentIndexZero()
    {
        var asset=new SourcePixelAsset(8,8,new ushort[64]){PaletteIndices=Enumerable.Range(0,64).Select(i=>(byte)(i==0?0:3)).ToArray(),Palette=new ushort[16],PaletteTag=123};
        var assets=new Dictionary<string,SourcePixelAsset>{{"background",new(240,112,Enumerable.Repeat((ushort)0x8000,240*112).ToArray())},{"impact",new(32,32,new ushort[1024])},{"music",asset}};var composer=new SourceBattleFrameComposer(assets,[]);
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var runtime=Runtime(resources.Resolve(47).Script with{Commands=[C("end")]});
        int slot=(int)Invoke(runtime,"AllocateSpritePalette",123)!;var colors=new ushort[16];colors[0]=32767;colors[3]=31;Invoke(runtime,"WriteFadedSpritePalette",slot,0,colors);
        Invoke(runtime,"FreeSpritePalette",123);Assert.IsFalse(runtime.Snapshot().SpritePalettes.ContainsKey(123));var slots=runtime.Snapshot().SpritePaletteSlots;
        var sprite=new SourceVisualSpriteFrame(4,"music",new(24,24),default,0,true,false,false,256,"music",2){Width=8,Height=8,PaletteSlot=slot};
        var frame=new SourceAnimationFrame(0,true,new PixelPoint[4],[],0,0,[],[]){Sprites=[sprite],SpritePaletteSlots=slots};
        var pixels=composer.ComposeField(frame);int start=(20*240+20)*4;Assert.AreEqual((byte)0,pixels[start]);Assert.AreEqual((byte)255,pixels[start+4]);
        Assert.AreEqual(slot,(int)Invoke(runtime,"AllocateSpritePalette",321)!);colors[3]=31<<10;Invoke(runtime,"WriteFadedSpritePalette",slot,0,colors);frame=frame with{SpritePaletteSlots=runtime.Snapshot().SpritePaletteSlots};pixels=composer.ComposeField(frame);Assert.AreEqual((byte)0,pixels[start+4]);Assert.AreEqual((byte)255,pixels[start+6]);Assert.AreEqual((ushort)31,slots[slot][3],"Earlier frame snapshots retain their own palette bytes.");
        Assert.ThrowsExactly<InvalidDataException>(()=>composer.ComposeField(frame with{SpritePaletteSlots=[]}));
    }
}
