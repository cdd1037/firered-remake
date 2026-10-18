using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class CureFamilyAnimationTests
{
    private const BindingFlags Hidden=BindingFlags.NonPublic|BindingFlags.Instance;
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound> Cues=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Bodies(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"body-"+i,new[]{5,11,7,13}[i],new[]{2,3,4,5}[i],64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor=0,int target=1)=>new("cure",1,move,new(actor),new(target));
    private static SourceAnimationCommand C(string op,params string[] a)=>new(op,a,1);
    private static SourceAnimationScript Minimal(SourceAnimationScript s,params SourceAnimationCommand[] commands)=>s with{Commands=commands,Labels=new Dictionary<string,int>(),Subroutines=new Dictionary<string,SourceAnimationCommand[]>(),SubroutineLabels=new Dictionary<string,IReadOnlyDictionary<string,int>>()};
    private static object? Call(SourceBattleAnimation r,string method,params object[] args)=>typeof(SourceBattleAnimation).GetMethod(method,Hidden)!.Invoke(r,args);
    private static object[] Sprites(SourceBattleAnimation r)=>((IEnumerable)typeof(SourceBattleAnimation).GetField("_sprites",Hidden)!.GetValue(r)!).Cast<object>().ToArray();
    private static SourceVisualSpriteFrame Sprite(object s)=>(SourceVisualSpriteFrame)s.GetType().GetProperty("Frame")!.GetValue(s)!;
    private static bool Finished(object s)=>(bool)s.GetType().GetProperty("Finished")!.GetValue(s)!;
    private static JsonDocument Vectors()=>SourceVectorFixture.Read("cure-family-source-vectors.json","python tests/Game.Presentation.Tests/Reference/generate_cure_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");
    [TestMethod]
    public void EverySelectedNativeSpriteCallbackFrameAffineAndLinearRowMatches()
    {
        using var resources=new SourceAnimationResources(Folder);using var vectors=Vectors();var root=vectors.RootElement;int cases=0,rows=0;
        foreach(var entry in root.GetProperty("cases").EnumerateObject())
        {
            int[] key=entry.Name.Split(':').Select(int.Parse).ToArray();int kind=key[0],actor=key[2],target=key[3];bool doubles=key[1]!=0;var bodies=Bodies(doubles);
            int[] args=root.GetProperty("configs")[kind][key[4]].EnumerateArray().Select(x=>x.GetInt32()).ToArray();
            if(doubles&&args[7]!=0)bodies[actor^2]=bodies[actor^2] with{Visible=false};
            var resolved=resources.Resolve(kind<4?312:215);string template=root.GetProperty("templates")[kind].GetString()!;var recipe=resolved.Script.SpriteRecipes[template];
            var r=new SourceBattleAnimation(Minimal(resolved.Script,C("end")),Cue(resolved.Script.MoveId,actor,target),bodies,doubles,new Audio());
            string tag="ANIM_TAG_"+recipe.Asset.ToUpperInvariant().Replace('-','_');Call(r,"LoadSpriteGraphics",tag);
            if(kind==5)Call(r,"TryCreateCureTask",C("createvisualtask","AnimTask_LoadMusicNotesPals","5"));
            short[] registers=(short[])typeof(SourceBattleAnimation).GetField("_animationArgs",Hidden)!.GetValue(r)!;for(int i=0;i<7;i++)registers[i]=unchecked((short)args[i]);
            var command=C("createsprite",[template,"ANIM_ATTACKER","2",..args.Take(kind is 3 or 4?4:7).Select(x=>x.ToString())]);
            Assert.AreEqual(true,Call(r,"TryCreateCureSprite",command,doubles?4:2,28));var sprite=Sprites(r).Single();
            foreach(var expected in entry.Value.EnumerateArray())
            {
                int[] row=expected.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(row[0]>=0)sprite.GetType().GetMethod("Step")!.Invoke(sprite,[r]);
                Assert.AreEqual(row[1]==0,Finished(sprite),entry.Name+" tick "+row[0]);
                if(row[1]!=0)
                {
                    var f=Sprite(sprite);int[] actual=[f.Position.X,f.Position.Y,f.Offset.X,f.Offset.Y,f.Tile,f.HFlip?1:0,f.VFlip?1:0,f.OamPriority,f.MatrixA,f.MatrixB,f.MatrixC,f.MatrixD,f.Affine?1:0,f.DoubleSize?1:0];
                    CollectionAssert.AreEqual(row.Skip(2).Take(14).ToArray(),actual,entry.Name+" tick "+row[0]);
                    if(kind==5)Assert.AreEqual(row[16],f.PaletteSlot);
                }
                Assert.AreEqual(row[17],(int)registers[0]);Assert.AreEqual(row[18],(int)registers[2]);rows++;
            }
            r.Cancel();cases++;
        }
        Assert.AreEqual(root.GetProperty("coverage").GetProperty("caseCount").GetInt32(),cases);Assert.AreEqual(root.GetProperty("coverage").GetProperty("spriteRows").GetInt32(),rows);Assert.IsTrue(cases>0&&rows>cases);
    }
    [TestMethod]
    public void EverySourceMaskInitializerAndNinetyUpdatesKeepsRegistersRawCopiesAndCleanup()
    {
        using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(312).Script;using var vectors=Vectors();int rows=0,cases=0;
        foreach(var item in vectors.RootElement.GetProperty("maskCases").EnumerateObject())
        {
            int[] k=item.Name.Split(':').Select(int.Parse).ToArray();bool doubles=k[0]!=0;int actor=k[1];var bodies=Bodies(doubles);if(doubles&&k[3]==0)bodies[actor^2]=bodies[actor^2] with{Visible=false};
            var r=new SourceBattleAnimation(Minimal(script,C("end")),Cue(312,actor,actor),bodies,doubles,new Audio());var type=typeof(SourceBattleAnimation).GetNestedType("CureScrollingMaskTask",BindingFlags.NonPublic)!;var task=Activator.CreateInstance(type,Hidden|BindingFlags.Public,null,[r,k[2]!=0],null)!;
            foreach(var expected in item.Value.EnumerateArray())
            {
                int[] v=expected.EnumerateArray().Select(x=>x.GetInt32()).ToArray();if(v[0]>=0)type.GetMethod("Step")!.Invoke(task,[r]);var f=r.Snapshot();
                Assert.AreEqual(v[1]==0,Finished(task));Assert.AreEqual(v[10]-v[11],Sprites(r).Length);Assert.AreEqual((v[4]&0x8000)!=0,f.ObjectWindow?.Enabled??false);
                if(v[1]!=0){Assert.IsNotNull(f.Overlay);Assert.AreEqual(v[2],f.Overlay.ScrollX);Assert.AreEqual(v[3],f.Overlay.ScrollY);Assert.AreEqual(v[6]&63,f.ObjectWindow!.OutsideMask);Assert.AreEqual((v[6]>>8)&63,f.ObjectWindow.InsideMask);}
                else Assert.IsNull(f.Overlay);
                Assert.AreEqual(v[7]&63,f.BlendFirstTargets);Assert.AreEqual((v[7]>>8)&63,f.BlendSecondTargets);Assert.AreEqual((v[7]&0xc0)==0x40,f.BlendEnabled);Assert.AreEqual(v[8]&31,f.AlphaA);Assert.AreEqual((v[8]>>8)&31,f.AlphaB);Assert.AreEqual(v[9]&3,f.BattlerBackgroundPriorities[0]);
                foreach(var sprite in Sprites(r)){var copy=Sprite(sprite);Assert.IsTrue(copy.ObjectWindow);Assert.AreEqual(0,copy.OamPriority);Assert.IsTrue(copy.BattlerCopySlot==actor||copy.BattlerCopySlot==(actor^2));}
                string[] fields=["_phase","_delay","_alpha","_hold","_fraction","_scroll"];int[] values=[v[29],v[25],v[26],v[24],v[27],v[3]];
                for(int i=0;i<fields.Length;i++)Assert.AreEqual(values[i],Convert.ToInt32(type.GetField(fields[i],Hidden)!.GetValue(task)),item.Name+" "+fields[i]+" tick"+v[0]);rows++;
            }
            r.Cancel();cases++;
        }
        Assert.AreEqual(vectors.RootElement.GetProperty("coverage").GetProperty("maskRows").GetInt32(),rows);Assert.AreEqual(24,cases);
    }
    [TestMethod]
    [DataRow(287)] [DataRow(312)] [DataRow(215)]
    public void CompleteRootsComposeEveryFrameReplaySixLayoutsAndReleaseAllOwners(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var bodies=Bodies(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8842,240*112).ToArray());assets.TryAdd("impact",new(32,32,new ushort[1024]));foreach(var b in bodies)assets[b.Asset]=new(64,64,Enumerable.Range(0,4096).Select(i=>i%64 is >4 and <59&&i/64 is >4 and <59?(ushort)0x83e0:(ushort)0).ToArray());
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var scene=new SourceBattleAnimation.SpriteScene();var audio=new Audio();var cue=Cue(move,actor,actor);
            var r=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,audio,spriteScene:scene,spriteMaterials:assets);var replay=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,new Audio(),spriteMaterials:assets);byte[] baseline=composer.ComposeField(r.Snapshot());int ticks=0,maskTicks=0;bool changed=false,notes=false,flowers=false,ring=false,stars=false;var paletteSlots=new HashSet<int>();
            while(r.Active&&ticks++<900)
            {
                var f=r.Tick();Assert.AreEqual(JsonSerializer.Serialize(f),JsonSerializer.Serialize(replay.Tick()));var rgba=composer.ComposeField(f);Assert.AreEqual(240*112*4,rgba.Length);changed|=!rgba.SequenceEqual(baseline);
                if(f.ObjectWindow is {Enabled:true}){maskTicks++;Assert.IsTrue(f.Sprites.All(s=>!s.ObjectWindow||s.BattlerCopySlot==actor||s.BattlerCopySlot==(actor^2)));}
                foreach(var s in f.Sprites){if(s.Template=="gHealBellMusicNoteSpriteTemplate"){notes=true;paletteSlots.Add(s.PaletteSlot!.Value);}flowers|=s.Asset=="flower";ring|=s.Asset=="thin-ring";stars|=s.Asset=="sparkle-2";}
            }
            Assert.IsFalse(r.Active);Assert.IsFalse(replay.Active);Assert.AreEqual(0,r.ActiveVisualCount);Assert.AreEqual(0,scene.Count);Assert.IsFalse(r.GraphicsLoaded);Assert.IsTrue(changed&&ring&&stars);
            Assert.AreEqual(move==215,notes);Assert.AreEqual(move==312,flowers);if(move==215)Assert.AreEqual(3,paletteSlots.Count);else Assert.AreEqual(89,maskTicks);
            var last=r.Snapshot();Assert.IsNull(last.Overlay);Assert.IsFalse(last.ObjectWindow?.Enabled??false);Assert.IsTrue(last.Offsets.All(p=>p==default));Assert.AreEqual(0,last.BackgroundSlots.Length);Assert.AreEqual(0,last.PaletteBlends.Length);CollectionAssert.AreEqual(baseline,composer.ComposeField(last));Assert.IsTrue(audio.Cues.Count>0);
        }
    }
    [TestMethod]
    public void CancellationAtSourcePhaseBoundariesRestoresMaskCopiesAndMusicBanks()
    {
        using var resources=new SourceAnimationResources(Folder);
        foreach(int move in new[]{287,312,215})foreach(int stop in new[]{0,1,5,30,59,60,89,90,100,160,240,320})
        {
            var r=new SourceBattleAnimation(resources.Resolve(move).Script,Cue(move),Bodies(true),true,new Audio());for(int i=0;i<stop&&r.Active;i++)r.Tick();r.Cancel();r.Cancel();var f=r.Snapshot();
            Assert.IsNull(f.ObjectWindow);Assert.IsNull(f.Overlay);Assert.AreEqual(0,r.ActiveVisualCount);Assert.AreEqual(0,f.Sprites.Length);Assert.AreEqual(0,f.PaletteBlends.Length);Assert.IsTrue(f.SpritePaletteSlots.All(p=>p.All(x=>x==0)));
        }
    }
    [TestMethod]
    public void MissingMaterialsMalformedOperandsAndCompetingRegisterOwnersFailBeforePlayback()
    {
        using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(287).Script;var cue=Cue(287);var bodies=Bodies(false);
        Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(script with{OverlayDependencies=[]},cue,bodies,false,new Audio()));
        foreach(var extra in new[]{C("setalpha","8","8"),C("createvisualtask","AnimTask_StatusClearedEffect","2","0"),C("createvisualtask","AnimTask_FakeOut","5"),C("monbg","ANIM_ATTACKER")})
        {var changed=script with{Commands=[..script.Commands.Take(script.Commands.Length-1),extra,C("end")]};Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(changed,cue,bodies,false,new Audio()));}
        var heal=resources.Resolve(215).Script;Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(heal with{SpritePaletteGroups=new Dictionary<int,ushort[][]>()},Cue(215),bodies,false,new Audio()));
        foreach(string value in new[]{"-1","2","32768"})
        {var bad=script with{Commands=script.Commands.Select(c=>c.Op=="createvisualtask"&&c.Args[0]=="AnimTask_StatusClearedEffect"?c with{Args=[c.Args[0],c.Args[1],value]}:c).ToArray()};Assert.Throws<SourceAnimationException>(()=>new SourceBattleAnimation(bad,cue,bodies,false,new Audio()));}
    }
    [TestMethod]
    public void MusicPaletteCapacityExcludesFourReservedBanksAndFreeRetainsDetachedBytes()
    {
        using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(215).Script;int tag=script.SpriteTags["ANIM_TAG_MUSIC_NOTES_2"];
        foreach(int free in new[]{0,1,2})
        {
            var r=new SourceBattleAnimation(Minimal(script,C("end")),Cue(215),Bodies(false),false,new Audio());Call(r,"LoadSpriteGraphics","ANIM_TAG_MUSIC_NOTES_2");
            for(int i=0;i<11-free;i++)Assert.AreNotEqual(255,Call(r,"AllocateSpritePalette",20000+i));
            int[] tags=(int[])typeof(SourceBattleAnimation).GetField("_spritePaletteTags",Hidden)!.GetValue(r)!;var before=(int[])tags.Clone();Assert.AreEqual(free+4,tags.Count(t=>t==65535));
            if(free<2)
            {
                var error=Assert.Throws<TargetInvocationException>(()=>Call(r,"TryCreateCureTask",C("createvisualtask","AnimTask_LoadMusicNotesPals","5")));Assert.IsInstanceOfType<InvalidDataException>(error.InnerException);CollectionAssert.AreEqual(before,tags);
            }
            else
            {
                Assert.AreEqual(true,Call(r,"TryCreateCureTask",C("createvisualtask","AnimTask_LoadMusicNotesPals","5")));var loaded=r.Snapshot();int[] bankTags=[tag,9999,9998];
                for(int i=0;i<3;i++)CollectionAssert.AreEqual(script.SpritePaletteGroups[tag][i],loaded.SpritePalettes[bankTags[i]]);
                Assert.IsTrue(tags.Take(4).All(t=>t==65535));Call(r,"TryCreateCureTask",C("createvisualtask","AnimTask_FreeMusicNotesPals","5"));var released=r.Snapshot();
                foreach(int t in bankTags)Assert.IsFalse(released.SpritePalettes.ContainsKey(t));for(int i=0;i<16;i++)CollectionAssert.AreEqual(loaded.SpritePaletteSlots[i],released.SpritePaletteSlots[i]);
                r.Cancel();for(int i=0;i<3;i++)CollectionAssert.AreEqual(script.SpritePaletteGroups[tag][i],loaded.SpritePalettes[bankTags[i]],"An earlier OAM frame owns detached bank bytes.");
            }
            r.Cancel();
        }
    }
    [TestMethod]
    public void RawWindowCopyKeepsCurrentBodyGeometryMaterialAndActorAffineOwnership()
    {
        using var resources=new SourceAnimationResources(Folder);var script=resources.Resolve(312).Script;
        foreach(bool invisible in new[]{false,true})
        {
            var bodies=Bodies(true);bodies[0]=bodies[0] with{SourceVisibility=new(true,false,invisible)};
            var material=new SourcePixelAsset(64,64,Enumerable.Repeat((ushort)0x8000,4096).ToArray());var assets=new Dictionary<string,SourcePixelAsset>{{bodies[0].Asset,material}};
            var r=new SourceBattleAnimation(Minimal(script,C("end")),Cue(312,0,0),bodies,true,new Audio(),spriteMaterials:assets);
            Call(r,"SetBattlerPosition",0,new PixelPoint(67,49));((PixelPoint[])typeof(SourceBattleAnimation).GetField("_offsets",Hidden)!.GetValue(r)!)[0]=new(11,-7);
            ((bool[])typeof(SourceBattleAnimation).GetField("_affineMode",Hidden)!.GetValue(r)!)[0]=true;string[] fields=["_xScale","_matrixB","_matrixC","_yScale"];int[] values=[173,-91,77,289];
            for(int i=0;i<4;i++)((int[])typeof(SourceBattleAnimation).GetField(fields[i],Hidden)!.GetValue(r)!)[0]=values[i];
            var type=typeof(SourceBattleAnimation).GetNestedType("CureScrollingMaskTask",BindingFlags.NonPublic)!;var task=Activator.CreateInstance(type,Hidden|BindingFlags.Public,null,[r,false],null)!;var copy=Sprite(Sprites(r).Single());
            Assert.AreEqual(new PixelPoint(67,49),copy.Position);Assert.AreEqual(new PixelPoint(11,-7),copy.Offset);Assert.AreEqual(!invisible,copy.Visible);Assert.AreSame(material,copy.Material);Assert.IsTrue(copy.Affine&&copy.DoubleSize&&copy.ObjectWindow);CollectionAssert.AreEqual(values,new[]{copy.MatrixA,copy.MatrixB,copy.MatrixC,copy.MatrixD});
            // The admitted roots have no concurrent actor-affine task. Source copies
            // reuse matrixNum; their destruction must not release the actor matrix.
            for(int i=0;i<90;i++)type.GetMethod("Step")!.Invoke(task,[r]);Assert.AreEqual(0,Sprites(r).Length);Assert.IsTrue(((bool[])typeof(SourceBattleAnimation).GetField("_affineMode",Hidden)!.GetValue(r)!)[0]);
            for(int i=0;i<4;i++)Assert.AreEqual(values[i],((int[])typeof(SourceBattleAnimation).GetField(fields[i],Hidden)!.GetValue(r)!)[0]);r.Cancel();
        }
    }

}
