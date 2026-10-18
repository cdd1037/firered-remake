using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class PrimaryStatAnimationTests
{
    private static readonly (int Move,string Template,string Asset)[] Families=[(43,"gLeerSpriteTemplate","leer"),(294,"gTailGlowOrbSpriteTemplate","circle-of-light"),(313,"gTearDropSpriteTemplate","small-bubbles")];
    private const BindingFlags Instance=BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic;
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound> Cues=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("primary-stat-source-vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_primary_stat_vectors.py --repo . --reference content/raw/pokefirered/037335f4 --output tests/Game.Presentation.Tests/Fixtures/primary-stat-source-vectors.json");
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static int[] Numbers(JsonElement row)=>row.EnumerateArray().Select(v=>v.GetInt32()).ToArray();
    private static object? Field(object value,string name)=>value.GetType().GetField(name,Instance)!.GetValue(value);
    private static T Field<T>(object value,string name)=>(T)Field(value,name)!;
    private static T Property<T>(object value,string name)=>(T)value.GetType().GetProperty(name,Instance)!.GetValue(value)!;
    private static object? Invoke(object value,string name,params object[] args)=>value.GetType().GetMethod(name,Instance)!.Invoke(value,args);
    private static AnimationBattler[] Battlers(bool doubles,int width=64,int height=64)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,width,height)).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript source,params SourceAnimationCommand[] commands)=>source with{Commands=commands,Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),Labels=new Dictionary<string,int>()};
    private static CommittedMoveAnimation Cue(int move,int actor,int target)=>new("primary-stat",1,move,new(actor),new(target));
    private static string Tag(string asset)=>"ANIM_TAG_"+asset.ToUpperInvariant().Replace('-','_');
    private static object NewSprite(SourceBattleAnimation owner,SourceSpriteRecipe recipe,int[] args)
    {
        var type=typeof(SourceBattleAnimation).GetNestedType("PrimaryStatSprite",BindingFlags.NonPublic)!;
        var sprite=Activator.CreateInstance(type,Instance,null,new object[]{4,20,recipe,args,owner},CultureInfo.InvariantCulture)!;
        Invoke(Field(owner,"_sprites")!,"Add",sprite);return sprite;
    }
    [TestMethod][DataRow(0,100)][DataRow(1,40)][DataRow(2,800)]
    public void NativeCallbacksMatchEveryOriginalCTickAcrossLayoutsParticipantsAndDimensions(int kind,int expectedCases)
    {
        using var vectors=Vectors();var root=vectors.RootElement;var source=AnimationCompositionFixture.Script(Families[kind].Move);int count=0;
        var columns=root.GetProperty("columns").EnumerateArray().Select(v=>v.GetString()!).ToArray();
        foreach(var item in root.GetProperty("cases").EnumerateObject())
        {
            var key=item.Name.Split(':').Select(int.Parse).ToArray();if(key[0]!=kind)continue;count++;bool doubles=key[1]!=0;
            var dimensions=Numbers(root.GetProperty("dimensions")[key[5]]);int[] args=kind==0?Numbers(root.GetProperty("leerArgs")[key[4]]):kind==1?[key[4]]:[key[4]/4,key[4]%4];
            var scene=new SourceBattleAnimation.SpriteScene();var script=Primitive(source,C("end"));var owner=new SourceBattleAnimation(script,Cue(source.MoveId,key[2],key[3]),Battlers(doubles,dimensions[0],dimensions[1]),doubles,new Audio(),spriteScene:scene);owner.Tick();
            var sprite=NewSprite(owner,source.SpriteRecipes[Families[kind].Template],args);int rows=0;
            foreach(var element in item.Value.EnumerateArray())
            {
                rows++;var values=Numbers(element);int V(string column)=>values[Array.IndexOf(columns,column)];string context=item.Name+" tick "+V("tick");
                if(V("tick")>=0){Invoke(owner,"StepSourceSprite",sprite);if(!Property<bool>(sprite,"Finished"))Invoke(sprite,"AdvanceAnimation",owner);}
                Assert.AreEqual(V("active")==0,Property<bool>(sprite,"Finished"),context);Assert.AreEqual(V("active"),scene.Count,context);
                if(V("active")==0)continue;
                var frame=Property<SourceVisualSpriteFrame>(sprite,"Frame");Assert.AreEqual(new PixelPoint(V("x"),V("y")),frame.Position,context);Assert.AreEqual(new PixelPoint(V("x2"),V("y2")),frame.Offset,context);Assert.AreEqual(V("tile"),frame.Tile,context);Assert.AreEqual(V("visible")!=0,frame.Visible,context);
                Assert.AreEqual(kind!=0,frame.Affine,context);Assert.AreEqual(kind==1,frame.Blend,context);Assert.IsFalse(frame.DoubleSize,context);Assert.AreEqual(kind==0?32:kind==1?64:16,frame.Width,context);Assert.AreEqual(frame.Width,frame.Height,context);
                if(kind==0)
                {
                    var frames=Field<SourceSpriteFrameProgram>(sprite,"_frames");Assert.AreEqual(V("frameEnded")!=0,frames.Ended,context);Assert.AreEqual(V("frameDelay"),frames.DelayCounter,context);
                }
                else
                {
                    var affine=Field<SourceSpriteAffineProgram>(sprite,"_affine");Assert.AreEqual(V("affineEnded")!=0,affine.Ended,context);CollectionAssert.AreEqual(new[]{V("scaleX"),V("scaleY"),V("rotation")},new[]{affine.XScale,affine.YScale,affine.Rotation},context);
                    CollectionAssert.AreEqual(new[]{V("a"),V("b"),V("c"),V("d")},new[]{frame.MatrixA,frame.MatrixB,frame.MatrixC,frame.MatrixD},context);
                }
            }
            Assert.IsTrue(rows>16,item.Name);Assert.IsTrue(Property<bool>(sprite,"Finished"),item.Name);scene.Cancel();
        }
        Assert.AreEqual(expectedCases,count,"Empty or partial source vectors must fail.");
    }
    [TestMethod]
    public void BlendParticleMatchesOriginalCPerTaskDelayEveryRgb555ColorAndSourceBankOwnership()
    {
        using var vectors=Vectors();var root=vectors.RootElement;var original=AnimationCompositionFixture.Script(313);string tag=Tag("small-bubbles");int numeric=original.SpriteTags[tag];var input=Numbers(root.GetProperty("paletteInput")).Select(v=>(ushort)v).ToArray();int cases=0,rows=0;
        var columns=root.GetProperty("paletteColumns").EnumerateArray().Select(v=>v.GetString()!).ToArray();
        foreach(var item in root.GetProperty("palettes").EnumerateObject())
        {
            cases++;var config=Numbers(root.GetProperty("paletteConfigs")[int.Parse(item.Name,CultureInfo.InvariantCulture)]);int slot=config[0];Assert.IsTrue(slot is >=4 and <=15);
            var palettes=original.SpritePalettes.ToDictionary(p=>p.Key,p=>p.Value);palettes[numeric]=(ushort[])input.Clone();var script=Primitive(original,C("end")) with{SpritePalettes=palettes};var runtime=new SourceBattleAnimation(script,Cue(313,0,1),Battlers(false),false,new Audio());
            var tags=Field<int[]>(runtime,"_spritePaletteTags");for(int i=4;i<slot;i++)tags[i]=12300+i;
            var faded=Field<ushort[][]>(runtime,"_spritePaletteBanks");var unfaded=Field<ushort[][]>(runtime,"_spritePaletteUnfadedBanks");for(int i=0;i<16;i++){Array.Copy(input,faded[i],16);Array.Copy(input,unfaded[i],16);}
            Invoke(runtime,"LoadSpriteGraphics",tag);Assert.AreEqual(slot,(int)Invoke(runtime,"FindSpritePalette",numeric)!);
            var command=C("createvisualtask",new[]{"AnimTask_BlendParticle","5",tag}.Concat(config.Skip(1).Select(v=>v.ToString(CultureInfo.InvariantCulture))).ToArray());Assert.IsTrue((bool)Invoke(runtime,"TryCreatePrimaryStatTask",command)!);
            var task=((System.Collections.IEnumerable)Field(runtime,"_tasks")!).Cast<object>().SingleOrDefault();
            foreach(var element in item.Value.EnumerateArray())
            {
                rows++;var values=Numbers(element);int V(string column)=>values[Array.IndexOf(columns,column)];string context=item.Name+" tick "+V("tick");
                if(V("tick")>=0){Assert.IsNotNull(task,context);Invoke(runtime,"RunSourceTasks");}
                Assert.AreEqual(V("active"),runtime.ActiveVisualCount,context);if(task is not null){Assert.AreEqual(V("counter"),Field<int>(task,"_counter"),context);Assert.AreEqual(V("coefficient"),Field<int>(task,"_coefficient"),context);Assert.AreEqual(V("active")==0,Property<bool>(task,"Finished"),context);}
                var frame=runtime.Snapshot();CollectionAssert.AreEqual(values.Skip(Array.IndexOf(columns,"color0")).Select(v=>(ushort)v).ToArray(),frame.SpritePaletteSlots[slot],context);CollectionAssert.AreEqual(frame.SpritePaletteSlots[slot],frame.SpritePalettes[numeric],context);
                if(V("writeCount")>0)Assert.AreEqual((slot+16)*16,V("offset"),context);
                for(int i=0;i<16;i++){CollectionAssert.AreEqual(input,unfaded[i],context);if(i!=slot)CollectionAssert.AreEqual(input,frame.SpritePaletteSlots[i],context);}
                Assert.IsFalse(runtime.PaletteFadeActive,context);Assert.AreEqual(0,frame.PaletteBlends.Length,context);
            }
            Assert.AreEqual(0,runtime.ActiveVisualCount);runtime.Cancel();
        }
        Assert.AreEqual(6,cases);Assert.AreEqual(349,rows);
    }
    private static int SourceVisibleFrames(int kind)
    {
        using var vectors=Vectors();var root=vectors.RootElement;var columns=root.GetProperty("columns").EnumerateArray().Select(v=>v.GetString()!).ToArray();int tick=Array.IndexOf(columns,"tick"),active=Array.IndexOf(columns,"active");
        return root.GetProperty("cases").GetProperty(kind+":1:0:1:0:0").EnumerateArray().Select(Numbers).Count(v=>v[tick]>=0&&v[active]!=0);
    }
    [TestMethod][DataRow(0)][DataRow(1)][DataRow(2)]
    public void CompleteRootsUseRealSourceMaterialsMultiplicityAudioPanningAndDeterministicReplay(int kind)
    {
        var family=Families[kind];using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(family.Move);int expected=SourceVisibleFrames(kind)*(kind==2?8:1);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)foreach(int target in kind==1?new[]{actor}:doubles?new[]{actor^1,actor^2}:new[]{actor^1})
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x801f,240*112).ToArray()));foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x83e0,4096).ToArray()));
            var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var runtime=new SourceBattleAnimation(resolved.Script,Cue(family.Move,actor,target),battlers,doubles,audio,spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,Cue(family.Move,actor,target),battlers,doubles,new Audio(),spriteMaterials:assets);var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);
            int ticks=0,visible=0;bool changed=false,affine=false;var sourcePalette=(ushort[])resolved.Script.SpritePalettes[resolved.Script.SpriteTags[Tag(family.Asset)]].Clone();
            while(runtime.Active&&ticks++<512)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(replay.Tick()));string before=JsonSerializer.Serialize(runtime.Snapshot());var pixels=composer.ComposeField(frame);composer.ComposeField(frame);runtime.Snapshot();Assert.AreEqual(before,JsonSerializer.Serialize(runtime.Snapshot()));
                var sprites=frame.Sprites.Where(s=>s.Template==family.Template).ToArray();visible+=sprites.Length;affine|=sprites.Any(s=>s.Affine&&s.MatrixA!=256);if(sprites.Length>0)changed|=!pixels.SequenceEqual(composer.ComposeField(frame with{Sprites=frame.Sprites.Where(s=>s.Template!=family.Template).ToArray()}));
                foreach(var sprite in sprites)
                {
                    Assert.IsNotNull(sprite.MaterialPalette);Assert.AreEqual(kind==0?32:kind==1?64:16,sprite.Width);if(kind==2)Assert.AreEqual(4,sprite.Tile);
                    if(kind==2)CollectionAssert.AreEqual(sourcePalette.Select(c=>SourcePaletteMath.Blend(c,4,(ushort)(12|(11<<5)|(31<<10)))).ToArray(),sprite.MaterialPalette);
                }
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.AreEqual(expected,visible);Assert.IsTrue(changed);if(kind!=0)Assert.IsTrue(affine);Assert.AreEqual(kind==0?1:kind==1?6:4,audio.Cues.Count);Assert.IsTrue(audio.Cues.All(c=>c.Pan==(actor%2==0?-64:63)));Assert.IsTrue(audio.Cues.All(c=>c.Id==(kind==0?"SE_M_LEER":kind==1?"SE_M_MORNING_SUN":"SE_M_TAIL_WHIP")));
            CollectionAssert.AreEqual(sourcePalette,resolved.Script.SpritePalettes[resolved.Script.SpriteTags[Tag(family.Asset)]]);AssertClean(runtime);Assert.AreEqual(0,scene.Count);
        }
    }
    private static void AssertClean(SourceBattleAnimation runtime)
    {var frame=runtime.Snapshot();Assert.IsFalse(runtime.Active);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,frame.Sprites.Length);Assert.AreEqual(0,frame.BackgroundSlots.Length);Assert.IsFalse(frame.BlendEnabled);Assert.IsTrue(frame.Offsets.All(p=>p==default));Assert.IsTrue(frame.BattlerAffine.All(a=>a.XScale==256&&a.YScale==256));}
    [TestMethod][DataRow(43)][DataRow(294)][DataRow(313)]
    public void CancellationAndReplacementReleaseAllSourceSpritesPalettesAndBattlerChanges(int move)
    {
        var source=AnimationCompositionFixture.Script(move);var battlers=Battlers(true);var scene=new SourceBattleAnimation.SpriteScene();
        foreach(int cancelAt in new[]{1,4,10,20,35,60,90,120,160})
        {
            var runtime=new SourceBattleAnimation(source,Cue(move,3,move==294?3:1),battlers,true,new Audio(),spriteScene:scene);for(int i=0;i<cancelAt&&runtime.Active;i++)runtime.Tick();runtime.Cancel();runtime.Cancel();AssertClean(runtime);Assert.AreEqual(0,scene.Count);Assert.IsTrue(runtime.Snapshot().SpritePaletteSlots.All(p=>p.All(c=>c==0)));
            var next=new SourceBattleAnimation(source,Cue(move,0,move==294?0:1),battlers,true,new Audio(),spriteScene:scene);int ticks=0;while(next.Active&&ticks++<512)next.Tick();AssertClean(next);Assert.AreEqual(0,scene.Count);
        }
    }
    [TestMethod]
    public void MissingIndexedPalettesMalformedSourceProgramsAndUnloadedTagsFailClosed()
    {
        void Reject(SourceAnimationScript source)=>Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(source,Cue(source.MoveId,0,1),Battlers(false),false,new Audio()));
        for(int kind=0;kind<3;kind++)
        {
            var family=Families[kind];var source=AnimationCompositionFixture.Script(family.Move);string[] args=kind==0?["24","-12"]:kind==1?["0"]:["0","0"];
            var sprite=C("createsprite",new[]{family.Template,"ANIM_ATTACKER","2"}.Concat(args).ToArray());var primitive=Primitive(source,C("loadspritegfx",Tag(family.Asset)),sprite,C("waitforvisualfinish"),C("end"));var recipe=source.SpriteRecipes[family.Template];
            Reject(primitive with{SpritePalettes=new Dictionary<int,ushort[]>()});Reject(primitive with{SpriteTags=new Dictionary<string,int>()});
            foreach(var bad in new[]{recipe with{Callback="UnknownPrimaryStat"},recipe with{Width=8},recipe with{Blend=!recipe.Blend},recipe with{AffineDoubleSize=true},recipe with{LoopStarts=[kind==0?0:-1]},recipe with{Animations=[[new(7,3,false,false)]]}})
            {var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[family.Template]=bad;Reject(primitive with{SpriteRecipes=recipes});}
            if(kind!=0){var recipes=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[family.Template]=recipe with{AffineAnimations=[]};Reject(primitive with{SpriteRecipes=recipes});}
            if(kind==2)Reject(primitive with{SineTable=[]});
            var missing=source.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);missing.Remove(family.Template);Reject(primitive with{SpriteRecipes=missing});
            var unloaded=Primitive(source,sprite,C("waitforvisualfinish"),C("end"));var runtime=new SourceBattleAnimation(unloaded,Cue(source.MoveId,0,1),Battlers(false),false,new Audio());Assert.ThrowsExactly<SourceAnimationException>(()=>runtime.Tick());runtime.Cancel();
            var changed=sprite.Args.ToArray();changed[3]=kind==0?"32768":"2";Reject(Primitive(source,sprite with{Args=changed},C("end")));if(kind==2){changed=sprite.Args.ToArray();changed[4]="4";Reject(Primitive(source,sprite with{Args=changed},C("end")));}
        }
        var tears=AnimationCompositionFixture.Script(313);var task=C("createvisualtask","AnimTask_BlendParticle","5","ANIM_TAG_SMALL_BUBBLES","0","4","4","RGB(12, 11, 31)");
        foreach(var (index,value) in new[]{(1,"256"),(2,"ANIM_TAG_UNKNOWN"),(3,"-1"),(3,"256"),(4,"17"),(5,"-1"),(6,"65536")}){var args=task.Args.ToArray();args[index]=value;Reject(Primitive(tears,task with{Args=args},C("end")));}
        Reject(Primitive(tears,task,C("end")) with{SpritePalettes=new Dictionary<int,ushort[]>()});var unbound=new SourceBattleAnimation(Primitive(tears,task,C("end")),Cue(313,0,1),Battlers(false),false,new Audio());Assert.ThrowsExactly<SourceAnimationException>(()=>unbound.Tick());unbound.Cancel();
    }
}
