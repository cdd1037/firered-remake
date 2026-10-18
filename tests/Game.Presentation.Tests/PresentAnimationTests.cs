using System.Reflection;
using System.Text.Json;
using Game.Domain.Randomness;
using Game.Presentation.BattleAnimations;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class PresentAnimationTests
{
    private const string Bag = "gPresentSpriteTemplate", Heal = "gPresentHealParticleSpriteTemplate";
    private const string Explosion = "gExplosionSpriteTemplate", Star = "gHealingBlueStarSpriteTemplate";
    private const string Predicate = "AnimTask_IsHealingMove";
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static string Folder => Path.Combine(TestAssets.Root, "content/generated/battle/animations");
    private static readonly (bool Doubles, int Actor, int Target)[] Layouts =
        [(false, 0, 1), (false, 1, 0), (true, 0, 3), (true, 1, 2), (true, 2, 0), (true, 3, 1)];
    private sealed class Audio : ISourceAnimationAudio
    {
        public readonly List<SourceAnimationSound> Cues = [];
        public int Stops;
        public bool IsPlaying => false;
        public void Play(SourceAnimationSound cue) => Cues.Add(cue);
        public void StopAnimationEffects() => Stops++;
    }
    private sealed class Occupied(int slot, int fragments, bool affine = false) : SourceBattleAnimation.VisualSprite(slot, 100)
    {
        public override bool Counted => false;
        public override SourceVisualSpriteFrame Frame => new(Slot, "retained", default, default, 0, false, false, false, 256, null, 100)
        { Affine = affine, Subsprites = Enumerable.Repeat(new SourceSpriteSubsprite(0, 0, 8, 8, 0, 0), fragments).ToArray() };
        public override void Step(SourceBattleAnimation owner) { }
    }
    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, Flags)!.GetValue(value)!;
    private static SourceAnimationCommand C(string op, params string[] args) => new(op, args, 1);
    private static CommittedMoveAnimation Cue(int actor, int target, bool? healing)
        => new("present-root", 41, 217, new(actor), new(target)) { IsHealingMove = healing };
    private static AnimationBattler[] Bodies(bool doubles, bool hiddenPartners = false)
        => Enumerable.Range(0, doubles ? 4 : 2).Select(i => new AnimationBattler(new(i), "body" + i, 19, 0, 64, 64)
        { SourceVisibility = new(true, hiddenPartners && i >= 2, hiddenPartners && i >= 2) }).ToArray();
    private static ushort[] Terrain(SourceAnimationResources resources)
    {
        var surface = resources.CreateBackgroundSurface(0);
        return Enumerable.Range(0, 16).Select(i => surface.PaletteColor(2, i)).ToArray();
    }
    private static SourceAnimationScript Primitive(SourceAnimationScript script) => script with
    {
        Commands = [C("end")], Labels = new Dictionary<string, int>(),
        Subroutines = new Dictionary<string, SourceAnimationCommand[]>(),
        SubroutineLabels = new Dictionary<string, IReadOnlyDictionary<string, int>>()
    };
    private static SourceAnimationScript Prepend(SourceAnimationScript script, params SourceAnimationCommand[] commands)
        => script with
        {
            Commands = [.. commands, .. script.Commands],
            Labels = script.Labels.ToDictionary(p => p.Key, p => p.Value + commands.Length)
        };
    private static SourceAnimationScript ReplaceRecipe(SourceAnimationScript script, string name, SourceSpriteRecipe recipe)
        => script with { SpriteRecipes = script.SpriteRecipes.ToDictionary(p => p.Key, p => p.Key == name ? recipe : p.Value) };

    [TestMethod, DataRow(false), DataRow(true)]
    public void BothCompleteBranchesComposeExactBirthCountsInAllSixLayoutsWithoutGameplayOrMaterialMutation(bool healing)
    {
        using var resources = new SourceAnimationResources(Folder);
        var resolved = resources.Resolve(217);
        foreach (var (doubles, actor, target) in Layouts)
        {
            var bodies = Bodies(doubles);
            var assets = resolved.Assets.ToDictionary(p => p.Key, p => p.Value);
            assets["background"] = new(240, 112, Enumerable.Repeat((ushort)0x8842, 240 * 112).ToArray());
            foreach (var body in bodies)
                assets[body.Asset] = new(64, 64, Enumerable.Range(0, 4096).Select(i =>
                    (ushort)(i % 64 is > 9 and < 55 && i / 64 is > 5 and < 59 ? i % 7 < 3 ? 0x83e0 : 0x801f : 0)).ToArray());
            string initialMaterials = JsonSerializer.Serialize(assets);
            string initialBodies = JsonSerializer.Serialize(bodies);
            string initialPalettes = JsonSerializer.Serialize(resolved.Script.SpritePalettes);
            var cue = Cue(actor, target, healing);
            string initialCue = JsonSerializer.Serialize(cue);
            var composer = AnimationCompositionFixture.Create(resources, resolved, assets, bodies);
            var scene = new SourceBattleAnimation.SpriteScene();
            // Capture only before attaching an active owner.
            var idle = scene.CaptureIdle(bodies, doubles);
            var audio = new Audio();
            var runtime = new SourceBattleAnimation(resolved.Script, cue, bodies, doubles, audio,
                Terrain(resources), spriteScene: scene, spriteMaterials: assets);
            string initialVisibility = JsonSerializer.Serialize(runtime.Snapshot().BattlerVisibility);
            var replay = new SourceBattleAnimation(resolved.Script, cue, bodies, doubles, new Audio(),
                Terrain(resources), spriteMaterials: assets);
            var seen = new HashSet<SourceBattleAnimation.VisualSprite>();
            var births = new Dictionary<string, int>();
            var painted = new HashSet<string>();
            bool sawAffineBag = false;
            int ticks = 0;
            var gameplay = new RngState(0x12345678, 91);
            var expectedGameplay = gameplay;
            try
            {
                while (runtime.Active && ticks++ < 600)
                {
                    gameplay = Gen3Rng.Next(gameplay, "independent-rule", 100).State;
                    expectedGameplay = Gen3Rng.Next(expectedGameplay, "independent-rule", 100).State;
                    var before = gameplay;
                    var frame = runtime.Tick();
                    Assert.AreEqual(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(runtime.Snapshot()));
                    Assert.AreEqual(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(replay.Tick()));
                    Assert.AreEqual(before, gameplay);
                    Assert.AreEqual(expectedGameplay, gameplay);
                    Assert.IsNull(Field<SourceVisualRandom?>(runtime, "_visualRandom"));
                    foreach (var sprite in scene.Sprites.Where(seen.Add))
                        births[sprite.Frame.Template] = births.GetValueOrDefault(sprite.Frame.Template) + 1;
                    byte[] image = composer.ComposeField(frame);
                    Assert.AreEqual(240 * 112 * 4, image.Length);
                    foreach (string template in frame.Sprites.Where(s => s.Visible).Select(s => s.Template).Distinct())
                        if (!image.SequenceEqual(composer.ComposeField(frame with { Sprites = frame.Sprites.Where(s => s.Template != template).ToArray() })))
                            painted.Add(template);
                    sawAffineBag |= frame.Sprites.Any(s => s.Template == Bag && s.Affine && (s.MatrixB != 0 || s.MatrixC != 0));
                }
                Assert.IsFalse(runtime.Active || replay.Active, $"Present branch {healing} did not drain in layout {doubles}/{actor}/{target}.");
                Assert.AreEqual(1, births.GetValueOrDefault(Bag));
                Assert.AreEqual(healing ? 0 : 5, births.GetValueOrDefault(Explosion));
                Assert.AreEqual(healing ? 9 : 0, births.GetValueOrDefault(Heal));
                Assert.AreEqual(healing ? 4 : 0, births.GetValueOrDefault(Star));
                Assert.AreEqual(healing ? 14 : 6, births.Values.Sum());
                foreach (string template in healing ? new[] { Bag, Heal, Star } : new[] { Bag, Explosion })
                    Assert.IsTrue(painted.Contains(template), template + " must contribute real composed pixels.");
                Assert.IsTrue(sawAffineBag);
                string[] sounds = healing
                    ? ["SE_M_TAIL_WHIP", "SE_M_BUBBLE2", "SE_M_BUBBLE2", "SE_M_BUBBLE2", "SE_M_MORNING_SUN", "SE_M_ABSORB_2"]
                    : ["SE_M_TAIL_WHIP", "SE_M_BUBBLE2", "SE_M_BUBBLE2", "SE_M_BUBBLE2", .. Enumerable.Repeat("SE_M_SELF_DESTRUCT", 5)];
                CollectionAssert.AreEqual(sounds, audio.Cues.Select(x => x.Id).ToArray());
                Assert.AreEqual(0, scene.Count);
                Assert.AreEqual(0, runtime.ActiveVisualCount);
                Assert.IsFalse(runtime.GraphicsLoaded || runtime.HasResidualSprites);
                var final = runtime.Snapshot();
                Assert.AreEqual(0, final.BackgroundSlots.Length);
                Assert.AreEqual(0, final.Sprites.Length);
                Assert.IsFalse(final.BlendEnabled);
                Assert.IsNull(final.Window0);
                Assert.IsNull(final.Window1);
                Assert.IsNull(final.ObjectWindow);
                Assert.IsNull(final.Brightness);
                Assert.IsNull(final.Overlay);
                Assert.IsNull(final.Scanline);
                Assert.IsTrue(final.Offsets.All(x => x == default));
                Assert.IsTrue(final.ObjectOffsets.All(x => x == default));
                Assert.IsTrue(final.BattlerAffine.All(x => !x.DoubleSize && x.XScale == 256 && x.YScale == 256 && x.MatrixB == 0 && x.MatrixC == 0));
                Assert.AreEqual(0, final.PaletteBlends.Length);
                Assert.AreEqual(initialVisibility, JsonSerializer.Serialize(final.BattlerVisibility));
                CollectionAssert.AreEqual(composer.ComposeField(idle), composer.ComposeField(final));
                Assert.AreEqual(initialMaterials, JsonSerializer.Serialize(assets));
                Assert.AreEqual(initialPalettes, JsonSerializer.Serialize(resolved.Script.SpritePalettes));
                Assert.AreEqual(initialBodies, JsonSerializer.Serialize(bodies));
                Assert.AreEqual(initialCue, JsonSerializer.Serialize(cue));
                Assert.AreEqual(Gen3Rng.Next(expectedGameplay, "next-rule"), Gen3Rng.Next(gameplay, "next-rule"));
            }
            finally { runtime.Cancel(); replay.Cancel(); }
        }
    }

    [TestMethod, DataRow(false), DataRow(true)]
    public void CancellationAcrossTravelSettleRestartAndBothOutcomeWavesRestoresHiddenPartners(bool healing)
    {
        using var resources = new SourceAnimationResources(Folder);
        var resolved = resources.Resolve(217);
        foreach (int stop in new[] { 0, 1, 3, 20, 45, 60, 65, 73, 90, 110, 115, 130, 145, 165, 190, 220 })
        {
            var audio = new Audio();
            var scene = new SourceBattleAnimation.SpriteScene();
            var bodies = Bodies(true, true);
            var owner = new SourceBattleAnimation(resolved.Script, Cue(0, 1, healing), bodies, true, audio,
                Terrain(resources), spriteScene: scene, spriteMaterials: resolved.Assets);
            string visibility = JsonSerializer.Serialize(owner.Snapshot().BattlerVisibility);
            for (int n = 0; n < stop && owner.Active; n++) owner.Tick();
            int sounds = audio.Cues.Count;
            owner.Cancel(); owner.Cancel();
            var final = owner.Snapshot();
            Assert.IsFalse(owner.Active || owner.GraphicsLoaded || owner.HasResidualSprites);
            Assert.AreEqual(0, scene.Count);
            Assert.AreEqual(0, owner.ActiveVisualCount);
            Assert.AreEqual(0, final.Sprites.Length);
            Assert.AreEqual(0, final.BackgroundSlots.Length);
            Assert.AreEqual(0, final.PaletteBlends.Length);
            Assert.IsFalse(final.BlendEnabled);
            Assert.IsNull(final.Scanline);
            Assert.IsNull(final.Overlay);
            Assert.IsNull(Field<SourceVisualRandom?>(owner, "_visualRandom"));
            Assert.IsTrue(final.Offsets.All(x => x == default));
            Assert.IsTrue(final.ObjectOffsets.All(x => x == default));
            Assert.IsTrue(final.BattlerAffine.All(x => !x.DoubleSize && x.XScale == 256 && x.YScale == 256 && x.MatrixB == 0 && x.MatrixC == 0));
            Assert.AreEqual(visibility, JsonSerializer.Serialize(final.BattlerVisibility));
            Assert.AreEqual(sounds, audio.Cues.Count);
            Assert.AreEqual(1, audio.Stops);
        }
    }

    [TestMethod]
    public void PredicateNeedsCommittedContextOnlyWhenInvokedButUnusedHelpersKeepStaticAbiChecks()
    {
        var script = AnimationCompositionFixture.Script(217);
        var bodies = Bodies(false);
        var task = C("createvisualtask", Predicate, "2");
        var unused = Primitive(script) with
        {
            Subroutines = new Dictionary<string, SourceAnimationCommand[]> { ["UnusedPresentPredicate"] = [task, C("return")] },
            SubroutineLabels = new Dictionary<string, IReadOnlyDictionary<string, int>>
            { ["UnusedPresentPredicate"] = new Dictionary<string, int>() }
        };
        var owner = new SourceBattleAnimation(unused, Cue(0, 1, null), bodies, false, new Audio());
        owner.Tick(); Assert.IsFalse(owner.Active); owner.Cancel();
        var invoked = unused with { Commands = [C("call", "UnusedPresentPredicate"), C("end")] };
        Assert.Throws<SourceAnimationException>(() => new SourceBattleAnimation(invoked, Cue(0, 1, null), bodies, false, new Audio()));
        Assert.Throws<SourceAnimationException>(() => new SourceBattleAnimation(script, Cue(0, 1, null), bodies, false, new Audio()));
        foreach (bool healing in new[] { false, true })
        {
            var exact = new SourceBattleAnimation(invoked, Cue(0, 1, healing), bodies, false, new Audio());
            exact.Tick(); Assert.IsFalse(exact.Active);
            Assert.AreEqual((short)(healing ? 1 : 0), exact.Snapshot().AnimationArguments[7]);
            Assert.AreEqual(0, exact.ActiveVisualCount); exact.Cancel();
        }
        foreach (var bad in new[] { C("createvisualtask", Predicate), C("createvisualtask", Predicate, "-1"), C("createvisualtask", Predicate, "256"), C("createvisualtask", Predicate, "2", "0") })
        {
            var broken = unused with { Subroutines = new Dictionary<string, SourceAnimationCommand[]> { ["UnusedPresentPredicate"] = [bad, C("return")] } };
            Assert.Throws<SourceAnimationException>(() => new SourceBattleAnimation(broken, Cue(0, 1, null), bodies, false, new Audio()));
        }
        var bag = C("createsprite", Bag, "ANIM_TARGET", "2", "0", "-5", "10", "2", "-1");
        var unusedBag = unused with { Subroutines = new Dictionary<string, SourceAnimationCommand[]> { ["UnusedPresentPredicate"] = [bag, C("return")] } };
        var validUnusedBag = new SourceBattleAnimation(unusedBag, Cue(0, 1, null), bodies, false, new Audio());
        validUnusedBag.Tick(); Assert.IsFalse(validUnusedBag.Active); validUnusedBag.Cancel();
        var malformed = ReplaceRecipe(unusedBag, Bag, script.SpriteRecipes[Bag] with { Width = 16 });
        Assert.Throws<SourceAnimationException>(() => new SourceBattleAnimation(malformed, Cue(0, 1, null), bodies, false, new Audio()));
    }

    [TestMethod]
    public void NativeBranchCapacityCountsWorstReachablePathAndPreservesRetainedOwners()
    {
        var script = AnimationCompositionFixture.Script(217);
        Assert.AreEqual(14, SourceAnimationProgramDependencies.MaximumCost(script, c => c.Op == "createsprite" ? 1 : 0));
        foreach (int fragments in new[] { 28, 29 })
        {
            var scene = new SourceBattleAnimation.SpriteScene();
            scene.Sprites.Add(new Occupied(63, fragments));
            var audio = new Audio();
            if (fragments == 28)
            {
                var owner = new SourceBattleAnimation(script, Cue(0, 1, true), Bodies(true), true, audio, spriteScene: scene);
                owner.Cancel();
            }
            else Assert.Throws<SourceAnimationException>(() => new SourceBattleAnimation(script, Cue(0, 1, true), Bodies(true), true, audio, spriteScene: scene));
            Assert.AreEqual(1, scene.Count);
            Assert.AreEqual(0, audio.Cues.Count);
        }
        foreach (int matrices in new[] { 27, 28 })
        {
            var scene = new SourceBattleAnimation.SpriteScene();
            for (int i = 0; i < matrices; i++) scene.Sprites.Add(new Occupied(63 - i, 0, true));
            var audio = new Audio();
            if (matrices == 27)
            {
                var owner = new SourceBattleAnimation(script, Cue(0, 1, false), Bodies(true), true, audio, spriteScene: scene);
                owner.Cancel();
            }
            else Assert.Throws<SourceAnimationException>(() => new SourceBattleAnimation(script, Cue(0, 1, false), Bodies(true), true, audio, spriteScene: scene));
            Assert.AreEqual(matrices, scene.Count);
            Assert.AreEqual(0, audio.Cues.Count);
        }
        foreach (int pendingSounds in new[] { 14, 15 })
        {
            var bounded = Prepend(script, Enumerable.Repeat(C("waitplaysewithpan", "SE_M_TAIL_WHIP", "0", "1"), pendingSounds).ToArray());
            var audio = new Audio();
            if (pendingSounds == 14)
            {
                var owner = new SourceBattleAnimation(bounded, Cue(0, 1, false), Bodies(true), true, audio);
                owner.Cancel();
            }
            else Assert.Throws<SourceAnimationException>(() => new SourceBattleAnimation(bounded, Cue(0, 1, false), Bodies(true), true, audio));
            Assert.AreEqual(0, audio.Cues.Count);
        }
    }

    [TestMethod]
    public void CorruptFramesAffineMaterialsOperandsAndCompanionOwnersFailBeforeEffects()
    {
        var script = AnimationCompositionFixture.Script(217);
        void Reject(SourceAnimationScript broken, AnimationBattler[]? bodies = null)
        {
            var scene = new SourceBattleAnimation.SpriteScene(); var audio = new Audio();
            Assert.Throws<SourceAnimationException>(() => new SourceBattleAnimation(broken, Cue(0, 1, true), bodies ?? Bodies(false), false, audio, spriteScene: scene));
            Assert.AreEqual(0, scene.Count); Assert.AreEqual(0, audio.Cues.Count);
        }
        foreach (string name in new[] { Bag, Heal })
        {
            var recipe = script.SpriteRecipes[name];
            foreach (var broken in new[]
            {
                recipe with { Asset = "wrong-material" }, recipe with { Callback = "UnsupportedCallback" },
                recipe with { Width = 8 }, recipe with { Height = 8 }, recipe with { Blend = true },
                recipe with { AffineDoubleSize = true }, recipe with { Animations = [] },
                recipe with { LoopStarts = [0] }, recipe with { Subsprites = [new(0, 0, 8, 8, 0, 0)] },
                recipe with { FrameLoops = [[new(1, 0, 1)]] },
                recipe with { Animations = [recipe.Animations[0].Select((f, i) => i == 0 ? f with { Duration = f.Duration + 1 } : f).ToArray()] }
            }) Reject(ReplaceRecipe(script, name, broken));
            Reject(script with { SpriteRecipes = script.SpriteRecipes.Where(p => p.Key != name).ToDictionary(p => p.Key, p => p.Value) });
        }
        var bag = script.SpriteRecipes[Bag];
        Reject(ReplaceRecipe(script, Bag, bag with { AffineAnimations = [] }));
        var affine = bag.AffineAnimations.Select(p => p.ToArray()).ToArray();
        affine[1][0] = affine[1][0] with { Duration = affine[1][0].Duration + 1 };
        Reject(ReplaceRecipe(script, Bag, bag with { AffineAnimations = affine }));
        Reject(ReplaceRecipe(script, Heal, script.SpriteRecipes[Heal] with { AffineAnimations = [bag.AffineAnimations[0]] }));
        Reject(Prepend(script, C("playsewithpan", "SE_M_TAIL_WHIP", "128")));
        Reject(script with { SineTable = script.SineTable.Take(319).ToArray() });
        foreach (var bad in new[]
        {
            C("createsprite", Bag, "2", "2", "0", "-5", "10", "2", "-1"),
            C("createsprite", Bag, "ANIM_TARGET", "128", "0", "-5", "10", "2", "-1"),
            C("createsprite", Bag, "ANIM_TARGET", "2", "32768", "-5", "10", "2", "-1"),
            C("createsprite", Heal, "ANIM_TARGET", "4", "0", "32", "-3"),
            C("createsprite", Heal, "ANIM_TARGET", "4", "0", "32", "-32769", "1"),
            C("createvisualtask", Predicate, "256")
        }) Reject(Prepend(script, bad));
        Reject(Prepend(script, C("createvisualtask", "AnimTask_ShakeMon", "2", "ANIM_TARGET", "1", "0", "2", "1")));
        var geometry = Bodies(false); geometry[0] = geometry[0] with { SourceWidth = 7 }; Reject(script, geometry);
        geometry = Bodies(false); geometry[1] = geometry[1] with { SourceHeight = 72 }; Reject(script, geometry);
        geometry = Bodies(false); geometry[1] = geometry[1] with { SourceVisibility = new(false, false, false) }; Reject(script, geometry);
    }

    [TestMethod]
    public void ResolvedMaterialsPreservePresentIndicesAndSharedCompanionSheets()
    {
        using var resources = new SourceAnimationResources(Folder);
        var resolved = resources.Resolve(217);
        Assert.AreEqual("Move_PRESENT", resolved.Script.Label);
        foreach (string name in new[] { Bag, Heal, Explosion, Star })
        {
            var recipe = resolved.Script.SpriteRecipes[name];
            Assert.IsTrue(resolved.Assets.TryGetValue(recipe.Asset, out var asset), name);
            Assert.IsNotNull(asset, name);
            Assert.AreEqual(asset.Width * asset.Height, asset.Pixels.Length, name);
            Assert.IsTrue(asset.Pixels.Any(pixel => pixel != 0), name);
            if (name is Bag or Heal)
            {
                Assert.AreEqual(asset.Pixels.Length, asset.PaletteIndices.Length, name);
                Assert.AreEqual(16, asset.Palette.Length, name);
                Assert.IsTrue(asset.PaletteIndices.All(i => i < 16), name);
                Assert.IsTrue(asset.PaletteTag >= 0, name);
                CollectionAssert.AreEqual(asset.Palette, resolved.Script.SpritePalettes[asset.PaletteTag], name);
            }
            // Explosion and HealingEffect2 keep their existing RGB555 sheet
            // owner. Present does not add palette writes to these companions.
        }
        var bag = resolved.Script.SpriteRecipes[Bag];
        var heal = resolved.Script.SpriteRecipes[Heal];
        Assert.AreEqual(2, bag.AffineAnimations.Length);
        CollectionAssert.AreEqual(new[] { 30 }, bag.Animations.Single().Select(f => f.Duration).ToArray());
        CollectionAssert.AreEqual(new[] { 0, 4, 8, 12 }, heal.Animations.Single().Select(f => f.Tile).ToArray());
        Assert.IsTrue(heal.Animations.Single().All(f => f.Duration == 4));
        CollectionAssert.AreEqual(new[] { -1 }, bag.LoopStarts);
        CollectionAssert.AreEqual(new[] { -1 }, heal.LoopStarts);
    }
}
