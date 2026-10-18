using System.Text.Json.Nodes;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SpritePaletteGroupTests
{
    [TestMethod]
    public void PaletteGroupsAreSelectedByCanonicalAssetAndValidateIndependentBanks()
    {
        string source=Path.Combine(TestAssets.Root,"content/generated/battle/animations");
        using var temporary=TestAssets.TemporaryOutput();string folder=temporary.Path;
        foreach(string file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories))
        {string target=Path.Combine(folder,Path.GetRelativePath(source,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);}
        using var original=new SourceAnimationResources(source);var asset=original.Resolve(47).Assets["music-notes"];int tag=asset.PaletteTag;
        byte[] bytes=new byte[96];for(int bank=0;bank<3;bank++)for(int i=0;i<16;i++)System.Buffers.Binary.BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(bank*32+i*2),bank==0?asset.Palette[i]:(ushort)(bank*128+i));
        string fileName="test-palettes.bin",path=Path.Combine(folder,"elemental.json");var json=JsonNode.Parse(File.ReadAllText(path))!;
        json["paletteGroups"]=new JsonArray(new JsonObject{{"tag",tag},{"path",fileName},{"banks",3}});File.WriteAllText(path,json.ToJsonString());File.WriteAllBytes(Path.Combine(folder,fileName),bytes);
        using(var resources=new SourceAnimationResources(folder))
        {
            Assert.AreEqual(0,resources.Resolve(1).Script.SpritePaletteGroups.Count);
            var groups=resources.Resolve(47).Script.SpritePaletteGroups;Assert.AreEqual(1,groups.Count);Assert.AreEqual(3,groups[tag].Length);
            CollectionAssert.AreEqual(asset.Palette,groups[tag][0]);Assert.AreEqual((ushort)271,groups[tag][2][15]);
            groups[tag][1][0]=0;Assert.AreEqual((ushort)128,resources.Resolve(47).Script.SpritePaletteGroups[tag][1][0],"Resolved groups must detach mutable callback input.");
        }
        foreach(var malformed in new[]{bytes[..^1],new byte[96],bytes.Select((b,i)=>i==33?(byte)128:b).ToArray()})
        {
            File.WriteAllBytes(Path.Combine(folder,fileName),malformed);using var invalid=new SourceAnimationResources(folder);
            Assert.Throws<InvalidDataException>(()=>invalid.Resolve(47));
        }
        File.WriteAllBytes(Path.Combine(folder,fileName),bytes);
        foreach(string badPath in new[]{"../test-palettes.bin",""})
        {json["paletteGroups"]![0]!["path"]=badPath;File.WriteAllText(path,json.ToJsonString());Assert.Throws<InvalidDataException>(()=>new SourceAnimationResources(folder));}
    }
    private sealed class Audio:ISourceAnimationAudio{public bool IsPlaying=>false;public void Play(SourceAnimationSound sound){}public void StopAnimationEffects(){}}
    [TestMethod]
    public void UnloadFreesTagWithoutASecondLoadWaitAndKeepsPreviousFrameDetached()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));var resolved=resources.Resolve(47);
        var script=resolved.Script with{Commands=[new("loadspritegfx",["ANIM_TAG_MUSIC_NOTES"],1),new("unloadspritegfx",["ANIM_TAG_MUSIC_NOTES"],2),new("setarg",["0","9"],3),new("delay",["1"],4),new("end",[],5)],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
        var bodies=new[]{new AnimationBattler(new(0),"a",0,0,64,64),new AnimationBattler(new(1),"b",0,0,64,64)};
        var owner=new SourceBattleAnimation(script,new("unload",1,47,new(0),new(1)),bodies,false,new Audio());var loaded=owner.Tick();Assert.IsTrue(owner.GraphicsLoaded);Assert.AreEqual(1,loaded.SpritePalettes.Count);
        owner.Tick();owner.Tick();Assert.IsTrue(owner.GraphicsLoaded);var unloaded=owner.Tick();Assert.IsFalse(owner.GraphicsLoaded);Assert.AreEqual(0,unloaded.SpritePalettes.Count);Assert.AreEqual((short)9,unloaded.AnimationArguments[0]);
        Assert.AreEqual(1,loaded.SpritePalettes.Count);CollectionAssert.AreEqual(loaded.SpritePaletteSlots[4],unloaded.SpritePaletteSlots[4],"Free changes ownership, retaining bank bytes.");owner.Cancel();
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script with{Commands=[new("unloadspritegfx",["unknown"],1),new("end",[],2)]},new("bad",1,47,new(0),new(1)),bodies,false,new Audio()));
    }
    [TestMethod]
    public void SimplePaletteUsesSourceAnimationBanksButRejectsOtherSelectorBits()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        var resolved=resources.Resolve(312);
        SourceAnimationScript Script(string selector)=>resolved.Script with
        {
            Commands=[new("createsprite",["gSimplePaletteBlendSpriteTemplate","ANIM_ATTACKER","0",selector,"0","0","1","RGB_WHITE"],1),new("waitforvisualfinish",[],2),new("end",[],3)],
            Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()
        };
        var bodies=new[]{new AnimationBattler(new(0),"a",0,0,64,64),new AnimationBattler(new(1),"b",0,0,64,64)};
        var owner=new SourceBattleAnimation(Script("F_PAL_ANIM_1 | F_PAL_ANIM_2"),new("banks",1,312,new(0),new(0)),bodies,false,new Audio());
        for(int tick=0;tick<32&&owner.Active;tick++)owner.Tick();Assert.IsFalse(owner.Active);var frame=owner.Snapshot();
        CollectionAssert.AreEquivalent(new[]{8,9},frame.PaletteBlends.Select(x=>x.Palette).ToArray());
        Assert.IsTrue(frame.PaletteBlends.All(x=>x.Coefficient==1));owner.Cancel();
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(Script("128"),new("bad-banks",1,312,new(0),new(0)),bodies,false,new Audio()));
    }

}
