using System.Collections;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Game.Presentation.BattleAnimations;

namespace FireRed.Tests.Presentation;

[TestClass, TestCategory("Game")]
public sealed class ReactionSourceVectorTests
{
    private const BindingFlags Instance = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private const string Fixture = "reaction_vectors.json";
    private static readonly int[] Moves = [161, 263, 263, 263, 265, 265, 265, 348];
    private static readonly string[] Templates =
        ["gTriAttackTriangleSpriteTemplate", "gSmellingSaltsHandSpriteTemplate", "gSmellingSaltExclamationSpriteTemplate"];

    private sealed class Audio : ISourceAnimationAudio
    {
        public bool IsPlaying => false;
        public void Play(SourceAnimationSound cue) { }
        public void StopAnimationEffects() { }
    }
    // Allocator obstacles have no callback effects, frame work, or visual counter.
    // They model occupied source slots without introducing another game mechanic.
    private sealed class Blocker(int slot) : SourceBattleAnimation.VisualSprite(slot, 0)
    {
        public override bool Counted => false;
        public override SourceVisualSpriteFrame Frame => new(Slot, "reference-slot-obstacle", default, default, 0, false, false, false, 256, null, 0);
        public override void Step(SourceBattleAnimation owner) { }
    }
    private static object? ReadField(object value, string name) =>
        (value.GetType().GetField(name, Instance) ?? throw new InvalidOperationException("Missing field " + name)).GetValue(value);
    private static T Field<T>(object value, string name) => (T)(ReadField(value, name) ?? throw new InvalidOperationException("Null " + name));
    private static T Property<T>(object value, string name) =>
        (T)((value.GetType().GetProperty(name, Instance) ?? throw new InvalidOperationException("Missing property " + name)).GetValue(value)
            ?? throw new InvalidOperationException("Null " + name));
    private static object? Invoke(object value, string name, params object[] args) =>
        (value.GetType().GetMethod(name, Instance) ?? throw new InvalidOperationException("Missing method " + name)).Invoke(value, args);
    private static object NewNested(string name, params object[] args) =>
        Activator.CreateInstance(typeof(SourceBattleAnimation).GetNestedType(name, BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing callback " + name), Instance, null, args, CultureInfo.InvariantCulture)
            ?? throw new InvalidOperationException("Could not construct " + name);
    private static IList Tasks(SourceBattleAnimation owner) => Field<IList>(owner, "_tasks");
    private static object SpriteRegistry(SourceBattleAnimation owner) => Field<object>(owner, "_sprites");
    private static object[] Sprites(SourceBattleAnimation owner) => ((IEnumerable)SpriteRegistry(owner)).Cast<object>().Where(s => s is not Blocker).ToArray();
    private static bool Finished(object value) => Property<bool>(value, "Finished");
    private static int[] Numbers(JsonElement value) => value.EnumerateArray().Select(v => v.GetInt32()).ToArray();
    private static int[] Key(string key) => key.Split(':').Select(v => int.Parse(v, CultureInfo.InvariantCulture)).ToArray();
    private static JsonDocument Vectors() => SourceVectorFixture.Read(Fixture, "python tests/Game.Presentation.Tests/Reference/generate_reaction_vectors.py --root . --output-dir tests/Game.Presentation.Tests/Fixtures");
    private sealed class Columns(JsonElement root, string name)
    {
        private readonly Dictionary<string, int> _indices = root.GetProperty(name).EnumerateArray().Select((v, i) => (Name: v.GetString()!, Index: i)).ToDictionary(p => p.Name, p => p.Index);
        public int Read(int[] row, string name) => row[_indices[name]];
    }
    private sealed record Probe(SourceBattleAnimation Owner, object?[] Callbacks, int Kind, int[] Config, int[] Key, AnimationBattler[] Battlers);

    private static Probe Create(JsonElement root, string caseName)
    {
        var key = Key(caseName); int kind = key[0]; var config = Numbers(root.GetProperty("configs")[kind][key[4]]);
        var geometry = root.GetProperty("geometry"); bool doubles = key[1] != 0; int profile = config[4];
        var battlers = Enumerable.Range(0, doubles ? 4 : 2).Select(i => new AnimationBattler(new(i), "b" + i,
            geometry.GetProperty("profileYOffset")[profile][i].GetInt32(), geometry.GetProperty("elevation")[i].GetInt32(),
            geometry.GetProperty("widths")[profile][i].GetInt32(), geometry.GetProperty("heights")[profile][i].GetInt32())).ToArray();
        var script = AnimationCompositionFixture.Script(Moves[kind]);
        var owner = new SourceBattleAnimation(script with { Commands = [new("end", [], 1)],
                Subroutines = new Dictionary<string, SourceAnimationCommand[]>(), Labels = new Dictionary<string, int>(),
                SubroutineLabels = new Dictionary<string, IReadOnlyDictionary<string, int>>() },
            new("reaction-source-vectors", 1, script.MoveId, new(key[2]), new(key[3])), battlers, doubles, new Audio());
        foreach (var b in battlers)
        {
            Assert.AreEqual(new PixelPoint(geometry.GetProperty("x")[key[1]][b.Slot.Value].GetInt32(),
                geometry.GetProperty("y")[key[1]][b.Slot.Value].GetInt32()), b.Base(doubles));
            Assert.AreEqual(geometry.GetProperty("subpriority")[b.Slot.Value].GetInt32(), b.Subpriority);
        }
        for (int bank = 0; bank < 16; bank++) for (int color = 0; color < 16; color++)
            Field<ushort[][]>(owner, "_spritePaletteBanks")[bank][color] = Field<ushort[][]>(owner, "_spritePaletteUnfadedBanks")[bank][color] = PaletteInput(bank, color);
        var args = Field<short[]>(owner, "_animationArgs"); for (int i = 0; i < 3; i++) args[i] = checked((short)config[i]);
        var obstacles = new List<Blocker>(); int lastBlocked = config[3] switch { 1 => 32, 2 => 61, 3 => 64, _ => battlers.Length };
        for (int slot = battlers.Length; slot < lastBlocked; slot++)
        { var obstacle = new Blocker(slot); obstacles.Add(obstacle); Invoke(SpriteRegistry(owner), "Add", obstacle); }
        var callbacks = new object?[2]; int selected = config[0] == 0 ? key[2] : key[3];
        if (kind is 0 or 4 or 5)
        {
            string template = Templates[kind == 0 ? 0 : kind == 4 ? 1 : 2];
            int slot = (int)Invoke(owner, "FindFreeSpriteSlot", -1)!;
            callbacks[0] = NewNested("ReactionSprite", slot, battlers[key[3]].Subpriority - 2, script.SpriteRecipes[template], owner);
            Invoke(SpriteRegistry(owner), "Add", callbacks[0]!);
        }
        else
        {
            if (kind is 1 or 3) callbacks[0] = NewNested("SquishSweatTask", owner, selected, checked((short)config[1]));
            if (kind == 2) callbacks[0] = NewNested("FacadeColorTask", selected, checked((short)config[1]));
            if (kind == 3) callbacks[1] = NewNested("FacadeColorTask", selected, checked((short)config[2]));
            if (kind == 6 && config[0] != 0) callbacks[0] = NewNested("SmellingSquishTask", owner, selected, checked((short)config[1]));
            if (kind == 7) callbacks[0] = NewNested("LeafBladeTask", owner);
            foreach (var callback in callbacks.OfType<object>())
            {
                // Match the source command priority and RunScript's registration, not default duplicate keys.
                int priority = kind == 7 ? 5 : kind == 6 ? 3 : 2;
                string name = callback == callbacks[1] || kind == 2 ? "AnimTask_FacadeColorBlend"
                    : kind == 7 ? "AnimTask_LeafBlade" : kind == 6 ? "AnimTask_SmellingSaltsSquish" : "AnimTask_SquishAndSweatDroplets";
                int visualStart = Tasks(owner).Count, soundStart = Field<IList>(owner, "_soundTasks").Count;
                long order = Field<long>(owner, "_nextTaskOrder");
                Tasks(owner).Add(callback);
                Invoke(owner, "AssignTaskOrder", visualStart, soundStart,
                    new SourceAnimationCommand("createvisualtask", [name, priority.ToString(CultureInfo.InvariantCulture)], 1));
                Assert.AreEqual(priority, Property<int>(callback, "Priority"));
                Assert.AreEqual(order, Property<long>(callback, "Order"));
            }
            if (kind == 6 && config[0] == 0)
                Assert.AreEqual(true, Invoke(owner, "TryCreateReactionTask", new SourceAnimationCommand("createvisualtask", ["AnimTask_SmellingSaltsSquish", "3", "0", config[1].ToString(CultureInfo.InvariantCulture)], 1)));
        }
        if (config[3] == 1) foreach (var obstacle in obstacles) Invoke(SpriteRegistry(owner), "Remove", obstacle);
        return new(owner, callbacks, kind, config, key, battlers);
    }

    private static void AssertFrame(object sprite, int[] row, Columns c, string context)
    {
        int S(string name) => c.Read(row, name); var frame = Property<SourceVisualSpriteFrame>(sprite, "Frame");
        Assert.AreEqual(S("slot"), frame.Slot, context); Assert.AreEqual(new PixelPoint(S("x"), S("y")), frame.Position, context + " position");
        Assert.AreEqual(new PixelPoint(S("x2"), S("y2")), frame.Offset, context + " offset");
        Assert.AreEqual(S("tile"), frame.Tile, context + " tile"); Assert.AreEqual(S("invisible") == 0, frame.Visible, context + " blink");
        Assert.AreEqual(S("hflip") != 0, frame.HFlip, context + " H flip"); Assert.AreEqual(S("vflip") != 0, frame.VFlip, context + " V flip");
        Assert.AreEqual(S("subpriority"), frame.Subpriority, context + " priority"); Assert.AreEqual((S("affineMode") & 1) != 0, frame.Affine, context);
        Assert.AreEqual(S("affineMode") == 3, frame.DoubleSize, context);
        Assert.AreEqual(new SourceAffineMatrix(S("a"), S("b"), S("c"), S("d")), new SourceAffineMatrix(frame.MatrixA, frame.MatrixB, frame.MatrixC, frame.MatrixD), context + " matrix");
        Assert.IsFalse(frame.Blend, context); Assert.AreEqual(256, frame.Scale, context);
        Assert.AreEqual(S("role") == 4, Property<bool>(sprite, "Counted"), context + " visual ownership");
        if (frame.Template == "gTriAttackTriangleSpriteTemplate")
        {
            var affine = Field<SourceSpriteAffineProgram>(sprite, "_affine");
            Assert.AreEqual(S("affineIndex"), Field<int>(affine, "_pc"), context); Assert.AreEqual(S("affineDelay"), Field<int>(affine, "_delay"), context);
            Assert.AreEqual(S("xScale"), affine.XScale, context); Assert.AreEqual(S("yScale"), affine.YScale, context);
            Assert.AreEqual(S("rotation"), affine.Rotation, context); Assert.AreEqual(S("affineEnded") != 0, affine.Ended, context);
            var frames = Field<SourceSpriteFrameProgram>(sprite, "_frames");
            Assert.AreEqual(S("frameDelay"), frames.DelayCounter, context); Assert.AreEqual(S("frameEnded") != 0, frames.Ended, context);
            int callback = S("callback"); int state = callback switch { 1 => 0, 2 => 1, 3 => 2, 4 => 3, _ => throw new InvalidDataException("Unexpected triangle callback") };
            Assert.AreEqual(state, (int)Field<short>(sprite, "_state"), context + " callback boundary");
            if (callback == 1) Assert.AreEqual(S("data0"), (int)Field<short>(sprite, "_timer"), context);
            if (callback is 3 or 4) AssertLinear(Field<object>(sprite, "_linear"), row, c, context);
        }
        else if (frame.Template == "gSmellingSaltsHandSpriteTemplate")
        {
            foreach (var pair in new[] { ("_state", "data0"), ("_timer", "data1"), ("_steps", "data2"), ("_repeat", "data6"), ("_direction", "data7") })
                Assert.AreEqual(S(pair.Item2), (int)Field<short>(sprite, pair.Item1), context + " " + pair.Item1);
        }
        else if (frame.Template == "gSmellingSaltExclamationSpriteTemplate")
        {
            Assert.AreEqual(S("data0"), (int)Field<short>(sprite, "_timer"), context); Assert.AreEqual(S("data1"), (int)Field<short>(sprite, "_delay"), context);
            Assert.AreEqual(S("data3"), (int)Field<short>(sprite, "_repeat"), context); Assert.AreEqual(S("data2") != 0, Field<bool>(sprite, "_invisible"), context);
        }
        else if (frame.Template == "gFacadeSweatDropSpriteTemplate")
            Assert.AreEqual(S("data0"), (int)Field<short>(sprite, "_timer"), context);
        else if (frame.Template == "gLeafBladeSpriteTemplate")
        {
            Assert.AreEqual(S("animNumber"), Field<int>(sprite, "_animation"), context);
            Assert.AreEqual(S("frameBeginning") != 0, Field<bool>(sprite, "_begin"), context);
            var frames = ReadField(sprite, "_frames") as SourceSpriteFrameProgram;
            Assert.AreEqual(S("frameEnded") != 0, !Field<bool>(sprite, "_begin") && (frames?.Ended ?? false), context);
            if (frames is not null) Assert.AreEqual(S("frameDelay"), frames.DelayCounter, context);
            if (S("role") == 1)
            {
                var arc = Field<object>(sprite, "_arc"); AssertLinear(Field<object>(arc, "_linear"), row, c, context);
                Assert.AreEqual(S("data6"), (int)Field<short>(arc, "_step"), context); Assert.AreEqual(S("data7"), (int)Field<short>(arc, "_angle"), context);
            }
            else
            {
                Assert.AreEqual(S("data0"), (int)Field<short>(sprite, "_blink"), context); Assert.AreEqual(S("data1"), (int)Field<short>(sprite, "_toggles"), context);
                Assert.AreEqual(0, S("data6"), context + " source parent task"); Assert.AreEqual(12, S("data7"), context + " source parent counter");
            }
        }
    }
    private static void AssertLinear(object linear, int[] row, Columns c, string context)
    {
        foreach (var pair in new[] { ("_remaining", "data0"), ("_vx", "data1"), ("_vy", "data2"), ("_x", "data3"), ("_y", "data4") })
            Assert.AreEqual(c.Read(row, pair.Item2), (int)unchecked((short)Field<int>(linear, pair.Item1)), context + " " + pair.Item1);
    }
    private static void AssertSnapshots(Probe probe, JsonElement snapshots, Columns c, string context)
    {
        var actual = Sprites(probe.Owner).ToDictionary(s => Property<SourceVisualSpriteFrame>(s, "Frame").Slot);
        int observed = 0;
        foreach (var raw in snapshots.EnumerateArray())
        {
            var row = Numbers(raw); int S(string name) => c.Read(row, name); int slot = S("slot");
            if (S("role") != 0)
            {
                Assert.IsTrue(actual.TryGetValue(slot, out var sprite), context + " missing sprite " + slot); AssertFrame(sprite!, row, c, context + " slot " + slot); observed++;
                if (S("role") is 2 or 3)
                {
                    Assert.IsNotNull(probe.Callbacks[0], context);
                    Assert.IsTrue(sprite!.GetType().GetFields(Instance).Any(f => ReferenceEquals(f.GetValue(sprite), probe.Callbacks[0])), context + " raw child keeps its owning task");
                }
                continue;
            }
            var b = probe.Battlers[slot]; Assert.AreEqual(new PixelPoint(S("x"), S("y")), b.Center(probe.Key[1] != 0, true), context);
            Assert.AreEqual(new PixelPoint(S("x2"), S("y2")), Field<PixelPoint[]>(probe.Owner, "_offsets")[slot], context + " battler offset");
            Assert.AreEqual(new SourceAffineMatrix(S("a"), S("b"), S("c"), S("d")), new SourceAffineMatrix(Field<int[]>(probe.Owner, "_xScale")[slot], Field<int[]>(probe.Owner, "_matrixB")[slot], Field<int[]>(probe.Owner, "_matrixC")[slot], Field<int[]>(probe.Owner, "_yScale")[slot]), context + " battler matrix");
            Assert.AreEqual(S("affineMode") == 3, Field<bool[]>(probe.Owner, "_affineMode")[slot], context + " prepared mode");
        }
        Assert.AreEqual(observed, actual.Count, context + " no hidden extra owned sprite");
    }
    private static void AssertTasks(Probe probe, JsonElement rows, Columns c, string context)
    {
        foreach (var raw in rows.EnumerateArray())
        {
            var row = Numbers(raw); int S(string name) => c.Read(row, name); int index = S("slot"); var callback = probe.Callbacks[index];
            if (probe.Kind is 0 or 4 or 5) { Assert.AreEqual(0, S("active"), context); continue; }
            if (callback is null) { Assert.AreEqual(0, S("active"), context); continue; }
            Assert.AreEqual(S("active") != 0, Tasks(probe.Owner).Contains(callback), context + " task slot " + index);
            Assert.AreEqual(S("active") == 0, Finished(callback), context + " task retirement");
            if (probe.Kind == 7)
            { CollectionAssert.AreEqual(row.Skip(3).Select(v => checked((short)v)).ToArray(), Field<short[]>(callback, "_data"), context + " all sixteen task words"); continue; }
            if (probe.Kind == 2 || probe.Kind == 3 && index == 1)
            {
                Assert.AreEqual(S("data0"), (int)Field<short>(callback, "_color"), context); Assert.AreEqual(S("data1"), (int)Field<short>(callback, "_remaining"), context);
                int selected = probe.Config[0] == 0 ? probe.Key[2] : probe.Key[3]; Assert.AreEqual((16 + selected) * 16, S("data2"), context); continue;
            }
            var program = Field<SourceTaskAffineProgram>(callback, "_program");
            Assert.AreEqual(S("data7"), program.CommandIndex, context); Assert.AreEqual(S("data8"), program.FrameTicks, context); Assert.AreEqual(S("data9"), program.LoopRemaining, context);
            Assert.AreEqual(S("data10"), program.XScale, context); Assert.AreEqual(S("data11"), program.YScale, context); Assert.AreEqual(S("data12"), program.Rotation, context);
            Assert.AreEqual(S("data15"), Field<int>(callback, "_slot"), context);
            if (probe.Kind is 1 or 3)
            {
                foreach (var pair in new[] { ("_state", "data0"), ("_timer", "data1"), ("_active", "data2"), ("_remaining", "data3") })
                    Assert.AreEqual(S(pair.Item2), (int)Field<short>(callback, pair.Item1), context + " " + pair.Item1);
                Assert.AreEqual(new PixelPoint(S("data4"), S("data5")), Field<PixelPoint>(callback, "_base"), context); Assert.AreEqual(S("data6"), Field<int>(callback, "_priority"), context);
            }
            else
            {
                Assert.AreEqual(S("data0"), (int)Field<short>(callback, "_remaining"), context); Assert.AreEqual(S("data1"), (int)Field<short>(callback, "_timer"), context);
                Assert.AreEqual(0, S("data2"), context + " original source never increments its shake-direction word");
            }
        }
    }
    private static ushort PaletteInput(int bank, int color) => checked((ushort)(((256 + bank * 16 + color) * 73 + 31) & 32767));
    private static void AssertPalettes(Probe probe, JsonElement palettes, string context)
    {
        var faded = Field<ushort[][]>(probe.Owner, "_spritePaletteBanks"); var unfaded = Field<ushort[][]>(probe.Owner, "_spritePaletteUnfadedBanks");
        var blends = Field<Dictionary<int, SourcePaletteBlend>>(probe.Owner, "_paletteBlends"); var selected = new HashSet<int>();
        foreach (var raw in palettes.EnumerateArray())
        {
            var row = Numbers(raw); int bank = row[0]; selected.Add(bank); blends.TryGetValue(16 + bank, out var blend);
            for (int color = 0; color < 16; color++)
            {
                // Battler banks are rendered through the retained symbolic blend.
                // The oracle supplies final RGB555 values independently from that path.
                ushort effective = bank < 4 ? SourcePaletteMath.Apply(unfaded[bank][color], blend, false) : faded[bank][color];
                Assert.AreEqual(row[1 + color], (int)effective, context + " color " + color); Assert.AreEqual(row[17 + color], (int)unfaded[bank][color], context + " unfaded " + color);
            }
        }
        for (int bank = 0; bank < 16; bank++) for (int color = 0; color < 16; color++)
        {
            Assert.AreEqual(PaletteInput(bank, color), unfaded[bank][color], context + " preserved input");
            if (!selected.Contains(bank)) { Assert.AreEqual(PaletteInput(bank, color), faded[bank][color], context + " unrelated bank"); Assert.IsFalse(blends.ContainsKey(16 + bank), context + " unrelated symbolic blend"); }
        }
    }

    [TestMethod]
    public void ReactionInitializersEveryOrdinaryPhaseRawOwnershipAndRestorationMatchExtractedSourceC()
    {
        using var document = Vectors(); var root = document.RootElement; var stateColumns = new Columns(root, "stateColumns");
        var spriteColumns = new Columns(root, "spriteColumns"); var taskColumns = new Columns(root, "taskColumns"); int cases = 0, rows = 0, leafPeak = 0;
        foreach (var entry in root.GetProperty("cases").EnumerateObject())
        {
            var probe = Create(root, entry.Name); int previous = -2;
            foreach (var row in entry.Value.EnumerateArray())
            {
                var state = Numbers(row.GetProperty("state")); int S(string name) => stateColumns.Read(state, name); int tick = S("tick"); string context = entry.Name + " tick " + tick;
                Assert.AreEqual(previous + 1, tick, context + " no omitted callback rows"); previous = tick;
                if (tick >= 0) Invoke(probe.Owner, "AdvanceSourceSprites");
                AssertSnapshots(probe, row.GetProperty("sprites"), spriteColumns, context + " before tasks");
                if (tick >= 0) Invoke(probe.Owner, "RunSourceTasks");
                if (probe.Kind is 1 or 3 or 6 or 7) AssertSnapshots(probe, row.GetProperty("afterTaskSprites"), spriteColumns, context + " after tasks");
                else Assert.AreEqual(0, row.GetProperty("afterTaskSprites").GetArrayLength(), context);
                AssertTasks(probe, row.GetProperty("tasks"), taskColumns, context); AssertPalettes(probe, row.GetProperty("palettes"), context);
                Assert.AreEqual(S("visualCount"), probe.Owner.ActiveVisualCount, context + " counted visuals"); Assert.AreEqual(S("activeTasks"), Tasks(probe.Owner).Count, context);
                var live = Sprites(probe.Owner); Assert.AreEqual(S("ownedSprites"), live.Length, context + " raw and counted registry ownership");
                int raw = probe.Kind == 7 ? live.Count(s => !ReferenceEquals(s, Field<object>(probe.Callbacks[0]!, "_leader"))) : live.Count(s => !Property<bool>(s, "Counted"));
                Assert.AreEqual(S("rawChildren"), raw, context + " task-owned raw child counter");
                Assert.AreEqual(S("matrices"), live.Count(s => Property<SourceVisualSpriteFrame>(s, "Frame").Affine), context + " effect matrix ownership");
                Assert.IsNull(ReadField(probe.Owner, "_visualRandom"), context + " no visual random draws");
                if (probe.Kind == 7) leafPeak = Math.Max(leafPeak, S("ownedSprites")); rows++;
            }
            Assert.AreEqual(0, probe.Owner.ActiveVisualCount, entry.Name); Assert.AreEqual(0, Sprites(probe.Owner).Length, entry.Name); probe.Owner.Cancel(); cases++;
        }
        AssertCoverage(root, cases, rows); Assert.AreEqual(12, leafPeak, "Eleven trails plus one controller, not a guessed twenty-slot reservation.");
    }

    [TestMethod]
    [DataRow("0:0:0:1:0", 60)]
    [DataRow("1:0:0:1:1", 5)]
    [DataRow("3:0:0:1:0", 71)]
    [DataRow("4:0:0:1:2", 35)]
    [DataRow("5:0:0:1:1", 7)]
    [DataRow("6:0:0:1:2", 6)]
    [DataRow("7:0:0:1:0", 10)]
    [DataRow("7:0:0:1:2", 26)]
    public void CancellationClearsSourceObservedLiveChildrenTasksAffineAndPaletteState(string key, int cancelTick)
    {
        // Cancellation is a host ownership contract, not a claim that these C
        // callbacks have a cancellation entrypoint. First establish the live source
        // state, then check the runtime's existing invocation cleanup independently.
        using var document = Vectors(); var root = document.RootElement; var probe = Create(root, key);
        var columns = new Columns(root, "spriteColumns"); var sourceRows = root.GetProperty("cases").GetProperty(key).EnumerateArray().ToArray();
        for (int tick = 0; tick <= cancelTick; tick++) { Invoke(probe.Owner, "AdvanceSourceSprites"); Invoke(probe.Owner, "RunSourceTasks"); }
        var source = sourceRows.Single(r => r.GetProperty("tick").GetInt32() == cancelTick);
        AssertSnapshots(probe, source.GetProperty(probe.Kind is 1 or 3 or 6 or 7 ? "afterTaskSprites" : "sprites"), columns, key + " cancellation source boundary");
        probe.Owner.Cancel(); probe.Owner.Cancel();
        Assert.IsFalse(probe.Owner.Active); Assert.AreEqual(0, probe.Owner.ActiveVisualCount); Assert.AreEqual(0, ((IEnumerable)SpriteRegistry(probe.Owner)).Cast<object>().Count()); Assert.AreEqual(0, Tasks(probe.Owner).Count);
        Assert.IsFalse(probe.Owner.HasResidualSprites); Assert.AreEqual(0, Field<Dictionary<int, SourcePaletteBlend>>(probe.Owner, "_paletteBlends").Count);
        foreach (var p in Field<PixelPoint[]>(probe.Owner, "_offsets")) Assert.AreEqual(default, p);
        foreach (bool active in Field<bool[]>(probe.Owner, "_affineMode")) Assert.IsFalse(active);
        foreach (int scale in Field<int[]>(probe.Owner, "_xScale")) Assert.AreEqual(256, scale);
        foreach (int scale in Field<int[]>(probe.Owner, "_yScale")) Assert.AreEqual(256, scale);
        foreach (var bank in Field<ushort[][]>(probe.Owner, "_spritePaletteBanks")) Assert.IsTrue(bank.All(c => c == 0));
        foreach (var bank in Field<ushort[][]>(probe.Owner, "_spritePaletteUnfadedBanks")) Assert.IsTrue(bank.All(c => c == 0));
        var scene = Field<SourceBattleAnimation.SpriteScene>(probe.Owner, "_spriteScene");
        for (int tick = 0; tick < 3; tick++) Assert.AreEqual(0, scene.TickIdle(probe.Battlers, probe.Key[1] != 0).Sprites.Length, "Cancelled raw children must not return during idle frames.");
    }

    [TestMethod]
    [DataRow("AnimTask_SquishAndSweatDroplets")]
    [DataRow("AnimTask_SmellingSaltsSquish")]
    public void UnsafeZeroSquishCountsAreRejectedBeforeSourceInitializerSideEffects(string name)
    {
        using var document = Vectors(); var probe = Create(document.RootElement, "2:0:0:1:0");
        var exception = Assert.ThrowsExactly<TargetInvocationException>(() => Invoke(probe.Owner, "ValidateReactionCommand", new SourceAnimationCommand("createvisualtask", [name, "2", "1", "0"], 1)));
        Assert.IsTrue(exception.InnerException is SourceAnimationException); probe.Owner.Cancel();
    }
    [TestMethod]
    public void ExhaustedLeafControllerAllocationThrowsInsteadOfIndexingTheSourceSentinel()
    {
        using var document = Vectors(); var probe = Create(document.RootElement, "7:0:0:1:0");
        foreach (var sprite in Sprites(probe.Owner)) Invoke(SpriteRegistry(probe.Owner), "Remove", sprite);
        Tasks(probe.Owner).Clear();
        for (int slot = probe.Battlers.Length; slot < 64; slot++) Invoke(SpriteRegistry(probe.Owner), "Add", new Blocker(slot));
        var exception = Assert.ThrowsExactly<TargetInvocationException>(() => NewNested("LeafBladeTask", probe.Owner));
        Assert.IsTrue(exception.InnerException is InvalidDataException);
        Assert.AreEqual(0, Sprites(probe.Owner).Length); Assert.AreEqual(0, Tasks(probe.Owner).Count); Assert.AreEqual(0, probe.Owner.ActiveVisualCount);
        probe.Owner.Cancel();
    }


    private static void AssertCoverage(JsonElement root, int cases, int rows)
    {
        string? profile = root.GetProperty("profile").GetString();
        Assert.IsTrue(profile is "representative" or "full", "Explicit source-vector profile is required.");
        string[] required =
        [
            // Every admitted configuration, including cancellation boundaries, dimensions and allocator modes
            "0:0:0:1:0","0:0:0:1:1","0:0:0:1:2",
            // Every admitted configuration, including cancellation boundaries, dimensions and allocator modes
            "1:0:0:1:0","1:0:0:1:1","1:0:0:1:2","1:0:0:1:3",
            // Every admitted configuration, including cancellation boundaries, dimensions and allocator modes
            "2:0:0:1:0","2:0:0:1:1","2:0:0:1:2","2:0:0:1:3",
            // Every admitted configuration, including cancellation boundaries, dimensions and allocator modes
            "3:0:0:1:0","3:0:0:1:1","3:0:0:1:2",
            // Every admitted configuration, including cancellation boundaries, dimensions and allocator modes
            "4:0:0:1:0","4:0:0:1:1","4:0:0:1:2","4:0:0:1:3",
            // Every admitted configuration, including cancellation boundaries, dimensions and allocator modes
            "5:0:0:1:0","5:0:0:1:1","5:0:0:1:3",
            // Every admitted configuration, including cancellation boundaries, dimensions and allocator modes
            "6:0:0:1:0","6:0:0:1:1","6:0:0:1:2",
            // Every admitted configuration, including cancellation boundaries, dimensions and allocator modes
            "7:0:0:1:0","7:0:0:1:1","7:0:0:1:2","7:0:0:1:3",
            // Reverse side: mirrored hand/frame choices, squish, palette and leaf trajectory
            "0:0:1:0:0",
            // Reverse side: mirrored hand/frame choices, squish, palette and leaf trajectory
            "1:0:1:0:2",
            // Reverse side: mirrored hand/frame choices, squish, palette and leaf trajectory
            "3:0:1:0:1",
            // Reverse side: mirrored hand/frame choices, squish, palette and leaf trajectory
            "4:0:1:0:0","4:0:1:0:1",
            // Reverse side: mirrored hand/frame choices, squish, palette and leaf trajectory
            "5:0:1:0:3",
            // Reverse side: mirrored hand/frame choices, squish, palette and leaf trajectory
            "6:0:1:0:2",
            // Reverse side: mirrored hand/frame choices, squish, palette and leaf trajectory
            "7:0:1:0:0",
            // Doubles rear geometry with alternate dimensions and allocation pressure
            "0:1:2:3:1",
            // Doubles rear geometry with alternate dimensions and allocation pressure
            "1:1:2:3:3",
            // Doubles rear geometry with alternate dimensions and allocation pressure
            "2:1:2:3:3",
            // Doubles rear geometry with alternate dimensions and allocation pressure
            "3:1:2:3:2",
            // Doubles rear geometry with alternate dimensions and allocation pressure
            "4:1:2:3:3",
            // Doubles rear geometry with alternate dimensions and allocation pressure
            "7:1:2:3:1",
            // Leaf: rear player target geometry; attacker identity does not affect this callback
            "7:1:3:2:0",
        ];
        var actual = root.GetProperty("cases").EnumerateObject().Select(entry => entry.Name).ToArray();
        foreach (string key in required) Assert.IsTrue(actual.Contains(key), "Missing source branch/boundary case " + key);
        if (profile == "representative") CollectionAssert.AreEquivalent(required, actual, "Representative selection must be exact.");
        Assert.AreEqual(profile == "full" ? 560 : 43, cases);
        Assert.AreEqual(profile == "full" ? 33820 : 2801, rows);
        Assert.AreEqual(cases, root.GetProperty("coverage").GetProperty("caseCount").GetInt32());
        Assert.AreEqual(rows, root.GetProperty("coverage").GetProperty("rowCount").GetInt32());
    }
}
