using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class RolePlayAnimationTests
{
    private const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound>Cues=[];public int Stops;public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects()=>Stops++;}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static T Field<T>(object o,string name)=>(T)o.GetType().GetField(name,Private)!.GetValue(o)!;
    private static object? Invoke(object o,string name,params object?[] args)=>o.GetType().GetMethod(name,Private|BindingFlags.Public)!.Invoke(o,args);
    private static int I(JsonElement e,string key)=>e.GetProperty(key).GetInt32();
    private static int[] N(JsonElement e)=>e.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static CommittedMoveAnimation Cue(int actor,int target)=>new("role-play",1,272,new(actor),new(target));
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,3,64,48)).ToArray();
    private static SourceBattlerFormMaterial Material(int actor,int y=8)=>new(new(new(actor),"additional-unown-letter",y,99,24,40),new(64,64,Enumerable.Range(0,4096).Select(i=>(ushort)(i%64>12&&i%64<51&&i/64>8&&i/64<55?0x83e0:0)).ToArray()));
    private static SourceAnimationScript Primitive(SourceAnimationScript s)=>s with{Commands=[C("delay","1"),C("end")],Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};

    [TestMethod]
    public void NativeRolePlayMatchesInitialAlphaThreeTickIncrementsNineMatricesAndDeferredOamTeardown()
    {
        using var vectors=SourceVectorFixture.Read("role-play-source-vectors.json", "python tests/Reference/RolePlay/generate.py content/raw/pokefirered/037335f4 --repo . --output tests/Game.Presentation.Tests/Fixtures/role-play-source-vectors.json");
        using var resources=new SourceAnimationResources(Folder);var script=Primitive(resources.Resolve(272).Script);int cases=0,rows=0;
        foreach(var c in vectors.RootElement.GetProperty("cases").EnumerateArray())
        {
            bool doubles=c.GetProperty("doubles").GetBoolean();int actor=I(c,"actor"),target=I(c,"target"),species=I(c,"species");
            int y=species==201?(actor%2==0?8:15):species==410?0:actor%2==0?11:7;
            var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(script,Cue(actor,target),Battlers(doubles),doubles,new Audio(),spriteScene:scene,additionalMonMaterial:Material(actor,y));
            var args=Field<short[]>(o,"_animationArgs");for(int i=0;i<args.Length;i++)args[i]=(short)(300+i);var before=(short[])args.Clone();
            Assert.IsTrue((bool)Invoke(o,"TryCreateRolePlayTask",C("createvisualtask","AnimTask_RolePlaySilhouette","2"))!);var copy=scene.Sprites.Single();
            foreach(var row in c.GetProperty("rows").EnumerateArray())
            {
                int tick=I(row,"frame");if(tick>0){Invoke(o,"AdvanceSourceSprites");Invoke(o,"SortSourceSprites",null,false,null);Invoke(o,"RunSourceTasks");}
                string why=c.GetProperty("id").GetString()+" frame "+tick;var f=o.Snapshot();var s=copy.Frame;var blend=N(row.GetProperty("blend"));
                Assert.AreEqual(I(row,"alive"),Field<IEnumerable>(o,"_tasks").Cast<object>().Count(),why+" task lifetime");Assert.AreEqual((blend[0]&64)!=0,f.BlendEnabled,why);Assert.AreEqual(blend[1]&255,f.AlphaA,why);Assert.AreEqual(blend[1]>>8,f.AlphaB,why);
                Assert.AreEqual(I(row,"copyAlive")==0,copy.Finished,why);Assert.AreEqual(I(row,"copyAlive"),scene.Count,why);CollectionAssert.AreEqual(N(row.GetProperty("position")),new[]{s.Position.X,s.Position.Y},why);
                CollectionAssert.AreEqual(N(row.GetProperty("matrix")),new[]{s.MatrixA,s.MatrixB,s.MatrixC,s.MatrixD},why);Assert.AreEqual(I(row,"affineMode")==3,s.DoubleSize,why);Assert.IsTrue(s.Affine&&s.Blend);Assert.AreEqual(5,s.Subpriority);Assert.AreEqual(I(row,"priority"),s.OamPriority);Assert.IsFalse(copy.Counted);
                if(tick>0)
                {
                    Assert.AreEqual(I(row,"builtAlive"),f.Sprites.Length,why+" OAM lifetime");
                    if(f.Sprites.SingleOrDefault() is {} built)
                    {CollectionAssert.AreEqual(N(row.GetProperty("builtMatrix")),new[]{built.MatrixA,built.MatrixB,built.MatrixC,built.MatrixD},why);Assert.AreEqual(I(row,"builtAffine")==3,built.DoubleSize,why);Assert.IsNotNull(built.Material);Assert.IsTrue(built.Material.Pixels.All(p=>p is 0 or 65535));Assert.IsTrue(built.MaterialPalette!.All(p=>p==32767));}
                }
                CollectionAssert.AreEqual(before,args,why);Assert.IsNull(Field<object?>(o,"_visualRandom"));rows++;
            }
            o.Cancel();cases++;
        }
        Assert.AreEqual(vectors.RootElement.GetProperty("profile").GetString()=="full"?84:9,cases);Assert.AreEqual(vectors.RootElement.GetProperty("profile").GetString()=="full"?3444:369,rows);
    }

    [TestMethod]
    public void CompleteRolePlayRootComposesBothSidesKeepsBaseGeometryAndRestoresAllOwners()
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(272);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            int target=actor^1;var bodies=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());assets.TryAdd("impact",new(32,32,new ushort[1024]));foreach(var b in bodies)assets[b.Asset]=Material(actor).Pixels;
            var material=Material(actor,15);var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var o=new SourceBattleAnimation(resolved.Script,Cue(actor,target),bodies,doubles,audio,spriteScene:scene,spriteMaterials:assets,additionalMonMaterial:material);
            bool seen=false,pixels=false,affine=false;int ticks=0;
            while(o.Active&&ticks++<400)
            {
                var f=o.Tick();var rgba=composer.ComposeField(f);
                foreach(var s in f.Sprites.Where(s=>s.Template=="AnimRolePlaySilhouette"))
                {seen=true;affine|=s.DoubleSize;Assert.AreEqual(bodies[actor].Base(doubles).Y+15,s.Position.Y,"Base species Y ignores bitmap letter geometry and enemy elevation.");Assert.AreEqual(-1,s.BattlerCopySlot);Assert.IsTrue(s.Material!.Pixels.All(p=>p is 0 or 65535));Assert.IsTrue(s.MaterialPalette!.All(p=>p==32767));pixels|=!rgba.SequenceEqual(composer.ComposeField(f with{Sprites=[]}));}
            }
            Assert.IsFalse(o.Active);Assert.IsTrue(seen&&pixels&&affine);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,o.ActiveVisualCount);Assert.IsFalse(o.Snapshot().BlendEnabled);Assert.AreEqual(0,o.Snapshot().BackgroundSlots.Length);Assert.IsFalse(o.GraphicsLoaded);Assert.IsFalse(o.HasResidualSprites);Assert.IsFalse(o.Snapshot().SpritePalettes.ContainsKey(55125));Assert.IsTrue(o.Snapshot().PaletteBlends.All(p=>p.Coefficient==0));Assert.IsNull(Field<object?>(o,"_visualRandom"));Assert.AreEqual(1,audio.Cues.Count(c=>c.Id=="SE_M_TRI_ATTACK"));Assert.AreEqual(1,audio.Cues.Count(c=>c.Id=="SE_M_DETECT"));
            foreach(int cancel in new[]{0,1,20,50,70,80,100,130})
            {var s=new SourceBattleAnimation.SpriteScene();var a=new Audio();var r=new SourceBattleAnimation(resolved.Script,Cue(actor,target),bodies,doubles,a,spriteScene:s,spriteMaterials:assets,additionalMonMaterial:material);for(int i=0;i<cancel&&r.Active;i++)r.Tick();r.Cancel();r.Cancel();Assert.AreEqual(0,s.Count);Assert.AreEqual(0,r.ActiveVisualCount);Assert.IsFalse(r.Snapshot().BlendEnabled);Assert.AreEqual(0,r.Snapshot().BackgroundSlots.Length);Assert.AreEqual(1,a.Stops);}
        }
    }

    [TestMethod]
    public void AdditionalMaterialIsDetachedAndCollisionsOrMissingInputsFailBeforeEffects()
    {
        using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(272).Script;var material=Material(0);material=material with{Pixels=material.Pixels with{PaletteIndices=new byte[4096],Palette=new ushort[16]}};var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();
        var o=new SourceBattleAnimation(script,Cue(0,1),Battlers(false),false,audio,spriteScene:scene,additionalMonMaterial:material);
        var selected=Field<SourceBattlerFormMaterial>(o,"_additionalMonMaterial");Assert.AreNotSame(material.Pixels.Pixels,selected.Pixels.Pixels);Assert.AreNotSame(material.Pixels.PaletteIndices,selected.Pixels.PaletteIndices);Assert.AreNotSame(material.Pixels.Palette,selected.Pixels.Palette);material.Pixels.PaletteIndices[0]=1;material.Pixels.Palette[0]=31;Assert.AreEqual((byte)0,selected.Pixels.PaletteIndices[0]);Assert.AreEqual((ushort)0,selected.Pixels.Palette[0]);ushort original=selected.Pixels.Pixels[1024+20];material.Pixels.Pixels[1024+20]=0;Assert.AreEqual(original,selected.Pixels.Pixels[1024+20]);o.Cancel();
        Assert.Throws<ArgumentException>(()=>new SourceBattleAnimation(script,Cue(0,1),Battlers(false),false,new Audio(),spriteMaterials:new Dictionary<string,SourcePixelAsset>{{material.Geometry.Asset,material.Pixels}},additionalMonMaterial:material));
        var blankScene=new SourceBattleAnimation.SpriteScene();var sound=new Audio();Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(0,1),Battlers(false),false,sound,spriteScene:blankScene));Assert.AreEqual(0,blankScene.Count);Assert.AreEqual(0,sound.Cues.Count);
        foreach(var bad in new[]{script with{Commands=[C("createvisualtask","AnimTask_RolePlaySilhouette","2","0"),C("end")]},script with{Commands=[C("createvisualtask","AnimTask_RolePlaySilhouette","2"),C("createvisualtask","AnimTask_RolePlaySilhouette","2"),C("end")]}})
            Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(bad,Cue(0,1),Battlers(false),false,new Audio(),additionalMonMaterial:Material(0)));
    }
    [TestMethod]
    public void SharedArtCatalogKeepsDeoxysBaseFrameSeparateFromOrdinaryRegionalDisplay()
    {
        var catalog=new SourceBattleFormCatalog(Path.Combine(TestAssets.Root,"content/generated/battle/art/campaign-hit-art"));
        foreach(string side in new[]{"front","back"})foreach(bool shiny in new[]{false,true}){Assert.IsTrue(catalog.TryBaseFrame(410,side,shiny,out var pixels));Assert.AreEqual(64,pixels.Width);Assert.AreEqual(64,pixels.Height);Assert.IsTrue(pixels.Pixels.Any(p=>(p&0x8000)!=0));}
        Assert.IsFalse(catalog.TryBaseFrame(25,"front",false,out _));Assert.IsTrue(catalog.HasForms(385));
    }

    [TestMethod]
    public void SourceProfileRetainsExplicitSemanticAndBoundaryCases()
    {
        using var vectors=SourceVectorFixture.Read("role-play-source-vectors.json", "python tests/Reference/RolePlay/generate.py content/raw/pokefirered/037335f4 --repo . --output tests/Game.Presentation.Tests/Fixtures/role-play-source-vectors.json");
        Assert.IsTrue(vectors.RootElement.GetProperty("profile").GetString() is "representative" or "full");
        var cases=vectors.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        var identities=cases.Select(c=>(I(c,"species"),c.GetProperty("transformed").GetBoolean(),I(c,"letter"))).Distinct().ToArray();
        CollectionAssert.AreEquivalent(new[]{(25,false,0),(201,false,0),(201,false,1),(201,true,1),(410,false,0),(385,true,0)},identities);
        Assert.IsTrue(cases.Any(c=>c.GetProperty("doubles").GetBoolean()&&I(c,"actor")==3));
        Assert.IsTrue(cases.Any(c=>I(c,"target")== (I(c,"actor")^2)));
    }
}
