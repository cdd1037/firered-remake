using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass,TestCategory("Game")]
public sealed class MaterialChangeRootAnimationTests
{
    private sealed class Audio:ISourceAnimationAudio
    {public List<SourceAnimationSound> Cues{get;}=[];public bool IsPlaying=>false;public void Play(SourceAnimationSound cue)=>Cues.Add(cue);public void StopAnimationEffects(){}}
    [TestMethod]
    public void BothGeneratedRootsReplayComposeAndRetainExactMaterialThroughIdleHandoffOnEveryLayout()
    {
        using var resources=new SourceAnimationResources(Path.Combine(TestAssets.Root,"content/generated/battle/animations"));
        string art=Path.Combine(TestAssets.Root,"content/generated/battle/art/campaign-hit-art");
        using var manifest=JsonDocument.Parse(File.ReadAllBytes(Path.Combine(art,"manifest.json")));
        var forms=new SourceBattleFormCatalog(art);
        foreach(int move in new[]{144,164})foreach(bool doubles in new[]{false,true})for(int actor=0;actor<(doubles?4:2);actor++)
        {
            var resolved=resources.Resolve(move);Assert.AreEqual(move==144?"Move_TRANSFORM":"Move_SUBSTITUTE",resolved.Script.Label);
            var assets=resolved.Assets.ToDictionary(p=>p.Key,p=>p.Value);
            assets["background"]=new(240,112,Enumerable.Repeat((ushort)0x8421,240*112).ToArray());
            var bodies=Enumerable.Range(0,doubles?4:2).Select(slot=>{
                var material=forms.Resolve(385,new(slot),0,false);var geometry=material.Geometry with{Asset="body-"+slot};
                assets[geometry.Asset]=material.Pixels;return geometry;
            }).ToArray();
            AnimationBattler replacement;SourcePixelAsset pixels;
            if(move==144)
            {
                var material=forms.Resolve(385,new(actor),2,true,true);replacement=material.Geometry with{Asset="replacement"};pixels=material.Pixels;
            }
            else
            {
                var center=bodies[actor].Base(doubles);
                replacement=bodies[actor] with{Asset="replacement",DisplayCenter=new(center.X,center.Y+(actor%2==0?17:16))};
                pixels=SourceHitAnimationCatalog.Asset(manifest,art,actor%2==0?"substitute-back":"substitute-front");
            }
            assets[replacement.Asset]=pixels;var replacements=new Dictionary<int,AnimationBattler>{{actor,replacement}};
            var cue=new CommittedMoveAnimation("material-roots",1,move,new(actor),new(move==164?actor:actor^1));
            var audio=new Audio();var replayAudio=new Audio();var scene=new SourceBattleAnimation.SpriteScene();
            var runtime=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,audio,battlerReplacements:replacements,spriteMaterials:assets,spriteScene:scene);
            var replay=new SourceBattleAnimation(resolved.Script,cue,bodies,doubles,replayAudio,battlerReplacements:replacements,spriteMaterials:assets);
            var composer=AnimationCompositionFixture.Create(resources,resolved,assets,bodies);var initial=runtime.Snapshot();
            bool swapped=false,mosaic=false,hidden=false,changed=false;int ticks=0,swaps=0;
            var original=composer.ComposeField(initial);
            while(runtime.Active&&ticks++<160)
            {
                var frame=runtime.Tick();Assert.AreEqual(JsonSerializer.Serialize(frame),JsonSerializer.Serialize(replay.Tick()));
                byte[] composed=composer.ComposeField(frame);Assert.AreEqual(240*112*4,composed.Length);changed|=!original.SequenceEqual(composed);
                bool next=frame.Battlers[actor].Asset==replacement.Asset;if(next&&!swapped)swaps++;if(swapped)Assert.IsTrue(next);swapped=next;
                mosaic|=frame.BackgroundMosaicHorizontal==15;hidden|=frame.BattlerVisibility[actor].SpriteInvisible;
                Assert.AreEqual(0,frame.Sprites.Length);
            }
            Assert.IsFalse(runtime.Active);Assert.IsFalse(replay.Active);Assert.AreEqual(0,runtime.ActiveVisualCount);Assert.AreEqual(0,scene.Count);
            Assert.AreEqual(1,swaps);Assert.IsTrue(changed);Assert.AreEqual(move==144,mosaic);Assert.IsTrue(hidden);
            CollectionAssert.AreEqual(move==144?new[]{"SE_M_TELEPORT","SE_M_MINIMIZE"}:new[]{"SE_M_ATTRACT","SE_M_BUBBLE2","SE_M_BUBBLE2"},audio.Cues.Select(c=>c.Id).ToArray());
            Assert.IsTrue(audio.Cues.All(c=>c.Pan==(actor%2==0?-64:63)));Assert.AreEqual(JsonSerializer.Serialize(audio.Cues),JsonSerializer.Serialize(replayAudio.Cues));
            if(move==164)CollectionAssert.AreEqual(new[]{0,41,66},audio.Cues.Select(c=>c.Frame).ToArray());
            var completed=runtime.Snapshot();Assert.AreEqual(replacement,completed.Battlers[actor]);
            Assert.AreEqual(0,completed.BackgroundSlots.Length);Assert.IsFalse(completed.BlendEnabled);
            Assert.AreEqual(0,completed.BackgroundMosaicHorizontal);Assert.IsTrue(completed.BattlerBackgroundMosaic.All(x=>!x));
            Assert.IsTrue(completed.Offsets.All(p=>p==default));
            foreach(var battler in completed.Battlers)
            {
                // UpdateOamCoords stores wrapped 9-bit X and 8-bit Y even at
                // rest; low front centers therefore retain a Y offset of 256.
                var center=battler.DisplayCenter??battler.Center(doubles);
                int x=((center.X-32)&511)-center.X+32;if(x>255)x-=512;
                int y=((center.Y-32)&255)-center.Y+32;
                Assert.AreEqual(new PixelPoint(x,y),completed.ObjectOffsets[battler.Slot.Value]);
            }
            var hold=new SourceCompletedBattlerState();Assert.IsTrue(hold.Capture(initial,completed,assets));Assert.IsTrue(hold.HasMaterial(actor));
            var idleAssets=assets.Where(p=>p.Key!=replacement.Asset).ToDictionary(p=>p.Key,p=>p.Value);hold.AddMaterials(idleAssets);
            Assert.AreNotSame(pixels.Pixels,idleAssets[replacement.Asset].Pixels);
            var idleComposer=AnimationCompositionFixture.Create(resources,resolved,idleAssets,bodies);
            byte[] retained=idleComposer.ComposeField(scene.CaptureIdle(bodies,doubles,hold));
            var ordinary=(AnimationBattler[])bodies.Clone();ordinary[actor]=replacement;
            CollectionAssert.AreEqual(composer.ComposeField(completed),retained);
            hold.DropMaterial(actor);Assert.IsFalse(hold.HasMaterial(actor));
            CollectionAssert.AreEqual(retained,idleComposer.ComposeField(scene.CaptureIdle(ordinary,doubles,hold)));
        }
    }
}
