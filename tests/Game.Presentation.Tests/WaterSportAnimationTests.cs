using System.Collections;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;
namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class WaterSportAnimationTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private sealed class Audio : ISourceAnimationAudio
    {
        public readonly List<SourceAnimationSound> Cues = []; public int Stops;
        public bool IsPlaying => false; public void Play(SourceAnimationSound cue) => Cues.Add(cue);
        public void StopAnimationEffects() => Stops++; public void SetPan(int pan, int frame) { }
    }
    private sealed class Occupied(int slot) : SourceBattleAnimation.VisualSprite(slot, 100)
    {
        public override bool Counted => false;
        public override SourceVisualSpriteFrame Frame => new(Slot, "occupied", default, default, 0, false, false, false, 256, null, 100);
        public override void Step(SourceBattleAnimation owner) { }
    }
    private static T Field<T>(object value, string name) => (T)value.GetType().GetField(name, Flags)!.GetValue(value)!;
    private static object? Invoke(object value, string name, params object[] args) => value.GetType().GetMethod(name, Flags)!.Invoke(value, args);
    private static SourceAnimationCommand C(string op, params string[] args) => new(op, args, 1);
    private static CommittedMoveAnimation Cue(int actor = 0, uint seed = 0) => new("water-sport", 1, 346, new(actor), new(actor)) { VisualSeed = seed };
    private static AnimationBattler[] Bodies(bool doubles) => Enumerable.Range(0, doubles ? 4 : 2).Select(i => new AnimationBattler(new(i), "body" + i, 0, 0, 64, 64)).ToArray();
    private static SourceAnimationScript Primitive(SourceAnimationScript source) => source with {
        Commands = [C("end")], Labels = new Dictionary<string, int>(), Subroutines = new Dictionary<string, SourceAnimationCommand[]>(),
        SubroutineLabels = new Dictionary<string, IReadOnlyDictionary<string, int>>() };
    private static SourceBattleAnimation.VisualSprite[] Live(SourceBattleAnimation owner) => Field<IEnumerable>(owner, "_sprites").Cast<SourceBattleAnimation.VisualSprite>().ToArray();

    [TestMethod]
    public void CompleteRootComposesIndexedOrbsAndFourSurfSoundsAtEveryBattlePosition()
    {
        using var resources = new SourceAnimationResources(Path.Combine(TestAssets.Root, "content/generated/battle/animations"));
        var resolved = resources.Resolve(346);
        Assert.AreEqual("Move_WATER_SPORT", resolved.Script.Label);
        Assert.AreEqual(16, resolved.Assets["glowy-blue-orb"].Palette.Length);
        foreach (bool doubles in new[] { false, true }) for (int actor = 0; actor < (doubles ? 4 : 2); actor++)
        foreach (uint seed in new uint[] { 0, uint.MaxValue })
        {
            string why = $"doubles {doubles}, actor {actor}, seed {seed}";
            var bodies = Bodies(doubles); var assets = resolved.Assets.ToDictionary(p => p.Key, p => p.Value);
            assets["background"] = new(240, 112, Enumerable.Repeat((ushort)0x8842, 240 * 112).ToArray());
            foreach (var body in bodies) assets[body.Asset] = new(64, 64, Enumerable.Repeat((ushort)0x83e0, 4096).ToArray());
            var composer = AnimationCompositionFixture.Create(resources, resolved, assets, bodies);
            var scene = new SourceBattleAnimation.SpriteScene(); var audio = new Audio();
            var owner = new SourceBattleAnimation(resolved.Script, Cue(actor, seed), bodies, doubles, audio, spriteScene: scene, spriteMaterials: assets);
            var initial = owner.Snapshot(); var idle = composer.ComposeField(initial); int ticks = 0; bool effectPixels = false;
            var births = new HashSet<SourceBattleAnimation.VisualSprite>(); int maxCount = 0;
            while (owner.Active && ticks++ < 1000)
            {
                var frame = owner.Tick(); var pixels = composer.ComposeField(frame);
                Assert.AreEqual(JsonSerializer.Serialize(frame), JsonSerializer.Serialize(owner.Snapshot()), why + " snapshot is observational");
                if (frame.Sprites.Length != 0) effectPixels |= !pixels.SequenceEqual(composer.ComposeField(frame with { Sprites = [] }));
                foreach (var sprite in Live(owner)) { births.Add(sprite); Assert.IsFalse(sprite.Counted, why); }
                maxCount = Math.Max(maxCount, scene.Count); Assert.IsTrue(scene.Count <= 64, why);
                Assert.IsTrue(frame.Offsets.All(p => p == default), why + " actor pose remains unchanged");
            }
            Assert.IsFalse(owner.Active, why); Assert.IsTrue(effectPixels && births.Count > 50 && maxCount > 1, why);
            Assert.AreEqual(0, scene.Count, why); Assert.AreEqual(0, owner.ActiveVisualCount, why); Assert.IsFalse(owner.GraphicsLoaded, why);
            CollectionAssert.AreEqual(Enumerable.Repeat("SE_M_SURF", 4).ToArray(), audio.Cues.Select(c => c.Id).ToArray(), why);
            CollectionAssert.AreEqual(idle, composer.ComposeField(owner.Snapshot()), why + " terminal composition");
            Assert.AreEqual(JsonSerializer.Serialize(initial.BattlerVisibility), JsonSerializer.Serialize(owner.Snapshot().BattlerVisibility), why);
            owner.Cancel(); Assert.IsNull(Field<SourceVisualRandom?>(owner, "_visualRandom"));
        }
    }

    [TestMethod]
    public void CancellationDuringEmissionBounceSweepAndDrainPreservesForeignRawOwners()
    {
        var source = AnimationCompositionFixture.Script(346);
        foreach (int stop in new[] { 0, 1, 8, 20, 26, 43, 62, 96, 144, 175, 210 })
        {
            var scene = new SourceBattleAnimation.SpriteScene(); var bodies = Bodies(true);
            var prior = new SourceBattleAnimation(Primitive(source), Cue(), bodies, true, new Audio(), spriteScene: scene);
            Invoke(Field<object>(prior, "_sprites"), "Add", new Occupied(63)); prior.Tick(); Assert.IsTrue(prior.HasResidualSprites);
            var audio = new Audio(); var owner = new SourceBattleAnimation(source, Cue(), bodies, true, audio, spriteScene: scene);
            for (int tick = 0; tick < stop && owner.Active; tick++) owner.Tick();
            int sounds = audio.Cues.Count; owner.Cancel(); owner.Cancel();
            Assert.AreEqual(1, scene.Count); Assert.AreEqual(0, owner.ActiveVisualCount); Assert.IsFalse(owner.GraphicsLoaded);
            Assert.AreEqual(1, audio.Stops); Assert.AreEqual(sounds, audio.Cues.Count); Assert.IsNull(Field<SourceVisualRandom?>(owner, "_visualRandom"));
            Assert.IsTrue(owner.Snapshot().Offsets.All(p => p == default)); prior.Cancel(); Assert.AreEqual(0, scene.Count);
        }
    }

    [TestMethod]
    public void MissingMaterialsSeedDuplicateOwnerAndMalformedOperandsFailBeforeMutation()
    {
        var source = AnimationCompositionFixture.Script(346); var audio = new Audio();
        void Reject(SourceAnimationScript script, CommittedMoveAnimation? cue = null)
        {
            var scene = new SourceBattleAnimation.SpriteScene();
            Assert.Throws<SourceAnimationException>(() => new SourceBattleAnimation(script, cue ?? Cue(), Bodies(true), true, audio, spriteScene: scene));
            Assert.AreEqual(0, scene.Count); Assert.AreEqual(0, audio.Cues.Count);
        }
        Reject(source, Cue() with { VisualSeed = null }); Reject(source with { SpriteDependencies = [] });
        Reject(source with { SineTable = source.SineTable[..^1] }); Reject(source with { SpritePalettes = new Dictionary<int, ushort[]>() });
        var recipes = source.SpriteRecipes.ToDictionary(p => p.Key, p => p.Value);
        recipes["gSmallWaterOrbSpriteTemplate"] = recipes["gSmallWaterOrbSpriteTemplate"] with { Width = 16 };
        Reject(source with { SpriteRecipes = recipes });
        Reject(source with { Commands = [C("createvisualtask", "AnimTask_WaterSport", "5"), .. source.Commands] });
        foreach (var args in new[] { new[] { "AnimTask_WaterSport", "-1" }, new[] { "AnimTask_WaterSport", "256" }, new[] { "AnimTask_WaterSport", "5", "0" } })
            Reject(source with { Commands = [C("createvisualtask", args), C("end")] });
        Reject(source with { Commands = [C("createvisualtask", "AnimTask_WaterSport", "5"), C("goto", "finish"), C("end")], Labels = new Dictionary<string, int> { ["finish"] = 2 } });
        var tags = source.SpriteTags.ToDictionary(p => p.Key, p => p.Value); tags.Remove("ANIM_TAG_GLOWY_BLUE_ORB"); Reject(source with { SpriteTags = tags });
        var sounds = source.SoundEffects.ToDictionary(p => p.Key, p => p.Value); sounds.Remove("SE_M_SURF"); Reject(source with { SoundEffects = sounds });
    }
}
