using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class SquashWaterFireNativeTests
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Fixture = "squash-water-fire-source-vectors.json";
    private const string Generate = "python tests/Reference/SquashWaterFire/generate.py --root . --output tests/Game.Presentation.Tests/Fixtures/squash-water-fire-source-vectors.json";
    private static readonly string[] Templates =
    [
        "gEruptionLaunchRockSpriteTemplate", "gEruptionFallingRockSpriteTemplate",
        "gSmallWaterOrbSpriteTemplate", "gWaterHitSplatSpriteTemplate"
    ];

    private sealed class Audio : ISourceAnimationAudio
    {
        public bool IsPlaying => false;
        public void Play(SourceAnimationSound cue) => Assert.Fail("These native particle callbacks do not play sounds.");
        public void StopAnimationEffects() { }
    }

    private static T Field<T>(object value, string name) =>
        (T)(value.GetType().GetField(name, Flags) ?? throw new MissingFieldException(value.GetType().Name, name)).GetValue(value)!;
    private static T Property<T>(object value, string name) =>
        (T)(value.GetType().GetProperty(name, Flags) ?? throw new MissingMemberException(value.GetType().Name, name)).GetValue(value)!;
    private static object? Invoke(object value, string name, params object[] args) =>
        (value.GetType().GetMethod(name, Flags) ?? throw new MissingMethodException(value.GetType().Name, name)).Invoke(value, args);
    private static object Nested(string name, params object[] args) =>
        Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(name, BindingFlags.NonPublic)!, Flags, null, args, CultureInfo.InvariantCulture)!;
    private static int I(JsonElement value, string name) => value.GetProperty(name).GetInt32();
    private static int[] V(JsonElement value, string name) => value.GetProperty(name).EnumerateArray().Select(x => x.GetInt32()).ToArray();
    private static PixelPoint Point(JsonElement value, string name) => new(value.GetProperty(name)[0].GetInt32(), value.GetProperty(name)[1].GetInt32());
    private static string Branch(JsonElement value) => value.GetProperty("branch").GetString()!;
    private static SourceAnimationScript Primitive(SourceAnimationScript script) => script with
    {
        Commands = [new("end", [], 1)], Labels = new Dictionary<string, int>(),
        Subroutines = new Dictionary<string, SourceAnimationCommand[]>(),
        SubroutineLabels = new Dictionary<string, IReadOnlyDictionary<string, int>>()
    };

    [TestMethod]
    public void ExtractedCorpusContainsAuthoredLayoutsHpQuarterBoundariesAndAllocationBranches()
    {
        using var document = SourceVectorFixture.Read(Fixture, Generate);
        var cases = ReadCases(document);
        Assert.AreEqual(4, cases.Select(c => I(c, "kind")).Distinct().Count());
        Assert.AreEqual(6, cases.Select(c => (c.GetProperty("doubles").GetBoolean(), I(c, "actor"))).Distinct().Count());
        AssertLayouts(cases.Where(c => I(c, "kind") == 0 && Branch(c) == "authored-root-exact"), "Eruption launch");
        foreach (int kind in new[] { 2, 3 })
        {
            foreach (int tier in Enumerable.Range(0, 4))
                AssertLayouts(cases.Where(c => I(c, "kind") == kind && I(c, "tier") == tier && Branch(c) == "authored-root-exact"), $"Water Spout kind {kind}, tier {tier}");
            foreach (int hp in new[] { 0, 1, 23, 24, 25, 26, 49, 50, 51, 74, 75, 76, 99, 100 })
                Assert.IsTrue(cases.Any(c => I(c, "kind") == kind && I(c, "hp") == hp && I(c, "maxHp") == 100), $"Missing kind {kind} HP quarter boundary {hp}/100.");
            foreach (int maximum in new[] { 1, 2, 3, 101, 65535 })
                Assert.IsTrue(cases.Any(c => I(c, "kind") == kind && I(c, "maxHp") == maximum), $"Missing kind {kind} source max-HP floor boundary {maximum}.");
        }
        foreach (var c in cases.Where(c => I(c, "kind") is 2 or 3))
        {
            int quarter = I(c, "maxHp") / 4, hp = I(c, "hp");
            int tier = hp < quarter ? 0 : hp < quarter * 2 ? 1 : hp < quarter * 3 ? 2 : 3;
            Assert.AreEqual(tier, I(c, "tier"), "The native tier floors max HP before strict quarter comparisons, including max HP below four.");
        }
        int[][] falling = [[200, -32, 0, 100, 0], [30, -32, 16, 90, 1], [150, -32, 32, 60, 2],
            [90, -32, 48, 80, 3], [110, -32, 64, 50, 0], [60, -32, 80, 70, 1]];
        foreach (var args in falling)
            AssertLayouts(cases.Where(c => I(c, "kind") == 1 && Branch(c) == "falling-authored" && V(c, "arguments").Take(5).SequenceEqual(args)), "Authored falling rock " + string.Join(",", args));
        foreach (int delay in new[] { 0, 1 })
            Assert.IsTrue(cases.Any(c => I(c, "kind") == 1 && Branch(c) == "falling-boundary" && V(c, "arguments")[2] == delay), "Missing falling delay boundary " + delay);
        foreach (int tile in Enumerable.Range(0, 4))
            Assert.IsTrue(cases.Any(c => I(c, "kind") == 1 && Branch(c) == "falling-boundary" && V(c, "arguments")[4] == tile), "Missing falling tile boundary " + tile);
        foreach (int power in new[] { 1, 9, 10, 19, 20, 99, 100, 150 })
            Assert.IsTrue(cases.Any(c => I(c, "kind") == 3 && I(c, "movePower") == power), "Missing rain shake power boundary " + power);
        foreach (string branch in new[] { "shifted-base-and-picture-geometry", "hidden-partner", "power-quantization" })
            Assert.IsTrue(cases.Any(c => Branch(c) == branch), "Missing native branch " + branch);
        foreach (var c in cases.Where(c => I(c, "kind") != 1))
        {
            var births = c.GetProperty("rows").EnumerateArray().SelectMany(r => r.GetProperty("births").EnumerateArray()).ToArray();
            int kind = I(c, "kind"), tier = I(c, "tier");
            int expectedChildren = kind == 0 ? 7 : kind == 2 ? (20 + (4 - tier) - 1) / (4 - tier) : 5 * (tier + 1);
            Assert.AreEqual(expectedChildren, births.Count(b => I(b, "template") == (kind == 0 ? 0 : 2)), "Source tier owns the actual successful particle allocation count.");
            if (kind == 3) Assert.AreEqual(expectedChildren, births.Count(b => I(b, "template") == 3), "Every rain drop hands off its retained parent count to one impact.");
        }
        foreach (int actor in new[] { 0, 1 })
        {
            var c = cases.Single(c => I(c, "kind") == 3 && !c.GetProperty("doubles").GetBoolean() && I(c, "actor") == actor && I(c, "tier") == 3 && Branch(c) == "authored-root-exact");
            var x = c.GetProperty("rows").EnumerateArray().SelectMany(r => r.GetProperty("births").EnumerateArray()).Where(b => I(b, "template") == 2).Take(4).Select(b => I(b, "x")).ToArray();
            CollectionAssert.AreEqual(actor == 0 ? new[] { 185, 116, 97, 124 } : new[] { 65, -34, 35, -44 }, x, "Signed32 wrapped recurrence and signed modulo must remain source exact.");
        }
        var rain = cases.Single(c => I(c, "kind") == 3 && c.GetProperty("doubles").GetBoolean() && I(c, "actor") == 0 && I(c, "target") == 1 && I(c, "tier") == 3 && Branch(c) == "authored-root-exact");
        var rainRows = rain.GetProperty("rows").EnumerateArray().ToArray();
        var splashes = rainRows.SelectMany(r => r.GetProperty("births").EnumerateArray()).Where(b => I(b, "template") == 3).ToArray();
        Assert.IsTrue(splashes.Any(b => I(b, "slot") < I(b, "parentSlot")), "A lower-slot impact must wait for the next sprite scan.");
        Assert.IsTrue(splashes.Any(b => I(b, "slot") > I(b, "parentSlot")), "A higher-slot impact must run during its birth-frame sprite scan.");
        foreach (var row in rainRows)
        {
            foreach (var birth in row.GetProperty("births").EnumerateArray().Where(b => I(b, "template") == 3))
            {
                var splash = row.GetProperty("sprites").EnumerateArray().Single(s => I(s, "instance") == I(birth, "instance"));
                bool later = I(birth, "slot") > I(birth, "parentSlot");
                Assert.AreEqual(later ? 1 : 0, V(splash, "data")[1], "Birth-frame callback visits follow native raw-slot traversal.");
                Assert.AreEqual(later ? 128 : 256, I(splash, "xScale"), "Lower-slot births retain unstarted affine scale.");
                CollectionAssert.AreEqual(later ? new[] { 512, 0, 0, 512 } : new[] { 256, 0, 0, 256 }, V(splash, "matrix"), "Birth-frame affine update follows callback visitation.");
            }
        }
        var firstShakes = rainRows.First(r => r.GetProperty("shakes").GetArrayLength() > 0).GetProperty("shakes").EnumerateArray().ToArray();
        CollectionAssert.AreEqual(new[] { 1, 1 }, firstShakes.Select(s => V(s, "data")[1]).ToArray(), "Both later-priority shake tasks run in their birth frame.");
        Assert.IsTrue(rainRows.Any(r => I(r, "alive") == 0 && I(r, "activeTasks") > 0), "The trace must continue after the rain root while its shake tasks remain active.");
    }

    [TestMethod]
    public void EveryNativeFrameMatchesRegistersSquashGeometryParticleHandoffsAndLiveTaskScheduling()
    {
        using var document = SourceVectorFixture.Read(Fixture, Generate);
        var cases = ReadCases(document);
        int totalRows = 0, caseIndex = 0;
        foreach (var c in cases)
        {
            int kind = I(c, "kind"), actor = I(c, "actor"), target = I(c, "target"), root = I(c, "root"), hidden = I(c, "hiddenMask");
            bool doubles = c.GetProperty("doubles").GetBoolean();
            Assert.AreEqual(kind < 2 ? 284 : 323, root);
            var script = AnimationCompositionFixture.Script(root);
            var bodies = Enumerable.Range(0, doubles ? 4 : 2).Select(slot =>
                new AnimationBattler(new(slot), "body" + slot, I(c, "yOffset"), I(c, "elevation"), 64, 64)
                { SourceVisibility = new(true, (hidden & (1 << slot)) != 0, (hidden & (1 << slot)) != 0) }).ToArray();
            var cue = new CommittedMoveAnimation("squash-native", 1, root, new(actor), new(target))
            { ActorHpQuarterTier = checked((byte)I(c, "tier")), MovePower = checked((ushort)I(c, "movePower")) };
            var scene = new SourceBattleAnimation.SpriteScene();
            var owner = new SourceBattleAnimation(Primitive(script), cue, bodies, doubles, new Audio(), spriteScene: scene);
            var args = Field<short[]>(owner, "_animationArgs");
            var inputArgs = V(c, "arguments");
            Assert.AreEqual(8, inputArgs.Length);
            for (int i = 0; i < inputArgs.Length; i++) args[i] = checked((short)inputArgs[i]);
            var offsets = Field<PixelPoint[]>(owner, "_offsets");
            if (c.GetProperty("shift").GetBoolean())
            {
                for (int i = 0; i < bodies.Length; i++)
                {
                    offsets[i] = new((i & 1) != 0 ? -5 : 5, -3);
                    var center = bodies[i].Center(doubles);
                    Invoke(owner, "SetBattlerPosition", i, new PixelPoint(center.X + 7, center.Y - 9));
                }
            }
            var sprites = Field<object>(owner, "_sprites");
            SourceBattleAnimation.VisualSprite? falling = null;
            object? task = null;
            if (kind == 1)
                falling = (SourceBattleAnimation.VisualSprite)Nested("EruptionFallingRockVisual", bodies.Length, I(c, "subpriority"), script.SpriteRecipes[Templates[1]], owner);
            else
            {
                task = Nested(kind switch { 0 => "EruptionLaunchTask", 2 => "WaterSpoutLaunchTask", 3 => "WaterSpoutRainTask", _ => throw new AssertFailedException("Unknown native callback kind.") }, owner);
                Invoke(owner, "AddSourceVisualTask", task, 5);
            }
            var instances = new Dictionary<SourceBattleAnimation.VisualSprite, int>();
            var taskSlots = new Dictionary<object, int>();
            if (task is not null) taskSlots.Add(task, 0);
            var rows = c.GetProperty("rows").EnumerateArray().ToArray();
            Assert.IsTrue(rows.Length > 1, "Native cases must include initialization and terminal frames.");
            for (int tick = 0; tick < rows.Length; tick++)
            {
                var row = rows[tick];
                string why = $"squash case {caseIndex}, kind {kind}, actor {actor}, target {target}, frame {tick} ({Branch(c)})";
                Assert.AreEqual(tick, I(row, "frame"), why + " contiguous source frame");
                if (tick > 0)
                {
                    if (falling is not null)
                    {
                        falling.Step(owner);
                        if (!falling.Finished) falling.AdvanceAnimation(owner);
                    }
                    else
                    {
                        Invoke(owner, "AdvanceSourceSprites");
                        Invoke(owner, "RunSourceTasks");
                    }
                }
                bool alive = falling is not null ? !falling.Finished : !Property<bool>(task!, "Finished");
                Assert.AreEqual(I(row, "alive") != 0, alive, why + " root lifetime");
                CollectionAssert.AreEqual(V(row, "args").Select(x => checked((short)x)).ToArray(), args.Take(8).ToArray(), why + " retained argument registers");
                Assert.IsTrue(args.Skip(8).All(x => x == 0), why + " untouched extended arguments");
                if (task is not null)
                    CollectionAssert.AreEqual(V(row, "task").Select(x => checked((short)x)).ToArray(), Field<short[]>(task, "Data"), why + " complete root registers");
                else
                    CollectionAssert.AreEqual(new int[16], V(row, "task"), why + " falling callback has no root task");
                CompareBattlers(row, owner, offsets, bodies.Length, why);
                var actual = falling is not null ? new[] { falling } : ((IEnumerable)sprites).Cast<SourceBattleAnimation.VisualSprite>().OrderBy(s => s.Slot).ToArray();
                var expected = row.GetProperty("sprites").EnumerateArray().ToArray();
                Assert.AreEqual(expected.Length, actual.Length, why + " live raw sprite ownership");
                BindBirths(row, actual, instances, why);
                for (int i = 0; i < actual.Length; i++) CompareSprite(expected[i], actual[i], instances[actual[i]], kind == 1, why);
                CompareTasks(row, owner, task, taskSlots, why);
                totalRows++;
            }
            Assert.AreEqual(0, I(rows[^1], "alive"), "Native trace must reach root destruction.");
            Assert.AreEqual(0, I(rows[^1], "activeTasks"), "Native trace must drain child shakes after root destruction.");
            Assert.AreEqual(0, scene.Count, "Raw child ownership must fully drain.");
            owner.Cancel();
            caseIndex++;
        }
        Assert.IsTrue(caseIndex >= 229, "The corpus must retain authored recipes and all representative boundary cases.");
        Assert.IsTrue(totalRows >= 21357, "Every representative native frame must be retained.");
    }

    private static JsonElement[] ReadCases(JsonDocument document)
    {
        Assert.AreEqual("firered-squash-water-fire-source-v1", document.RootElement.GetProperty("schema").GetString());
        CollectionAssert.AreEqual(new[] { "O0", "O2" }, document.RootElement.GetProperty("optimizationParity").EnumerateArray().Select(x => x.GetString()).ToArray());
        var cases = document.RootElement.GetProperty("cases").EnumerateArray().ToArray();
        Assert.IsTrue(cases.Length > 0, "Native corpus is empty.");
        return cases;
    }

    private static void AssertLayouts(IEnumerable<JsonElement> cases, string why)
    {
        // Full corpora add ally and opposite-flank targets. The exact authored
        // opposite-slot subset must still appear once in each of the six layouts.
        var selected = cases.Where(c => I(c, "target") == (I(c, "actor") ^ 1)).ToArray();
        Assert.AreEqual(6, selected.Length, why + " requires all six authored layouts exactly once.");
        CollectionAssert.AreEquivalent(new[] { (false, 0), (false, 1), (true, 0), (true, 1), (true, 2), (true, 3) },
            selected.Select(c => (c.GetProperty("doubles").GetBoolean(), I(c, "actor"))).ToArray(), why);
        Assert.IsTrue(selected.All(c => I(c, "target") == (I(c, "actor") ^ 1)), why + " authored opposing target");
    }

    private static void CompareBattlers(JsonElement row, SourceBattleAnimation owner, PixelPoint[] offsets, int count, string why)
    {
        var battlers = row.GetProperty("battlers").EnumerateArray().ToArray();
        Assert.AreEqual(count, battlers.Length, why + " present battlers");
        var snapshot = owner.Snapshot();
        for (int slot = 0; slot < count; slot++)
        {
            var expected = battlers[slot].EnumerateArray().Select(x => x.GetInt32()).ToArray();
            Assert.AreEqual(9, expected.Length, why + " complete battler state");
            Assert.AreEqual(new PixelPoint(expected[0], expected[1]), (PixelPoint)Invoke(owner, "BattlerPosition", slot)!, why + " base position");
            Assert.AreEqual(new PixelPoint(expected[2], expected[3]), offsets[slot], why + " secondary offset and foot anchor");
            Assert.AreEqual(expected[4] != 0, snapshot.BattlerVisibility[slot].SpriteInvisible, why + " source visibility");
            var affine = snapshot.BattlerAffine[slot];
            CollectionAssert.AreEqual(expected[5..], new[] { affine.XScale, affine.MatrixB, affine.MatrixC, affine.YScale }, why + " battler matrix");
        }
    }

    private static void BindBirths(JsonElement row, SourceBattleAnimation.VisualSprite[] actual,
        Dictionary<SourceBattleAnimation.VisualSprite, int> instances, string why)
    {
        var births = row.GetProperty("births").EnumerateArray().ToArray();
        var fresh = actual.Where(sprite => !instances.ContainsKey(sprite)).ToArray();
        Assert.AreEqual(births.Length, fresh.Length, why + " every native allocation has a newly owned managed object");
        foreach (var birth in births)
        {
            var matches = fresh.Where(sprite => sprite.Slot == I(birth, "slot") && sprite.Frame.Template == Templates[I(birth, "template")]).ToArray();
            Assert.AreEqual(1, matches.Length, why + " native allocation slot and template");
            int instance = I(birth, "instance");
            Assert.IsTrue(instance > 0 && !instances.ContainsValue(instance), why + " unique allocation generation");
            instances.Add(matches[0], instance);
        }
    }

    private static void CompareSprite(JsonElement expected, SourceBattleAnimation.VisualSprite sprite, int instance, bool counted, string why)
    {
        var frame = sprite.Frame;
        Assert.AreEqual(I(expected, "slot"), sprite.Slot, why + " raw sprite slot");
        Assert.AreEqual(I(expected, "instance"), instance, why + " allocation reuse preserves reference identity");
        Assert.AreEqual(Templates[I(expected, "template")], frame.Template, why + " template");
        Assert.AreEqual(Point(expected, "position"), frame.Position, why + " particle base");
        Assert.AreEqual(Point(expected, "offset"), frame.Offset, why + " particle offset");
        Assert.AreEqual(I(expected, "tile"), frame.Tile, why + " tile");
        Assert.AreEqual(I(expected, "visible") != 0, frame.Visible, why + " visible");
        Assert.AreEqual(I(expected, "priority"), frame.OamPriority, why + " OAM priority");
        Assert.AreEqual(I(expected, "subpriority"), frame.Subpriority, why + " subpriority");
        Assert.AreEqual(I(expected, "hFlip") != 0, frame.HFlip, why + " horizontal flip");
        Assert.AreEqual(I(expected, "vFlip") != 0, frame.VFlip, why + " vertical flip");
        Assert.AreEqual(expected.GetProperty("affine").GetBoolean(), frame.Affine, why + " affine mode");
        CollectionAssert.AreEqual(V(expected, "matrix"), new[] { frame.MatrixA, frame.MatrixB, frame.MatrixC, frame.MatrixD }, why + " particle matrix");
        Assert.AreEqual(counted, sprite.Counted, why + " raw children never count as visual script owners");
        CollectionAssert.AreEqual(V(expected, "data").Select(x => checked((short)x)).ToArray(), Field<short[]>(sprite, "Data"), why + " all signed particle registers");
        // Source dummy ANIM_END stays in BeginAnim without setting animEnded.
        // Its converted static one-frame recipe has the same observable state.
        Assert.AreEqual(0, I(expected, "animEnded"), why + " static dummy does not terminate");
        Assert.AreEqual(0, I(expected, "animDelay"), why + " static dummy has zero retained delay");
        var affine = frame.Template == Templates[3] ? Field<SourceSpriteAffineProgram?>(sprite, "_affine") : null;
        Assert.AreEqual(I(expected, "xScale"), affine?.XScale ?? 256, why + " source affine X state, including unstarted births");
        Assert.AreEqual(I(expected, "yScale"), affine?.YScale ?? 256, why + " source affine Y state, including unstarted births");
        Assert.AreEqual(I(expected, "affineEnded") != 0, affine?.Ended ?? false, why + " affine completion");
    }

    private static void CompareTasks(JsonElement row, SourceBattleAnimation owner, object? root,
        Dictionary<object, int> slots, string why)
    {
        var active = ((IEnumerable)Field<object>(owner, "_tasks")).Cast<object>().Where(t => !Property<bool>(t, "Finished")).ToArray();
        Assert.AreEqual(I(row, "activeTasks"), active.Length, why + " live task count");
        if (root is not null) Assert.AreEqual(I(row, "alive") != 0, active.Contains(root), why + " scheduler retains only active roots");
        var children = active.Where(t => !ReferenceEquals(t, root)).OrderBy(t => Property<long>(t, "Order")).ToArray();
        foreach (var child in children.Where(t => !slots.ContainsKey(t)))
        {
            var occupied = active.Where(slots.ContainsKey).Select(t => slots[t]).ToHashSet();
            slots.Add(child, Enumerable.Range(0, 16).First(slot => !occupied.Contains(slot)));
        }
        var expected = row.GetProperty("shakes").EnumerateArray().ToArray();
        Assert.AreEqual(expected.Length, children.Length, why + " live linked-priority child tasks");
        for (int i = 0; i < children.Length; i++)
        {
            var child = children[i];
            Assert.AreEqual("HorizontalShakeTask", child.GetType().Name, why + " child callback");
            Assert.AreEqual(I(expected[i], "slot"), slots[child], why + " source task slot");
            Assert.AreEqual(I(expected[i], "priority"), Property<int>(child, "Priority"), why + " birth-frame source task priority");
            CollectionAssert.AreEqual(V(expected[i], "data").Select(x => checked((short)x)).ToArray(), ShakeRegisters(child), why + " complete shake state and same-frame scheduling");
        }
    }

    private static short[] ShakeRegisters(object task)
    {
        // Project all native registers, including zero-filled unused cells.
        var result = new short[16];
        result[0] = Field<short>(task, "_state");
        result[1] = Field<short>(task, "_delay");
        result[2] = Field<short>(task, "_timer");
        result[3] = Field<short>(task, "<maximum>P");
        var battlers = Field<int[]>(task, "<slots>P");
        for (int i = 0; i < battlers.Length; i++) result[9 + i] = checked((short)battlers[i]);
        result[13] = checked((short)battlers.Length);
        result[14] = Field<short>(task, "_amplitude");
        result[15] = Field<short>(task, "_initial");
        return result;
    }
}
