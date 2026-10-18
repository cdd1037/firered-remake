using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class FrozenCubeAnimationTests
{
    private const BindingFlags Flags=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private const string Template="sFrozenIceCubeSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio{public readonly List<SourceAnimationSound>Cues=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects(){}public void SetPan(int p,int f){}}
    private sealed class Occupied(int slot):SourceBattleAnimation.VisualSprite(slot,100){public override bool Counted=>false;public override SourceVisualSpriteFrame Frame=>new(Slot,"external",default,default,0,false,false,false,256,null,100);public override void Step(SourceBattleAnimation o){}}
    private static T Field<T>(object o,string n)=>(T)o.GetType().GetField(n,Flags)!.GetValue(o)!;
    private static object? Call(object o,string n,params object?[] args)=>o.GetType().GetMethod(n,Flags)!.Invoke(o,args);
    private static object[] Tasks(SourceBattleAnimation o)=>Field<IList>(o,"_tasks").Cast<object>().ToArray();
    private static SourceBattleAnimation.VisualSprite[] Sprites(SourceBattleAnimation o)=>((IEnumerable)Field<object>(o,"_sprites")).Cast<SourceBattleAnimation.VisualSprite>().ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static SourceAnimationScript Minimal(SourceAnimationScript s,params SourceAnimationCommand[] commands)=>s with{Commands=commands,Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static AnimationBattler[] Bodies(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],64,64)).ToArray();
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static int[] Values(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static SourceBattleAnimation New(SourceAnimationScript s,int actor=0,int target=1,bool doubles=false,Audio? audio=null,SourceBattleAnimation.SpriteScene? scene=null,IReadOnlyDictionary<string,SourcePixelAsset>? assets=null)=>new(s,new("frozen-cube",1,329,new(actor),new(target)),Bodies(doubles),doubles,audio??new Audio(),spriteScene:scene,spriteMaterials:assets);
    [TestMethod]
    public void EveryFrozenCubeSourceFrameMatchesRawLifetimeAllColorsAndRegisterWrites()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(329);
        using var doc=SourceVectorFixture.Read("frozen-cube-source-vectors.json","python tests/Game.Presentation.Tests/Reference/generate_frozen_cube_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");int cases=0,frames=0;
        foreach(var entry in doc.RootElement.GetProperty("cases").EnumerateObject())
        {
            int[] key=entry.Name.Split(':').Select(int.Parse).ToArray(),config=Values(doc.RootElement.GetProperty("configs")[key[3]]);bool doubles=key[0]!=0;var o=New(Minimal(resolved.Script,C("end")),key[1],key[2],doubles,assets:resolved.Assets);
            for(int slot=4;slot<config[0];slot++)Assert.AreEqual(slot,Call(o,"AllocateSpritePalette",60000+slot));Call(o,"LoadSpriteGraphics","ANIM_TAG_ICE_CUBE");
            var seeded=Enumerable.Range(0,16).Select(i=>(ushort)((((16+config[0])*16+i)*997)&32767)).ToArray();Call(o,"LoadSpritePaletteRange",config[0],0,seeded);
            int count=doubles?4:2;if(config[1]==1)for(int slot=count;slot<63;slot++)Call(Field<object>(o,"_sprites"),"Add",new Occupied(slot));
            var affine=o.Snapshot().BattlerAffine;Assert.AreEqual(true,Call(o,"TryCreateFrozenCubeTask",C("createvisualtask","AnimTask_FrozenIceCube","2")));var task=Tasks(o).Single();SourceAnimationFrame? retained=null;ushort[]? retainedPalette=null;
            foreach(var expected in entry.Value.EnumerateArray())
            {
                int[] state=Values(expected.GetProperty("state"));if(state[0]>=0)Call(o,"AdvanceSourceSprites");Call(o,"SortSourceSprites",null,false,null);if(state[0]>=0)Call(o,"RunSourceTasks");var frame=o.Snapshot();string why=entry.Name+" tick "+state[0];
                Assert.AreEqual(state[1],Tasks(o).Length,why);Assert.AreEqual(state[2],Sprites(o).Count(s=>s.Frame.Template==Template),why);Assert.AreEqual(state[3],frame.BlendEnabled?64|frame.BlendFirstTargets|(frame.BlendSecondTargets<<8):0,why);Assert.AreEqual(state[4],Field<int>(o,"_alphaA")|(Field<int>(o,"_alphaB")<<8),why);Assert.AreEqual(count,state[5],why);Assert.AreEqual(0,state[6],why);
                CollectionAssert.AreEqual(affine,frame.BattlerAffine,why);foreach(var pair in new[]{("_timer",7),("_paletteTick",8),("_rotations",9),("_cycles",10)})Assert.AreEqual(state[pair.Item2],Field<int>(task,pair.Item1),why+pair.Item1);
                var actual=frame.Sprites.Where(s=>s.Template==Template).ToArray();var sprites=expected.GetProperty("sprites").EnumerateArray().ToArray();Assert.AreEqual(sprites.Length,actual.Length,why);
                for(int i=0;i<actual.Length;i++){var f=actual[i];int[] s=Values(sprites[i]);CollectionAssert.AreEqual(s,new[]{f.Slot,f.Position.X,f.Position.Y,f.Offset.X,f.Offset.Y,f.Visible?1:0,f.Tile,f.OamPriority,f.Subpriority,f.Blend?1:0},why);Assert.IsFalse(f.Affine);Assert.AreEqual(4,f.Subsprites.Length);Assert.AreEqual(64,f.Width);Assert.AreEqual(64,f.Height);}
                int[] palette=Values(expected.GetProperty("palette"));var unfaded=Field<ushort[][]>(o,"_spritePaletteUnfadedBanks")[config[0]];
                for(int i=0;i<16;i++){Assert.AreEqual(palette[2*i],(int)unfaded[i],why+" unfaded "+i);Assert.AreEqual(palette[2*i+1],(int)frame.SpritePaletteSlots[config[0]][i],why+" faded "+i);}
                if(key[0]==1&&key[1]==0&&key[3]==0&&new[]{-1,7,9,26,29,53,66,102,104}.Contains(state[0]))AssertEverySubspritePixel(resolved,frame,palette,state[4],sprites,why);
                if(retained is null&&state[0]>=27){retained=frame;retainedPalette=(ushort[])frame.SpritePaletteSlots[config[0]].Clone();}if(retained is not null)CollectionAssert.AreEqual(retainedPalette!,retained.SpritePaletteSlots[config[0]],why+" detached palette");frames++;
            }
            CollectionAssert.AreEqual(seeded,o.Snapshot().SpritePaletteSlots[config[0]]);o.Cancel();cases++;
        }
        Assert.AreEqual(doc.RootElement.GetProperty("coverage").GetProperty("caseCount").GetInt32(),cases);Assert.AreEqual(doc.RootElement.GetProperty("coverage").GetProperty("frames").GetInt32(),frames);Assert.IsTrue(cases>=22&&frames>=2332);
    }
    private static void AssertEverySubspritePixel(SourceResolvedAnimation resolved,SourceAnimationFrame frame,int[] palette,int alpha,JsonElement[] sprites,string why)
    {
        // Independent GBA 1D tile addressing, literal source quadrants, alpha math
        // and clipping. The import check separately proves every source PNG byte.
        const ushort background=0x8421;var bodies=frame.Battlers.Select(b=>b with{Visible=false,SourceVisibility=null}).ToArray();var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat(background,240*112).ToArray());foreach(var b in bodies)assets[b.Asset]=new(64,64,new ushort[4096]);
        var clean=frame with{Battlers=bodies,BattlerVisibility=[],BackgroundSlots=[],StaticBackgroundCopies=[],MoveBackground=null};var actual=new SourceBattleFrameComposer(assets,bodies).ComposeField(clean);var expected=Enumerable.Repeat(background,240*112).ToArray();byte[] tiles=File.ReadAllBytes(Path.Combine(Folder,"physical-ice-cube.4bpp"));
        foreach(var row in sprites)
        {
            int[] sprite=Values(row);foreach(var part in new[]{(-16,-16,64,64,0),(-16,48,64,32,64),(48,-16,32,64,96),(48,48,32,32,128)})
            {
                int left=(sprite[1]+part.Item1)&511,top=(sprite[2]+part.Item2)&255;if(left>255)left-=512;if(top>=160)top-=256;
                for(int y=0;y<part.Item4;y++)for(int x=0;x<part.Item3;x++)
                {
                    int dx=left+x,dy=top+y;if(dx<0||dx>=240||dy<0||dy>=112)continue;int tile=part.Item5+(y/8)*(part.Item3/8)+x/8,pixel=tile*64+(y%8)*8+x%8,index=(tiles[pixel/2]>>((pixel%2)*4))&15;if(index==0)continue;int color=palette[index*2+1],result=0;
                    for(int channel=0;channel<3;channel++){int a=(color>>(channel*5))&31,b=(background>>(channel*5))&31;result|=Math.Min(31,(a*Math.Min(16,alpha&31)+b*Math.Min(16,(alpha>>8)&31))>>4)<<(channel*5);}expected[dy*240+dx]=(ushort)(result|0x8000);
                }
            }
        }
        var rgba=new byte[expected.Length*4];for(int i=0;i<expected.Length;i++){rgba[i*4]=(byte)((expected[i]&31)*255/31);rgba[i*4+1]=(byte)(((expected[i]>>5)&31)*255/31);rgba[i*4+2]=(byte)(((expected[i]>>10)&31)*255/31);rgba[i*4+3]=255;}CollectionAssert.AreEqual(rgba,actual,why+" every source subsprite pixel");
    }
    [TestMethod,DataRow(false,0),DataRow(false,1),DataRow(true,0),DataRow(true,1),DataRow(true,2),DataRow(true,3)]
    public void CompleteRootRestoresIceBackgroundAudioAndUncountedChild(bool doubles,int actor)
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(329);var bodies=Bodies(doubles);var assets=r.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,new ushort[240*112]);foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray());var composer=AnimationCompositionFixture.Create(resources,r,assets,bodies);var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=New(r.Script,actor,actor^1,doubles,audio,scene,assets);int frames=0,rawFrames=0;bool ice=false;var affine=o.Snapshot().BattlerAffine;
        while(o.Active&&frames++<350){var f=o.Tick();Assert.AreEqual(240*112*4,composer.ComposeField(f).Length);ice|=f.MoveBackground?.Id==r.Script.BackgroundSymbols["BG_ICE"];rawFrames+=f.Sprites.Count(s=>s.Template==Template);CollectionAssert.AreEqual(affine,f.BattlerAffine);Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(o.Snapshot()));}
        Assert.IsFalse(o.Active);Assert.IsTrue(ice&&rawFrames>95);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,o.ActiveVisualCount);Assert.IsFalse(o.GraphicsLoaded);var end=o.Snapshot();Assert.IsFalse(end.BlendEnabled);Assert.AreEqual(0,end.BackgroundSlots.Length);Assert.IsNull(end.MoveBackground?.Id);CollectionAssert.AreEqual(new[]{"SE_M_ICY_WIND","SE_M_HAIL"},audio.Cues.Select(c=>c.Id).ToArray());Assert.AreEqual(0,audio.Cues[0].Pan);Assert.AreEqual(actor%2==0?63:-63,audio.Cues[1].Pan);Assert.IsFalse(scene.HasResidualSprites);
    }
    [TestMethod]
    public void CancellationAtEveryPhaseDropsOnlyOwnedRawSpriteAndDetachedSnapshotsStayStable()
    {
        using var resources=new SourceAnimationResources(Folder);var r=resources.Resolve(329);
        foreach(int tick in new[]{0,9,28,56,72,102}){var scene=new SourceBattleAnimation.SpriteScene();var o=New(Minimal(r.Script,C("end")),scene:scene,assets:r.Assets);Call(o,"LoadSpriteGraphics","ANIM_TAG_ICE_CUBE");Call(o,"TryCreateFrozenCubeTask",C("createvisualtask","AnimTask_FrozenIceCube","2"));for(int i=0;i<tick;i++){Call(o,"AdvanceSourceSprites");Call(o,"SortSourceSprites",null,false,null);Call(o,"RunSourceTasks");}var held=o.Snapshot();string serialized=JsonSerializer.Serialize(held);o.Cancel();Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,Tasks(o).Length);Assert.IsFalse(o.Snapshot().BlendEnabled);Assert.AreEqual(serialized,JsonSerializer.Serialize(held));}
    }
    [TestMethod]
    public void MissingRecipeChangedPiecesPaletteAndCompetingOwnersFailClosed()
    {
        using var resources=new SourceAnimationResources(Folder);var source=resources.Resolve(329).Script;var task=C("createvisualtask","AnimTask_FrozenIceCube","2");SourceAnimationCommand[] Good()=>[C("loadspritegfx","ANIM_TAG_ICE_CUBE"),task,C("waitforvisualfinish"),C("end")];
        foreach(string p in new[]{"-1","256"})Assert.ThrowsExactly<SourceAnimationException>(()=>New(Minimal(source,C("loadspritegfx","ANIM_TAG_ICE_CUBE"),C("createvisualtask","AnimTask_FrozenIceCube",p),C("waitforvisualfinish"),C("end"))));
        foreach(var commands in new[]{new[]{C("loadspritegfx","ANIM_TAG_ICE_CUBE"),C("unloadspritegfx","ANIM_TAG_ICE_CUBE"),task,C("waitforvisualfinish"),C("end")},new[]{task,C("waitforvisualfinish"),C("end")},new[]{C("loadspritegfx","ANIM_TAG_ICE_CUBE"),task,task,C("waitforvisualfinish"),C("end")},new[]{C("loadspritegfx","ANIM_TAG_ICE_CUBE"),task,C("blendoff"),C("waitforvisualfinish"),C("end")},new[]{C("loadspritegfx","ANIM_TAG_ICE_CUBE"),task,C("waitforvisualfinish"),C("unloadspritegfx","ANIM_TAG_ICE_CUBE"),C("end")}})Assert.ThrowsExactly<SourceAnimationException>(()=>New(Minimal(source,commands)));
        Assert.ThrowsExactly<SourceAnimationException>(()=>New(Minimal(source,Good()) with{SpriteDependencies=[]}));Assert.ThrowsExactly<SourceAnimationException>(()=>New(Minimal(source,Good()) with{SpritePalettes=new Dictionary<int,ushort[]>()}));
        var recipe=source.SpriteRecipes[Template];foreach(var bad in new[]{recipe with{Blend=false},recipe with{Subsprites=recipe.Subsprites.Take(3).ToArray()},recipe with{Subsprites=recipe.Subsprites.Select((p,i)=>i==3?p with{TileOffset=127}:p).ToArray()}}){var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[Template]=bad;Assert.ThrowsExactly<SourceAnimationException>(()=>New(Minimal(source,Good()) with{SpriteRecipes=recipes}));}
        var scene=new SourceBattleAnimation.SpriteScene();var owner=New(Minimal(source,C("end")),scene:scene);for(int slot=2;slot<44;slot++)Call(Field<object>(owner,"_sprites"),"Add",new Occupied(slot));Assert.AreEqual(42,scene.Count);owner.Tick();Assert.IsFalse(owner.Active);var exhausted=Assert.ThrowsExactly<SourceAnimationException>(()=>New(Minimal(source,Good()),scene:scene));StringAssert.Contains(exhausted.Message,"logical sprite or OAM capacity");scene.Cancel();
        var crowded=new[]{C("loadspritegfx","ANIM_TAG_ICE_CUBE"),task}.Concat(Enumerable.Repeat(C("waitplaysewithpan","SE_M_HAIL","0","1"),15)).Concat(new[]{C("waitforvisualfinish"),C("end")}).ToArray();var tooMany=Assert.ThrowsExactly<SourceAnimationException>(()=>New(Minimal(source,crowded)));StringAssert.Contains(tooMany.Message,"native task capacity");
        var reloaded=New(Minimal(source,C("loadspritegfx","ANIM_TAG_ICE_CUBE"),C("unloadspritegfx","ANIM_TAG_ICE_CUBE"),C("loadspritegfx","ANIM_TAG_ICE_CUBE"),task,C("waitforvisualfinish"),C("end")));reloaded.Cancel();
    }
}
