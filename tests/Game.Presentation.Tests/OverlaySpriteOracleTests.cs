using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class OverlaySpriteOracleTests
{
    private const string Mist = "gMistBallSpriteTemplate";
    private const string Glare = "gGlareEyeDotSpriteTemplate";
    private const string Commit = "037335f4c725d7c9aecdac87066f2002b4bd7e14";

    private sealed class Audio : ISourceAnimationAudio
    {
        public bool IsPlaying => false;
        public void Play(SourceAnimationSound cue) { }
        public void StopAnimationEffects() { }
    }

    // Controlled source occupancy is independent of animation visual ownership.
    // Invisible battlers leave all 64 native cells available to this fixture.
    private sealed class OccupiedSprite(int slot) : SourceBattleAnimation.VisualSprite(slot, 255)
    {
        public override bool Counted => false;
        public override SourceVisualSpriteFrame Frame =>
            new(Slot, "oracle-occupied-cell", default, default, 0, false, false, false, 256, null, Subpriority);
        public override void Step(SourceBattleAnimation owner) { }
    }

    private static string Folder => Path.Combine(TestAssets.Root, "content/generated/battle/animations");
    private static JsonDocument Vectors(string family) => JsonDocument.Parse(File.ReadAllBytes(
        Path.Combine(TestAssets.Root, $"tests/Game.Presentation.Tests/Fixtures/overlay-{family}-source-vectors.json")));
    private static int[] Numbers(JsonElement value) => value.EnumerateArray().Select(v => v.GetInt32()).ToArray();
    private static string[] Arguments(JsonElement value) => Numbers(value)
        .Select(v => v.ToString(CultureInfo.InvariantCulture)).ToArray();
    private static CommittedMoveAnimation Cue(int move, int actor, int target) =>
        new("overlay-source-oracle", 1, move, new(actor), new(target));

    private static SourceAnimationScript Primitive(SourceAnimationScript source, string graphics, SourceAnimationCommand command) => source with
    {
        // Keep the canonical recipe, sprite/tag/palette and material dependencies.
        // Only isolate the script control flow surrounding the callback under test.
        Commands = [new("loadspritegfx", [graphics], 1), command, new("waitforvisualfinish", [], 3), new("end", [], 4)],
        Labels = new Dictionary<string, int>(),
        Subroutines = new Dictionary<string, SourceAnimationCommand[]>(),
        SubroutineLabels = new Dictionary<string, IReadOnlyDictionary<string, int>>()
    };

    private static SourceAnimationFrame FirstMistSprite(SourceBattleAnimation runtime)
    {
        for (int tick = 0; tick < 8; tick++)
        {
            var frame = runtime.Tick();
            if (frame.Sprites.Any(s => s.Template == Mist))
            {
                Assert.AreEqual(3, frame.Frame, "Graphics load occupies frames 0–2 before script creation on frame 3.");
                return frame;
            }
        }
        throw new InvalidOperationException("Mist Ball did not reach its first sprite callback.");
    }

    private static void AssertFinished(SourceBattleAnimation runtime, SourceBattleAnimation.SpriteScene scene, string message)
    {
        for (int tick = 0; runtime.Active && tick < 8; tick++) runtime.Tick();
        Assert.IsFalse(runtime.Active, message);
        Assert.AreEqual(0, runtime.ActiveVisualCount, message);
        Assert.IsFalse(runtime.HasResidualSprites, message);
        Assert.IsFalse(scene.HasResidualSprites, message);
        Assert.AreEqual(0, scene.Count, message);
        Assert.AreEqual(0, runtime.Snapshot().Sprites.Length, message);
        Assert.IsFalse(runtime.GraphicsLoaded, message);
    }

    private static T Field<T>(object instance, string name)
    {
        var field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(field, $"Missing source-state observation {instance.GetType().Name}.{name}.");
        return (T)field.GetValue(instance)!;
    }

    [TestMethod]
    public void MistBallOrdinarySourceVectorsMatchBothLayoutsAllFourFlagsAndDestruction()
    {
        using var vectors = Vectors("mist");
        var root = vectors.RootElement;
        Assert.AreEqual("firered-mist-ball-source-v1", root.GetProperty("schema").GetString());
        Assert.AreEqual(Commit, root.GetProperty("commit").GetString());
        CollectionAssert.AreEqual(Numbers(root.GetProperty("stub_picture_offsets")),
            Numbers(root.GetProperty("normal_coord2_picture_offsets")), "Normal vectors give both source coordinate helpers the same species geometry.");

        using var resources = new SourceAnimationResources(Folder);
        var source = resources.Resolve(296).Script;
        var command = source.Commands.Single(c => c.Op == "createsprite" && c.Args[0] == Mist);
        CollectionAssert.AreEqual(Arguments(root.GetProperty("recipe")), command.Args.Skip(3).ToArray());
        var offsets = Numbers(root.GetProperty("stub_picture_offsets"));
        int ordinary = 0, tested = 0, unsupportedSinglePositions = 0;

        foreach (var scenario in root.GetProperty("cases").EnumerateArray())
        {
            // Divergent helper, manually seeded accumulator and geometry stress
            // probes are explicit source-helper tests, not representable battlers.
            if (scenario.GetProperty("probe").GetBoolean()) continue;
            ordinary++;
            bool doubles = scenario.GetProperty("double").GetBoolean();
            int actor = scenario.GetProperty("attacker").GetInt32(), target = scenario.GetProperty("target").GetInt32();
            if (!doubles && (actor >= 2 || target >= 2)) { unsupportedSinglePositions++; continue; }
            string message = $"{scenario.GetProperty("name").GetString()} double={doubles} actor={actor} target={target}";
            Assert.IsFalse(scenario.GetProperty("divergent_coord2").GetBoolean(), message);
            Assert.IsFalse(scenario.GetProperty("equal_x").GetBoolean(), message);
            // Center(animationTarget:true) adds eight pixels on the player side.
            // Subtract those eight from the fixture picture offset at construction.
            var battlers = Enumerable.Range(0, doubles ? 4 : 2).Select(i =>
                new AnimationBattler(new(i), "b" + i, offsets[i] - (i % 2 == 0 ? 8 : 0), 0, 64, 64)).ToArray();
            var primitive = Primitive(source, "ANIM_TAG_SMALL_BUBBLES", command with
            { Args = [.. command.Args.Take(3), .. Arguments(scenario.GetProperty("args"))] });
            var scene = new SourceBattleAnimation.SpriteScene();
            var runtime = new SourceBattleAnimation(primitive, Cue(296, actor, target), battlers, doubles, new Audio(), spriteScene: scene);
            var frame = FirstMistSprite(runtime);
            int invocation = 1, compared = 0;

            foreach (var row in scenario.GetProperty("rows").EnumerateArray())
            {
                int expectedInvocation = row.GetProperty("invocation").GetInt32();
                // Wrapper invocation 0 is synchronous inside createsprite; the
                // same Tick then executes initializer invocation 1 in AnimateSprites.
                if (expectedInvocation < 1) continue;
                while (invocation < expectedInvocation) { frame = runtime.Tick(); invocation++; }
                string at = $"{message} invocation={invocation}";
                Assert.AreEqual(2, row.GetProperty("phase").GetInt32(), at);
                Assert.AreEqual(row.GetProperty("visual_count").GetInt32(), runtime.ActiveVisualCount, at);
                CollectionAssert.AreEqual(Numbers(row.GetProperty("args")).Select(v => (short)v).ToArray(), frame.AnimationArguments.Take(8).ToArray(), at);
                Assert.IsTrue(frame.AnimationArguments.Skip(8).All(value=>value==0),at);
                bool alive = row.GetProperty("alive").GetInt32() != 0;
                var sprites = frame.Sprites.Where(s => s.Template == Mist).ToArray();
                Assert.AreEqual(alive ? 1 : 0, sprites.Length, at);
                Assert.AreEqual(alive ? 1 : 0, scene.Count, at);
                if (alive)
                {
                    var position = Numbers(row.GetProperty("position"));
                    var sprite = sprites.Single();
                    Assert.AreEqual(new PixelPoint(position[0], position[1]), sprite.Position, at);
                    Assert.AreEqual(new PixelPoint(position[2], position[3]), sprite.Offset, at);
                    Assert.AreEqual(doubles ? 4 : 2, sprite.Slot, at);
                    Assert.AreEqual("small-bubbles", sprite.Asset, at);
                    Assert.AreEqual(16, sprite.Width, at); Assert.AreEqual(16, sprite.Height, at);
                    Assert.AreEqual(0, sprite.Tile, at); Assert.AreEqual(256, sprite.Scale, at);
                    Assert.IsTrue(sprite.Visible, at); Assert.IsFalse(sprite.Affine, at);
                    Assert.IsFalse(sprite.DoubleSize, at); Assert.IsFalse(sprite.Blend, at);
                    Assert.IsFalse(sprite.HFlip, at); Assert.IsFalse(sprite.VFlip, at);
                }
                compared++;
            }

            Assert.AreEqual(33, compared, message);
            Assert.AreEqual(33, invocation, message);
            Assert.AreEqual(0, runtime.ActiveVisualCount, message);
            AssertFinished(runtime, scene, message);
            tested++;
        }

        Assert.AreEqual(160, ordinary);
        Assert.AreEqual(60, unsupportedSinglePositions, "Single battles accept only positions 0 and 1.");
        Assert.AreEqual(100, tested, "Five argument programs across four single and sixteen double ordered pairs.");
    }

    [TestMethod]
    public void GlareSourceFramesMatchGeometryRawChildrenOamPhaseSlotReuseAndSaturation()
    {
        using var vectors = Vectors("glare");
        var root = vectors.RootElement;
        Assert.AreEqual("firered.glare.source-oracle.v1", root.GetProperty("schema").GetString());
        Assert.AreEqual(Commit, root.GetProperty("referenceCommit").GetString());
        using var resources = new SourceAnimationResources(Folder);
        var source = resources.Resolve(137).Script;
        var command = source.Commands.Single(c => c.Op == "createvisualtask" && c.Args[0] == "AnimTask_GlareEyeDots");
        var primitive = Primitive(source, "ANIM_TAG_SMALL_RED_EYE", command);
        int tested = 0, normal = 0, saturated = 0;

        foreach (var scenario in root.GetProperty("scenarios").EnumerateArray())
        {
            string name = scenario.GetProperty("name").GetString()!;
            bool ordinary = name.StartsWith("single_a", StringComparison.Ordinal) || name.StartsWith("double_a", StringComparison.Ordinal);
            if (!ordinary && name is not ("full_saturation" or "one_free_slot_partial_pairs")) continue;
            bool doubles = scenario.GetProperty("layout").GetString() == "double";
            int actor = scenario.GetProperty("attacker").GetInt32(), target = scenario.GetProperty("target").GetInt32();
            if (!doubles && (actor >= 2 || target >= 2)) continue;
            int occupied = scenario.GetProperty("occupiedSlots").GetInt32();
            var inputs = scenario.GetProperty("battlerInputs").EnumerateArray().Take(doubles ? 4 : 2).ToArray();
            var battlers = inputs.Select(input =>
            {
                int id = input.GetProperty("battler").GetInt32();
                return new AnimationBattler(new(id), "b" + id,
                    input.GetProperty("pictureYOffset").GetInt32() - (id % 2 == 0 ? 8 : 0), 0, 64,
                    input.GetProperty("heightRead").GetInt32()) { Visible = false };
            }).ToArray();
            for (int i = 0; i < battlers.Length; i++)
                Assert.AreEqual(new PixelPoint(inputs[i].GetProperty("x2Read").GetInt32(), inputs[i].GetProperty("yPicRead").GetInt32()),
                    battlers[i].Center(doubles, true), name);
            var scene = new SourceBattleAnimation.SpriteScene();
            var runtime = new SourceBattleAnimation(primitive, Cue(137, actor, target), battlers, doubles, new Audio(), spriteScene: scene);
            var fillers = Enumerable.Range(0, occupied).Select(slot => new OccupiedSprite(slot) { Owner = runtime }).ToArray();
            foreach (var filler in fillers) scene.Sprites.Add(filler);
            for (int tick = 0; tick < 3; tick++)
            {
                var loading = runtime.Tick();
                Assert.AreEqual(0, runtime.ActiveVisualCount, name);
                Assert.AreEqual(0, loading.Sprites.Count(s => s.Template == Glare), name);
            }

            var instances = new Dictionary<SourceBattleAnimation.VisualSprite, int>();
            int successfulAllocations = 0, peakChildren = 0, lastFrame = 0;
            foreach (var row in scenario.GetProperty("frames").EnumerateArray())
            {
                int sourceFrame = row.GetProperty("frame").GetInt32();
                // Init and task step 1 share runtime frame 3. Source frame 0
                // is the initializer-only observation, before the task pass.
                if (sourceFrame == 0) continue;
                var frame = runtime.Tick();
                string at = $"{name} sourceFrame={sourceFrame}";
                Assert.AreEqual(sourceFrame + 2, frame.Frame, at);
                Assert.AreEqual(row.GetProperty("visualCount").GetInt32(), runtime.ActiveVisualCount, at);
                Assert.IsTrue(frame.AnimationArguments.All(value => value == 0), at);

                foreach (var allocation in row.GetProperty("allocations").EnumerateArray())
                {
                    var values = Numbers(allocation);
                    if (values[6] == 64) continue;
                    var child = scene.Sprites[values[6]];
                    Assert.IsNotNull(child, at);
                    Assert.IsFalse(instances.ContainsKey(child), "A reused slot must own a fresh sprite instance. " + at);
                    instances.Add(child, values[7]);
                    successfulAllocations++;
                }

                var children = scene.Sprites.Where(s => s.Frame.Template == Glare).OrderBy(s => s.Slot).ToArray();
                var expectedChildren = row.GetProperty("children").EnumerateArray().Select(Numbers).OrderBy(v => v[0]).ToArray();
                Assert.AreEqual(expectedChildren.Length, children.Length, at);
                Assert.AreEqual(occupied + children.Length, scene.Count, at);
                peakChildren = Math.Max(peakChildren, children.Length);
                for (int i = 0; i < children.Length; i++)
                {
                    var expected = expectedChildren[i]; var child = children[i];
                    AssertGlareSprite(expected, child.Frame, at);
                    Assert.AreEqual(expected[1], instances[child], at);
                    Assert.AreEqual(expected[6], Field<int>(child, "_timer"), at);
                    Assert.IsFalse(child.Counted, "Raw children must not increment the script visual count. " + at);
                    Assert.AreSame(runtime, child.Owner, at);
                }

                // Source BuildOamBuffer is before RunTasks: newly allocated dots
                // are present in children above, but absent from oamChildren here.
                var oam = frame.Sprites.Where(s => s.Template == Glare).OrderBy(s => s.Slot).ToArray();
                var expectedOam = row.GetProperty("oamChildren").EnumerateArray().Select(Numbers).OrderBy(v => v[0]).ToArray();
                Assert.AreEqual(expectedOam.Length, oam.Length, at);
                for (int i = 0; i < oam.Length; i++) AssertGlareSprite(expectedOam[i], oam[i], at);

                var tasks = Field<IEnumerable>(runtime, "_tasks").Cast<object>().ToArray();
                bool taskAlive = row.GetProperty("taskAlive").GetBoolean();
                Assert.AreEqual(taskAlive ? 1 : 0, tasks.Length, at);
                if (taskAlive)
                {
                    var data = Numbers(row.GetProperty("taskData")); var task = tasks.Single();
                    Assert.AreEqual(data[0] != 0, Field<bool>(task, "_waiting"), at);
                    Assert.AreEqual(data[1], Field<int>(task, "_timer"), at);
                    Assert.AreEqual(data[2], Field<int>(task, "_pair"), at);
                    Assert.AreEqual(data[10], Field<int>(task, "_active"), at);
                    Assert.AreEqual(new PixelPoint(data[11], data[12]), Field<PixelPoint>(task, "_start"), at);
                    Assert.AreEqual(new PixelPoint(data[13], data[14]), Field<PixelPoint>(task, "_end"), at);
                }
                else
                {
                    Assert.IsTrue(row.GetProperty("taskDestroyedInTaskPhase").GetBoolean(), at);
                    Assert.AreEqual(0, children.Length, "The last child releases its parent in the same frame. " + at);
                    Assert.AreEqual(0, runtime.ActiveVisualCount, at);
                }
                lastFrame = sourceFrame;
            }

            var summary = scenario.GetProperty("summary");
            Assert.AreEqual(summary.GetProperty("lastFrame").GetInt32(), lastFrame, name);
            Assert.AreEqual(summary.GetProperty("successfulChildren").GetInt32(), successfulAllocations, name);
            Assert.AreEqual(summary.GetProperty("peakChildren").GetInt32(), peakChildren, name);
            foreach (var filler in fillers) Assert.IsTrue(scene.Sprites.Remove(filler), name);
            AssertFinished(runtime, scene, name);
            tested++; if (ordinary) normal++; else saturated++;
        }

        Assert.AreEqual(20, normal, "All four legal single and sixteen double ordered actor/target pairs.");
        Assert.AreEqual(2, saturated, "Both 64 occupied cells and one free cell are exercised.");
        Assert.AreEqual(22, tested);
    }

    private static void AssertGlareSprite(int[] expected, SourceVisualSpriteFrame actual, string message)
    {
        Assert.AreEqual(expected[0], actual.Slot, message);
        Assert.AreEqual(new PixelPoint(expected[2], expected[3]), actual.Position, message);
        Assert.AreEqual(new PixelPoint(expected[4], expected[5]), actual.Offset, message);
        Assert.AreEqual(expected[9], actual.Subpriority, message);
        Assert.AreEqual(Glare, actual.Template, message); Assert.AreEqual("small-red-eye", actual.Asset, message);
        Assert.AreEqual(8, actual.Width, message); Assert.AreEqual(8, actual.Height, message);
        Assert.AreEqual(0, actual.Tile, message); Assert.AreEqual(256, actual.Scale, message);
        Assert.IsTrue(actual.Visible, message); Assert.IsFalse(actual.Affine, message);
        Assert.IsFalse(actual.DoubleSize, message); Assert.IsFalse(actual.Blend, message);
        Assert.IsFalse(actual.HFlip, message); Assert.IsFalse(actual.VFlip, message);
    }
}
