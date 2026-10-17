using System.Text.Json;
using Game.Presentation.BattleAnimations;

namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class WishHealingGeneralAnimationTests
{
    private const string Label="General_WishHeal";
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void SetPan(int pan,int frame){}public void StopAnimationEffects(){}}
    private static string Folder=>Path.Combine(TestAssets.Root,"content/generated/battle/animations");
    private static AnimationBattler[] Battlers(bool doubles)=>Enumerable.Range(0,doubles?4:2).Select(i=>new AnimationBattler(new(i),"b"+i,8,0,64,64)).ToArray();
    private static CommittedMoveAnimation Cue(int actor)=>new("wish-heal",20,0,new(actor),new(actor)){GeneralAnimationLabel=Label};

    [TestMethod][DataRow(false)][DataRow(true)]
    public void NamedGeneralHealsCurrentRecipientWithTwoGrantingAndFourHealingStars(bool doubles)
    {
        using var resources=new SourceAnimationResources(Folder);var resolved=resources.ResolveGeneral(Label);
        CollectionAssert.AreEquivalent(new[]{"impact","sparkle-2","blue-star"},resolved.Assets.Keys.ToArray());
        Assert.AreEqual(0,resolved.Script.MoveId);Assert.AreEqual(Label,resolved.Script.Label);
        for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var battlers=Battlers(doubles);var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);
            assets.Add("background",new(240,112,Enumerable.Repeat((ushort)0x83e0,240*112).ToArray()));
            foreach(var b in battlers)assets.Add(b.Asset,new(64,64,Enumerable.Repeat((ushort)0x801f,4096).ToArray()));
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,battlers);var audio=new Audio();
            var runtime=new SourceBattleAnimation(resolved.Script,Cue(actor),battlers,doubles,audio);
            var repeat=new SourceBattleAnimation(resolved.Script,Cue(actor),battlers,doubles,new Audio());
            var births=new HashSet<(string Template,int Slot,PixelPoint Position)>();bool dark=false,pixels=false;int frames=0,firstGrant=-1,firstHealing=-1;
            while(runtime.Active&&frames++<512)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(repeat.Tick()));
                foreach(var sprite in frame.Sprites.Where(s=>s.Asset is "sparkle-2" or "blue-star"))
                {
                    births.Add((sprite.Template,sprite.Slot,sprite.Position));
                    if(sprite.Asset=="sparkle-2"&&firstGrant<0)firstGrant=frame.Frame;
                    if(sprite.Asset=="blue-star"&&firstHealing<0)firstHealing=frame.Frame;
                }
                dark|=frame.PaletteBlends.Any(p=>p.Coefficient==10);
                var snapshot=JsonSerializer.Serialize(runtime.Snapshot());var rgba=composer.ComposeField(frame);composer.ComposeField(frame);
                Assert.AreEqual(snapshot,JsonSerializer.Serialize(runtime.Snapshot()));
                if(frame.Sprites.Any(s=>s.Asset is "sparkle-2" or "blue-star"))pixels|=!rgba.SequenceEqual(composer.ComposeField(frame with{Sprites=[]}));
                if(firstHealing>=0)Assert.IsFalse(frame.Sprites.Any(s=>s.Asset=="sparkle-2"),"Source waitforvisualfinish drains granting stars before HealingEffect.");
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(repeat.Active);Assert.IsTrue(dark);Assert.IsTrue(pixels);
            Assert.AreEqual(2,births.Count(b=>b.Template=="gGrantingStarsSpriteTemplate"));
            Assert.AreEqual(4,births.Count(b=>b.Template=="gHealingBlueStarSpriteTemplate"));
            CollectionAssert.AreEqual(new[]{"SE_M_MEGA_KICK","SE_M_ABSORB_2"},audio.Cues.Select(c=>c.Id).ToArray());
            Assert.IsTrue(audio.Cues.All(c=>c.Pan==(actor%2==0?-64:63)));
            // General script runs after BuildOamBuffer; its new sprites enter OAM next frame.
            Assert.AreEqual(audio.Cues[0].Frame+1,firstGrant);Assert.AreEqual(audio.Cues[1].Frame+1,firstHealing);
            var final=runtime.Snapshot();Assert.AreEqual(0,final.Sprites.Length);Assert.AreEqual(0,final.PaletteBlends.Length);
            Assert.IsFalse(runtime.HasResidualSprites);Assert.IsFalse(runtime.GraphicsLoaded);Assert.AreEqual(0,runtime.ActiveVisualCount);
            Assert.IsTrue(final.Offsets.All(x=>x==default));
        }
    }

    [TestMethod]
    public void CancellationAndCompletedReplayGateReleaseAllNamedGeneralState()
    {
        using var resources=new SourceAnimationResources(Folder);var script=resources.ResolveGeneral(Label).Script;
        foreach(int stop in new[]{1,20,70,140,220,512})
        {
            var audio=new Audio();var gate=new SourceBattleAnimationGate();var cue=Cue(0);
            Assert.IsTrue(gate.TryStart(cue,()=>new(script,cue,Battlers(true),true,audio)));int reached=0;
            while(gate.IsBlocking&&reached<stop){gate.Current!.Tick();reached++;}
            Assert.IsTrue(reached==stop||!gate.IsBlocking);
            int sounds=audio.Cues.Count;gate.Current!.Cancel();gate.Current.Cancel();
            Assert.IsFalse(gate.IsBlocking);Assert.IsFalse(gate.Current.HasResidualSprites);Assert.IsFalse(gate.Current.GraphicsLoaded);
            Assert.AreEqual(0,gate.Current.Snapshot().Sprites.Length);Assert.AreEqual(0,gate.Current.Snapshot().PaletteBlends.Length);
            Assert.AreEqual(sounds,audio.Cues.Count);Assert.IsFalse(gate.TryStart(cue,()=>throw new InvalidOperationException("Duplicate general receipt must not recreate the animation.")));
        }
    }

    [TestMethod]
    public void SharedNamedCatalogKeepsHeldItemAndWishSeparateWithoutNewMoveAdmission()
    {
        using var resources=new SourceAnimationResources(Folder);
        Assert.IsTrue(resources.GeneralScripts.ContainsKey("General_HeldItemEffect"));Assert.IsTrue(resources.GeneralScripts.ContainsKey(Label));
        var held=resources.ResolveGeneral("General_HeldItemEffect");var wish=resources.ResolveGeneral(Label);
        CollectionAssert.AreEquivalent(new[]{"sparkle-2","thin-ring","impact"},held.Assets.Keys.ToArray());
        Assert.IsFalse(held.Script.Subroutines.ContainsKey("HealingEffect"));Assert.IsTrue(wish.Script.Subroutines.ContainsKey("HealingEffect"));
        Assert.AreEqual(273,resources.Resolve(273).Script.MoveId);Assert.AreEqual(0,wish.Script.MoveId);
    }
}
