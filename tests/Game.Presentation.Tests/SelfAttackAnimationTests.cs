using System.Reflection;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class SelfAttackAnimationTests
{
    private const string Sword="gSwordsDanceBladeSpriteTemplate",Sphere="gSharpenSphereSpriteTemplate";
    private sealed class Audio:ISourceAnimationAudio
    {public readonly List<SourceAnimationSound>Cues=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound c)=>Cues.Add(c);public void SetPan(int p,int f){}public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static SourceAnimationCommand C(string op,params string[] args)=>new(op,args,1);
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int move,int actor=0)=>new("self-attack",1,move,new(actor),new(actor));
    private static object? Invoke(object target,string method,params object[] args)=>target.GetType().GetMethod(method,BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public)!.Invoke(target,args);

    [TestMethod][DataRow(14)][DataRow(159)]
    public void CompleteSourceRootsComposeEverySelfLayoutAndReleaseOwnedState(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);string id=move==14?"sword":"sphere-to-cube";var source=resolved.Assets[id];
        Assert.AreEqual(32,source.Width);Assert.AreEqual(move==14?64:160,source.Height);Assert.AreEqual(source.Width*source.Height,source.PaletteIndices.Length);
        foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var b=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);assets["background"]=new(240,112,new ushort[240*112]);foreach(var mon in b)assets[mon.Asset]=new(64,64,Enumerable.Repeat((ushort)0xffff,4096).ToArray());
            var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(resolved.Script,Cue(move,actor),b,doubles,audio,spriteScene:scene,spriteMaterials:assets);var composer=AnimationCompositionFixture.Create(resources,resolved,assets,b);
            int ticks=0,peak=0;bool visible=false,flashPixels=false;ushort[] palette=source.Palette;
            while(o.Active&&ticks++<1024)
            {
                var f=o.Tick();peak=Math.Max(peak,o.ActiveVisualCount);var output=composer.ComposeField(f);Assert.AreEqual(240*112*4,output.Length);
                foreach(var s in f.Sprites.Where(s=>s.Asset==id&&s.Visible))
                {
                    visible=true;Assert.IsNotNull(s.MaterialPalette);
                    if(move==14&&!s.MaterialPalette.SequenceEqual(palette))
                    {
                        var unfaded=f with{Sprites=f.Sprites.Select(sprite=>sprite.Asset==id?sprite with{MaterialPalette=palette}:sprite).ToArray()};
                        flashPixels|=!output.SequenceEqual(composer.ComposeField(unfaded));
                    }
                }
            }
            Assert.IsFalse(o.Active);Assert.IsTrue(visible);Assert.IsTrue(peak>0);Assert.AreEqual(0,o.ActiveVisualCount);Assert.AreEqual(0,scene.Count);Assert.IsFalse(o.HasResidualSprites);Assert.IsFalse(o.Snapshot().BlendEnabled);Assert.IsFalse(o.GraphicsLoaded);
            if(move==14){Assert.IsTrue(flashPixels,"The indexed Sword palette fade must alter actual composed pixels.");CollectionAssert.AreEqual(new[]{"SE_M_SWORDS_DANCE"},audio.Cues.Select(c=>c.Id).ToArray());}
            else
            {
                Assert.AreEqual(7,audio.Cues.Count);Assert.IsTrue(audio.Cues.All(c=>c.Id=="SE_M_SWAGGER2"));Assert.IsTrue(audio.Cues.All(c=>c.Pan==(actor%2==0?-64:63)));
                // Each source visible-return cue is separated by the growing signed interval.
                CollectionAssert.AreEqual(new[]{18,26,34,42,50,58},audio.Cues.Zip(audio.Cues.Skip(1),(a,z)=>z.Frame-a.Frame).ToArray());
            }
        }
    }
    [TestMethod][DataRow(14)][DataRow(159)]
    public void CancellationAtActiveAndCompletedBoundariesReleasesOnlyOwnedState(int move)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.Resolve(move);
        foreach(int target in move==14?new[]{0,22,48}:new[]{0,53,192,260})
        {
            var audio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();var o=new SourceBattleAnimation(resolved.Script,Cue(move),Battlers(true),true,audio,spriteScene:scene,spriteMaterials:resolved.Assets);
            for(int t=0;t<=target;t++){Assert.IsTrue(o.Active);o.Tick();}int sounds=audio.Cues.Count;o.Cancel();
            Assert.IsFalse(o.Active);Assert.AreEqual(0,scene.Count);Assert.AreEqual(0,o.ActiveVisualCount);Assert.IsFalse(o.HasResidualSprites);Assert.IsFalse(o.GraphicsLoaded);Assert.IsTrue(o.Snapshot().SpritePaletteSlots.All(p=>p.All(c=>c==0)));Assert.AreEqual(sounds,audio.Cues.Count);
            o.Cancel();Assert.AreEqual(sounds,audio.Cues.Count);
        }
        var ended=new SourceBattleAnimation(resolved.Script,Cue(move),Battlers(true),true,new Audio());int tick=0;while(ended.Active&&tick++<1024)ended.Tick();Assert.IsFalse(ended.Active);ended.Cancel();Assert.AreEqual(0,ended.ActiveVisualCount);Assert.IsEmpty(ended.Snapshot().Sprites);
    }
    [TestMethod]
    public void NormalSharpenPaletteLoadDoesNotReuseTheMusicRainbowAlias()
    {
        using var resources=new SourceAnimationResources(Folder);var music=resources.Resolve(47);var sharpen=resources.Resolve(159);int tag=sharpen.Script.SpriteTags["ANIM_TAG_SPHERE_TO_CUBE"];
        var scene=new SourceBattleAnimation.SpriteScene();var b=Battlers(true);var old=new SourceBattleAnimation(music.Script,new("palette-alias",1,47,new(0),new(1)),b,true,new Audio(),spriteScene:scene,spriteMaterials:music.Assets);
        int tick=0;bool aliasObserved=false;while(old.Active&&tick++<1024)
        {
            old.Tick();int bank=(int)Invoke(old,"FindSpritePalette",tag)!;
            if(bank!=255){aliasObserved=true;Assert.IsFalse(old.Snapshot().SpritePaletteSlots[bank].SequenceEqual(sharpen.Assets["sphere-to-cube"].Palette));}
        }
        Assert.IsTrue(aliasObserved);Assert.IsFalse(old.Active);Assert.AreEqual(255,(int)Invoke(old,"FindSpritePalette",tag)!);
        var next=new SourceBattleAnimation(sharpen.Script,new("palette-alias",2,159,new(0),new(0)),b,true,new Audio(),spriteScene:scene,spriteMaterials:sharpen.Assets);var frame=next.Tick();
        int loaded=(int)Invoke(next,"FindSpritePalette",tag)!;Assert.AreEqual(4,loaded);CollectionAssert.AreEqual(sharpen.Assets["sphere-to-cube"].Palette,frame.SpritePaletteSlots[loaded]);Assert.IsEmpty(frame.Sprites);
        // Cmd_loadspritegfx installs WaitAnimFrameCount(1): decrement, restore
        // the script callback, then create the sphere on the following tick.
        Assert.IsEmpty(next.Tick().Sprites);Assert.IsEmpty(next.Tick().Sprites);frame=next.Tick();
        CollectionAssert.AreEqual(sharpen.Assets["sphere-to-cube"].Palette,frame.Sprites.Single(s=>s.Asset=="sphere-to-cube").MaterialPalette);next.Cancel();
    }
    [TestMethod]
    public void MissingIndexedPaletteCallbackSoundAndMalformedUnusedRecipesFailClosed()
    {
        var sword=AnimationCompositionFixture.Script(14);var sharpen=AnimationCompositionFixture.Script(159);
        void Reject(SourceAnimationScript script)=>Assert.ThrowsExactly<SourceAnimationException>(()=>new SourceBattleAnimation(script,Cue(script.MoveId),Battlers(true),true,new Audio()));
        Reject(sword with{SpritePalettes=new Dictionary<int,ushort[]>()});Reject(sharpen with{SoundEffects=new Dictionary<string,int>()});
        Reject(sword with{Commands=[C("createsprite",Sword,"ANIM_ATTACKER","2","32768","0"),C("end")]});
        Reject(sharpen with{Commands=[C("createsprite",Sphere,"ANIM_ATTACKER","2","0"),C("end")]});
        foreach(string template in new[]{Sword,Sphere})
        {
            var script=template==Sword?sword:sharpen;var recipes=script.SpriteRecipes.ToDictionary(p=>p.Key,p=>p.Value);recipes[template]=recipes[template] with{Width=16};
            Reject(script with{SpriteRecipes=recipes,Commands=[C("end")],Subroutines=new Dictionary<string,SourceAnimationCommand[]>{{"Unused",[template==Sword?C("createsprite",template,"ANIM_ATTACKER","2","0","0"):C("createsprite",template,"ANIM_ATTACKER","2"),C("return")]}}});
        }
    }
}
