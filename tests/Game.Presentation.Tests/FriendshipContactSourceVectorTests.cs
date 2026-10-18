using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class FriendshipContactSourceVectorTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static readonly int[] Moves = [216, 122, 218, 218, 216];
    private static readonly string[] Templates = ["gMovementWavesSpriteTemplate", "gLickSpriteTemplate", "gWeakFrustrationAngerMarkSpriteTemplate"];
    private sealed class Audio : ISourceAnimationAudio
    { public bool IsPlaying => false; public void Play(SourceAnimationSound cue) { } public void StopAnimationEffects() { } }
    private sealed class Blocker(int slot) : SourceBattleAnimation.VisualSprite(slot, 0)
    {
        public override bool Counted => false;
        public override SourceVisualSpriteFrame Frame => new(Slot, "source-slot-obstacle", default, default, 0, false, false, false, 256, null, 0);
        public override void Step(SourceBattleAnimation owner) { }
    }
    private static T Field<T>(object value, string name) => (T)(value.GetType().GetField(name, Instance)?.GetValue(value)
        ?? throw new InvalidOperationException("Missing field " + name));
    private static T Property<T>(object value, string name) => (T)(value.GetType().GetProperty(name, Instance)?.GetValue(value)
        ?? throw new InvalidOperationException("Missing property " + name));
    private static object? Invoke(object value, string name, params object[] args) =>
        (value.GetType().GetMethod(name, Instance) ?? throw new InvalidOperationException("Missing method " + name)).Invoke(value, args);
    private static object Nested(string name, params object[] args) => Activator.CreateInstance(
        typeof(SourceBattleAnimation).GetNestedType(name, BindingFlags.NonPublic) ?? throw new InvalidOperationException(name),
        Instance, null, args, CultureInfo.InvariantCulture) ?? throw new InvalidOperationException(name);
    private static IList Tasks(SourceBattleAnimation owner) => Field<IList>(owner, "_tasks");
    private static object Registry(SourceBattleAnimation owner) => Field<object>(owner, "_sprites");
    private static object[] Sprites(SourceBattleAnimation owner) => ((IEnumerable)Registry(owner)).Cast<object>().Where(x => x is not Blocker).ToArray();
    private static int[] Numbers(JsonElement e) => e.EnumerateArray().Select(x => x.GetInt32()).ToArray();
    private static JsonDocument Vectors() => SourceVectorFixture.Read("friendship_contact_vectors.json", "python tests/Game.Presentation.Tests/Reference/generate_friendship_contact_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");
    private sealed class Columns(JsonElement root, string name)
    {
        private readonly Dictionary<string, int> indices = root.GetProperty(name).EnumerateArray().Select((e, i) => (e.GetString()!, i)).ToDictionary(x => x.Item1, x => x.Item2);
        public int Read(int[] row, string name) => row[indices[name]];
    }
    private sealed record Probe(SourceBattleAnimation Owner, object? Callback, int Kind, int[] Key, int[] Config, AnimationBattler[] Battlers);
    private static Probe Create(JsonElement root, string name)
    {
        int[] key = name.Split(':').Select(x => int.Parse(x, CultureInfo.InvariantCulture)).ToArray(); int kind = key[0]; bool doubles = key[1] != 0;
        int[] config = Numbers(root.GetProperty("configs")[kind][key[4]]); var geometry = root.GetProperty("geometry");
        var battlers = Enumerable.Range(0, doubles ? 4 : 2).Select(i => new AnimationBattler(new(i), "b" + i,
            geometry.GetProperty("yOffset")[i].GetInt32(), geometry.GetProperty("elevation")[i].GetInt32(), 64,
            geometry.GetProperty("heights")[i].GetInt32())).ToArray();
        var script = AnimationCompositionFixture.Script(Moves[kind]);
        var owner = new SourceBattleAnimation(script with { Commands = [new("end", [], 1)],
            Subroutines = new Dictionary<string, SourceAnimationCommand[]>(), Labels = new Dictionary<string, int>(),
            SubroutineLabels = new Dictionary<string, IReadOnlyDictionary<string, int>>() },
            new("friendship-source", 1, script.MoveId, new(key[2]), new(key[3])) { Friendship = 128 }, battlers, doubles, new Audio());
        var args = Field<short[]>(owner, "_animationArgs"); for (int i = 0; i < 4; i++) args[i] = checked((short)config[i]);
        int firstFree = kind != 4 ? battlers.Length : config[4] switch { 1 => 64, 2 => 63, _ => battlers.Length };
        for (int slot = battlers.Length; slot < firstFree; slot++) Invoke(Registry(owner), "Add", new Blocker(slot));
        object? callback;
        if (kind < 3)
        {
            callback = Nested("FriendshipContactSprite", firstFree, battlers[key[3]].Subpriority - 2, script.SpriteRecipes[Templates[kind]], owner);
            if (!Property<bool>(callback, "Finished")) Invoke(Registry(owner), "Add", callback);
        }
        else
        {
            callback = kind == 3 ? Nested("TaskAffinePlayback", owner, new AnimationSlot(key[2]), script.TaskAffinePrograms["sStrongFrustrationAffineAnimCmds"])
                : Nested("TraceMonTask", config[0], config[1], config[2], config[3]);
            Tasks(owner).Add(callback);
        }
        return new(owner, callback, kind, key, config, battlers);
    }
    private static void AssertSprite(Probe p, object sprite, int[] row, Columns c, string context, bool beforeTasks)
    {
        int S(string name) => c.Read(row, name); var frame = Property<SourceVisualSpriteFrame>(sprite, "Frame");
        Assert.AreEqual(S("slot"), frame.Slot, context); Assert.AreEqual(new PixelPoint(S("x"), S("y")), frame.Position, context + " position");
        Assert.AreEqual(new PixelPoint(S("x2"), S("y2")), frame.Offset, context + " offset");
        Assert.AreEqual(S("subpriority"), frame.Subpriority, context + " subpriority");
        Assert.AreEqual(S("tile"), frame.Tile, context + " tile"); Assert.AreEqual(S("hflip") != 0, frame.HFlip, context);
        Assert.AreEqual(S("vflip") != 0, frame.VFlip, context);
        if (p.Kind != 4 || beforeTasks) Assert.AreEqual(S("invisible") == 0, frame.Visible, context + " OAM visibility");
        Assert.AreEqual(p.Kind != 4, Property<bool>(sprite, "Counted"), context + " counted ownership");
        if (p.Kind == 4)
        {
            // These native trace probes use present, visible, nonaffine battlers only.
            Assert.AreEqual((S("affineMode") & 1) != 0, frame.Affine, context + " supported clone affine mode");
            Assert.AreEqual((S("affineMode") & 2) != 0, frame.DoubleSize, context + " supported clone double size");
            Assert.AreEqual(new SourceAffineMatrix(S("a"), S("b"), S("c"), S("d")),
                new SourceAffineMatrix(frame.MatrixA, frame.MatrixB, frame.MatrixC, frame.MatrixD), context + " supported clone matrix");
            Assert.IsTrue(frame.Blend); Assert.AreEqual(S("priority"), frame.OamPriority); Assert.AreEqual(1, S("objMode"));
            int selected = p.Config[0] == 0 ? p.Key[2] : p.Key[3]; Assert.AreEqual(selected, frame.BattlerCopySlot);
            Assert.AreEqual(S("data0"), Field<int>(sprite, "_life"), context + " raw clone lifetime");
            Assert.IsTrue(sprite.GetType().GetFields(Instance).Any(f => ReferenceEquals(f.GetValue(sprite), p.Callback)), context + " raw clone retains its owner task");
            return;
        }
        Assert.IsFalse(frame.Blend); Assert.IsFalse(frame.Affine);
        if (p.Kind is 0 or 1)
        {
            var frames = Field<SourceSpriteFrameProgram>(sprite, "_frames");
            Assert.AreEqual(S("frameDelay"), frames.DelayCounter, context + " frame delay");
            Assert.AreEqual(S("frameEnded") != 0, frames.Ended, context + " frame completion");
            Assert.AreEqual(S("frameIndex"), Field<int>(frames, "_index"), context + " source frame cursor");
        }
        if (p.Kind == 0) Assert.AreEqual(S("data0"), (int)Field<short>(sprite, "_cycles"), context + " cycles");
        if (p.Kind == 1) foreach (var pair in new[] { ("_state", "data0"), ("_timer", "data1"), ("_cycles", "data2") })
            Assert.AreEqual(S(pair.Item2), (int)Field<short>(sprite, pair.Item1), context + " " + pair.Item1);
        if (p.Kind == 2) foreach (var pair in new[] { ("_state", "data0"), ("_xFraction", "data1"), ("_yFraction", "data2") })
            Assert.AreEqual(S(pair.Item2), (int)Field<short>(sprite, pair.Item1), context + " " + pair.Item1);
    }
    private static void AssertSnapshots(Probe p, JsonElement rows, Columns c, string context, bool beforeTasks)
    {
        if (p.Kind == 3)
        {
            var row = Numbers(rows.EnumerateArray().Single()); int slot = p.Key[2]; int S(string n) => c.Read(row, n);
            Assert.AreEqual(new PixelPoint(S("x2"), S("y2")), Field<PixelPoint[]>(p.Owner, "_offsets")[slot], context + " affine foot offset");
            Assert.AreEqual(new SourceAffineMatrix(S("a"), S("b"), S("c"), S("d")), new SourceAffineMatrix(
                Field<int[]>(p.Owner, "_xScale")[slot], Field<int[]>(p.Owner, "_matrixB")[slot], Field<int[]>(p.Owner, "_matrixC")[slot], Field<int[]>(p.Owner, "_yScale")[slot]), context + " affine matrix");
            Assert.AreEqual(S("affineMode") == 3, Field<bool[]>(p.Owner, "_affineMode")[slot], context + " affine restoration"); return;
        }
        var actual = Sprites(p.Owner).ToDictionary(x => Property<SourceVisualSpriteFrame>(x, "Frame").Slot);
        Assert.AreEqual(rows.GetArrayLength(), actual.Count, context + " exact registry occupants");
        foreach (var raw in rows.EnumerateArray())
        {
            var row = Numbers(raw); Assert.IsTrue(actual.TryGetValue(c.Read(row, "slot"), out var sprite), context);
            AssertSprite(p, sprite!, row, c, context, beforeTasks);
        }
    }
    [TestMethod]
    public void EveryFriendshipByteMatchesBothExtractedSourcePredicatesIncludingReturnSixty()
    {
        using var doc = Vectors(); int count = 0;
        foreach (var entry in doc.RootElement.GetProperty("friendship").EnumerateArray())
        {
            var row = Numbers(entry); Assert.AreEqual(count++, row[0]);
            Assert.AreEqual(row[1], (int)SourceFriendshipAnimationRules.ReturnLevel(checked((byte)row[0])));
            Assert.AreEqual(row[2], (int)SourceFriendshipAnimationRules.FrustrationLevel(checked((byte)row[0])));
        }
        Assert.AreEqual(256, count);
    }
    [TestMethod]
    public void SourceSpriteTaskPhasesOwnershipAndAffineRestorationMatchAllExtractedCallbacks()
    {
        using var doc = Vectors(); var root = doc.RootElement; var sc = new Columns(root, "spriteColumns"); var st = new Columns(root, "stateColumns");
        int cases = 0, count = 0;
        foreach (var entry in root.GetProperty("cases").EnumerateObject())
        {
            var p = Create(root, entry.Name); int previous = -2;
            foreach (var row in entry.Value.EnumerateArray())
            {
                var state = Numbers(row.GetProperty("state")); int S(string n) => st.Read(state, n); int tick = S("tick"); string context = entry.Name + " tick " + tick;
                Assert.AreEqual(++previous, tick, context + " contiguous source callback rows");
                if (tick >= 0) Invoke(p.Owner, "AdvanceSourceSprites");
                AssertSnapshots(p, row.GetProperty("sprites"), sc, context + " before tasks", true);
                if (tick >= 0) Invoke(p.Owner, "RunSourceTasks");
                if (p.Kind >= 3) AssertSnapshots(p, row.GetProperty("afterTaskSprites"), sc, context + " after tasks", false);
                Assert.AreEqual(S("visualCount"), p.Owner.ActiveVisualCount, context + " source visual counter");
                Assert.AreEqual(S("activeTask"), Tasks(p.Owner).Count, context + " task retirement");
                Assert.AreEqual(S("liveSprites"), Sprites(p.Owner).Length, context + " sprite allocation and retirement");
                if (p.Kind == 3)
                {
                    var program = Field<SourceTaskAffineProgram>(p.Callback!, "_program");
                    foreach (var pair in new[] { ("taskData7", program.CommandIndex), ("taskData8", program.FrameTicks), ("taskData9", program.LoopRemaining),
                        ("taskData10", program.XScale), ("taskData11", program.YScale), ("taskData12", program.Rotation) }) Assert.AreEqual(S(pair.Item1), pair.Item2, context);
                }
                if (p.Kind == 4)
                {
                    Assert.AreEqual(S("taskData1"), Field<int>(p.Callback!, "_wait"), context + " spacing");
                    Assert.AreEqual(S("taskData4"), Field<int>(p.Callback!, "_remaining"), context + " attempts, including failed allocations");
                    Assert.AreEqual(S("taskData5"), Field<int>(p.Callback!, "LiveCopies"), context + " owning task child counter");
                }
                count++;
            }
            Assert.AreEqual(0, p.Owner.ActiveVisualCount, entry.Name); Assert.AreEqual(0, Sprites(p.Owner).Length, entry.Name); p.Owner.Cancel(); cases++;
        }
        AssertCoverage(root, cases, count);
    }

    private static void AssertCoverage(JsonElement root, int cases, int rows)
    {
        string? profile = root.GetProperty("profile").GetString();
        Assert.IsTrue(profile is "representative" or "full", "Explicit source-vector profile is required.");
        string[] required =
        [
            // Every callback configuration: frame variants, signed extremes, trace delay/count and allocator boundaries
            "0:0:0:1:0","0:0:0:1:1","0:0:0:1:2","0:0:0:1:3","0:0:0:1:4",
            // Every callback configuration: frame variants, signed extremes, trace delay/count and allocator boundaries
            "1:0:0:1:0","1:0:0:1:1","1:0:0:1:2","1:0:0:1:3","1:0:0:1:4","1:0:0:1:5",
            // Every callback configuration: frame variants, signed extremes, trace delay/count and allocator boundaries
            "2:0:0:1:0","2:0:0:1:1","2:0:0:1:2","2:0:0:1:3","2:0:0:1:4",
            // Every callback configuration: frame variants, signed extremes, trace delay/count and allocator boundaries
            "3:0:0:1:0",
            // Every callback configuration: frame variants, signed extremes, trace delay/count and allocator boundaries
            "4:0:0:1:0","4:0:0:1:1","4:0:0:1:2","4:0:0:1:3","4:0:0:1:4","4:0:0:1:5","4:0:0:1:6",
            // Opposite side: mirrored offsets, picture anchors and trace origin
            "0:0:1:0:1","0:0:1:0:2","0:0:1:0:3","0:0:1:0:4",
            // Opposite side: mirrored offsets, picture anchors and trace origin
            "1:0:1:0:1","1:0:1:0:4","1:0:1:0:5",
            // Opposite side: mirrored offsets, picture anchors and trace origin
            "2:0:1:0:0","2:0:1:0:1","2:0:1:0:2","2:0:1:0:3","2:0:1:0:4",
            // Opposite side: mirrored offsets, picture anchors and trace origin
            "3:0:1:0:0",
            // Opposite side: mirrored offsets, picture anchors and trace origin
            "4:0:1:0:1","4:0:1:0:5",
            // Doubles rear geometry and trace no-free/one-free allocation
            "0:1:2:3:4",
            // Doubles rear geometry and trace no-free/one-free allocation
            "1:1:2:3:4",
            // Doubles rear geometry and trace no-free/one-free allocation
            "2:1:2:3:4",
            // Doubles rear geometry and trace no-free/one-free allocation
            "3:1:2:3:0",
            // Doubles rear geometry and trace no-free/one-free allocation
            "4:1:2:3:4",
            // Doubles rear geometry and trace no-free/one-free allocation
            "4:1:2:3:5",
            // Doubles rear geometry and trace no-free/one-free allocation
            "0:1:3:2:4",
            // Doubles rear geometry and trace no-free/one-free allocation
            "1:1:3:2:4",
            // Doubles rear geometry and trace no-free/one-free allocation
            "2:1:3:2:4",
            // Doubles rear geometry and trace no-free/one-free allocation
            "3:1:3:2:0",
            // Doubles rear geometry and trace no-free/one-free allocation
            "4:1:3:2:4",
            // Doubles rear geometry and trace no-free/one-free allocation
            "4:1:3:2:5",
        ];
        var actual = root.GetProperty("cases").EnumerateObject().Select(entry => entry.Name).ToArray();
        foreach (string key in required) Assert.IsTrue(actual.Contains(key), "Missing source branch/boundary case " + key);
        if (profile == "representative") CollectionAssert.AreEquivalent(required, actual, "Representative selection must be exact.");
        Assert.AreEqual(profile == "full" ? 480 : 51, cases);
        Assert.AreEqual(profile == "full" ? 17420 : 1979, rows);
        Assert.AreEqual(cases, root.GetProperty("coverage").GetProperty("caseCount").GetInt32());
        Assert.AreEqual(rows, root.GetProperty("coverage").GetProperty("rowCount").GetInt32());
    }
}
